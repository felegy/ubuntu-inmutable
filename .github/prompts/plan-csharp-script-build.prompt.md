## Plan: CSharp Script Build Migration

Convert the current compiled Bullseye build host into a repository-root C# script workflow with launcher scripts, while preserving the existing target semantics, CLI flags, CI behavior, and container security flow as closely as possible. The updated approach includes self-bootstrapping launcher scripts that verify `dotnet` availability first and, if missing, install the SDK into a project-local `.dotnet/` directory using the official `dotnet-install.sh` / `dotnet-install.ps1` helpers. The repository must ignore `.dotnet/` in both git and Docker build context.

### Steps
1. Create and switch to branch `feature/csharp-script-build` from `main`.
2. Add project-local .NET bootstrap infrastructure:
- keep `global.json` as the SDK version source of truth,
- add `.config/dotnet-tools.json` to pin `dotnet-script`,
- make launcher scripts install the SDK under `.dotnet/` when `dotnet` is not already available,
- use the official endpoints `https://dot.net/v1/dotnet-install.sh` and `https://dot.net/v1/dotnet-install.ps1`,
- use `--jsonfile global.json` / `-JSonFile global.json` with project-local install dir.
3. Replace the compiled build entrypoint with `build.csx` at repository root.
4. Add launcher scripts:
- `build.sh`
- `build.ps1`
- `build.cmd`
5. Add `.dotnet/` to `.gitignore` and `.dockerignore`.
6. Retire the compiled host files:
- `build/build.csproj`
- `build/Program.cs`
- `build/BuildContext.cs`
7. Update workflows, docs, and prompt files to use the script-based entrypoint.
8. Validate launcher bootstrap, `dotnet tool restore`, `dotnet script build.csx`, and workflow behavior.

### Relevant files
- `build.csx`
- `build.sh`
- `build.ps1`
- `build.cmd`
- `.config/dotnet-tools.json`
- `.gitignore`
- `.dockerignore`
- `.github/workflows/container.yml`
- `.gitea/workflows/container.yml`
- `README.md`
- `docs/ci-cd.md`

### Verification
1. `bash -n build.sh`
2. `dotnet tool restore`
3. `dotnet script build.csx -- print-context --push false --image local/ubuntu-inmutable`
4. `./build.sh print-context --push false --image local/ubuntu-inmutable`
5. `.dotnet/` stays excluded from git tracking and the Docker build context.

### Decisions
- Source of truth becomes `build.csx`.
- Launcher scripts are responsible for local `dotnet` bootstrap.
- `.dotnet/` is a project-local install directory and must not be committed.
