# transcribe.ps1
#
# Core shim that POSTs an audio file's raw bytes to WhisperHeim's transcribe
# endpoint (default http://127.0.0.1:7777/transcribe) and prints ONLY the
# resulting transcript text to stdout. Bundled inside the whisperheim-transcribe
# plugin so consumers don't need a path to the WhisperHeim repo or the
# whisperheim-transcribe.exe CLI on PATH -- pure PowerShell + the loopback API
# (ADR-0001).
#
# Endpoint resolution (first hit wins):
#   1. -Endpoint parameter
#   2. $env:WHISPERHEIM_ENDPOINT
#   3. http://127.0.0.1:7777
#
# Exit codes (mirror the whisperheim-transcribe CLI):
#   0  success -- transcript (possibly empty for no-speech audio) printed to stdout
#   1  usage / file error -- no path, or missing / unreadable file (before any network call)
#   2  HTTP non-success -- server returned an error; body written to stderr
#   3  cannot reach the endpoint -- WhisperHeim not running / port unreachable / timeout

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Path,

    [string]$Endpoint,

    # 0 = infinite. Transcription blocks until the engine finishes; long files
    # can take minutes, so we wait indefinitely by default rather than risk a
    # client-side timeout mid-transcription.
    [int]$TimeoutSec = 0
)

$ErrorActionPreference = 'Stop'
$tool = 'whisperheim-transcribe'

. (Join-Path $PSScriptRoot 'transcribe-lib.ps1')

# --- 1. Validate the file before any network call (exit 1) ------------------
if ([string]::IsNullOrWhiteSpace($Path)) {
    [Console]::Error.WriteLine("${tool}: usage: transcribe.ps1 <path-to-audio-file>")
    exit 1
}
if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    [Console]::Error.WriteLine("${tool}: file not found: $Path")
    exit 1
}

try {
    $bytes = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Path).ProviderPath)
}
catch {
    [Console]::Error.WriteLine("${tool}: cannot read file: $Path ($($_.Exception.Message))")
    exit 1
}

# --- 2. Build the request ---------------------------------------------------
$endpointBase = Resolve-WhisperHeimEndpoint -Explicit $Endpoint -EnvValue $env:WHISPERHEIM_ENDPOINT
$url = Get-TranscribeUrl -Endpoint $endpointBase -FilePath $Path
$fileName = Split-Path -Path $Path -Leaf

# --- 3. POST raw bytes, map the outcome to an exit code ---------------------
try {
    $response = Invoke-WebRequest `
        -Uri $url `
        -Method Post `
        -ContentType 'application/octet-stream' `
        -Headers @{ 'X-Filename' = $fileName } `
        -Body $bytes `
        -TimeoutSec $TimeoutSec `
        -UseBasicParsing `
        -ErrorAction Stop

    $text = Get-TranscriptText -Json ([string]$response.Content)
    if ($null -eq $text) {
        [Console]::Error.WriteLine("${tool}: response had no 'text' field: $($response.Content)")
        exit 2
    }

    # Print only the transcript. Empty/no-speech audio prints a blank line.
    Write-Output $text
    exit 0
}
catch [System.Net.WebException] {
    $resp = $_.Exception.Response
    if ($resp) {
        # Got an HTTP response with a non-success status -> exit 2 + body to stderr.
        $status = [int]$resp.StatusCode
        $errBody = ''
        try {
            $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
            $errBody = $reader.ReadToEnd()
            $reader.Close()
        } catch { }
        [Console]::Error.WriteLine("${tool}: HTTP $status from $url")
        if (-not [string]::IsNullOrWhiteSpace($errBody)) { [Console]::Error.WriteLine($errBody) }
        exit 2
    }
    # No response at all -> couldn't reach the server.
    [Console]::Error.WriteLine("${tool}: cannot reach $url -- is WhisperHeim running?")
    exit 3
}
catch {
    [Console]::Error.WriteLine("${tool}: cannot reach $url -- is WhisperHeim running? ($($_.Exception.Message))")
    exit 3
}
