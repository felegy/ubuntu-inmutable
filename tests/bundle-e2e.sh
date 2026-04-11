#!/bin/bash
set -euo pipefail

# Real E2E Bundle Testing
# 1) Build bundle image with bundle build orchestrator
# 2) Extract image filesystem like kairos-agent install-bundle does
# 3) Execute /run.sh inside a clean test container
# 4) Verify bundle-specific installation results

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TMPDIR="${REPO_ROOT}/tests/bundles-e2e-tmp"
BUILD_PATH="${REPO_ROOT}/bundles"
IMAGE_PREFIX="ghcr.io/local/ubuntu-inmutable-bundles"
RUNNER_IMAGE="local/ubuntu-inmutable-bundle-e2e-runner:dev"

ALL_BUNDLES=("docker" "etcd" "helm" "k9s" "podman")
SELECTED_BUNDLE="${1:-all}"
DOCKER_VERSION="${DOCKER_VERSION:-latest}"

if [ "${SELECTED_BUNDLE}" = "all" ]; then
    BUNDLES=("${ALL_BUNDLES[@]}")
else
    BUNDLES=("${SELECTED_BUNDLE}")
fi

ARCH="$(uname -m)"
if [ "${ARCH}" = "x86_64" ]; then
    TARGETARCH="amd64"
elif [ "${ARCH}" = "aarch64" ]; then
    TARGETARCH="arm64"
else
    TARGETARCH="${ARCH}"
fi

RUNTIME="docker"
if ! command -v docker >/dev/null 2>&1; then
    if command -v podman >/dev/null 2>&1; then
        RUNTIME="podman"
    else
        echo "No container runtime found (docker or podman)." >&2
        exit 1
    fi
fi

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

TOTAL=0
PASSED=0
FAILED=0

test_start() { echo -e "${BLUE}[TEST]${NC} $1"; TOTAL=$((TOTAL + 1)); }
test_pass() { echo -e "${GREEN}[PASS]${NC} $1"; PASSED=$((PASSED + 1)); }
test_fail() { echo -e "${RED}[FAIL]${NC} $1"; FAILED=$((FAILED + 1)); }
info() { echo -e "${YELLOW}[INFO]${NC} $1"; }

cleanup() {
    rm -rf "${TMPDIR}" >/dev/null 2>&1 || true
}

trap cleanup EXIT
mkdir -p "${TMPDIR}"

info "Runtime: ${RUNTIME}"
info "Arch: ${TARGETARCH}"
info "Bundles: ${BUNDLES[*]}"
info "DockerVersion(for docker bundle): ${DOCKER_VERSION}"
echo

build_bundle() {
    local bundle="$1"
    local image="${IMAGE_PREFIX}:${bundle}-dev"

    test_start "Build bundle image: ${bundle}"
    local build_args=(
        buildx build
        --platform "linux/${TARGETARCH}"
        --file "${BUILD_PATH}/${bundle}/Containerfile"
        --tag "${image}"
        --load
        "${BUILD_PATH}/${bundle}"
    )

    if [ "${bundle}" = "docker" ]; then
        build_args=(
            buildx build
            --platform "linux/${TARGETARCH}"
            --file "${BUILD_PATH}/${bundle}/Containerfile"
            --build-arg "DOCKER_VERSION=${DOCKER_VERSION}"
            --build-arg "TARGETARCH=${TARGETARCH}"
            --tag "${image}"
            --load
            "${BUILD_PATH}/${bundle}"
        )
    elif [ "${bundle}" = "podman" ] || [ "${bundle}" = "helm" ] || [ "${bundle}" = "k9s" ] || [ "${bundle}" = "etcd" ]; then
        build_args=(
            buildx build
            --platform "linux/${TARGETARCH}"
            --file "${BUILD_PATH}/${bundle}/Containerfile"
            --build-arg "TARGETARCH=${TARGETARCH}"
            --tag "${image}"
            --load
            "${BUILD_PATH}/${bundle}"
        )
    fi

    if "${RUNTIME}" "${build_args[@]}" >/tmp/bundle-build-${bundle}.log 2>&1; then
        test_pass "Built ${image}"
    else
        test_fail "Build failed for ${bundle}"
        tail -n 20 /tmp/bundle-build-${bundle}.log >&2 || true
        return 1
    fi
}

