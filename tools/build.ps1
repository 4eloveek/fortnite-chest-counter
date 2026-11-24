#Requires -Version 5.1
[CmdletBinding()]
param([switch] $Offline)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$Root = Split-Path -Parent $PSScriptRoot
Push-Location $Root
try {
    $Sdk = Assert-DotNetSdk
    Write-Host "Building with .NET SDK $Sdk"
    Assert-OcrModels -Root $Root -Offline:$Offline
    Restore-WorkspaceProject -Root $Root -Project (Join-Path $Root 'FortniteChestCounter.sln') -Offline:$Offline
    & (Join-Path $PSScriptRoot 'test.ps1') -NoRestore
    $Output = Join-Path $Root 'dist/FortniteChestCounter'
    & dotnet publish (Join-Path $Root 'src/FortniteChestCounter.Windows/FortniteChestCounter.Windows.csproj') -c Release --no-restore -o $Output
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Assert-ReleaseFiles -Root $Root
    & (Join-Path $PSScriptRoot 'collect-dependencies.ps1')
    $Archive = Join-Path $Root 'FortniteChestCounter-Windows-x64.zip'
    Compress-Archive -LiteralPath $Output -DestinationPath $Archive -Force
    Write-Host "App archive: $Archive"
    & (Join-Path $PSScriptRoot 'package-workspace.ps1')
}
finally { Pop-Location }
