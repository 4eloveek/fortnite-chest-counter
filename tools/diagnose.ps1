#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$Root = Split-Path -Parent $PSScriptRoot
$Output = Join-Path $Root ('diagnostics/report-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$Lines = [Collections.Generic.List[string]]::new()
$Lines.Add('Fortnite Chest Counter diagnostics')
$Lines.Add('Time: ' + (Get-Date -Format o))
$Lines.Add('Workspace: ' + $Root)
$Lines.Add('OS: ' + [Environment]::OSVersion.VersionString)
$Lines.Add('64-bit OS: ' + [Environment]::Is64BitOperatingSystem)
$Lines.Add('64-bit PowerShell process: ' + [Environment]::Is64BitProcess)
$Lines.Add('PowerShell: ' + $PSVersionTable.PSVersion.ToString())
$Lines.Add('')

if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) {
    try {
        Add-Type -AssemblyName System.Windows.Forms
        foreach ($Screen in [System.Windows.Forms.Screen]::AllScreens) {
            $Lines.Add('Screen: ' + $Screen.DeviceName + ' ' + $Screen.Bounds.ToString())
        }
    }
    catch { $Lines.Add('Screen metadata error: ' + $_.Exception.Message) }
}
$Lines.Add('')
$Lines.Add('Release files:')
foreach ($File in Get-RequiredReleaseFiles) {
    $Exists = Test-Path -LiteralPath (Join-Path $Root ('dist/FortniteChestCounter/' + $File)) -PathType Leaf
    $Lines.Add("$Exists`t$File")
}
$Lines.Add('')
$Lines.Add('OCR model hashes:')
$Models = Get-Content -LiteralPath (Join-Path $Root 'assets/ocr-models.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($Model in $Models.models) {
    $Path = Join-Path $Root ('assets/tessdata/' + $Model.id + '.traineddata')
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        $Hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
        $Lines.Add("$($Model.id): $Hash; expected match: $($Hash -eq $Model.sha256)")
    }
    else { $Lines.Add("$($Model.id): MISSING") }
}
$Feed = Join-Path $Root 'assets/nuget'
$Lines.Add('Bundled NuGet packages: ' + @(Get-ChildItem -LiteralPath $Feed -Filter '*.nupkg' -File).Count)
$Lines.Add('')
$Data = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'FortniteChestCounter'
$Lines.Add('App data: ' + $Data)
foreach ($Name in @('settings.json', 'state.json')) {
    $Path = Join-Path $Data $Name
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        try {
            Copy-Item -LiteralPath $Path -Destination (Join-Path $Output $Name)
            Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json | Out-Null
            $Lines.Add("$Name copied; JSON syntax OK.")
        }
        catch { $Lines.Add("$Name error: " + $_.Exception.Message) }
    }
    else { $Lines.Add("$Name not present yet.") }
}
$Lines.Add('')
$Lines.Add('dotnet --info:')
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    Push-Location $Root
    try {
        $Info = @(& dotnet --info 2>&1 | ForEach-Object { $_.ToString() })
        $Lines.AddRange([string[]] $Info)
    }
    finally { Pop-Location }
}
else { $Lines.Add('dotnet not installed; it is needed for source builds, not for Run.cmd.') }
[IO.File]::WriteAllLines((Join-Path $Output 'report.txt'), $Lines, [Text.UTF8Encoding]::new($false))
Write-Host "Diagnostics saved: $Output"
