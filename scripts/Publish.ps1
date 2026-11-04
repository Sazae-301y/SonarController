<#
.SYNOPSIS
    Publish SonarController for release (self-contained, single-file exe).
.DESCRIPTION
    Runs dotnet publish and outputs to a publish/ folder (not bin/) so that
    scripts/Setup.iss (Inno Setup) can use it as its source folder.
#>
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $root "SonarController.csproj"
$publishDir = Join-Path $root "publish"

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

dotnet publish $csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed (exit code $LASTEXITCODE)"
    exit $LASTEXITCODE
}

Write-Host "Publish complete: $publishDir"
Write-Host "Next: build scripts\Setup.iss with Inno Setup (ISCC.exe) to produce the installer exe."
