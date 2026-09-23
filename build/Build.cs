using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

/// <summary>Builds, tests, and packs BetterWinUI through one local and CI entry point.</summary>
[SuppressMessage("ReSharper", "InconsistentNaming")]
// ReSharper disable once CheckNamespace
sealed class Build : NukeBuild
{
    /// <summary>Runs the complete test pipeline by default.</summary>
    public static int Main() => Execute<Build>(build => build.Test);

    [Parameter("Build configuration. Default: Release.")]
    readonly Configuration Configuration = Configuration.Release;

    [Parameter("Optional NuGet package version supplied by CI or the release workflow.")]
    readonly string? PackageVersion = null;

    AbsolutePath SolutionFile => RootDirectory / "src" / "BetterWinUI.slnx";
    AbsolutePath ResultsDirectory => RootDirectory / "TestResults";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    /// <summary>Restores the production and test projects.</summary>
    Target Restore => target => target
        .Executes(() => DotNetRestore(settings => settings.SetProjectFile(SolutionFile)));

    /// <summary>Compiles the entire solution once before executing tests.</summary>
    Target Compile => target => target
        .DependsOn(Restore)
        .Executes(() => DotNetBuild(settings => settings
            .SetProjectFile(SolutionFile)
            .SetConfiguration(Configuration)
            .EnableNoRestore()));

    /// <summary>Runs ordinary and compiler tests directly against production assemblies.</summary>
    Target UnitTests => target => target
        .DependsOn(Compile)
        .Executes(() =>
        {
            string[] projects =
            [
                "BetterWinUI.Navigation.Tests",
                "BetterWinUI.Navigation.Generator.Tests",
                "BetterWinUI.PageActivation.DependencyInjection.Generator.Tests"
            ];
            foreach (var project in projects)
            {
                var results = ResultsDirectory / project;
                Directory.CreateDirectory(results);
                var report = results / "results.trx";
                File.Delete(report);
                DotNetTest(settings => settings
                    .SetProjectFile(ProjectFile(project))
                    .SetConfiguration(Configuration)
                    .EnableNoBuild()
                    .SetResultsDirectory(results)
                    .SetProcessAdditionalArguments("--", "--report-xunit-trx", "--report-xunit-trx-filename", "results.trx"));
                VerifyTestsExecuted(report);
            }
        });

    /// <summary>Runs independent Page factory and DI navigation tests in real WinUI application processes.</summary>
    Target IntegrationTests => target => target
        .DependsOn(Compile)
        .Executes(() =>
        {
            foreach (var project in new[] { "BetterWinUI.PageActivation.IntegrationTests", "BetterWinUI.IntegrationTests" })
            {
                var results = ResultsDirectory / project;
                Directory.CreateDirectory(results);
                var report = results / "results.trx";
                File.Delete(report);
                DotNetRun(settings => settings
                    .SetProjectFile(ProjectFile(project))
                    .SetConfiguration(Configuration)
                    .EnableNoBuild()
                    .SetApplicationArguments(
                        "--minimum-expected-tests", "1", "--zero-tests-policy", "strict", "--timeout", "2m",
                        "--report-trx", "--report-trx-filename", "results.trx", "--results-directory", results));
                VerifyTestsExecuted(report);
            }
        });

    /// <summary>Publishes and launches a Native AOT WinUI app with early Page factory registration.</summary>
    Target PageActivationAotSmoke => target => target
        .DependsOn(Compile)
        .Executes(() =>
        {
            var output = ResultsDirectory / "BetterWinUI.PageActivation.AotSmoke" / "publish";
            DotNetPublish(settings => settings
                .SetProject(ProjectFile("BetterWinUI.PageActivation.AotSmoke"))
                .SetConfiguration(Configuration)
                .SetOutput(output)
                .EnableNoRestore());

            var executable = output / "BetterWinUI.PageActivation.AotSmoke.exe";
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = output,
                UseShellExecute = false,
                CreateNoWindow = true
            }) ?? throw new InvalidOperationException($"Could not launch {executable}");
            if (!process.WaitForExit(30_000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException($"AOT smoke test timed out: {executable}");
            }
            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"AOT smoke test exited with 0x{unchecked((uint)process.ExitCode):X8}: {executable}");
        });

    /// <summary>Requires unit, WinUI integration, and Native AOT startup tests to pass.</summary>
    Target Test => target => target.DependsOn(UnitTests, IntegrationTests, PageActivationAotSmoke);

    /// <summary>Creates the distributable packages only after all tests pass.</summary>
    Target Pack => target => target
        .DependsOn(Test)
        .Executes(() =>
        {
            string[] projects =
            [
                "BetterWinUI.Navigation",
                "BetterWinUI.Navigation.Frame",
                "BetterWinUI.PageActivation",
                "BetterWinUI.PageActivation.DependencyInjection"
            ];
            foreach (var project in projects)
                DotNetPack(settings => settings
                    .SetProject(ProjectFile(project))
                    .SetConfiguration(Configuration)
                    .EnableNoBuild()
                    .SetOutputDirectory(ArtifactsDirectory)
                    .When(_ => PackageVersion is not null, options => options.SetVersion(PackageVersion)));
        });

    /// <summary>Locates a project using the repository's conventional layout.</summary>
    AbsolutePath ProjectFile(string name) => RootDirectory / "src" / name / $"{name}.csproj";

    /// <summary>Rejects missing reports and successful runner exits that executed no tests.</summary>
    static void VerifyTestsExecuted(AbsolutePath report)
    {
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var counters = XDocument.Load(report).Descendants(ns + "Counters").Single();
        if ((int?)counters.Attribute("executed") is not > 0)
            throw new InvalidOperationException($"No tests executed: {report}");
    }
}
