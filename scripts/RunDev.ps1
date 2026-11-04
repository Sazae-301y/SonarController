<#
.SYNOPSIS
    Build SonarController, copy it into %LOCALAPPDATA%\Programs\SonarController,
    and launch the installed copy.
.DESCRIPTION
    Development-only script for testing the app in a user-install-like location.
    This keeps the app out of the source tree and matches an AppData/Programs layout.
#>
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$appName = "SonarController"
$csproj = Join-Path $root "$appName.csproj"
[xml]$project = Get-Content -LiteralPath $csproj -Raw
$targetFramework = $project.Project.PropertyGroup |
    ForEach-Object { $_.TargetFramework } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -First 1
$runtimeIdentifier = $project.Project.PropertyGroup |
    ForEach-Object { $_.RuntimeIdentifiers } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -First 1

if (-not $targetFramework -or -not $runtimeIdentifier) {
    throw "SonarController.csproj must define TargetFramework and RuntimeIdentifiers."
}

$sourceDir = Join-Path $root "bin\Debug\$targetFramework\$runtimeIdentifier"
$installDir = Join-Path $env:LOCALAPPDATA "Programs\$appName"
$sourceExePath = Join-Path $sourceDir "$appName.exe"
$installedExePath = Join-Path $installDir "$appName.exe"

Stop-Process -Name $appName -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300

Push-Location $root
try {
    dotnet build $csproj
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
