<#
.SYNOPSIS
    Full edition (no restrictions, for friends) build.
.EXAMPLE
    .\scripts\BuildFull.ps1                  # exe + installer
    .\scripts\BuildFull.ps1 -SkipInstaller   # exe only
#>
param([switch]$SkipInstaller)
& (Join-Path $PSScriptRoot "_Build.ps1") -Edition Full -SkipInstaller:$SkipInstaller
