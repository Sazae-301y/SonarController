<#
.SYNOPSIS
    Build the release publish output and installer.
.DESCRIPTION
    Runs Publish.ps1 and then compiles Setup.iss with Inno Setup.
    The finished installer is written to the artifacts/ folder.
#>
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$publishScript = Join-Path $PSScriptRoot "Publish.ps1"
$setupScript = Join-Path $PSScriptRoot "Setup.iss"
$installerPath = Join-Path $root "artifacts\SonarController2-Setup-1.0.0.exe"

$iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
    $candidatePaths = @(
        (Join-Path ${env:LOCALAPPDATA} "Programs\Inno Setup 6\ISCC.exe"),
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    $iscc = $candidatePaths |
        Where-Object { Test-Path $_ } |
        Select-Object -First 1
}

if (-not $iscc) {
    throw "Inno Setup Compiler (ISCC.exe) was not found. Install Inno Setup 6 first."
}

Write-Host "Step 1/2: Publishing release files..."
& $publishScript
if ($LASTEXITCODE -ne 0) {
    throw "Publish.ps1 failed (exit code $LASTEXITCODE)"
}

Write-Host "Step 2/2: Building installer..."
& $iscc $setupScript
if ($LASTEXITCODE -ne 0) {
    throw "Setup.iss compilation failed (exit code $LASTEXITCODE)"
}

if (-not (Test-Path $installerPath)) {
    throw "Installer was not created: $installerPath"
}

Write-Host "Build complete: $installerPath"
