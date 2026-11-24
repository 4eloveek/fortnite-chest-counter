#Requires -Version 5.1
[CmdletBinding()]
param([string] $Asset, [string] $Sample, [switch] $Offline)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$Root = Split-Path -Parent $PSScriptRoot
[xml] $ProjectXml = Get-Content -LiteralPath (Join-Path $Root 'src/FortniteChestCounter.Windows/FortniteChestCounter.Windows.csproj') -Raw -Encoding UTF8
$Version = [string] $ProjectXml.Project.PropertyGroup.Version
if (-not $Asset) {
    $Name = "FortniteChestCounter-v$Version-win-x64.exe"
    $Asset = Join-Path $Root $Name
    if (-not (Test-Path -LiteralPath $Asset)) { $Asset = Join-Path $Root "release/v$Version-win-x64/$Name" }
}
if (-not (Test-Path -LiteralPath $Asset -PathType Leaf)) { throw 'Build the single-file release first, or specify -Asset.' }
$Sources = Join-Path $PSScriptRoot 'verify-release'
if (-not (Test-Path -LiteralPath (Join-Path $Sources 'ReleaseVerifyHook.csproj'))) {
    $Sources = Join-Path $Root 'diagnostics/single-file-020/verify-hook'
}
$Driver = Join-Path $PSScriptRoot 'verify-release/verify.ps1'
if (-not (Test-Path -LiteralPath $Driver)) { $Driver = Join-Path $Root 'diagnostics/single-file-020/verify.ps1' }
$Report = Join-Path $Root ('diagnostics/verify-release-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Force -Path $Report | Out-Null
$null = Assert-DotNetSdk
Restore-WorkspaceProject -Root $Root -Project (Join-Path $Sources 'ReleaseVerifyHook.csproj') -Offline:$Offline
& dotnet build (Join-Path $Sources 'ReleaseVerifyHook.csproj') -c Release --no-restore -o (Join-Path $Report 'hook')
if ($LASTEXITCODE -ne 0) { throw 'Verification hook build failed.' }
& $Driver -Asset ([IO.Path]::GetFullPath($Asset)) -Hook (Join-Path $Report 'hook/ReleaseVerifyHook.dll') -Report $Report -Sample $Sample
