# Plan: CI Sync, Deterministic Tagging, Release ISO, and Verbosity

Implement the following in this repository.

## Objectives

1. Keep GitHub and Gitea CI in sync for core build behavior.
2. Derive `KAIROS_VERSION` from the latest repository git tag (fallback `0.1`).
3. Fix matrix tag race so `latest` and `sha-<short>` are published only by the newest k3s variant.
4. Keep variant tags (`iso`, `k3s-vX.Y`) on their own matrix entries.
5. Add Bullseye-compatible `--verbosity` handling in `build.csx` and propagate it through the pipeline.
6. On GitHub release publish, build ISO from the `:iso` image and upload ISO assets to the release.

## Scope

Files to update:

- `.github/workflows/container.yml`
- `.gitea/workflows/container.yml`
- `build.csx`
- `Containerfile`
- `README.md`
- `docs/ci-cd.md`

File to add:

- `.github/prompts/plan-ci-sync-release-verbosity.prompt.md`

## Constraints

1. Preserve Bullseye argument pass-through. Custom args must not break Bullseye-native options/targets.
2. Keep the existing CI image matrix concept (iso + latest 3 k3s minors).
3. Do not publish `latest`/`sha` from non-primary matrix entries.
4. Keep release upload GitHub-only unless explicitly requested for Gitea releases.
5. Keep workflow YAML valid and editorconfig-compliant.

## Required Changes

### 1) GitHub workflow

1. Add `release.published` trigger.
2. Add `contents: write` permission (keep `packages: write`).
3. Add version resolution from latest git tag with fallback `0.1`.
4. Extend matrix entries with a boolean marker (for example `is_primary_tag_target`).
5. Mark only the newest k3s entry as primary.
6. Pass `KAIROS_VERSION` and `IS_PRIMARY_TAG_TARGET` into the build job env.
7. Restrict release event matrix execution to `image_extra_tag == "iso"`.
8. Add release ISO job using auroraboot with:
   - `container_image=oci:ghcr.io/${{ github.repository }}:iso`
   - mounted `iso-config.yaml` as `/config.yaml`
   - mounted `build` as `/tmp/auroraboot`
   - `disable_netboot=true`
   - `disable_http_server=true`
   - `state_dir=/tmp/auroraboot`
9. Validate ISO output under `build/iso` and upload ISO + checksum files to release assets.

### 2) Gitea workflow

1. Mirror GitHub core CI graph for push/manual paths:
   - validation
   - version resolution
   - k3s matrix build
2. Use tag-derived `KAIROS_VERSION` and matrix `is_primary_tag_target` env wiring.
3. Keep registry auth via existing Gitea secrets.

### 3) build.csx

1. Add typed `--verbosity` option with allowed values:
   - `quiet`, `minimal`, `normal`, `detailed`, `diagnostic`
2. Resolve verbosity from CLI/env and store in `BuildContext`.
3. Preserve Bullseye pass-through by continuing to forward unmatched tokens.
4. Propagate verbosity where possible:
   - docker buildx progress/output behavior
   - trivy verbosity (`--quiet` where appropriate)
   - validation/context logging behavior
5. Fix tag logic:
   - `latest` + `sha-<short>` only when `Push=true` and `IsPrimaryTagTarget=true`
   - keep `IMAGE_EXTRA_TAG` output where present
   - keep local non-push as `dev`

### 4) Containerfile

1. Replace hardcoded `/kairos-init -l debug` with build-arg-driven log level.
2. Ensure install/init/validate invocations use the propagated log level.

## Acceptance Criteria

1. `KAIROS_VERSION` in CI comes from latest repo tag (with fallback `0.1`).
2. In matrix builds, only newest k3s image receives `latest` and `sha-<short>`.
3. `iso` and `k3s-vX.Y` tags are still published per matrix variant.
4. `--verbosity` affects build output and does not break Bullseye options.
5. GitHub release run builds ISO from `:iso` image and uploads ISO/checksum assets.
6. Gitea and GitHub core CI paths are logically aligned.

## Non-goals

1. Introducing a separate release workflow file.
2. Changing image naming conventions beyond required tag determinism.
3. Reworking unrelated build targets or repository structure.
