#!/usr/bin/env bash
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPO_ROOT="$SCRIPT_DIR"
DOTNET_INSTALL_DIR="$REPO_ROOT/.dotnet"
DOTNET_EXE=""
USED_LOCAL_DOTNET=0

ensure_dotnet() {
    if command -v dotnet >/dev/null 2>&1; then
        DOTNET_EXE=$(command -v dotnet)
        return
    fi

    USED_LOCAL_DOTNET=1
    mkdir -p "$DOTNET_INSTALL_DIR"

    INSTALL_SCRIPT="$DOTNET_INSTALL_DIR/dotnet-install.sh"
    if [ ! -f "$INSTALL_SCRIPT" ]; then
        if command -v curl >/dev/null 2>&1; then
            curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$INSTALL_SCRIPT"
        elif command -v wget >/dev/null 2>&1; then
            wget -q https://dot.net/v1/dotnet-install.sh -O "$INSTALL_SCRIPT"
        else
            echo "Neither curl nor wget is available to download dotnet-install.sh." >&2
            exit 1
        fi
    fi

    chmod +x "$INSTALL_SCRIPT"
    bash "$INSTALL_SCRIPT" --jsonfile "$REPO_ROOT/global.json" --install-dir "$DOTNET_INSTALL_DIR" --no-path
    DOTNET_EXE="$DOTNET_INSTALL_DIR/dotnet"
}

ensure_dotnet

if [ "$USED_LOCAL_DOTNET" -eq 1 ]; then
    export DOTNET_ROOT="$DOTNET_INSTALL_DIR"
    export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
fi

export DOTNET_EXE
"$DOTNET_EXE" tool restore
exec "$DOTNET_EXE" script "$REPO_ROOT/build.csx" -- "$@"
