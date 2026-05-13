# Releasing Whisperheim

Whisperheim ships via [Velopack](https://docs.velopack.io) and GitHub Releases.
A push of a `v*` tag triggers `.github/workflows/release.yml`, which publishes
the app, packs it with `vpk`, and uploads everything to a GitHub Release named
after the tag.

## What gets uploaded

`vpk upload github` attaches the contents of `Releases/` to the Release:

- `WhisperHeim-{version}-win-Setup.exe` — single-file installer (primary user download)
- `WhisperHeim-{version}-full.nupkg` — full Velopack package
- `WhisperHeim-{version}-win-Delta.nupkg` — delta against the previous release (skipped on the first ever release)
- `RELEASES` — Velopack manifest consumed by in-app auto-update

The job summary also surfaces the **SHA-256 of `Setup.exe`** so it can be pasted
into the Release notes for users who want to verify the download.

## Local-iteration recipe (dry-run before tagging)

Reproduce the exact pipeline locally to smoke-test before pushing a tag:

```pwsh
# 1. Publish self-contained win-x64, ReadyToRun, no trimming
dotnet publish src/WhisperHeim/WhisperHeim.csproj `
  -c Release -r win-x64 --self-contained `
  -p:PublishReadyToRun=true `
  -o publish

# 2. Install vpk (one-time; pinned to the version the CI uses)
dotnet tool install -g vpk --version 0.0.1298

# 3. Pack — produces Releases/*-Setup.exe, *-full.nupkg, RELEASES
vpk pack `
  --packId WhisperHeim `
  --packVersion 0.0.1-local `
  --packDir publish `
  --mainExe WhisperHeim.exe `
  --packTitle "Whisperheim" `
  --packAuthors "Marco Heimeshoff"

# 4. Manually run the installer to verify it boots into the tray
.\Releases\WhisperHeim-0.0.1-local-win-Setup.exe
```

Delta packages require a previous release. To test delta packing locally, run
`vpk download github --repoUrl https://github.com/<owner>/<repo> --token <PAT>`
before `vpk pack`. On the very first release this is a no-op (and the CI step
is marked `continue-on-error` for that reason).

## Triggering a release

1. Bump the version, commit, push.
2. Tag and push:

   ```pwsh
   git tag v0.0.1
   git push origin v0.0.1
   ```

3. Watch the `Release` workflow run under **Actions**. On success, the Release
   appears at `https://github.com/<owner>/<repo>/releases/tag/v0.0.1` with
   `Setup.exe`, the nupkgs, and `RELEASES` attached.

4. Edit the Release notes to include:
   - SmartScreen click-through instructions (More info → Run anyway)
   - The SHA-256 from the workflow summary
   - The Smart App Control caveat (no override; signing arrives post-UG-registration)

   The Release-body template lives at
   [`.github/release-template.md`](../.github/release-template.md) — copy it
   into the Release editor and fill the four placeholders
   (`{{VERSION}}`, `{{SETUP_NAME}}`, `{{SHA256}}`, `{{CHANGES}}`).

## Surfacing the SHA-256 in the Release body

The workflow's final `Summary` step writes the SHA-256 to the GitHub Actions
job summary so it's one click away from the run. Two ways to get it from there
into the Release body the user actually sees:

1. **Manual (current default).** Open the job summary, copy the hash, paste it
   into the `{{SHA256}}` slot of the
   [`release-template.md`](../.github/release-template.md), then save the
   Release. This is the workflow assumed by Task 112.

2. **Automated (follow-up).** After `vpk upload github`, append a
   `gh release edit v${{ version }} --notes-file <rendered-template>` step
   that substitutes the placeholders. Sketch:

   ```pwsh
   $body = Get-Content .github/release-template.md -Raw
   $body = $body.Replace('{{VERSION}}',    '${{ steps.ver.outputs.version }}')
   $body = $body.Replace('{{SETUP_NAME}}', '${{ steps.hash.outputs.setup_name }}')
   $body = $body.Replace('{{SHA256}}',     '${{ steps.hash.outputs.sha256 }}')
   $body = $body.Replace('{{CHANGES}}',    '- (fill in or auto-generate from commits)')
   Set-Content rendered-notes.md -Value $body -Encoding utf8
   gh release edit v${{ steps.ver.outputs.version }} --notes-file rendered-notes.md
   ```

   Equivalent: `vpk upload --releaseNotes rendered-notes.md` consumes the same
   file. Either path keeps the SHA-256 in the user-visible body without any
   log grepping. Choose at implementation time; the manual paste is fine for
   the first few public releases.

## Signing

Code signing is **deferred** until the UG (Unternehmergesellschaft) is registered.
Microsoft Trusted Signing is not available to individual developers in Germany at
the time of writing, and EV certificates require a registered legal entity. Until
that lands, every release ships unsigned and relies on the SmartScreen
click-through documented in [`docs/why-unsigned.md`](why-unsigned.md).

When signing is enabled, the change is a 5-line diff to the `vpk pack` step in
[`.github/workflows/release.yml`](../.github/workflows/release.yml). No structural
change is required:

- `vpk` does **incremental** signing of both the app binaries and Velopack's own
  `Update.exe` / `Setup.exe` in the correct order, so signing MUST go through
  `vpk pack` -- a post-build `signtool` step would miss bootstrappers.
- The release workflow has an inline TODO at the `vpk pack` step naming both
  signing paths, the GitHub Secrets to add, and the exact flag to append.

### Path A -- Traditional signtool (PFX file + password)

Suitable for an OV / EV certificate held as a `.pfx` file. Requires the PFX to
land on the runner securely.

**GitHub Secrets to add:**

| Secret | Contents |
|---|---|
| `CERT_PFX_BASE64` | `base64`-encoded PFX bundle (private key + cert chain) |
| `CERT_PASSWORD` | Password for the PFX |

**Workflow changes** (sketch -- see the TODO block in `release.yml` for the exact
spot):

```yaml
- name: Decode signing certificate
  shell: pwsh
  env:
    CERT_PFX_BASE64: ${{ secrets.CERT_PFX_BASE64 }}
  run: |
    [IO.File]::WriteAllBytes("$env:RUNNER_TEMP\cert.pfx",
      [Convert]::FromBase64String($env:CERT_PFX_BASE64))

- name: Pack (vpk pack)
  shell: pwsh
  env:
    CERT_PASSWORD: ${{ secrets.CERT_PASSWORD }}
  run: |
    vpk pack `
      --packId WhisperHeim `
      --packVersion ${{ steps.ver.outputs.version }} `
      --packDir publish `
      --mainExe WhisperHeim.exe `
      --packTitle "Whisperheim" `
      --packAuthors "Marco Heimeshoff" `
      --signParams "/td sha256 /fd sha256 /tr http://timestamp.acs.microsoft.com /f $env:RUNNER_TEMP\cert.pfx /p $env:CERT_PASSWORD"
```

The Microsoft timestamp authority (`http://timestamp.acs.microsoft.com`) keeps
the signature valid past certificate expiry. Substitute the DigiCert or Sectigo
timestamp URL if preferred.

### Path B -- Azure Trusted Signing (preferred post-UG)

Microsoft's modern, cheap (~$10/month) signing path. No hardware token, no PFX
in CI, federated identity (OIDC) for auth.

**Prerequisites** (one-time, after UG registration):

1. Enroll the UG in Microsoft Trusted Signing via the Azure portal.
2. Create a Trusted Signing Account, an Identity Validation request, and a
   Certificate Profile.
3. Configure GitHub Actions OIDC federation against the Azure tenant.

**GitHub Secrets / variables:**

- No PFX or password secrets -- auth is OIDC.
- `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` (as variables or
  secrets) for `azure/login@v2`.

**`signing.json`** (committed to the repo; contains no secrets):

```json
{
  "Endpoint": "https://eus.codesigning.azure.net",
  "CodeSigningAccountName": "<account>",
  "CertificateProfileName": "<profile>"
}
```

**Workflow changes:**

```yaml
permissions:
  contents: write
  id-token: write       # required for OIDC federation

# ... earlier in the job:
- name: Azure login (OIDC)
  uses: azure/login@v2
  with:
    client-id: ${{ vars.AZURE_CLIENT_ID }}
    tenant-id: ${{ vars.AZURE_TENANT_ID }}
    subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}

- name: Pack (vpk pack)
  shell: pwsh
  run: |
    vpk pack `
      --packId WhisperHeim `
      --packVersion ${{ steps.ver.outputs.version }} `
      --packDir publish `
      --mainExe WhisperHeim.exe `
      --packTitle "Whisperheim" `
      --packAuthors "Marco Heimeshoff" `
      --azureTrustedSignFile signing.json
```

See [Velopack -- Code Signing](https://docs.velopack.io/packaging/signing) for
the canonical reference.

### SmartScreen reputation impact after signing

Signing does not automatically make every SmartScreen warning disappear. The
behaviour depends on the certificate type:

| Cert type | SmartScreen behaviour | Notes |
|---|---|---|
| **OV (Organisation Validated)** | Warnings persist until reputation accrues (~15 000 safe downloads in 25H2). Each new signed binary re-starts the warming period. | Cheaper but slow; reputation is per-binary, not per-publisher. |
| **EV (Extended Validation)** | Instant SmartScreen trust. No warming period. | Requires a hardware token; ~$300+/year. |
| **Azure Trusted Signing** | Instant SmartScreen trust (treated like EV). | ~$10/month. The right answer once the UG is registered. |

Numbers above come from
[`.workflow/research/installer-and-github-distribution.md`](../.workflow/research/installer-and-github-distribution.md) §5.
Smart App Control is unaffected by reputation -- it requires a signed binary
full stop. Once signing is on, `docs/why-unsigned.md` and the README
"Installation" disclaimer should be revised to drop the SAC caveat.

### What to do once signing is live

This is intentionally a tiny follow-up task, not part of Task 115:

1. Acquire the signing identity (Trusted Signing enrollment or PFX purchase).
2. Add the secrets / variables listed above to the repo.
3. Apply the diff at the TODO block in `.github/workflows/release.yml`.
4. Update `README.md` "Installation" section -- drop the unsigned disclaimer.
5. Update `docs/why-unsigned.md` -- either delete or convert to a historical
   note ("Whisperheim was unsigned through `v0.x`; from `v1.0` it ships signed").
6. Tag a fresh release and flag it as the first signed build in the Release
   notes.
7. If `Setup.exe` is still flagged by Defender after signing, submit it via
   the Microsoft false-positive form.

## Related tasks

- **Task 107** — Velopack bootstrap (custom `Main`, `App.xaml` as `Page`, etc.)
- **Task 109** — Small models bundled in the publish output, so CI doesn't need to fetch them
- **Task 110** — FFmpeg is user-installed; the workflow has no FFmpeg step
- **Task 112** — Public README + Release-body template + the SHA-256 surfacing recipe above
- **Task 114** — End-to-end pack dry run + first real tag (manual)
- **Task 115** — Code signing slot (documentation-only; references the TODO in this workflow)
