# CI/CD Notes

This project uses a shared C# script orchestration layer with Bullseye.
Both GitHub Actions and Gitea Actions call the same launcher entrypoint:

```bash
./build.sh ci --push true --image <registry/image>
```

The launcher scripts are responsible for:

- checking whether `dotnet` is already available,
- installing a project-local SDK into `.dotnet/` with the official `dotnet-install` scripts when needed,
- exporting `DOTNET_ROOT` and updating `PATH` for the current process,
- running `dotnet tool restore`,
- forwarding all arguments to `build.csx`.

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

## Local Bootstrap

- Project-local SDK install directory: `.dotnet/`
- Tool manifest: `.config/dotnet-tools.json`
- Official install scripts:
    - `https://dot.net/v1/dotnet-install.sh`
    - `https://dot.net/v1/dotnet-install.ps1`
- `.dotnet/` must stay excluded from both git and the Docker build context.

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

The workflow mirrors the same script entrypoint and relies on compatible runners with Docker, Buildx, and .NET support.
