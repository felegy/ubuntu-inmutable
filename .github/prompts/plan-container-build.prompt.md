## Generate GitHub + Gitea Container CI/CD Scaffold

Please scaffold a complete container build/publish foundation in an empty repository, centered around a `Containerfile`, with parallel GitHub Actions and Gitea Actions support.

The orchestration must be driven by a C# build project using Bullseye (in the style of `mysticmind/bullseye-sample`), not by shell scripts as the primary control layer.

### Goal
- On `main` branch push, automatically build and publish the image to the `ghcr.io` registry.
- Medium supply-chain baseline: SBOM + vulnerability scan + provenance/attestation.
- A consistent tag policy across both CI systems.
- One shared build orchestration entrypoint (`dotnet run --project build`) used by both CI systems.

### Requirements
1. Create the required files and folders:
- `Containerfile`
- `.dockerignore`
- `.github/workflows/container.yml`
- `.gitea/workflows/container.yml`
- `build/build.csproj`
- `build/Program.cs`
- `build/BuildContext.cs` (or equivalent options/config class)
- `build/Targets/` (optional split per concern)
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
- call Bullseye targets via `dotnet run --project build -- <target>` (for example: `ci`),
- buildx + cache,
- SBOM + provenance attestation,
- vulnerability scan (fail on critical/high),
- push to `ghcr.io`.

4. The Gitea workflow (`.gitea/workflows/container.yml`) must mirror the same logic:
- `main` push trigger,
- .NET setup + same Bullseye entrypoint,
- build + scan + SBOM/provenance,
- identical tag policy,
- publish to `ghcr.io`,
- runner-compatible action/tool choices.

5. Centralize CI orchestration in Bullseye targets (C#):
- implement targets such as `restore`, `lint`, `container-build`, `sbom`, `scan`, `publish`, `ci`,
- keep dependency/order logic in Bullseye target graph,
- implement tag generation (`latest` + `sha-<short>`) in C# code,
- keep shell scripts optional helpers only (not orchestration source of truth).

6. Use Bullseye + SimpleExec style:
- execute external commands from C# (docker/buildx/scanners) via `SimpleExec`,
- keep command arguments explicit and reproducible,
- expose key parameters (image name, registry, tag, push true/false) via args/env.

7. Documentation:
- `README.md`: local build and run instructions,
- `docs/ci-cd.md`: GitHub vs Gitea differences, required secrets, troubleshooting,
- document the Bullseye target graph and typical local commands.

### Secrets and Permissions
- On GitHub, use the minimum required permissions and `GITHUB_TOKEN` (`packages:write`).
- On Gitea, document the use of `REGISTRY_USERNAME`, `REGISTRY_TOKEN`, and optional `REGISTRY_HOST`.
- Pin action versions where reasonable.

### Expected Output
1. Create the files above with actual content.
2. At the end, provide a short summary of:
- which files were created,
- which Bullseye targets were defined and how they depend on each other,
- which tags the pipeline publishes,
- under what condition the security gate fails.

### Validation
- It should work locally: `docker build -f Containerfile -t local/test:dev .`
- It should work locally via Bullseye: `dotnet run --project build -- ci --push false`
- The workflows must be syntactically valid and follow a consistent logic.
