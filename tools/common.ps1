#Requires -Version 5.1

function Assert-DotNetSdk {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'Install .NET SDK 10 from https://dotnet.microsoft.com/download/dotnet/10.0'
    }
    $Version = (& dotnet --version | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $Version -notmatch '^10\.') {
        throw 'This workspace requires .NET SDK 10. Check dotnet --list-sdks and global.json.'
    }
    return $Version
}

function Restore-WorkspaceProject {
    param([string] $Root, [string] $Project, [switch] $Offline)
    $Arguments = @('restore', $Project, '--locked-mode', '--configfile', (Join-Path $Root 'NuGet.Config'))
    if ($Offline) {
        $Arguments += @('--source', (Join-Path $Root 'assets/nuget'), '-p:NuGetAudit=false')
    }
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw 'NuGet restore failed.' }
}

function Assert-OcrModels {
    param([string] $Root, [switch] $Offline)
    $Info = Get-Content -LiteralPath (Join-Path $Root 'assets/ocr-models.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $Directory = Join-Path $Root 'assets/tessdata'
    New-Item -ItemType Directory -Force -Path $Directory | Out-Null
    foreach ($Model in $Info.models) {
        $Path = Join-Path $Directory ($Model.id + '.traineddata')
        if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
            if ($Offline) { throw "OCR model missing in offline mode: $Path" }
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            $Url = "https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/$($Info.revision)/$($Model.id).traineddata"
            Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $Path
        }
        if ((Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash -ne $Model.sha256) {
            throw "Unexpected SHA256 for OCR model: $Path"
        }
    }
}

function Get-RequiredReleaseFiles {
    return @(
        'FortniteChestCounter.exe', 'FortniteChestCounter.dll',
        'FortniteChestCounter.deps.json', 'FortniteChestCounter.runtimeconfig.json',
        'hostfxr.dll', 'hostpolicy.dll', 'coreclr.dll', 'System.Windows.Forms.dll', 'Tesseract.dll',
        'x64/tesseract50.dll', 'x64/leptonica-1.82.0.dll',
        'tessdata/eng.traineddata', 'tessdata/rus.traineddata',
        'licenses/Tesseract-Apache-2.0.txt', 'licenses/Leptonica-BSD.txt', 'licenses/dotnet-MIT.txt',
        'README.md', 'THIRD-PARTY-NOTICES.md'
    )
}

function Assert-ReleaseFiles {
    param([string] $Root)
    $Release = Join-Path $Root 'dist/FortniteChestCounter'
    foreach ($File in Get-RequiredReleaseFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $Release $File) -PathType Leaf)) {
            throw "Missing release file: $File. Run Build.cmd first."
        }
    }
}

function Get-WorkspaceFiles {
    param([string] $Root)
    $Excluded = @('bin', 'obj', '.packages', '.git', '.vs', '.idea', '__pycache__', 'TestResults', 'diagnostics')
    $Pending = [System.Collections.Generic.Stack[string]]::new()
    $Pending.Push($Root)
    while ($Pending.Count -gt 0) {
        $Directory = $Pending.Pop()
        foreach ($Item in Get-ChildItem -LiteralPath $Directory -Force) {
            if (($Item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { continue }
            if ($Item.PSIsContainer) {
                if ($Excluded -notcontains $Item.Name) { $Pending.Push($Item.FullName) }
                continue
            }
            $Relative = $Item.FullName.Substring($Root.Length + 1).Replace('\', '/')
            if ($Item.Name -eq '.gitkeep' -or $Relative -eq 'workspace-manifest.json') { continue }
            if ($Directory -eq $Root -and ($Item.Extension -eq '.zip' -or $Item.Name -like '*.zip.sha256')) { continue }
            [pscustomobject]@{ File = $Item; RelativePath = $Relative }
        }
    }
}

function Get-CmdFileBytes {
    param([string] $Path)
    # Ship batch files with Windows line endings; paths and script text are ASCII-safe.
    $Text = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8).Replace("`r`n", "`n").Replace("`n", "`r`n")
    return ,([Text.Encoding]::UTF8.GetBytes($Text))
}

function Get-BytesSha256 {
    param([byte[]] $Bytes)
    $Hasher = [Security.Cryptography.SHA256]::Create()
    try {
        return [BitConverter]::ToString($Hasher.ComputeHash($Bytes)).Replace('-', '').ToLowerInvariant()
    }
    finally { $Hasher.Dispose() }
}
