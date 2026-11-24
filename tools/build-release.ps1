#Requires -Version 5.1
[CmdletBinding()]
param([switch] $Offline)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root 'src/FortniteChestCounter.Windows/FortniteChestCounter.Windows.csproj'
[xml] $ProjectXml = Get-Content -LiteralPath $Project -Raw -Encoding UTF8
$Version = [string] $ProjectXml.Project.PropertyGroup.Version
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'The release filename requires a numeric three-part project version.' }
$Name = "FortniteChestCounter-v$Version-win-x64.exe"
$Output = Join-Path $Root "release/v$Version-win-x64"
$Report = Join-Path $Root ('diagnostics/release-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$Staging = Join-Path $Report 'publish'

# Never clear a directory that might contain user files or a different release.
if (Test-Path -LiteralPath $Output) {
    $OtherFiles = @(Get-ChildItem -LiteralPath $Output -Force | Where-Object { $_.Name -ne $Name -or $_.PSIsContainer })
    $SourceBundle = (Test-Path -LiteralPath (Join-Path $Output 'UPLOAD-GITHUB.md')) -and
        (Test-Path -LiteralPath (Join-Path $Output 'src/FortniteChestCounter.Windows/FortniteChestCounter.Windows.csproj'))
    if ($OtherFiles.Count -gt 0 -and -not $SourceBundle) {
        throw "Release directory contains unrelated files: $Output. Nothing was removed."
    }
    # A recognized source bundle keeps all its source files; only the named EXE is updated.
}
New-Item -ItemType Directory -Force -Path $Staging | Out-Null
Push-Location $Root
try {
    $Sdk = Assert-DotNetSdk
    Write-Host "Single-file release with .NET SDK $Sdk"
    Assert-OcrModels -Root $Root -Offline:$Offline
    Restore-WorkspaceProject -Root $Root -Project (Join-Path $Root 'FortniteChestCounter.sln') -Offline:$Offline
    & (Join-Path $PSScriptRoot 'test.ps1') -NoRestore
    & dotnet publish $Project -c Release --no-restore -p:PublishProfile=SingleFile -p:DebugType=none -p:DebugSymbols=false -o $Staging |
        Tee-Object -FilePath (Join-Path $Report 'publish.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Single-file publish failed.' }

    $Files = @(Get-ChildItem -LiteralPath $Staging -File -Recurse)
    $Exe = Join-Path $Staging 'FortniteChestCounter.exe'
    if ($Files.Count -ne 1 -or -not (Test-Path -LiteralPath $Exe -PathType Leaf)) {
        throw "Expected exactly one EXE, found $($Files.Count) files. Nothing was copied to release."
    }
    New-Item -ItemType Directory -Force -Path $Output | Out-Null
    $Asset = Join-Path $Output $Name
    Copy-Item -LiteralPath $Exe -Destination $Asset -Force
    $Hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $Asset).Hash.ToLowerInvariant()
    $Length = (Get-Item -LiteralPath $Asset).Length
    [IO.File]::WriteAllText((Join-Path $Report 'SHA256SUMS.txt'), "$Hash  $Name`n", [Text.UTF8Encoding]::new($false))
    [ordered]@{
        version = $Version
        asset = $Asset
        bytes = $Length
        sha256 = $Hash
        sdk = $Sdk
        deployment = 'self-contained single-file, all content self-extracts'
        report = $Report
        note = 'Build does not stop the running counter, publish to dist, read user statistics, or publish to GitHub. Runtime verification is separate.'
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Report 'release.json') -Encoding UTF8
    Write-Host "Release EXE: $Asset"
    Write-Host ('Size: {0:N1} MiB; SHA256: {1}' -f ($Length / 1MB), $Hash)
    Write-Host "Report: $Report"
}
finally { Pop-Location }
