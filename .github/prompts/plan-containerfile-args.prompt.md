# Plan: Containerfile Build ARG Parametrization in build.csx

Expose all meaningful Containerfile `ARG` declarations as typed `System.CommandLine` options in `build.csx`,
with environment variable fallbacks as secondary defaults and hard-coded values as the final fallback.
The resolution chain is: `CLI flag → environment variable → hard-coded default`.

## Containerfile ARG Inventory

| Containerfile ARG    | Default in Containerfile  | Scope                              |
|----------------------|---------------------------|------------------------------------|
| `BASE_IMAGE`         | `ubuntu:24.04`            | Global — used in 2 `FROM` lines    |
| `KAIROS_INIT_VERSION`| `0.7.0`                   | First `FROM` stage                 |
| `DEFAULT_USER`       | `kairos`                  | `base-kairos` stage                |
| `DEBIAN_FRONTEND`    | `noninteractive`          | Internal APT — excluded from CLI   |
| `VARIANT`            | `core`                    | kairos-init args                   |
| `MODEL`              | `generic`                 | kairos-init args                   |
| `TRUSTED_BOOT`       | `false`                   | kairos-init args (string bool)     |
| `VERSION`            | *(none — required)*       | kairos-init version string         |
| `KUBERNETES_DISTRO`  | *(none — optional)*       | kubernetes provider flag           |
| `KUBERNETES_VERSION` | `${VERSION}`              | kubernetes version (k3s/k0s)       |
| `DEBS`               | `open-vm-tools`           | Extra APT packages                 |
| `IMAGE_CREATED`      | *(injected by build)*     | OCI label — already wired          |
| `IMAGE_REVISION`     | *(injected by build)*     | OCI label — already wired          |

`DEBIAN_FRONTEND` is intentionally excluded — it's a build-time apt concern, not a CI/CD parameter.

## CLI Option to Environment Variable Mapping

| CLI flag               | Env var              | `--build-arg` name    | Fallback value                      |
|------------------------|----------------------|-----------------------|-------------------------------------|
| `--base-image`         | `BASE_IMAGE`         | `BASE_IMAGE`          | `ubuntu:24.04`                      |
| `--kairos-init-version`| `KAIROS_INIT_VERSION`| `KAIROS_INIT_VERSION` | `0.7.0`                             |
| `--default-user`       | `KAIROS_DEFAULT_USER`| `DEFAULT_USER`        | `kairos`                            |
| `--variant`            | `KAIROS_VARIANT`     | `VARIANT`             | `core`                              |
| `--model`              | `KAIROS_MODEL`       | `MODEL`               | `generic`                           |
| `--trusted-boot`       | `KAIROS_TRUSTED_BOOT`| `TRUSTED_BOOT`        | `false`                             |
| `--image-version`      | `KAIROS_VERSION`     | `VERSION`             | `""` (warn outside CI, fail in CI)  |
| `--kubernetes-distro`  | `KUBERNETES_DISTRO`  | `KUBERNETES_DISTRO`   | `""` (skip `--build-arg` if empty)  |
| `--kubernetes-version` | `KUBERNETES_VERSION` | `KUBERNETES_VERSION`  | `""` (skip `--build-arg` if empty)  |
| `--debs`               | `KAIROS_DEBS`        | `DEBS`                | `open-vm-tools`                     |

## Implementation

### New Helper Methods

Add two resolution helpers for the CLI → env → default chain:

```csharp
// Resolve a string value from CLI option, then environment variable, then hard-coded default.
static string ResolveWithEnvFallback(string? cliValue, string envVar, string hardDefault)
{
    if (!string.IsNullOrEmpty(cliValue)) return cliValue;
    var envValue = Environment.GetEnvironmentVariable(envVar);
    if (!string.IsNullOrEmpty(envValue)) return envValue;
    return hardDefault;
}

// Resolve a bool value from CLI option, then environment variable, then hard-coded default.
static bool ResolveBoolWithEnvFallback(bool? cliValue, string envVar, bool hardDefault)
{
    if (cliValue.HasValue) return cliValue.Value;
    var envValue = Environment.GetEnvironmentVariable(envVar);
    if (!string.IsNullOrEmpty(envValue))
        return envValue.Equals("true", StringComparison.OrdinalIgnoreCase) || envValue == "1";
    return hardDefault;
}
```

### New SC.Option Definitions

Add 10 new options after the existing `imageOption`:

- `Option<string?>` for: `--base-image`, `--kairos-init-version`, `--default-user`, `--variant`, `--model`, `--image-version`, `--kubernetes-distro`, `--kubernetes-version`, `--debs`
- `Option<bool?>` for: `--trusted-boot`

All options are optional; defaults come from environment variables or hard-coded values.

### BuildContext Record Expansion

Add 10 new properties to `BuildContext`:
`BaseImage`, `KairosInitVersion`, `DefaultUser`, `Variant`, `Model`, `TrustedBoot` (bool), `Version`, `KubernetesDistro`, `KubernetesVersion`, `Debs`.

### container-build Target

Replace the static `buildArgs` string with a `List<string>` that conditionally appends
`KUBERNETES_DISTRO` and `KUBERNETES_VERSION` only when non-empty (Docker's Containerfile default
covers the empty case for `KUBERNETES_VERSION`).

### print-context Target

Extend to print all new fields. If `Version` is empty:
- Outside CI: print a warning.
- In CI: throw `InvalidOperationException` (fail fast before any build attempt).

## Validation

After implementation:

1. `dotnet script build.csx -- print-context` → defaults from hard-coded values
2. `KAIROS_VERSION=0.3.1 dotnet script build.csx -- print-context` → version from env var
3. `dotnet script build.csx -- print-context --image-version 0.3.1 --variant standard` → CLI overrides env
4. `dotnet script build.csx -- print-context` → warning (VERSION empty, outside CI)
5. `CI=true dotnet script build.csx -- print-context` → error (VERSION empty in CI)
6. `dotnet script build.csx -- --dry-run container-build --image-version 0.3.1` → correct `--build-arg` list
7. `dotnet script build.csx -- --dry-run container-build --image-version 0.3.1 --kubernetes-distro k3s --kubernetes-version v1.29.0` → includes `KUBERNETES_DISTRO` and `KUBERNETES_VERSION` args
8. Without `--kubernetes-distro`: `KUBERNETES_DISTRO` and `KUBERNETES_VERSION` are absent from the `--build-arg` list

## Files Modified

- `build.csx` — SC options, extraction block, BuildContext ctor call, `print-context`, `container-build`, new helpers, BuildContext class
