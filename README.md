# Ubuntu Inmutable OS Image

An immutable Ubuntu 24.04 container image with optional Kubernetes support (k3s or k0s) and secure supply chain practices. Multi-architecture builds with dynamic version resolution, vulnerability scanning, and provenance attestation.

## Quick Start: Using the Image

### Prerequisites

- Container runtime (Docker, Podman, or similar)
- For Kubernetes variants: sufficient resources to run k3s/k0s

### Pull and Run

The image is published to `ghcr.io/felegy/ubuntu-inmutable` with variants:

**Base image (no Kubernetes):**
```bash
docker run -it ghcr.io/felegy/ubuntu-inmutable:latest
```

**With k3s Kubernetes (minor version tags):**
```bash
docker run -it ghcr.io/felegy/ubuntu-inmutable:k3s-v1.27
docker run -it ghcr.io/felegy/ubuntu-inmutable:k3s-v1.28
docker run -it ghcr.io/felegy/ubuntu-inmutable:k3s-v1.29
```

**With k0s Kubernetes:**
```bash
docker run -it ghcr.io/felegy/ubuntu-inmutable:k0s-v1.27
```

### Image Variants

| Tag | Description |
|-----|-------------|
| `latest` | Base Ubuntu 24.04 with default tools (open-vm-tools) |
| `k3s-v1.X` | Ubuntu with k3s lightweight Kubernetes (minor version only) |
| `k0s-v1.X` | Ubuntu with k0s lightweight Kubernetes (minor version only) |
| `dev` | Development build tag (local builds) |

### Default Configuration

- **Base OS:** Ubuntu 24.04
- **Kairos Init:** 0.7.0 (immutable OS bootstrap)
- **Default User:** `kairos` with passwordless sudo
- **Variant:** `core` (minimal footprint)
- **Extra Packages:** `open-vm-tools` (VM guest agent)
- **Additional Packages:** Configurable via `--debs` flag during build

### Custom Deployment

Mount volumes and configure environment variables as needed:

```bash
docker run -it \
    -v /var/lib/rancher:/var/lib/rancher \
    -e KUBERNETES_SERVICE_HOST=k3s-master \
    ghcr.io/felegy/ubuntu-inmutable:k3s-v1.29
```

For persistent configuration, use Docker compose or container orchestration tools.

## Building the Image Locally

### Prerequisites

- Linux/macOS with Bash, or Windows with PowerShell
- Docker with Buildx (`docker buildx version`)
- .NET SDK 10.0+ (optional; build script bootstraps project-local installation)
- Trivy (optional locally; required in CI for vulnerability scanning)

### Quick Local Build

Build without publishing to registry:

```bash
./build.sh ci --push false
```

This produces a loadable image with tag `ghcr.io/local/ubuntu-inmutable:dev`.

### Build with Kubernetes Support

Build with k3s v1.29 (latest 1.29.x release):

```bash
KUBERNETES_DISTRO=k3s KUBERNETES_VERSION=v1.29.1+k3s1 ./build.sh ci --push false
```

Resolve k3s versions dynamically:

```bash
# Fetch latest 3 k3s releases and build matrix (local testing)
./build.sh print-context \
    --kubernetes-distro k3s \
    --skip-trivy-ignore false
```

### Build Options

The build system supports fine-grained configuration via CLI flags:

#### Image Configuration

- `--image ghcr.io/myregistry/ubuntu-inmutable`
    Container image name (default: `ghcr.io/local/ubuntu-inmutable`)

- `--image-version 1.0.0`
    Semantic version tag (default: git short SHA)

- `--image-extra-tag custom-tag`
    Additional image tag suffix (e.g., `ubuntu-inmutable:custom-tag-dev`)

- `--base-image ubuntu:22.04`
    Custom base image (default: `ubuntu:24.04`)

#### OS Configuration

- `--version 0.3.2`
    Kairos OS image version (required for `container-build` target)

- `--kairos-init-version 0.8.0`
    Kairos Init bootstrapper version (default: `0.7.0`)

- `--default-user ubuntu`
    Default unprivileged user name (default: `kairos`)

- `--variant immutable`
    Kairos variant type (default: `core`)

- `--model generic`
    Kairos model selection (default: `generic`)

- `--trusted-boot true`
    Enable Secure Boot and trusted boot measurement (default: `false`)

- `--debs "git curl jq"`
    Space-separated APT packages to install (default: `open-vm-tools`)

#### Kubernetes Configuration

- `--kubernetes-distro k3s`
    Kubernetes provider: `k3s` or `k0s` (optional)

- `--kubernetes-version v1.29.1+k3s1`
    Specific Kubernetes version (optional, overrides Containerfile default)

#### Security & Supply Chain

- `--push true`
    Publish to registry and generate provenance/SBOM attestations (default: `false`)

- `--skip-trivy-ignore false`
    Use `.trivyignore` file to suppress known upstream vulnerabilities (default: `false` = use ignore file)

- `--verbosity normal`
    Build output verbosity: `quiet`, `minimal`, `normal`, `detailed`, `diagnostic` (default: `normal`)

### Build Targets

#### Full CI Flow

```bash
./build.sh ci --push false
```

Runs: restore → verify-tools → print-context → container-build → scan → publish

#### Individual Targets

- **`restore`** – Install project dotnet tools from manifest
- **`verify-tools`** – Check Docker/Trivy availability
- **`print-context`** – Display resolved build configuration
- **`container-build`** – Build and load image with Buildx
- **`scan`** – Run Trivy HIGH/CRITICAL vulnerability scan
- **`publish`** – Push image and attestations to registry (requires `--push true`)

