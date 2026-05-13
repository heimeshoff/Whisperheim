# Why is Whisperheim unsigned?

Short version: code signing for individual developers in Germany is in an
awkward gap year. We'll sign as soon as we can. Until then, here's exactly what
you'll see, why, and how to verify the download yourself.

## What is SmartScreen?

Windows Defender SmartScreen is the reputation-based filter Windows 10/11
shows when you run an executable downloaded from the internet. It is **not** an
antivirus check — it asks "has this exact binary been downloaded and run safely
by lots of other Windows users?"

For a freshly built binary from a small project, the answer is "no, this is
new", and SmartScreen shows the **"Windows protected your PC"** dialog. The
fix is to click **More info** → **Run anyway**. SmartScreen does not block the
launch; it just adds friction.

Reputation accrues over time as more users run the binary safely. Code signing
with an EV certificate jumps a binary straight into "trusted" reputation, but
sufficient organic downloads also clear the threshold eventually (currently on
the order of ~15 000 safe downloads in 25H2).

## Why is Whisperheim flagged?

Two reasons:

1. **No code signature.** A signed binary inherits the publisher's reputation
   immediately. Whisperheim's `Setup.exe` is unsigned, so it has to earn
   reputation from scratch each release.
2. **It's a new binary.** Every Whisperheim release is a different `Setup.exe`
   with a different hash. SmartScreen warns on each one until enough downloads
   accumulate, which for a personal project never happens before the next
   release ships.

Both will be solved by signing.

## How can I verify the download is safe?

Three independent checks:

1. **Verify the SHA-256.** Every Release publishes the hash of `Setup.exe` in
   the Release notes. After downloading, run:

   ```pwsh
   certutil -hashfile WhisperHeim-<version>-win-Setup.exe SHA256
   ```

   The hash must match the one in the Release notes byte-for-byte. If it
   doesn't, the download was corrupted or tampered with — delete it.

2. **Read the source.** Whisperheim is open-source. The release workflow at
   [`.github/workflows/release.yml`](../.github/workflows/release.yml) is the
   exact recipe that builds the `Setup.exe` you downloaded. `git log` and `git
   blame` are your audit trail.

3. **Check the network.** Whisperheim's only outbound traffic on a fresh
   install is the one-time ~640 MB Parakeet model download from the Hugging
   Face CDN, and the periodic auto-update check against GitHub Releases. No
   telemetry, no analytics, no usage pings — verify with any process-level
   network monitor (Wireshark, Fiddler, etc.).

## What is Smart App Control and why might it block the install entirely?

Smart App Control (SAC) is a stricter cousin of SmartScreen that landed with
Windows 11. SAC will **hard-block** unsigned binaries — there is no "Run
anyway" option, no override. The dialog reads "Smart App Control" rather than
"Windows Defender SmartScreen".

SAC is opt-in and starts in "evaluation mode" on fresh Windows 11 installs.
Microsoft transitions it to "on" or "off" automatically based on the user's
behavior over the first few weeks. On a new 25H2 machine, you may end up with
SAC enforcing without ever having clicked anything.

If SAC blocks the install:

- **The only "fix" is to turn SAC off**, which is **irreversible without a
  full OS reinstall**. We do not recommend doing this just for Whisperheim.
- **Or wait for a signed build.** Signed binaries are trusted by SAC without
  intervention.

## When will Whisperheim be signed?

The plan, in order:

1. **Register the UG (Unternehmergesellschaft).** Microsoft Trusted Signing
   (the cheap, modern signing path that has effectively replaced individual EV
   certificates) is not available to individual developers in Germany at the
   time of writing. A registered legal entity changes that.
2. **Enroll in Microsoft Trusted Signing.** Roughly $10/month, no hardware
   token required, integrates directly with `signtool` and Velopack.
3. **Flip the signing flags in the release workflow.** A single TODO comment
   in [`.github/workflows/release.yml`](../.github/workflows/release.yml)
   marks the spot — `--signParams` or `--azureTrustedSignFile`. No structural
   change required.

Tracking ticket: see Task 115 (`code-signing-deferred-hook`) and the project
[roadmap](../.workflow/roadmap.md).

## What can I do in the meantime?

- Run from source (`dotnet run`) if you don't trust the binary.
- Verify the SHA-256 against the Release notes before you install.
- If SmartScreen blocks: **More info → Run anyway**.
- If Smart App Control blocks: turn SAC off (not recommended just for us) or
  wait for a signed release.
- If you find a binary that doesn't match the published SHA-256, please open
  a GitHub issue immediately.
