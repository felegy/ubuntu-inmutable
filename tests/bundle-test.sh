#!/usr/bin/env bash
# Bundle test harness: validates bundle orchestrator options, output, and env fallback.
# Usage: ./tests/bundle-test.sh [--verbose]

set -eu

SCRIPT_DIR=$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)
REPO_ROOT=$(cd "$SCRIPT_DIR/.." && pwd)
TEST_RESULTS=0
VERBOSE=${1:-}

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_test() {
    echo -e "${YELLOW}[TEST]${NC} $1"
}

log_pass() {
    echo -e "${GREEN}[PASS]${NC} $1"
}

log_fail() {
    echo -e "${RED}[FAIL]${NC} $1"
    TEST_RESULTS=$((TEST_RESULTS + 1))
}

# Test 1: Verify docker-version option accepts latest
log_test "Option validation: docker-version=latest"
if cd "$REPO_ROOT" && OUTPUT=$(dotnet script bundles/build.csx -- print-context --bundle docker --docker-version latest --push false 2>&1); then
    if echo "$OUTPUT" | grep -q "DockerVersion: latest"; then
        log_pass "docker-version=latest resolved correctly"
    else
        log_fail "docker-version=latest not in output"
    fi
else
    log_fail "docker-version=latest execution failed"
fi

# Test 2: Verify docker-version option accepts numeric major
log_test "Option validation: docker-version=27"
if cd "$REPO_ROOT" && OUTPUT=$(dotnet script bundles/build.csx -- print-context --bundle docker --docker-version 27 --push false 2>&1); then
    if echo "$OUTPUT" | grep -q "DockerVersion: 27"; then
        log_pass "docker-version=27 resolved correctly"
    else
        log_fail "docker-version=27 not in output"
    fi
else
    log_fail "docker-version=27 execution failed"
fi

# Test 3: Verify docker-version rejects non-numeric major
log_test "Option validation: docker-version rejects 27.1 (patch version)"
if cd "$REPO_ROOT" && OUTPUT=$(dotnet script bundles/build.csx -- print-context --bundle docker --docker-version 27.1 --push false 2>&1); then
    log_fail "docker-version=27.1 should be rejected but was accepted"
else
    if echo "$OUTPUT" | grep -q "Invalid docker version"; then
        log_pass "docker-version=27.1 rejected with expected error"
    else
        log_fail "docker-version=27.1 rejected but with unexpected error message"
    fi
fi

# Test 4: Env fallback DOCKER_VERSION=latest
log_test "Env fallback: DOCKER_VERSION=latest"
if OUTPUT=$(cd "$REPO_ROOT" && DOCKER_VERSION=latest dotnet script bundles/build.csx -- print-context --bundle docker --push false 2>&1); then
    if echo "$OUTPUT" | grep -q "DockerVersion: latest"; then
        log_pass "DOCKER_VERSION env var resolved correctly"
    else
        log_fail "DOCKER_VERSION env var not applied"
    fi
else
    log_fail "DOCKER_VERSION env fallback execution failed"
fi

# Test 5: Env fallback DOCKER_VERSION=29
log_test "Env fallback: DOCKER_VERSION=29"
if OUTPUT=$(cd "$REPO_ROOT" && DOCKER_VERSION=29 dotnet script bundles/build.csx -- print-context --bundle docker --push false 2>&1); then
    if echo "$OUTPUT" | grep -q "DockerVersion: 29"; then
        log_pass "DOCKER_VERSION=29 env var resolved correctly"
    else
        log_fail "DOCKER_VERSION=29 env var not applied"
    fi
else
    log_fail "DOCKER_VERSION=29 env fallback execution failed"
fi

# Test 6: CLI overrides env
log_test "CLI overrides env: DOCKER_VERSION=latest but --docker-version 27"
if OUTPUT=$(cd "$REPO_ROOT" && DOCKER_VERSION=latest dotnet script bundles/build.csx -- print-context --bundle docker --docker-version 27 --push false 2>&1); then
    if echo "$OUTPUT" | grep -q "DockerVersion: 27"; then
        log_pass "CLI flag --docker-version correctly overrode DOCKER_VERSION env"
    else
        log_fail "CLI flag did not override env"
    fi
else
    log_fail "Override test execution failed"
fi

# Test 7: Verify all bundles are discovered
log_test "Bundle discovery: all bundles present"
if OUTPUT=$(cd "$REPO_ROOT" && dotnet script bundles/build.csx -- print-context --push false 2>&1); then
    EXPECTED_BUNDLES=("docker" "etcd" "helm" "k9s" "podman")
    MISSING=0
    for bundle in "${EXPECTED_BUNDLES[@]}"; do
        if echo "$OUTPUT" | grep -q "$bundle"; then
            : # Found
        else
            log_fail "Bundle $bundle not discovered"
            MISSING=$((MISSING + 1))
        fi
    done
    if [ $MISSING -eq 0 ]; then
        log_pass "All expected bundles discovered"
    fi
else
    log_fail "Bundle discovery execution failed"
fi

# Test 8: Default docker-version is latest
log_test "Default docker-version: should be latest when not specified"
if OUTPUT=$(cd "$REPO_ROOT" && dotnet script bundles/build.csx -- print-context --bundle docker --push false 2>&1); then
    if echo "$OUTPUT" | grep -q "DockerVersion: latest"; then
        log_pass "Default docker-version is latest"
    else
        log_fail "Default docker-version is not latest"
    fi
else
    log_fail "Default test execution failed"
fi

echo ""
echo "========================================"
if [ $TEST_RESULTS -eq 0 ]; then
    echo -e "${GREEN}All tests passed!${NC}"
    exit 0
else
    echo -e "${RED}$TEST_RESULTS test(s) failed.${NC}"
    exit 1
fi