### Environment Variable Fallback

All build flags support environment variable fallback:

```bash
export KAIROS_VERSION=0.3.1
export KUBERNETES_DISTRO=k3s
export KUBERNETES_VERSION=v1.29.1+k3s1
export SKIP_TRIVY_IGNORE=true
export BUILD_VERBOSITY=normal

./build.sh ci --push false
```

In CI, `KAIROS_VERSION` is resolved from the latest repository tag (for example `v0.3.1` becomes `0.3.1`) with fallback `0.1` when no tags exist.

#### Supported Environment Variables

| Variable | Flag | Default |
|----------|------|---------|
| `KAIROS_VERSION` | `--version` | (required) |
| `KAIROS_INIT_VERSION` | `--kairos-init-version` | `0.7.0` |
| `KAIROS_DEFAULT_USER` | `--default-user` | `kairos` |
| `KAIROS_VARIANT` | `--variant` | `core` |
| `KAIROS_MODEL` | `--model` | `generic` |
| `KAIROS_TRUSTED_BOOT` | `--trusted-boot` | `false` |
| `KAIROS_DEBS` | `--debs` | `open-vm-tools` |
| `BASE_IMAGE` | `--base-image` | `ubuntu:24.04` |
| `KUBERNETES_DISTRO` | `--kubernetes-distro` | (none) |
| `KUBERNETES_VERSION` | `--kubernetes-version` | (none) |
| `IMAGE_EXTRA_TAG` | `--image-extra-tag` | (none) |
| `BUILD_VERBOSITY` | `--verbosity` | `normal` |
| `IS_PRIMARY_TAG_TARGET` | (CI matrix env) | `false` |
| `SKIP_TRIVY_IGNORE` | `--skip-trivy-ignore` | `false` |

### Direct Docker Build

For simple one-off builds without orchestration:

```bash
docker build \
    -f Containerfile \
    --build-arg BASE_IMAGE=ubuntu:24.04 \
    --build-arg VERSION=0.3.1 \
    --build-arg KUBERNETES_DISTRO=k3s \
    --build-arg KUBERNETES_VERSION=v1.29.1+k3s1 \
    -t local/ubuntu-inmutable:k3s-v1.29 \
    .
```

## CI/CD Pipeline

### GitHub Actions Matrix Build

The workflow automatically:

1. **Validates code** – EditorConfig compliance and ShellCheck linting
2. **Resolves `KAIROS_VERSION`** – Reads latest repository git tag and normalizes version
3. **Resolves k3s versions** – Fetches latest stable k3s releases via GitHub API
4. **Generates matrix** – 1 `iso` baseline + 3 k3s variants with minor-version tags
5. **Marks primary matrix entry** – Newest k3s entry gets `latest` and `sha-<short>` ownership
6. **Builds containers** – Parallel builds using Bullseye orchestration
7. **Scans security** – Trivy HIGH/CRITICAL vulnerability detection
8. **Attests supply chain** – SBOM and provenance generation
9. **Publishes** – Push to ghcr.io with deterministic tagging

Tag behavior in CI push builds:
- `latest` and `sha-<short>` are published only by the newest k3s matrix entry.
- Matrix-specific tags (`iso`, `k3s-vX.Y`) are published by their corresponding entries.

Release behavior:
- On `release.published`, GitHub Actions builds an offline ISO from `ghcr.io/<repo>:iso` and uploads ISO + checksum artifacts to the release assets.

Workflow triggers:
- Push to `main`
- Manual dispatch via `workflow_dispatch`
- `release.published` for ISO artifact generation and upload

### Vulnerability Management

The `.trivyignore` file temporarily suppresses known upstream vulnerabilities (e.g., CVE-2026-25679 in Go stdlib). For strict scanning without suppressions:

```bash
./build.sh ci --push false --skip-trivy-ignore true
```

See [docs/ci-cd.md](docs/ci-cd.md) for tracking and remediation details.

## Build System Architecture

### Bullseye Orchestration

The `build.csx` C# script uses [Bullseye](https://github.com/adamralph/bullseye) for task orchestration:

- Composable targets with dependency management
- Consistent exit codes for CI integration
- Minimal runtime dependencies (no GNU Make or shell scripting)

### Launcher Scripts

Platform-specific launchers bootstrap a project-local .NET SDK:

- **`build.sh`** – Bash (Linux/macOS)
- **`build.ps1`** – PowerShell (Windows)
- **`build.cmd`** – CMD (Windows legacy)

Use launchers if `dotnet` is not in `$PATH`; otherwise call `dotnet script build.csx` directly.

### Containerfile

Multi-stage Dockerfile:

1. **Base stage** – Ubuntu 24.04 + Kairos Init
2. **Optional Kubernetes** – k3s or k0s bootstrap (conditional)
3. **Custom packages** – APT install from `DEBS` build arg
4. **Metadata** – OCI image labels (created, revision, version)

## Development Notes

- **Planning prompts:** [.github/prompts/](.github/prompts/)  
- **Workspace conventions:** [.github/copilot-instructions.md](.github/copilot-instructions.md)
- **CI/CD details:** [docs/ci-cd.md](docs/ci-cd.md)
- **EditorConfig compliance:** Strict 2-space YAML, 4-space Markdown indentation
- **Commit format:** `<symbol> <TYPE>: <summary>` (e.g., `+ ADD: new feature`)
