<#
.SYNOPSIS
    Trial edition (15-day trial) build.
.EXAMPLE
    .\scripts\BuildTrial.ps1                 # exe + installer
    .\scripts\BuildTrial.ps1 -SkipInstaller  # exe only
#>
param([switch]$SkipInstaller)
& (Join-Path $PSScriptRoot "_Build.ps1") -Edition Trial -SkipInstaller:$SkipInstaller
