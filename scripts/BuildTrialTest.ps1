<#
.SYNOPSIS
    Builds a Debug-only Trial edition with a shorter trial period for testing (1-14 days).
.EXAMPLE
    .\scripts\BuildTrialTest.ps1
    .\scripts\BuildTrialTest.ps1 -TrialDays 1 -SkipInstaller
#>
param(
    [ValidateRange(1, 14)]
    [int]$TrialDays = 1,
    [switch]$SkipInstaller
)

& (Join-Path $PSScriptRoot "_Build.ps1") -Edition TrialTest -TrialTestDays $TrialDays -SkipInstaller:$SkipInstaller
