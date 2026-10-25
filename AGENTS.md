# AGENTS.md

## Scope

These instructions apply to the entire repository.

## Repository map

- `src/BetterWinUI.Navigation`: WinUI Page maps and generated mapping modules.
- `src/BetterWinUI.Navigation.Frame`: WinUI `Frame` adapter.
- `src/BetterWinUI.PageActivation`: application Page factory hooks and XAML activation integration.
- `src/BetterWinUI.PageActivation.DependencyInjection`: DI registrations and the container-backed factory.
- `*.Generator`: source generators embedded in their corresponding packages.
- `*.Tests` and `*.IntegrationTests`: unit and WinUI integration tests.

## Commands

Run from the repository root on Windows with the .NET 10 SDK:

```powershell
./build.ps1 --target Test
./build.ps1 --target Pack
```

## Conventions

- Keep packages focused; navigation registration, navigation execution, and page activation are separate concerns.
- Preserve .NET 8 compatibility for published packages and `netstandard2.0` compatibility for source generators.
- Compile published source generators against Roslyn 4.8 so they remain loadable by the .NET 8 SDK; newer Roslyn versions belong in test overrides.
- Tests target .NET 8. Generator tests compile consumer fixtures; runtime WinUI behavior belongs in `BetterWinUI.IntegrationTests` and `BetterWinUI.PageActivation.IntegrationTests`, which use real XAML metadata to exercise DI navigation and independent Page factories in separate applications.
- Test observable behavior, not generated text, private names, or internal data structures.
- Manage NuGet versions centrally in `src/Directory.Packages.props`.
- Add XML documentation to public APIs and keep source-generator diagnostics in `AnalyzerReleases.Unshipped.md` using the exact Roslyn table format.
- Do not edit `bin`, `obj`, or generated source files.
- Prefer small changes and add or update tests for observable behavior.

## Testing

- Name tests `Member_Scenario_ExpectedBehavior`, where `Member` is the public API, attribute, or generated entry point under test, for example `Resolve_UnmappedViewModel_ThrowsKeyNotFoundException`.
- Arrange, act, and assert in that order, separated by blank lines. Each test performs one act; use `[Theory]` or `[DataRow]` for input variations instead of loops or conditionals.
- Assert with Shouldly in every test project. Pass `TestContext.Current.CancellationToken` to cancellable calls in xUnit tests.
- Name repeated or unexplained literals, such as diagnostic IDs, with constants. Build fixtures through local helper methods instead of shared mutable state.
- Generator tests assert diagnostics, compilation success, cached incremental steps, and the runtime behavior of emitted code.
- Use `[UITestMethod]` only for tests that create or touch XAML objects. Keep logic that does not need the XAML runtime in xUnit projects.

## Comments

Follow the [.NET API documentation guidelines](https://github.com/dotnet/dotnet-api-docs/wiki) and the [C# comment conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions#comment-style).

- Document every public and protected API, including public API emitted by generator templates, with XML comments. Internal and private members get an XML summary only when it says more than the name and signature.
- Write summaries as one sentence that starts with a present-tense, third-person verb and does not restate the member name.
  - Constructors: `Initializes a new instance of the <see cref="T" /> class.`
  - Properties: `Gets ...` or `Gets or sets ...`; Boolean properties: `Gets a value that indicates whether ...`.
  - Interfaces: `Defines ...` or `Provides ...`.
- Describe what to pass in `<param>` and what comes back in `<returns>` as noun phrases, including whether `null` is allowed. Boolean returns read `<see langword="true" /> if ...; otherwise, <see langword="false" />.` Out parameters read `When this method returns, contains ...`.
- Add one `<exception>` per exception type the member throws directly, stating the condition, for example `<paramref name="value" /> is <see langword="null" />.`
- Put usage details, ordering requirements, and threading rules in `<remarks>`. Use `<see langword="..." />` for `null`, `true`, and `false`, and `<see cref="..." />` for code references. Use `<inheritdoc />` for overrides and interface implementations.
- Use `//` comments to explain why code is written as it is (compatibility constraints, invariants, platform behavior), not what it does. Place them on their own line, start with an uppercase letter, and end with a period.
- Do not add XML comments to test methods; the test name is the documentation. Add a `//` comment only when a scenario or assertion needs a reason.
