## Generate GitHub + Gitea Container CI/CD Scaffold

Please scaffold a complete container build/publish foundation in an empty repository, centered around a `Containerfile`, with parallel GitHub Actions and Gitea Actions support.

### Goal
- On `main` branch push, automatically build and publish the image to the `ghcr.io` registry.
- Medium supply-chain baseline: SBOM + vulnerability scan + provenance/attestation.
- A consistent tag policy across both CI systems.

### Requirements
1. Create the required files and folders:
- `Containerfile`
- `.dockerignore`
- `.github/workflows/container.yml`
- `.gitea/workflows/container.yml`
- `scripts/ci/tags.sh`
- `scripts/ci/build.sh`
- `scripts/ci/scan.sh`
- `README.md`
- `docs/ci-cd.md`

2. The `Containerfile` must provide a secure baseline:
- pinned base image tag,
- non-root user,
- explicit `WORKDIR`,
- minimal runtime layers.

3. The GitHub workflow (`.github/workflows/container.yml`) must support:
- trigger: `push` only on the `main` branch,
- checkout + metadata/tag generation,
- buildx + cache,
- SBOM + provenance attestation,
- vulnerability scan (fail on critical/high),
- push to `ghcr.io`.

4. The Gitea workflow (`.gitea/workflows/container.yml`) must mirror the same logic:
- `main` push trigger,
- build + scan + SBOM/provenance,
- identical tag policy,
- publish to `ghcr.io`,
- runner-compatible action/tool choices.

5. Under `scripts/ci/`, centralize the shared CI logic:
- `tags.sh`: `latest` + `sha-<short>` tags,
- `build.sh`: shared build parameters/cache,
- `scan.sh`: scanner invocation and severity threshold.

6. Documentation:
- `README.md`: local build and run instructions,
- `docs/ci-cd.md`: GitHub vs Gitea differences, required secrets, troubleshooting.

### Secrets and Permissions
- On GitHub, use the minimum required permissions and `GITHUB_TOKEN` (`packages:write`).
- On Gitea, document the use of `REGISTRY_USERNAME`, `REGISTRY_TOKEN`, and optional `REGISTRY_HOST`.
- Pin action versions where reasonable.

### Expected Output
1. Create the files above with actual content.
2. At the end, provide a short summary of:
- which files were created,
- which tags the pipeline publishes,
- under what condition the security gate fails.

### Validation
- It should work locally: `docker build -f Containerfile -t local/test:dev .`
- The workflows must be syntactically valid and follow a consistent logic.
