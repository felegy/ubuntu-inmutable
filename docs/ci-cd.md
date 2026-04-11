# CI/CD Notes

This project uses a shared C# script orchestration layer with Bullseye.
Both GitHub Actions and Gitea Actions call the same launcher entrypoint:

```bash
./build.sh ci --push true --image <registry/image> --verbosity normal
```

Bundle builds support Docker major-version selection through `DOCKER_VERSION`.
Allowed values are `latest` or a numeric major (for example `27`).
When a major is provided, the docker bundle resolves the newest available patch release in the Docker apt repository.

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
- CI push builds (newest k3s matrix entry only): `latest`, `sha-<short>`
- CI matrix variant builds: always publish their own `IMAGE_EXTRA_TAG` (for example `iso`, `k3s-v1.29`)

`KAIROS_VERSION` is resolved in CI from the latest repository tag (optional `v` prefix removed), with fallback `0.1` when no tags exist.

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

- `contents: write`
- `packages: write`

Authentication:

- Uses `GITHUB_TOKEN` via `docker/login-action` for GHCR publishing.

Bundle workflow environment:

- `DOCKER_VERSION` defaults to `latest` and can be overridden via repository variable `DOCKER_VERSION`.

Release behavior:

- Trigger: `release.published`
- Builds offline ISO from `ghcr.io/<repo>:iso` using auroraboot
- Uploads `*.iso`, `*.sha256`, and normalized checksum assets to the GitHub release

## Gitea Actions

Workflow file: `.gitea/workflows/container.yml`

Required secrets:

- `REGISTRY_USERNAME`
- `REGISTRY_TOKEN`
- Optional: `REGISTRY_HOST` if publishing somewhere else later.

The workflow mirrors the same script entrypoint and relies on compatible runners with Docker, Buildx, and .NET support.

Bundle workflow environment:

- `DOCKER_VERSION` defaults to `latest` and can be overridden via variable `DOCKER_VERSION`.

## Verbosity

`build.csx` supports `--verbosity` with values:

- `quiet`
- `minimal`
- `normal`
- `detailed`
- `diagnostic`

The selected verbosity is propagated to buildx progress mode, Trivy output, and kairos-init logging during image build.
