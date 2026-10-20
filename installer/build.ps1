<#
.SYNOPSIS
    Publishes Desktop Dashboard and, if Inno Setup 6 is installed, builds the installer.
.EXAMPLE
    .\installer\build.ps1
    .\installer\build.ps1 -Version 0.2.0 -SkipTests
#>
param(
    [string]$Version = "0.1.0",
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$publishDir = Join-Path $root "artifacts\publish\$Runtime"
$platform = if ($Runtime -eq "win-arm64") { "ARM64" } else { "x64" }

if (-not $SkipTests) {
    dotnet test --project (Join-Path $root "tests\DesktopDashboard.Core.Tests")
    if ($LASTEXITCODE -ne 0) { throw "Tests failed." }
}

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish (Join-Path $root "src\DesktopDashboard\DesktopDashboard.csproj") `
    -c Release -r $Runtime -p:Platform=$platform -p:Version=$Version -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

$size = (Get-ChildItem $publishDir -Recurse | Measure-Object Length -Sum).Sum / 1MB
Write-Host ("Published to {0} ({1:N0} MB)" -f $publishDir, $size)

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Warning "Inno Setup 6 not found; skipped the installer. Install it with: winget install JRSoftware.InnoSetup"
    return
}

& $iscc "/DAppVersion=$Version" "/DPublishDir=$publishDir" (Join-Path $PSScriptRoot "DesktopDashboard.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed." }
Get-ChildItem (Join-Path $root "artifacts\installer") -Filter *.exe | ForEach-Object { Write-Host "Installer: $($_.FullName)" }
