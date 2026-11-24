#Requires -Version 5.1
param([string] $Asset, [string] $Hook, [string] $Report, [string] $Sample)
$ErrorActionPreference = 'Stop'
$UnicodeName = -join ([char[]] @(0x0421, 0x0447, 0x0451, 0x0442, 0x0447, 0x0438, 0x043a))
$TempRoot = Join-Path $env:LOCALAPPDATA ('Temp/opencode/single-exe ' + $UnicodeName + ' ' + [Guid]::NewGuid().ToString('N'))
$OnlyExe = Join-Path $TempRoot 'only exe'
$Cache = Join-Path $TempRoot 'bundle cache'
New-Item -ItemType Directory -Path $OnlyExe, $Cache -Force | Out-Null
$Copy = Join-Path $OnlyExe ($UnicodeName + '.exe')
Copy-Item -LiteralPath $Asset -Destination $Copy
$OriginalHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $Asset).Hash
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $Copy).Hash -ne $OriginalHash) { throw 'Copied EXE hash mismatch.' }
$Runs = @()
foreach ($Run in @('cold', 'warm')) {
    $Output = Join-Path $Report $Run
    New-Item -ItemType Directory -Path $Output -Force | Out-Null
    $Start = New-Object System.Diagnostics.ProcessStartInfo
    $Start.FileName = $Copy
    $Start.WorkingDirectory = $OnlyExe
    $Start.UseShellExecute = $false
    $Start.RedirectStandardError = $true
    $Start.EnvironmentVariables['DOTNET_STARTUP_HOOKS'] = $Hook
    $Start.EnvironmentVariables['FCC_VERIFY_OUTPUT'] = $Output
    if ($Sample) { $Start.EnvironmentVariables['FCC_VERIFY_SAMPLE'] = $Sample }
    $Start.EnvironmentVariables['DOTNET_BUNDLE_EXTRACT_BASE_DIR'] = $Cache
    $Start.EnvironmentVariables['DOTNET_ROOT'] = (Join-Path $TempRoot 'no-installed-dotnet')
    $Start.EnvironmentVariables['DOTNET_ROOT_X64'] = (Join-Path $TempRoot 'no-installed-dotnet')
    $Start.EnvironmentVariables['DOTNET_MULTILEVEL_LOOKUP'] = '0'
    $Start.EnvironmentVariables['PATH'] = "$env:WINDIR\system32;$env:WINDIR"
    $Timer = [Diagnostics.Stopwatch]::StartNew()
    $Process = [Diagnostics.Process]::Start($Start)
    if (-not $Process.WaitForExit(90000)) {
        $Process.CloseMainWindow() | Out-Null
        throw "Verification did not finish. Process $($Process.Id) was not force terminated."
    }
    $Timer.Stop()
    $ErrorText = $Process.StandardError.ReadToEnd()
    $ErrorText | Set-Content -LiteralPath (Join-Path $Output 'stderr.txt') -Encoding UTF8
    $Result = Get-Content -LiteralPath (Join-Path $Output 'verification.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($Process.ExitCode -ne 0 -or -not $Result.Passed) { throw "Single-file $Run verification failed: $($Result.Error) $ErrorText" }
    $Runs += [pscustomobject]@{ Run = $Run; Passed = $true; Seconds = [Math]::Round($Timer.Elapsed.TotalSeconds, 3); Checks = @($Result.Checks | Where-Object Passed).Count; BaseDirectory = $Result.BaseDirectory }
}
$Summary = [pscustomobject]@{ Asset = $Asset; SHA256 = $OriginalHash; CleanDirectory = $OnlyExe; ExtractionCache = $Cache; FilesBesideExe = @(Get-ChildItem -LiteralPath $OnlyExe -File -Recurse).Count; RenamedWithCyrillicAndSpaces = $true; InstalledDotNetLookupDisabled = $true; Runs = $Runs }
$Summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Report 'summary.json') -Encoding UTF8
$Summary | ConvertTo-Json -Depth 5
