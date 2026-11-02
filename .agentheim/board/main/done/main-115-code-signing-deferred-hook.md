---
id: main-115
title: Code Signing — Deferred Hook (Wire-Up Now, Flip Post-UG)
status: done
type: feature
context: main
created: 2026-05-12
completed: 2026-05-12
commit:
depends_on: [main-111]
blocks: []
tags: [m5, release]
related_adrs: []
related_research: []
prior_art: []
milestone: M5 - Public Release (GitHub Distribution)
size: Small
---
# Code Signing — Deferred Hook (Wire-Up Now, Flip Post-UG)

## Objective

Pre-wire the code-signing slot in the release workflow so flipping it on post-UG-registration is a one-line change, not a re-architecture. Leave explicit TODOs at every relevant spot.

## Details

### What's deferred

Code signing is blocked until the UG is registered (per `.workflow/research/auto-update-and-distribution.md` and `.workflow/research/legal-risks-commercial-release.md`):
- Microsoft Trusted Signing: EU individuals not supported as of 2026; orgs only.
- EV cert: requires registered business.
- OV cert: possible for individuals but needs 2–8 weeks of SmartScreen warming after each renewal.

So all M5 work ships unsigned. This task is the "make the future flip cheap" task.

### What this task does NOT do

- Purchase a certificate.
- Set up Azure Trusted Signing.
- Actually sign anything.

### What this task DOES

1. **Document the two signing paths** in `docs/release.md` (or wherever the release runbook lives — see Task 111):
   - **Traditional signtool** (PFX file + password): `--signParams "/td sha256 /fd sha256 /tr http://timestamp.acs.microsoft.com /f cert.pfx /p $PASSWORD"`. Requires the PFX file to land in the runner securely (GitHub Actions secret holding base64 of the PFX, decoded at job start).
   - **Azure Trusted Signing** (after UG): `--azureTrustedSignFile signing.json` with the JSON config committed (no secrets in it; Azure auth comes from federated identity / OIDC).
2. **Stub the workflow input.** Add an optional `cert_source` workflow_dispatch input or env var that gates a signing step. Default empty → unsigned. When populated, the step pipes the right flag into `vpk pack`. (Inline TODO comment also acceptable if the workflow_dispatch path adds noise.)
3. **Inline TODO at the `vpk pack` step** in `.github/workflows/release.yml`:
   ```yaml
   # TODO post-UG: insert one of
   #   --signParams "/td sha256 /fd sha256 /tr http://timestamp.acs.microsoft.com /f cert.pfx /p $env:CERT_PASSWORD"
   #   --azureTrustedSignFile signing.json
   ```
4. **README note** (delegated to Task 112): once signed, the README's "unsigned developer" disclaimer changes. Add a TODO bullet in Task 112's content noting the post-sign edit.
5. **Add `.github/workflows/release.yml` signing-secret references** as `secrets.CERT_PFX_BASE64`, `secrets.CERT_PASSWORD` placeholders — commented out or in a `# TODO post-UG` block. Documenting the names ahead of time means whoever wires it up later (could be future-us, could be a contractor) has zero guesswork.
6. **Document the SmartScreen reputation impact** in `docs/release.md`: even after signing, OV certs take ~15 000 safe downloads to clear SmartScreen warnings; EV certs and Trusted Signing give instant trust. Reference the 2026 numbers from `installer-and-github-distribution.md` §5.

### Future task (out of scope here)

