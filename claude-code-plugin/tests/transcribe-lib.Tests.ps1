# transcribe-lib.Tests.ps1
#
# Pester spec for the whisperheim-transcribe plugin's pure functions:
#   Resolve-WhisperHeimEndpoint  - explicit > env value > loopback default
#   Get-TranscribeUrl            - build <endpoint>/transcribe?filename=<name>
#   Get-TranscriptText           - pull the "text" field out of a response body
#
# Compatible with Pester 3.x (bundled with Windows PowerShell 5.1) and Pester 5.x.
# Run:  Invoke-Pester -Path .\tests\transcribe-lib.Tests.ps1

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $here '..\scripts\transcribe-lib.ps1')

Describe 'Resolve-WhisperHeimEndpoint' {

    It 'uses the loopback default when nothing is supplied' {
        Resolve-WhisperHeimEndpoint -Explicit $null -EnvValue $null | Should Be 'http://127.0.0.1:7777'
        Resolve-WhisperHeimEndpoint -Explicit ''   -EnvValue ''     | Should Be 'http://127.0.0.1:7777'
        Resolve-WhisperHeimEndpoint -Explicit '  ' -EnvValue '   '  | Should Be 'http://127.0.0.1:7777'
    }

    It 'uses the env value when set and no explicit value' {
        Resolve-WhisperHeimEndpoint -Explicit $null -EnvValue 'http://127.0.0.1:9000' | Should Be 'http://127.0.0.1:9000'
    }

    It 'lets an explicit value win over the env value' {
        Resolve-WhisperHeimEndpoint -Explicit 'http://host:1234' -EnvValue 'http://127.0.0.1:9000' | Should Be 'http://host:1234'
    }

    It 'trims surrounding whitespace' {
        Resolve-WhisperHeimEndpoint -Explicit '  http://host:1234  ' -EnvValue $null | Should Be 'http://host:1234'
    }
}

Describe 'Get-TranscribeUrl' {

    It 'appends /transcribe with a filename hint' {
        Get-TranscribeUrl -Endpoint 'http://127.0.0.1:7777' -FilePath 'C:\audio\clip.ogg' |
            Should Be 'http://127.0.0.1:7777/transcribe?filename=clip.ogg'
    }

    It 'trims a trailing slash on the endpoint' {
        Get-TranscribeUrl -Endpoint 'http://127.0.0.1:7777/' -FilePath 'C:\audio\clip.ogg' |
            Should Be 'http://127.0.0.1:7777/transcribe?filename=clip.ogg'
    }

    It 'url-encodes spaces in the filename' {
        Get-TranscribeUrl -Endpoint 'http://127.0.0.1:7777' -FilePath 'C:\audio\voice message.m4a' |
            Should Be 'http://127.0.0.1:7777/transcribe?filename=voice%20message.m4a'
    }

    It 'uses only the leaf filename, not the directory' {
        Get-TranscribeUrl -Endpoint 'http://127.0.0.1:7777' -FilePath 'C:\some\deep\path\note.wav' |
            Should Be 'http://127.0.0.1:7777/transcribe?filename=note.wav'
    }
}

Describe 'Get-TranscriptText' {

    It 'returns the transcript for a valid response' {
        Get-TranscriptText -Json '{"text":"hello world","audioDurationSeconds":42.13,"chunkCount":3}' |
            Should Be 'hello world'
    }

    It 'returns an empty string for no-speech audio (text:"")' {
        Get-TranscriptText -Json '{"text":"","audioDurationSeconds":1.0,"chunkCount":0}' | Should Be ''
    }

    It 'returns empty string when text is explicitly null' {
        Get-TranscriptText -Json '{"text":null}' | Should Be ''
    }

    It 'returns null when the text field is absent (e.g. an error body)' {
        Get-TranscriptText -Json '{"error":"something broke"}' | Should Be $null
    }

    It 'returns null for empty / whitespace / non-JSON bodies' {
        Get-TranscriptText -Json ''        | Should Be $null
        Get-TranscriptText -Json '   '     | Should Be $null
        Get-TranscriptText -Json $null     | Should Be $null
        Get-TranscriptText -Json 'not json' | Should Be $null
    }
}
