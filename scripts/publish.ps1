<#
.SYNOPSIS
    Stop, publish, and relaunch WhisperHeim.

.DESCRIPTION
    Full local deploy cycle:
      1. Stop any running WhisperHeim.exe (releases the file lock so publish
         can overwrite it).
      2. Publish src\WhisperHeim\WhisperHeim.csproj in Release / win-x64,
         self-contained, to .\publish.
      3. Publish src\WhisperHeim.Cli\WhisperHeim.Cli.csproj (the
         whisperheim-transcribe CLI wrapper, single-file self-contained) into the
         same .\publish folder so it ships alongside the tray exe.
      4. Launch the freshly published WhisperHeim.exe (detached).

.PARAMETER Configuration
    MSBuild configuration. Defaults to Release.

.PARAMETER NoLaunch
    Skip step 3 (do not start the new exe after publish).

.EXAMPLE
    .\scripts\publish.ps1
    .\scripts\publish.ps1 -NoLaunch
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'

$repoRoot   = Split-Path -Parent $PSScriptRoot
$project    = Join-Path $repoRoot 'src\WhisperHeim\WhisperHeim.csproj'
$cliProject = Join-Path $repoRoot 'src\WhisperHeim.Cli\WhisperHeim.Cli.csproj'
$publishDir = Join-Path $repoRoot 'publish'
$exePath    = Join-Path $publishDir 'WhisperHeim.exe'
$cliExePath = Join-Path $publishDir 'whisperheim-transcribe.exe'

# 1. Stop running instance(s) so the exe isn't locked.
$running = Get-Process -Name 'WhisperHeim' -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Stopping $($running.Count) running WhisperHeim process(es)..." -ForegroundColor Yellow
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

Write-Host "Publishing WhisperHeim ($Configuration, win-x64, self-contained)" -ForegroundColor Cyan

# 2. Publish. Why: PublishSingleFile=true makes this WPF app crash on startup
#    with DllNotFoundException, so we publish to a folder instead.
$publishArgs = @(
    'publish', $project,
    '-c', $Configuration,
    '-r', 'win-x64',
    '--self-contained',
    '-o', $publishDir,
    '-v', 'q'
)
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "publish failed (exit $LASTEXITCODE)" }

# 3. Publish the CLI wrapper into the same folder so whisperheim-transcribe.exe
#    ships next to WhisperHeim.exe (mirrors how Utterheim ships utterheim-speak).
#    The csproj already pins PublishSingleFile + SelfContained.
$cliArgs = @(
    'publish', $cliProject,
    '-c', $Configuration,
    '-r', 'win-x64',
    '-o', $publishDir,
    '-v', 'q'
)
& dotnet @cliArgs
if ($LASTEXITCODE -ne 0) { throw "cli publish failed (exit $LASTEXITCODE)" }

Write-Host ""
Write-Host "Published." -ForegroundColor Green
Write-Host "  Tray: $exePath"
Write-Host "  CLI : $cliExePath"

# 4. Launch detached so this script returns immediately.
if (-not $NoLaunch) {
    Write-Host ""
    Write-Host "Launching $exePath ..." -ForegroundColor Cyan
    Start-Process -FilePath $exePath -WorkingDirectory $publishDir
}
