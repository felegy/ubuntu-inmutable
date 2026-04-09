## Generate GitHub + Gitea Container CI/CD Scaffold

Please scaffold a complete container build/publish foundation in an empty repository, centered around a `Containerfile`, with parallel GitHub Actions and Gitea Actions support.

The orchestration must be driven by a repository-root C# script using Bullseye, with `build.sh`, `build.ps1`, and `build.cmd` acting as thin launchers. If `dotnet` is missing locally, the launchers must install it into a project-local `.dotnet/` directory using the official `dotnet-install` scripts.

### Goal
- On `main` branch push, automatically build and publish the image to the `ghcr.io` registry.
- Medium supply-chain baseline: SBOM + vulnerability scan + provenance/attestation.
- A consistent tag policy across both CI systems.
- One shared build orchestration source of truth (`build.csx`) used by both CI systems through launcher scripts.

### Requirements
1. Create the required files and folders:
- `Containerfile`
- `.dockerignore`
- `.github/workflows/container.yml`
- `.gitea/workflows/container.yml`
- `.config/dotnet-tools.json`
- `build.csx`
- `build.sh`
- `build.ps1`
- `build.cmd`
- `README.md`
- `docs/ci-cd.md`

2. The `Containerfile` must provide a secure baseline:
- pinned base image tag,
- non-root user,
- explicit `WORKDIR`,
- minimal runtime layers.

3. The GitHub workflow (`.github/workflows/container.yml`) must support:
- trigger: `push` only on the `main` branch,
- checkout,
- .NET SDK setup,
- call the launcher/script entrypoint (for example: `./build.sh ci --push true --image "$IMAGE_NAME"`),
- buildx + cache,
- SBOM + provenance attestation,
- vulnerability scan (fail on critical/high),
- push to `ghcr.io`.

4. The Gitea workflow (`.gitea/workflows/container.yml`) must mirror the same logic:
- `main` push trigger,
- .NET setup + same launcher/script entrypoint,
- build + scan + SBOM/provenance,
- identical tag policy,
- publish to `ghcr.io`,
- runner-compatible action/tool choices.

5. Centralize CI orchestration in Bullseye targets inside `build.csx`:
- implement targets such as `restore`, `lint`, `container-build`, `sbom`, `scan`, `publish`, `ci`,
- keep dependency/order logic in Bullseye target graph,
- implement tag generation (`latest` + `sha-<short>`) in C# code,
- keep launcher scripts limited to bootstrap and argument forwarding.

6. Add project-local .NET bootstrap behavior:
- launcher scripts must verify whether `dotnet` exists,
- if not, install it into `.dotnet/` using `dotnet-install.sh` / `dotnet-install.ps1`,
- set `DOTNET_ROOT` and update `PATH` for the current process,
- run `dotnet tool restore` before invoking `build.csx`,
- ensure `.dotnet/` is ignored by both git and Docker build context.

7. Documentation:
- `README.md`: local build and run instructions,
- `docs/ci-cd.md`: GitHub vs Gitea differences, required secrets, troubleshooting,
- document the Bullseye target graph, launcher behavior, and typical local commands.

### Secrets and Permissions
- On GitHub, use the minimum required permissions and `GITHUB_TOKEN` (`packages:write`).
- On Gitea, document the use of `REGISTRY_USERNAME`, `REGISTRY_TOKEN`, and optional `REGISTRY_HOST`.
- Pin action versions where reasonable.

### Expected Output
1. Create the files above with actual content.
2. At the end, provide a short summary of:
- which files were created,
- which Bullseye targets were defined and how they depend on each other,
- how the launcher scripts bootstrap `dotnet` and where it is installed,
- which tags the pipeline publishes,
- under what condition the security gate fails.

### Validation
- It should work locally: `docker build -f Containerfile -t local/test:dev .`
- It should work locally via launcher script: `./build.sh ci --push false --image local/ubuntu-inmutable`
- The workflows must be syntactically valid and follow a consistent logic.
