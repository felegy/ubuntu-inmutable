---
description: "Implement docker bundle major-version override (DOCKER_VERSION=27 style)"
name: "Docker Bundle Major Version"
argument-hint: "Optional major version (latest or integer, e.g. 27)"
agent: "agent"
---
Implement the approved plan for Docker bundle version externalization with these exact constraints:
- Accept only Docker major version input: `latest` or numeric major (example: `27`).
- Support both CLI and env fallback:
    - CLI flag: `--docker-version`
    - Env var: `DOCKER_VERSION`
- For docker bundle builds, pass `DOCKER_VERSION` as build-arg.
- In `bundles/docker/Containerfile`, resolve the newest available package matching the requested major for both `docker-ce` and `docker-ce-cli`.
- If requested major is unavailable, fail fast with a clear error message.
- Keep backward compatibility: default stays `latest`.

Scope of changes:
- `bundles/build.csx`
- `bundles/docker/Containerfile`
- `.github/workflows/container.yml`
- `.gitea/workflows/container.yml`
- `README.md`
- `docs/ci-cd.md`

Validation requirements:
- Build with `DOCKER_VERSION=latest` succeeds.
- Build with `DOCKER_VERSION=27` succeeds and installs major version 27.
- Non-existing major fails with clear message.
- CI env default/override path is documented and reflected in workflow.

Implementation notes:
- Keep changes minimal and focused.
- Follow repository formatting/editorconfig rules.
- Preserve existing behavior outside docker bundle version selection.
