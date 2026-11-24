#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$Feed = Join-Path $Root 'assets/nuget'
New-Item -ItemType Directory -Force -Path $Feed | Out-Null
$Packages = @{}
$AssetsFiles = @(
    'src/FortniteChestCounter.Windows/obj/project.assets.json',
    'tests/FortniteChestCounter.Tests/obj/project.assets.json'
)

foreach ($Relative in $AssetsFiles) {
    $Path = Join-Path $Root $Relative
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Run restore first: missing $Relative" }
    $Assets = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    $CacheFolders = @($Assets.packageFolders.PSObject.Properties | ForEach-Object { $_.Name })
    $Pairs = @()
    foreach ($Library in $Assets.libraries.PSObject.Properties) {
        if ($Library.Value.type -eq 'package') {
            $Parts = $Library.Name.Split('/')
            $Pairs += [pscustomobject]@{ Id = $Parts[0]; Version = $Parts[1] }
        }
    }
    foreach ($Framework in $Assets.project.frameworks.PSObject.Properties) {
        foreach ($Download in $Framework.Value.downloadDependencies) {
            $Range = $Download.version.Trim('[', ']').Split(',')
            if ($Range.Count -ne 2 -or $Range[0].Trim() -ne $Range[1].Trim()) {
                throw "Expected an exact SDK dependency version: $($Download.name) $($Download.version)"
            }
            $Pairs += [pscustomobject]@{ Id = $Download.name; Version = $Range[0].Trim() }
        }
    }

    foreach ($Package in $Pairs) {
        $Id = $Package.Id.ToLowerInvariant()
        $Version = $Package.Version.ToLowerInvariant()
        $Name = "$Id.$Version.nupkg"
        if ($Packages.ContainsKey($Name)) { continue }
        $Source = $null
        foreach ($Cache in $CacheFolders) {
            $Candidate = Join-Path $Cache "$Id/$Version/$Name"
            if (Test-Path -LiteralPath $Candidate -PathType Leaf) { $Source = $Candidate; break }
        }
        if (-not $Source) { throw "NuGet package not found in restored cache: $Name" }
        $Hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $Source).Hash.ToLowerInvariant()
        $Target = Join-Path $Feed $Name
        if (-not (Test-Path -LiteralPath $Target) -or (Get-FileHash -Algorithm SHA256 -LiteralPath $Target).Hash -ne $Hash) {
            Copy-Item -LiteralPath $Source -Destination $Target -Force
        }
        if ((Get-FileHash -Algorithm SHA256 -LiteralPath $Target).Hash -ne $Hash) { throw "Copy verification failed: $Name" }
        $Packages[$Name] = [pscustomobject]@{
            id = $Package.Id; version = $Package.Version; file = $Name
            sha256 = $Hash; bytes = (Get-Item -LiteralPath $Target).Length
        }
    }
}

$Manifest = [ordered]@{
    schemaVersion = 1
    packages = @($Packages.Values | Sort-Object id, version)
}
$Json = $Manifest | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText((Join-Path $Feed 'packages.json'), $Json, [Text.UTF8Encoding]::new($false))
Write-Host "Bundled $($Packages.Count) NuGet packages in assets/nuget."
