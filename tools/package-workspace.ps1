#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$Root = Split-Path -Parent $PSScriptRoot
$RootName = 'fortnite-chest-counter'
$Archive = Join-Path $Root 'FortniteChestCounter-Workspace.zip'
Assert-ReleaseFiles -Root $Root
$Required = @(
    'START-HERE.md', 'AGENTS.md', 'FortniteChestCounter.sln', 'NuGet.Config', 'global.json',
    'Run.cmd', 'Build.cmd', 'Test.cmd', 'Diagnostics.cmd', 'Package.cmd',
    'assets/ocr-models.json', 'assets/nuget/packages.json',
    'src/FortniteChestCounter.Core/FortniteChestCounter.Core.csproj',
    'src/FortniteChestCounter.Windows/FortniteChestCounter.Windows.csproj',
    'tests/FortniteChestCounter.Tests/FortniteChestCounter.Tests.csproj',
    'docs/PROJECT-CONTEXT.md', 'docs/DESIGN-DECISIONS.md',
    'docs/WINDOWS-DEVELOPMENT.md', 'docs/TROUBLESHOOTING.md', 'docs/VERIFICATION.md'
)
foreach ($File in $Required) {
    if (-not (Test-Path -LiteralPath (Join-Path $Root $File) -PathType Leaf)) { throw "Workspace file missing: $File" }
}
$Files = @(Get-WorkspaceFiles -Root $Root | Sort-Object RelativePath)
$Index = [ordered]@{ schemaVersion = 1; rootFolder = $RootName; files = @() }
foreach ($Item in $Files) {
    if ($Item.File.Extension -eq '.cmd') {
        $Bytes = Get-CmdFileBytes -Path $Item.File.FullName
        $Hash = Get-BytesSha256 -Bytes $Bytes
        $Length = $Bytes.Length
    }
    else {
        $Hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $Item.File.FullName).Hash.ToLowerInvariant()
        $Length = $Item.File.Length
    }
    $Index.files += [ordered]@{ path = $Item.RelativePath; bytes = $Length; sha256 = $Hash }
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$Stream = [IO.File]::Open($Archive, [IO.FileMode]::Create, [IO.FileAccess]::Write)
$Zip = [IO.Compression.ZipArchive]::new($Stream, [IO.Compression.ZipArchiveMode]::Create, $false)
try {
    foreach ($Item in $Files) {
        $Name = "$RootName/$($Item.RelativePath)"
        if ($Item.File.Extension -eq '.cmd') {
            $Entry = $Zip.CreateEntry($Name, [IO.Compression.CompressionLevel]::Optimal)
            $EntryStream = $Entry.Open()
            try {
                $Bytes = Get-CmdFileBytes -Path $Item.File.FullName
                $EntryStream.Write($Bytes, 0, $Bytes.Length)
            }
            finally { $EntryStream.Dispose() }
        }
        else {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($Zip, $Item.File.FullName, $Name,
                [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    $Entry = $Zip.CreateEntry("$RootName/workspace-manifest.json")
    $Writer = [IO.StreamWriter]::new($Entry.Open(), [Text.UTF8Encoding]::new($false))
    try { $Writer.Write(($Index | ConvertTo-Json -Depth 6)) }
    finally { $Writer.Dispose() }
}
finally { $Zip.Dispose(); $Stream.Dispose() }

$Zip = [IO.Compression.ZipFile]::OpenRead($Archive)
try {
    if ($Zip.Entries.Count -ne $Files.Count + 1) { throw 'Unexpected archive entry count.' }
    foreach ($Record in $Index.files) {
        $Entry = $Zip.GetEntry("$RootName/$($Record.path)")
        if (-not $Entry -or $Entry.Length -ne $Record.bytes) { throw "Archive file missing or truncated: $($Record.path)" }
        $Input = $Entry.Open()
        $Hasher = [Security.Cryptography.SHA256]::Create()
        try { $Hash = [BitConverter]::ToString($Hasher.ComputeHash($Input)).Replace('-', '').ToLowerInvariant() }
        finally { $Input.Dispose(); $Hasher.Dispose() }
        if ($Hash -ne $Record.sha256) { throw "Archive SHA256 mismatch: $($Record.path)" }
    }
}
finally { $Zip.Dispose() }
$Hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $Archive).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($Archive + '.sha256', "$Hash  FortniteChestCounter-Workspace.zip`n", [Text.UTF8Encoding]::new($false))
$Size = [Math]::Round((Get-Item -LiteralPath $Archive).Length / 1MB, 1)
Write-Host "Workspace archive: $Archive"
Write-Host "$($Files.Count + 1) entries, $Size MiB; every file SHA256 verified."
