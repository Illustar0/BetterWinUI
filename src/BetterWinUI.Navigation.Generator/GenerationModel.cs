using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace BetterWinUI.Navigation.Generator;

/// <summary>
/// Validates local registrations and stores deterministic generation inputs.
/// </summary>
internal sealed class GenerationModel
{
    private GenerationModel(
        string assemblyName,
        ImmutableArray<RegistrationInfo> registrations,
        ImmutableArray<Diagnostic> diagnostics)
    {
        AssemblyName = assemblyName;
        Registrations = registrations;
        Diagnostics = diagnostics;
        Suffix = NameUtilities.CreateSuffix(assemblyName);
    }

    internal string AssemblyName { get; }

    internal string Suffix { get; }

    internal ImmutableArray<RegistrationInfo> Registrations { get; }

    internal ImmutableArray<Diagnostic> Diagnostics { get; }

    internal bool HasModule => !Registrations.IsDefaultOrEmpty;

    internal string ModuleTypeName =>
        $"global::BetterWinUI.Navigation.Generated.NavigationViewModule_{Suffix}";

    internal static GenerationModel Create(
        ImmutableArray<RegistrationInfo> parameterless,
        ImmutableArray<RegistrationInfo> parameterized,
        string assemblyName)
    {
        var all = parameterless.AddRange(parameterized);
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var registration in all)
            if (registration.Diagnostic is not null)
                diagnostics.Add(registration.Diagnostic);

        var valid = all.Where(static item => item.IsValid).ToArray();
        var conflicting = new HashSet<RegistrationInfo>();
        AddConflicts(valid, static item => item.ViewTypeName, "View", conflicting, diagnostics);
        AddConflicts(valid, static item => item.ViewModelTypeName, "ViewModel", conflicting, diagnostics);
        AddConflicts(valid, static item => item.Route, "route", conflicting, diagnostics);

        var emittable = valid
            .Where(item => !conflicting.Contains(item))
            .OrderBy(static item => item.ViewModelTypeName, StringComparer.Ordinal)
            .ToImmutableArray();

        return new GenerationModel(
            assemblyName,
            emittable,
            diagnostics.ToImmutable());
    }

    private static void AddConflicts(
        IEnumerable<RegistrationInfo> registrations,
        Func<RegistrationInfo, string> getKey,
        string keyKind,
        HashSet<RegistrationInfo> conflicting,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        foreach (var group in registrations
                     .GroupBy(getKey, StringComparer.Ordinal)
                     .Where(static group => group.Count() > 1))
        foreach (var registration in group)
        {
            conflicting.Add(registration);
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.ConflictingMapping,
                registration.Location,
                $"{keyKind} '{group.Key}'"));
        }
    }
}