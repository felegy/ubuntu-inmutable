#!/bin/bash
set -ex

export DOTNET_ROOT=/dotnet
export PATH=/dotnet:$PATH

exec /dotnet/dotnet script run.csx "$@"
