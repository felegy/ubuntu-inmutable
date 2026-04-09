# CI/CD Notes

This project uses a shared C# build orchestration layer with Bullseye.
Both GitHub Actions and Gitea Actions call the same entrypoint:

```bash
dotnet run --project build -- ci --push true --image <registry/image>
```

## Target Graph

- `restore`
- `verify-tools`
- `print-context`
- `container-build`
- `scan`
- `publish`
- `ci`

Dependency flow:

`ci -> publish -> scan -> container-build -> (restore, verify-tools, print-context)`

## Tag Policy

- Local/non-push builds: `dev`
- CI push builds: `latest`, `sha-<short>`

## Security Baseline

- Buildx runs with `--sbom=true` and `--provenance=true`.
- Trivy scan blocks on `HIGH` and `CRITICAL` vulnerabilities.

## GitHub Actions

Workflow file: `.github/workflows/container.yml`

Required permissions:

- `contents: read`
- `packages: write`

Authentication:

- Uses `GITHUB_TOKEN` via `docker/login-action` for GHCR publishing.

## Gitea Actions

Workflow file: `.gitea/workflows/container.yml`

Required secrets:

- `REGISTRY_USERNAME`
- `REGISTRY_TOKEN`
- Optional: `REGISTRY_HOST` if publishing somewhere else later.

The workflow mirrors the same Bullseye entrypoint and relies on compatible runners with Docker, Buildx, and .NET support.
