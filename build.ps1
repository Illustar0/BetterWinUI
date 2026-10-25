<# .SYNOPSIS Runs the repository's Fallout targets using the installed .NET SDK. #>
$ErrorActionPreference = 'Stop'
dotnet run --project "$PSScriptRoot/build/_build.csproj" -- @args
exit $LASTEXITCODE