When the UG is registered, file a single concrete "Enable code signing for releases" task that:
- Acquires the chosen cert / identity.
- Flips the workflow flag.
- Updates the README disclaimer (Task 112's note).
- Tags a fresh release as the first signed build.
- Submits the signed binary to Defender / SmartScreen if needed.

## Acceptance Criteria

- [ ] `docs/release.md` (or equivalent) documents both signtool and Azure Trusted Signing paths with exact `vpk pack` flags
- [ ] Inline TODO comment at the `vpk pack` step of `.github/workflows/release.yml` showing exactly where signing flags go
- [ ] Signing-secret names (`CERT_PFX_BASE64`, `CERT_PASSWORD`, or chosen alternatives) documented so a future task knows what to add to GitHub Secrets
- [ ] README disclaimer "we ship unsigned, signing planned post-UG" remains accurate (Task 112)
- [ ] Note in docs about post-sign SmartScreen reputation requirements (OV warming period vs EV/Trusted Signing instant trust)
- [ ] No certs purchased, no signing actually happening — this task is documentation + plumbing only

## Notes

- Source: `.workflow/research/installer-and-github-distribution.md` (2026-05-12), §3 "Code signing hook for later" + Implications #9. Also `.workflow/research/auto-update-and-distribution.md` (2026-03-27).
- Reference: [Code Signing | Velopack](https://docs.velopack.io/packaging/signing).
- The point of this task is to never have to think about the signing architecture again. The next signing-related task should be a 5-line PR.

## Work Log
<!-- Appended by /work during execution -->

### 2026-05-12 14:30 — Work Completed

**What was done:**
- Refined the inline TODO block at the `vpk pack` step in `.github/workflows/release.yml`. The TODO now names both signing paths (signtool + Azure Trusted Signing), the secret names (`CERT_PFX_BASE64`, `CERT_PASSWORD`) and OIDC variables, and the exact flag to append for each path. Placed as a header comment above `run:` so the diff to flip signing on is genuinely 5 lines.
- Expanded the "Signing" section of `docs/release.md` from a 3-line stub into a full deferred runbook. Documents both paths with copy-pasteable workflow snippets, lists the GitHub Secrets needed for Path A, OIDC prerequisites for Path B, an example `signing.json`, the SmartScreen reputation impact table (OV warming ~15 000 safe downloads vs EV/Trusted Signing instant trust), and a 7-step "what to do once signing is live" follow-up checklist.
- Verified the README "we ship unsigned" disclaimer in `README.md:21-31, 116-123` and the supporting `docs/why-unsigned.md`. Both are accurate and cross-link correctly. No edits made to README per task scope.
- Confirmed no certs were purchased, no Azure resources created, no signing step actually wired in -- this is documentation + plumbing only, as scoped.

**Acceptance criteria status:**
- [x] `docs/release.md` documents both signtool and Azure Trusted Signing paths with exact `vpk pack` flags — see new "Path A" and "Path B" subsections in the Signing section.
- [x] Inline TODO comment at the `vpk pack` step of `.github/workflows/release.yml` showing exactly where signing flags go — header comment block above `run:` enumerates both paths, both flags, and the surrounding scaffolding (decode step / `azure/login@v2`).
- [x] Signing-secret names (`CERT_PFX_BASE64`, `CERT_PASSWORD`) documented so a future task knows what to add to GitHub Secrets — named in both the workflow TODO and the docs/release.md Path A table.
- [x] README disclaimer "we ship unsigned, signing planned post-UG" remains accurate (Task 112) — verified at `README.md:21-31` and `:116-123`; cross-links to `docs/why-unsigned.md`, no changes needed.
- [x] Note in docs about post-sign SmartScreen reputation requirements (OV warming period vs EV/Trusted Signing instant trust) — see the "SmartScreen reputation impact after signing" subsection, citing the 2026 numbers from `installer-and-github-distribution.md` §5.
- [x] No certs purchased, no signing actually happening — confirmed; all signing code in the workflow remains commented and the `vpk pack` invocation is unchanged.

**Files changed:**
- `.github/workflows/release.yml` — replaced one-line `# TODO post-UG` comment with a structured TODO header documenting both signing paths, the secret/variable names, and the scaffolding steps.
- `docs/release.md` — expanded "Signing" section from stub into full deferred-runbook with both paths, secrets table, OIDC sketch, SmartScreen reputation table, and follow-up checklist.
