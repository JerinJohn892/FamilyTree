#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$repo_root/scripts/cloud-env.sh"
if [[ "$(uname -sm)" != 'Linux x86_64' ]]; then
    echo 'This setup script targets Linux x64 cloud machines. See README.md for Windows setup.' >&2
    exit 1
fi
sdk_version=10.0.401
sdk_sha512=51c8b999af9e8dd9998c9edc5944e19a90788862068acd38694e098889054ce8c23d4f0c5cccfa16bf187d044562359e5ee69a9f8ad0bbe913ba90311fbce25b
mkdir -p "$FAMILYTREE_TOOLING_DIR" "$DOTNET_CLI_HOME" "$NUGET_PACKAGES"
if [[ ! -x "$DOTNET_ROOT/dotnet" ]] || ! "$DOTNET_ROOT/dotnet" --list-sdks | grep -q "^$sdk_version "; then
    download_dir="$(mktemp -d "$FAMILYTREE_TOOLING_DIR/sdk-download.XXXXXX")"
    trap 'rm -rf "$download_dir"' EXIT
    curl --fail --show-error --silent --location \
        "https://builds.dotnet.microsoft.com/dotnet/Sdk/$sdk_version/dotnet-sdk-$sdk_version-linux-x64.tar.gz" \
        --output "$download_dir/sdk.tar.gz"
    # SHA-512 from Microsoft's official .NET 10 release metadata, checked before extraction.
    (cd "$download_dir" && printf '%s  sdk.tar.gz\n' "$sdk_sha512" | sha512sum --check --status)
    mkdir -p "$DOTNET_ROOT"
    tar -xzf "$download_dir/sdk.tar.gz" -C "$DOTNET_ROOT"
fi
cd "$repo_root"
dotnet restore FamilyTree.sln --locked-mode
dotnet build FamilyTree.sln -c Release --no-restore
dotnet test tests/FamilyTree.Tests -c Release --no-build --no-restore \
    --logger 'trx;LogFileName=acceptance.trx' --results-directory TestResults
