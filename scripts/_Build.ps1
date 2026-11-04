<#
.SYNOPSIS
    Shared build logic for BuildFull.ps1 / BuildTrial.ps1 (not meant to be run directly).
.DESCRIPTION
    Cleans, publishes publish\<Edition>, then builds the installer with Inno Setup
    into artifacts\ unless -SkipInstaller is given.
#>
param(
    [Parameter(Mandatory)][ValidateSet("Full", "Trial", "Store")]
    [string]$Edition,
    [switch]$SkipInstaller
)
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $root "SonarController.csproj"
$publishDir = Join-Path $root "publish\$Edition"
$setupScript = Join-Path $PSScriptRoot "Setup.iss"

[xml]$project = Get-Content -LiteralPath $csproj -Raw
$version = $project.Project.PropertyGroup |
    ForEach-Object { $_.Version } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -First 1

if ($version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$') {
    throw "SonarController.csproj must define a valid semantic <Version> (for example 1.3.0)."
}

Write-Host "=== $Edition $version : publish ===" -ForegroundColor Cyan
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

# 版を切り替えるとコンパイル定数が変わるため、前回の中間ファイルを破棄する
dotnet clean $csproj -c Release -r win-x64 -v q | Out-Null

# StoreはMSIX内に展開されるため、単一ファイル化（実行時の自己展開）は使わない
$singleFile = if ($Edition -eq "Store") { "false" } else { "true" }

dotnet publish $csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:LicenseEdition=$Edition `
    -p:PublishSingleFile=$singleFile `
    -p:IncludeNativeLibrariesForSelfExtract=$singleFile `
    -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed (exit code $LASTEXITCODE)"
}
Write-Host "exe: $(Join-Path $publishDir 'SonarController.exe')"

# StoreはInno Setupではなく BuildStore.ps1 でMSIXにする
if ($SkipInstaller -or $Edition -eq "Store") { return }

Write-Host "=== $Edition $version : installer ===" -ForegroundColor Cyan
$iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
    $iscc = @(
        (Join-Path ${env:LOCALAPPDATA} "Programs\Inno Setup 6\ISCC.exe"),
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $iscc) {
    throw "Inno Setup Compiler (ISCC.exe) was not found. Install Inno Setup 6, or use -SkipInstaller."
}

& $iscc "/DMyAppVersion=$version" "/DMyEdition=$Edition" $setupScript
if ($LASTEXITCODE -ne 0) {
    throw "Setup.iss compilation failed (exit code $LASTEXITCODE)"
}

$installerPath = Join-Path $root "artifacts\SonarController-Setup-$version-$Edition.exe"
if (-not (Test-Path $installerPath)) {
    throw "Installer was not created: $installerPath"
}
Write-Host "installer: $installerPath" -ForegroundColor Green
