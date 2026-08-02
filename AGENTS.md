# AGENTS.md

## Scope

These instructions apply to the entire repository.

## Repository map

- `src/BetterWinUI.Navigation`: platform-neutral navigation registry.
- `src/BetterWinUI.Navigation.Frame`: WinUI `Frame` adapter.
- `src/BetterWinUI.DependencyInjection.PageActivation`: page activation API.
- `*.Generator`: source generators embedded in their corresponding packages.
- `*.Tests` and `*.IntegrationTests`: unit and WinUI integration tests.

## Commands

Run from the repository root on Windows with the .NET 10 SDK:

```powershell
dotnet restore src/BetterWinUI.slnx
dotnet build src/BetterWinUI.slnx --configuration Release --no-restore
dotnet test src/BetterWinUI.slnx --configuration Release --no-build
```

## Conventions

- Keep packages focused; navigation registration, navigation execution, and page activation are separate concerns.
- Preserve .NET 8 compatibility for published packages and `netstandard2.0`
  compatibility for source generators.
- Compile published source generators against Roslyn 4.8 so they remain loadable by the .NET 8 SDK; newer Roslyn
  versions belong in test overrides.
- `BetterWinUI.DependencyInjection.PageActivation.Tests` intentionally targets .NET 10 because it loads assemblies
  compiled against .NET 10 reference assemblies and verifies forward-looking WinUI contracts.
- Manage NuGet versions centrally in `src/Directory.Packages.props`.
- Add XML documentation to public APIs and keep source-generator diagnostics in
  `AnalyzerReleases.Unshipped.md` using the exact Roslyn table format.
- Do not edit `bin`, `obj`, or generated source files.
- Prefer small changes and add or update tests for observable behavior.
