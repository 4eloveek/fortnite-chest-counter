#Requires -Version 5.1
[CmdletBinding()]
param([string] $Destination, [switch] $Force)
$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
[xml] $Project = Get-Content -LiteralPath (Join-Path $Root 'src/FortniteChestCounter.Windows/FortniteChestCounter.Windows.csproj') -Raw -Encoding UTF8
$Version = [string] $Project.Project.PropertyGroup.Version
$Name = "FortniteChestCounter-v$Version-win-x64.exe"
if (-not $Destination) { $Destination = Join-Path $Root "release/v$Version-win-x64" }
$Destination = [IO.Path]::GetFullPath($Destination)
if ($Destination.TrimEnd('\', '/') -eq $Root.TrimEnd('\', '/')) { throw 'Do not export over the working project.' }
$Asset = Join-Path $Root "release/v$Version-win-x64/$Name"
if (-not (Test-Path -LiteralPath $Asset)) { $Asset = Join-Path $Root $Name }
if (-not (Test-Path -LiteralPath $Asset -PathType Leaf)) { throw 'Build the single-file EXE first.' }
if (Test-Path -LiteralPath $Destination) {
    $Other = @(Get-ChildItem -LiteralPath $Destination -Force | Where-Object { $_.Name -ne $Name })
    if ($Other.Count -gt 0 -and -not $Force) {
        throw 'Destination already contains other files. Choose an empty folder or explicitly use -Force. No files were removed.'
    }
}
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$Excluded = @('bin', 'obj', '.git', '.packages', '.vs', '.idea', '__pycache__', 'TestResults', 'diagnostics')
$Copies = [System.Collections.Generic.List[object]]::new()

function Copy-SourceFile([string] $Source, [string] $Relative) {
    $Target = Join-Path $Destination $Relative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Target) | Out-Null
    if ([IO.Path]::GetFullPath($Source) -ne [IO.Path]::GetFullPath($Target)) {
        Copy-Item -LiteralPath $Source -Destination $Target -Force
    }
    $SourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $Source).Hash
    $TargetHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $Target).Hash
    if ($SourceHash -ne $TargetHash) { throw "Copy mismatch: $Relative" }
    $Copies.Add([pscustomobject]@{ Path = $Relative; SHA256 = $TargetHash })
}

