# Bundle Testing Strategy

This document describes the three-level testing approach for Ubuntu Immutable bundles.

## Overview

Bundles are container images that extend Kairos with additional tools (docker, helm, k9s, etc.). Testing ensures they build correctly, package the right contents, and install properly in Kairos environments.

### Bundle Architecture

Each bundle:
```
Bundle Image (Container)
├── Binary/packages (docker debs, helm binary, etcd, k9s, podman)
├── /run.sh (wrapper script)
├── /run.csx (C# installation script)
└── /Dockerfile (build definition)
```

During Kairos installation, bundles are:
1. Downloaded from registry as container images
2. Extracted into the target rootfs
3. `/run.sh` executed, which runs `run.csx` via dotnet
4. `run.csx` copies/installs files to `/usr/local/bin` or similar

## Testing Levels

### Level 1: CLI/Orchestration Testing ✅

**Test File:** `tests/bundle-test.sh`

**What it validates:**
- CLI options (`--docker-version`, `--bundle`, etc.)
- Environment variable fallback (`DOCKER_VERSION` env var)
- CLI override precedence (flag > env > default)
- Bundle discovery (all 5 bundles detected)
- Default behaviors

**Run:**
```bash
./tests/bundle-test.sh
```

**Why:** Fast, unit-test level validation. Runs in seconds. No Docker needed.

**Current Status:** ✅ 8/8 tests passing

---

### Level 2: Container Build Verification

**Test Files:** `tests/bundle-e2e.sh` (comprehensive) or `tests/bundle-e2e-simple.sh` (targeted)

**What it validates:**
- Bundle images actually build with docker buildx
- Docker version parameter works (e.g., DOCKER_VERSION=27 resolves to 5:27.5.1)
- Bundle structure verified (Containerfile, run.sh, run.csx present)
- Architecture-specific builds (amd64/arm64 support)

**Run comprehensive test (all bundles):**
```bash
./tests/bundle-e2e.sh
```

**Run targeted test (single bundle, faster):**
```bash
./tests/bundle-e2e-simple.sh        # Tests helm (default, ~2 min)
./tests/bundle-e2e-simple.sh docker # Tests docker bundle (~5-10 min)
```

**Why:** Validates real image builds, catches dockerfile errors, verifies version resolution.

**Test Duration:** 2-10 minutes per bundle (includes docker buildx, dotnet download, container operations)

---

### Level 3: Full Kairos Installation Simulation

**Recommended Approach:**

For true end-to-end Kairos simulation, create a Dockerfile that:
1. Starts with Ubuntu base
2. Adds bundle container as a layer
3. Runs bundle `/run.sh` 
4. Verifies installation

**Example (manual test for docker bundle):**

```dockerfile
FROM ubuntu:24.04 AS bundle
# This simulates pulling the built bundle image
# In practice: FROM ghcr.io/local:docker-latest

FROM ubuntu:24.04
# Copy bundle contents (simulating kairos-agent extraction)
COPY --from=bundle / /
# Run bundle installation (simulating Kairos)
RUN /run.sh
# Verify installation
RUN which docker && docker --version
```

**Build and test manually:**
```bash
# Build bundle
dotnet script bundles/build.csx -- bundle-ci --bundle docker --push false

# Create test Dockerfile
cat > Dockerfile.bundle-test << 'EOF'
FROM ubuntu:24.04
RUN apt-get update && apt-get install -y curl ca-certificates
RUN mkdir -p /dotnet && \
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o /dotnet/dotnet-install.sh && \
    chmod +x /dotnet/dotnet-install.sh && \
    /dotnet/dotnet-install.sh --version 10.0.0 --runtime dotnet --install-dir /dotnet --no-path
COPY --from=ghcr.io/local:docker-latest / ./bundle-contents/
RUN cd ./bundle-contents && DOTNET_ROOT=/dotnet /run.sh
RUN which docker-compose && docker-compose --version
EOF

# Test
docker build -f Dockerfile.bundle-test -t test-docker-bundle .
docker run --rm test-docker-bundle docker --version
```

---

## Bundle-Specific Verification

### Docker Bundle
```bash
# Check that docker packages are present
docker run --rm ghcr.io/local:docker-latest ls -la deb/
# Should contain: docker-ce_*.deb, docker-ce-cli_*.deb, etc.

# Verify version resolution works
DOCKER_VERSION=27 dotnet script bundles/build.csx -- print-context --bundle docker
# Should show DockerVersion: 27 in output
```