extract_bundle() {
    local bundle="$1"
    local image="${IMAGE_PREFIX}:${bundle}-dev"
    local out_dir="${TMPDIR}/${bundle}/bundlefs"
    mkdir -p "${out_dir}"

    test_start "Extract image filesystem: ${bundle}"
    local cid
    cid="$(${RUNTIME} create "${image}" true)"
    ${RUNTIME} export "${cid}" | tar -xf - -C "${out_dir}"
    ${RUNTIME} rm "${cid}" >/dev/null

    if [ -f "${out_dir}/run.sh" ] && [ -f "${out_dir}/run.csx" ]; then
        chmod +x "${out_dir}/run.sh"
        test_pass "Extracted run.sh and run.csx for ${bundle}"
    else
        test_fail "Bundle filesystem invalid for ${bundle}"
        return 1
    fi
}

run_install_and_verify() {
    local bundle="$1"
    local work_dir="${TMPDIR}/${bundle}"
    local bundlefs="${work_dir}/bundlefs"

    test_start "Install bundle in test container: ${bundle}"

    cat > "${work_dir}/verify.sh" <<'EOS'
#!/bin/bash
set -euo pipefail

bundle="$1"

cd /bundle
./run.sh

case "${bundle}" in
    helm)
        test -x /usr/local/bin/helm
        /usr/local/bin/helm version >/dev/null 2>&1 || true
        ;;
    k9s)
        test -x /usr/local/bin/k9s
        ;;
    etcd)
        test -x /usr/local/bin/etcd
        test -x /usr/local/bin/etcdctl
        ;;
    docker)
        command -v docker >/dev/null
        command -v docker-compose >/dev/null || command -v docker compose >/dev/null || true
        ;;
    podman)
        command -v podman >/dev/null
        ;;
    *)
        echo "Unknown bundle: ${bundle}" >&2
        exit 1
        ;;
esac

echo "OK:${bundle}"
EOS

    chmod +x "${work_dir}/verify.sh"

    if ${RUNTIME} run --rm \
        --platform "linux/${TARGETARCH}" \
        -v "${bundlefs}:/bundle" \
        -v "${work_dir}/verify.sh:/verify.sh" \
        "${RUNNER_IMAGE}" "/verify.sh ${bundle}" >"${work_dir}/verify.log" 2>&1; then
        test_pass "Installed and verified ${bundle}"
    else
        test_fail "Install/verify failed for ${bundle}"
        tail -n 40 "${work_dir}/verify.log" >&2 || true
        return 1
    fi
}

prepare_runner_image() {
    test_start "Build E2E runner image"

    cat > "${TMPDIR}/Dockerfile.runner" <<'EOF'
FROM ubuntu:24.04

RUN apt-get update -qq && \
    apt-get install -y -qq --no-install-recommends curl ca-certificates file bash libicu74 && \
    rm -rf /var/lib/apt/lists/*

RUN mkdir -p /dotnet && \
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o /dotnet/dotnet-install.sh && \
    chmod +x /dotnet/dotnet-install.sh && \
    arch="$(uname -m)" && \
    if [ "${arch}" = "aarch64" ]; then dot_arch="arm64"; else dot_arch="x64"; fi && \
    /dotnet/dotnet-install.sh --version 10.0.100 --install-dir /dotnet --architecture "${dot_arch}" --no-path && \
    /dotnet/dotnet tool install -g dotnet-script --version 2.0.0

RUN mkdir -p /usr/local/sbin && \
    printf '#!/bin/sh\nexit 0\n' > /usr/local/sbin/systemctl && \
    chmod +x /usr/local/sbin/systemctl

ENV DOTNET_ROOT=/dotnet
ENV PATH=/usr/local/sbin:/dotnet:/root/.dotnet/tools:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin

ENTRYPOINT ["/bin/bash", "-lc"]
EOF

    if ${RUNTIME} build --platform "linux/${TARGETARCH}" -f "${TMPDIR}/Dockerfile.runner" -t "${RUNNER_IMAGE}" "${TMPDIR}" >/tmp/bundle-e2e-runner.log 2>&1; then
        test_pass "Runner image ready: ${RUNNER_IMAGE}"
    else
        test_fail "Failed to build runner image"
        tail -n 30 /tmp/bundle-e2e-runner.log >&2 || true
        return 1
    fi
}

prepare_runner_image || exit 1

for bundle in "${BUNDLES[@]}"; do
    mkdir -p "${TMPDIR}/${bundle}"
    build_bundle "${bundle}" || continue
    extract_bundle "${bundle}" || continue
    run_install_and_verify "${bundle}" || continue
done

echo
echo "========================================"
echo "E2E Results:"
echo "Total: ${TOTAL}"
echo -e "${GREEN}Passed: ${PASSED}${NC}"
echo -e "${RED}Failed: ${FAILED}${NC}"
echo "========================================"

if [ "${FAILED}" -eq 0 ]; then
    echo -e "${GREEN}All E2E tests passed.${NC}"
    exit 0
fi

echo -e "${RED}E2E tests have failures.${NC}"
exit 1