function Copy-SourceTree([string] $RelativeRoot) {
    $Directory = Join-Path $Root $RelativeRoot
    if (-not (Test-Path -LiteralPath $Directory -PathType Container)) { return }
    $Pending = [System.Collections.Generic.Stack[string]]::new()
    $Pending.Push($Directory)
    while ($Pending.Count -gt 0) {
        $Current = $Pending.Pop()
        foreach ($Item in Get-ChildItem -LiteralPath $Current -Force) {
            if (($Item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { continue }
            if ($Item.PSIsContainer) {
                if ($Excluded -notcontains $Item.Name) { $Pending.Push($Item.FullName) }
            }
            else {
                $Relative = $Item.FullName.Substring($Root.Length + 1).Replace('\', '/')
                Copy-SourceFile $Item.FullName $Relative
            }
        }
    }
}

foreach ($Relative in @('README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'FortniteChestCounter.sln',
    'Directory.Build.props', 'global.json', 'NuGet.Config', 'Build.cmd', 'Test.cmd', 'Diagnostics.cmd', 'Package.cmd')) {
    Copy-SourceFile (Join-Path $Root $Relative) $Relative
}
foreach ($Directory in @('src', 'tests', 'tools', 'docs', '.github', 'assets/licenses', 'assets/tessdata', 'assets/nuget')) {
    Copy-SourceTree $Directory
}
Copy-SourceFile (Join-Path $Root 'assets/ocr-models.json') 'assets/ocr-models.json'
Copy-SourceFile $Asset $Name

# Ship reusable verification source, not the private diagnostic reports or fixture data.
$VerifySources = Join-Path $PSScriptRoot 'verify-release'
if (-not (Test-Path -LiteralPath (Join-Path $VerifySources 'ReleaseVerifyHook.csproj'))) {
    $VerifySources = Join-Path $Root 'diagnostics/single-file-020/verify-hook'
}
foreach ($File in @('ReleaseVerifyHook.csproj', 'StartupHook.cs')) {
    Copy-SourceFile (Join-Path $VerifySources $File) "tools/verify-release/$File"
}
$VerifyDriver = Join-Path $PSScriptRoot 'verify-release/verify.ps1'
if (-not (Test-Path -LiteralPath $VerifyDriver)) { $VerifyDriver = Join-Path $Root 'diagnostics/single-file-020/verify.ps1' }
Copy-SourceFile $VerifyDriver 'tools/verify-release/verify.ps1'

$Hash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Destination $Name)).Hash.ToLowerInvariant()
$Length = (Get-Item -LiteralPath (Join-Path $Destination $Name)).Length
$Templates = [ordered]@{
    '.gitignore' = '.gitignore'
    '.gitattributes' = '.gitattributes'
    'AGENTS.md' = 'AGENTS.md'
    'START-HERE.md' = 'START-HERE.md'
    'UPLOAD-GITHUB.md' = 'UPLOAD-GITHUB.md'
    'PROJECT-CONTEXT.md' = 'docs/PROJECT-CONTEXT.md'
    'VERIFICATION.md' = 'docs/VERIFICATION.md'
    'GITHUB-RELEASE.md' = 'docs/GITHUB-RELEASE.md'
    'SAMPLES-README.md' = 'assets/samples/README.md'
    'Run.cmd.template' = 'Run.cmd'
}
foreach ($Template in $Templates.Keys) {
    $Text = Get-Content -LiteralPath (Join-Path $PSScriptRoot "github-templates/$Template") -Raw -Encoding UTF8
    $Text = $Text.Replace('{{VERSION}}', $Version).Replace('{{EXE}}', $Name).Replace('{{SHA256}}', $Hash).Replace('{{BYTES}}', [string]$Length)
    if ($Template -eq 'Run.cmd.template') { $Text = $Text.Replace("`r`n", "`n").Replace("`n", "`r`n") }
    $Target = Join-Path $Destination $Templates[$Template]
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Target) | Out-Null
    [IO.File]::WriteAllText($Target, $Text, [Text.UTF8Encoding]::new($false))
}
[IO.File]::WriteAllText((Join-Path $Destination 'assets/nuget/README.md'),
    "Local NuGet packages are included for offline builds but ignored by Git. After cloning, restore from nuget.org without -Offline.`n", [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $Destination 'SHA256SUMS.txt'), "$Hash  $Name`n", [Text.UTF8Encoding]::new($false))

$Files = @(Get-ChildItem -LiteralPath $Destination -File -Force -Recurse)
foreach ($File in $Files) {
    $Relative = $File.FullName.Substring($Destination.Length + 1).Replace('\', '/')
    if ($File.Name -in @('state.json', 'settings.json') -or $Relative -match '(^|/)(bin|obj|\.packages|diagnostics|\.git)/' -or
        ($Relative -like 'assets/samples/*' -and $Relative -ne 'assets/samples/README.md')) {
        throw "Private/generated file unexpectedly present: $Relative"
    }
}
$Report = Join-Path $Root ('diagnostics/github-export-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Force -Path $Report | Out-Null
[ordered]@{
    version = $Version
    destination = $Destination
    executable = $Name
    sha256 = $Hash
    fileCount = $Files.Count
    sourceFilesCopied = $Copies.Count
    offlinePackages = @(Get-ChildItem -LiteralPath (Join-Path $Destination 'assets/nuget') -Filter '*.nupkg').Count
    license = 'MIT (chosen by owner)'
    privateDataIncluded = $false
    githubPublished = $false
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Report 'export.json') -Encoding UTF8
$Copies | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Report 'copy-checks.json') -Encoding UTF8
Write-Host "GitHub-ready folder: $Destination"
Write-Host "$($Files.Count) files; source copy hashes verified; private data excluded."
Write-Host "Report: $Report"
