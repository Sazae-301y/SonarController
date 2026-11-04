<#
.SYNOPSIS
    Microsoft Store edition: publish + MSIX package (artifacts\SonarController-<version>-Store.msix).
.DESCRIPTION
    Identity / Publisher must match Partner Center (Product management > Product identity).
    Without them a development identity is used, which is fine for local testing but NOT for Store submission.
    The Store signs the package itself, so submit the unsigned .msix. Use -DevSign only for local installs.
    Use -DevLicenseMode for a separately named, Debug-only MSIX that grants full local test access.
.EXAMPLE
    .\scripts\BuildStore.ps1 -IdentityName "12345Publisher.SonarController" -Publisher "CN=XXXXXXXX-XXXX-..." -PublisherDisplayName "Your Name"
.EXAMPLE
    .\scripts\BuildStore.ps1 -DevSign    # signed with a self-signed certificate for local testing
    .EXAMPLE
        .\scripts\BuildStore.ps1 -DevLicenseMode # development MSIX with all features enabled
#>
param(
    [string]$IdentityName = "SonarController.Dev",
    [string]$Publisher = "CN=SonarControllerDev",
    [string]$PublisherDisplayName = "SonarController Dev",
    [string]$DisplayName = "SonarController",
    [switch]$SkipPublish,
    [switch]$DevSign,
    [switch]$DevLicenseMode
)
$ErrorActionPreference = "Stop"

if ($DevLicenseMode) {
    if ($IdentityName -ne "SonarController.Dev" -or $Publisher -ne "CN=SonarControllerDev") {
        throw "DevLicenseMode requires the development identity 'SonarController.Dev' and publisher 'CN=SonarControllerDev'."
    }
}

$root = Split-Path -Parent $PSScriptRoot
$edition = if ($DevLicenseMode) { "StoreDev" } else { "Store" }
$publishDir = Join-Path $root "publish\$edition"
$stagingDir = Join-Path $root "artifacts\msix-staging"
$templatePath = Join-Path $root "packaging\SonarController.Package\Package.appxmanifest.template"
$logoSource = Join-Path $root "Assets\SonarController-App.png"

if ($SkipPublish -and -not (Test-Path (Join-Path $publishDir "SonarController.exe"))) {
    throw "publish\$edition\SonarController.exe was not found. Run without -SkipPublish."
}

[xml]$project = Get-Content -LiteralPath (Join-Path $root "SonarController.csproj") -Raw
$version = $project.Project.PropertyGroup | ForEach-Object { $_.Version } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1
if ($version -notmatch '^(\d+)\.(\d+)\.(\d+)$') {
    throw "MSIX requires a plain Major.Minor.Patch <Version> (got '$version')."
}
# Storeはリビジョン(4桁目)を予約しているため必ず0にする
$msixVersion = "$version.0"
$msixPath = Join-Path $root "artifacts\SonarController-$version-$edition.msix"

function Find-SdkTool([string]$name) {
    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $kits = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    $found = Get-ChildItem $kits -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName "x64\$name" } |
        Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $found) { throw "$name was not found. Install the Windows 10/11 SDK." }
    return $found
}

if (-not $SkipPublish) {
    & (Join-Path $PSScriptRoot "_Build.ps1") -Edition $edition -SkipInstaller
}
if (-not (Test-Path (Join-Path $publishDir "SonarController.exe"))) {
    throw "publish\$edition\SonarController.exe was not found. Run without -SkipPublish."
}

Write-Host "=== $edition $version : MSIX ===" -ForegroundColor Cyan
if (Test-Path $stagingDir) { Remove-Item $stagingDir -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $stagingDir "Assets") | Out-Null
Copy-Item (Join-Path $publishDir "*") $stagingDir -Recurse -Force

# マニフェスト生成
$manifest = Get-Content -LiteralPath $templatePath -Raw -Encoding UTF8
$values = @{
    IDENTITY_NAME          = $IdentityName
    PUBLISHER              = $Publisher
    VERSION                = $msixVersion
    DISPLAY_NAME           = $DisplayName
    PUBLISHER_DISPLAY_NAME = $PublisherDisplayName
}
foreach ($key in $values.Keys) {
    $manifest = $manifest.Replace("{{$key}}", [System.Security.SecurityElement]::Escape($values[$key]))
}
[System.IO.File]::WriteAllText((Join-Path $stagingDir "AppxManifest.xml"), $manifest, (New-Object System.Text.UTF8Encoding($false)))

# ロゴ生成（元画像を正方形の透明キャンバスに収める）
Add-Type -AssemblyName System.Drawing
function New-Logo([string]$path, [int]$size) {
    $src = [System.Drawing.Image]::FromFile($logoSource)
    try {
        $bmp = New-Object System.Drawing.Bitmap $size, $size
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $scale = [Math]::Min($size / $src.Width, $size / $src.Height)
        $w = [int]($src.Width * $scale); $h = [int]($src.Height * $scale)
        $g.DrawImage($src, [int](($size - $w) / 2), [int](($size - $h) / 2), $w, $h)
        $g.Dispose()
        $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
    }
    finally { $src.Dispose() }
}
$assets = Join-Path $stagingDir "Assets"
New-Logo (Join-Path $assets "StoreLogo.png") 50
New-Logo (Join-Path $assets "Square44x44Logo.png") 44
New-Logo (Join-Path $assets "Square150x150Logo.png") 150

$makeappx = Find-SdkTool "makeappx.exe"
if (Test-Path $msixPath) { Remove-Item $msixPath -Force }
& $makeappx pack /d $stagingDir /p $msixPath /o | Out-Host
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $msixPath)) { throw "makeappx failed (exit code $LASTEXITCODE)" }

if ($DevSign -or $DevLicenseMode) {
    $signtool = Find-SdkTool "signtool.exe"
    $cert = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Subject -eq $Publisher -and $_.NotAfter -gt (Get-Date) } |
        Select-Object -First 1
    if (-not $cert) {
        $cert = New-SelfSignedCertificate -Type Custom -Subject $Publisher -KeyUsage DigitalSignature `
            -FriendlyName "SonarController dev signing" -CertStoreLocation Cert:\CurrentUser\My `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
    }
    & $signtool sign /fd SHA256 /sha1 $cert.Thumbprint $msixPath | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "signtool failed (exit code $LASTEXITCODE)" }

    $cerPath = [System.IO.Path]::ChangeExtension($msixPath, ".cer")
    Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null
    Write-Host "To install locally, trust the certificate once (Administrator):" -ForegroundColor Yellow
    Write-Host "  Import-Certificate -FilePath '$cerPath' -CertStoreLocation Cert:\LocalMachine\TrustedPeople"
}

Remove-Item $stagingDir -Recurse -Force
Write-Host "msix: $msixPath" -ForegroundColor Green
if ($IdentityName -eq "SonarController.Dev") {
    Write-Warning "Development identity was used. Pass -IdentityName / -Publisher from Partner Center before submitting to the Store."
}
