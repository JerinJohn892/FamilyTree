#!/usr/bin/env bash
# Source in each cloud shell: dependencies persist, shell exports do not.
export FAMILYTREE_TOOLING_DIR="${FAMILYTREE_TOOLING_DIR:-/workspace/tooling/familytree}"
export DOTNET_ROOT="$FAMILYTREE_TOOLING_DIR/dotnet"
export DOTNET_CLI_HOME="$FAMILYTREE_TOOLING_DIR/cli"
export NUGET_PACKAGES="$FAMILYTREE_TOOLING_DIR/nuget"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
case ":$PATH:" in
    *":$DOTNET_ROOT:"*) ;;
    *) export PATH="$DOTNET_ROOT:$PATH" ;;
esac
