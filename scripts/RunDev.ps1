<#
.SYNOPSIS
    Build SonarController2, copy it into %LOCALAPPDATA%\Programs\SonarController2,
    and launch the installed copy.
.DESCRIPTION
    Development-only script for testing the app in a user-install-like location.
    This keeps the app out of the source tree and matches an AppData/Programs layout.
#>
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$appName = "SonarController2"
$sourceDir = Join-Path $root "bin\Debug\net8.0-windows\win-x64"
$installDir = Join-Path $env:LOCALAPPDATA "Programs\$appName"
$sourceExePath = Join-Path $sourceDir "$appName.exe"
$installedExePath = Join-Path $installDir "$appName.exe"

Stop-Process -Name $appName -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300

Push-Location $root
try {
    dotnet build
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet build failed (exit code $LASTEXITCODE)"
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}

if (Test-Path $installDir) {
    Remove-Item $installDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $installDir | Out-Null

Get-ChildItem $sourceDir -Force | Copy-Item -Destination $installDir -Recurse -Force

if (-not (Test-Path $installedExePath)) {
    Write-Error "Installed app not found: $installedExePath"
    exit 1
}

Start-Process $installedExePath
Write-Host "Installed to: $installDir"
Write-Host "Started: $installedExePath"
