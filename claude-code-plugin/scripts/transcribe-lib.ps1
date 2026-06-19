# transcribe-lib.ps1
#
# Pure, dependency-free helpers shared by the whisperheim-transcribe plugin.
# Dot-sourced by transcribe.ps1 and by the Pester spec. No I/O at load time,
# no network — every function is a pure transform of its inputs so it can be
# unit-tested without a running WhisperHeim. Mirrors the WhisperHeim.Cli
# `CliCore` helpers (ResolveEndpoint / BuildTranscribeUrl / ExtractText), the
# same wire contract from ADR-0001.
#
# Exposes:
#   Resolve-WhisperHeimEndpoint  - explicit > env value > loopback default
#   Get-TranscribeUrl            - build <endpoint>/transcribe?filename=<name>
#   Get-TranscriptText           - pull the "text" field out of a response body

Set-Variable -Name WhisperHeimDefaultEndpoint -Value 'http://127.0.0.1:7777' -Option Constant -Scope Script -ErrorAction SilentlyContinue

# Resolve the base endpoint: an explicit value wins, then the (already-read)
# env value, then the loopback default. Pure — the caller passes the env value
# in rather than this function reading $env, so it stays testable.
function Resolve-WhisperHeimEndpoint {
    [CmdletBinding()]
    param(
        [AllowNull()][AllowEmptyString()][string]$Explicit,
        [AllowNull()][AllowEmptyString()][string]$EnvValue
    )
    if (-not [string]::IsNullOrWhiteSpace($Explicit)) { return $Explicit.Trim() }
    if (-not [string]::IsNullOrWhiteSpace($EnvValue))  { return $EnvValue.Trim() }
    return $script:WhisperHeimDefaultEndpoint
}

# Build the full /transcribe URL with the filename hint so the server preserves
# the extension for its decoder (ADR-0001: ?filename= / X-Filename).
function Get-TranscribeUrl {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Endpoint,
        [Parameter(Mandatory = $true)][string]$FilePath
    )
    $fileName = Split-Path -Path $FilePath -Leaf
    $encoded  = [uri]::EscapeDataString($fileName)
    return ($Endpoint.TrimEnd('/')) + '/transcribe?filename=' + $encoded
}

# Extract the transcript from a /transcribe JSON response body.
#   - returns the (possibly empty) transcript when a string "text" field exists
#     (empty/no-speech audio legitimately returns "text": "")
#   - returns $null when the body is empty, not JSON, or has no string "text"
function Get-TranscriptText {
    [CmdletBinding()]
    param([AllowNull()][AllowEmptyString()][string]$Json)

    if ([string]::IsNullOrWhiteSpace($Json)) { return $null }

    try { $obj = $Json | ConvertFrom-Json -ErrorAction Stop }
    catch { return $null }

    if ($null -eq $obj) { return $null }
    if (-not ($obj.PSObject.Properties.Name -contains 'text')) { return $null }

    $text = $obj.text
    if ($null -eq $text) { return '' }
    return [string]$text
}
