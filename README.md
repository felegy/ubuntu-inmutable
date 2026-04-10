# Ubuntu Inmutable OS image

This repository contains the initial planning and scaffolding direction for building an Ubuntu immutable OS image with container-based workflows. The current focus is a shared CI/CD approach for GitHub Actions and Gitea Actions, using a `Containerfile`, publishing to `ghcr.io`, and including a medium security baseline with SBOM, vulnerability scanning, and provenance.

Build orchestration is now driven by a repository-root C# script using Bullseye and thin launcher scripts for Linux, PowerShell, and CMD. The launcher scripts can bootstrap a project-local `.dotnet/` SDK installation when `dotnet` is not already available on the machine.

See the current planning prompts here:

- [.github/prompts/plan-container-build.prompt.md](.github/prompts/plan-container-build.prompt.md)
- [.github/prompts/plan-csharp-script-build.prompt.md](.github/prompts/plan-csharp-script-build.prompt.md)

## Local Build

Prerequisites:

- Bash or PowerShell launcher support
- Docker with Buildx
- Trivy (optional locally, required in CI)

Run full local CI flow without publishing:

```bash
./build.sh ci --push false --image local/ubuntu-inmutable
```

PowerShell alternative:

```powershell
./build.ps1 ci --push false --image local/ubuntu-inmutable
```

The launchers install `dotnet` into `.dotnet/` only when it is missing from the current machine.

Build the image directly:

```bash
docker build -f Containerfile -t local/ubuntu-inmutable:dev .
```

Additional CI/CD notes are available in [docs/ci-cd.md](docs/ci-cd.md).
