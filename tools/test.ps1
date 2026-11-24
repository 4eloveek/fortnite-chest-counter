#Requires -Version 5.1
[CmdletBinding()]
param([switch] $Offline, [switch] $NoRestore)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$Root = Split-Path -Parent $PSScriptRoot
Push-Location $Root
try {
    $Sdk = Assert-DotNetSdk
    $Project = Join-Path $Root 'tests/FortniteChestCounter.Tests/FortniteChestCounter.Tests.csproj'
    if (-not $NoRestore) { Restore-WorkspaceProject -Root $Root -Project $Project -Offline:$Offline }
    $Results = Join-Path $Root ('diagnostics/tests-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    New-Item -ItemType Directory -Force -Path $Results | Out-Null
    Write-Host "Testing with .NET SDK $Sdk"
    & dotnet test $Project -c Release --no-restore --logger 'trx;LogFileName=tests.trx' --results-directory $Results |
        Tee-Object -FilePath (Join-Path $Results 'console.txt')
    if ($LASTEXITCODE -ne 0) { throw "Tests failed. See $Results" }
    Write-Host "Test report: $Results"
}
finally { Pop-Location }