### Helm Bundle
```bash
docker run --rm ghcr.io/local:helm-latest /bin/bash -c 'ls -l helm && file helm'
# Should show: ELF 64-bit binary
```

### K9S Bundle
```bash
docker run --rm ghcr.io/local:k9s-latest /bin/bash -c 'ls -l k9s && ./k9s version'
```

### Etcd Bundle
```bash
docker run --rm ghcr.io/local:etcd-latest /bin/bash -c 'ls -l etcd etcdctl'
# Should show both binaries
```

### Podman Bundle  
```bash
docker run --rm ghcr.io/local:podman-latest /bin/bash -c 'apt-cache --help | head -1'
# Podman deb packages should be present
```

---

## CI Integration

E2E bundle testing is now integrated in both workflows:

- GitHub: `.github/workflows/container.yml`
- Gitea: `.gitea/workflows/container.yml`

Current behavior:

- Pull requests to `main`: runs smoke E2E (`./tests/bundle-e2e.sh helm`)
- Push/release/manual runs: runs full E2E (`./tests/bundle-e2e.sh all`)
- Bundle publish/login steps are skipped on pull requests

This ensures fast PR feedback and full bundle installation validation before publish on mainline flows.

---

## Docker Version Resolution (Docker Bundle Specific)

The docker bundle demonstrates dynamic version resolution:

### Latest (Default)
```bash
DOCKER_VERSION=latest dotnet script bundles/build.csx -- bundle-ci --bundle docker --push false
# Result: Newest docker-ce package from apt (e.g., 5:29.4.0)
```

### Major Version Pinning
```bash
DOCKER_VERSION=27 dotnet script bundles/build.csx -- bundle-ci --bundle docker --push false
# Result: Newest patch for docker 27.x (e.g., 5:27.5.1)

DOCKER_VERSION=26 dotnet script bundles/build.csx -- bundle-ci --bundle docker --push false
# Result: Newest patch for docker 26.x (e.g., 5:26.1.4)
```

### Version Validation
```bash
# Rejects patch versions
DOCKER_VERSION=27.1 dotnet script bundles/build.csx -- bundle-ci --bundle docker
# ERROR: Invalid version '27.1' - only 'latest' or major version (e.g., 27) allowed

# Rejects non-numeric
DOCKER_VERSION=latest-dev dotnet script bundles/build.csx -- bundle-ci --bundle docker
# ERROR: Invalid version 'latest-dev'
```

---

## Testing Checklist

- [ ] **CLI Level**: Run `./tests/bundle-test.sh` - should pass 8/8
- [ ] **Build Level**: Run `./tests/bundle-e2e-simple.sh helm` - should pass build + verify
- [ ] **Provider Tests**:
  - [ ] Docker version resolution: `DOCKER_VERSION=27 ./tests/bundle-e2e-simple.sh docker`
  - [ ] Helm binary verification: `./tests/bundle-e2e-simple.sh helm`
  - [ ] K9s, Etcd, Podman: similar pattern
- [ ] **Manual Kairos Simulation**: Create test Dockerfile, verify bundle extracts and `/run.sh` works
- [ ] **CI Integration**: Confirmation that workflows can run tests successfully

---

## Troubleshooting

### "No image available after build"
- Check `docker buildx` is working: `docker buildx version`
- Verify dotnet 10.0+ available: `dotnet --version`
- Check network (dotnet-install.sh downloads runtime)

### "Failed to extract bundle"
- Ensure Docker/Podman daemon is running
- Check permissions: `docker run --rm alpine echo OK`

### "Dotnet installation failed in test"
- Internet connectivity required (downloads from dot.net)
- Check architecture matches: `uname -m` should be x86_64 or aarch64

### Docker version resolution not working
- Verify apt package cache: `apt-cache madison docker-ce`
- Should show packages with epoch: `5:27.5.1-1~ubuntu~jammy`
- The regex `^([0-9]+:)?27\.` should match

---

## Next Steps

1. Add optional nightly `all` E2E run with artifact/log upload for diagnostics.
2. Add cache optimization for E2E runner image layers to reduce CI runtime.
3. Add focused docker-version matrix in E2E (for example `latest`, `27`).
