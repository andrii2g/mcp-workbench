#!/usr/bin/env sh
set -eu

output_root="${OUTPUT_ROOT:-artifacts}"
rids="${*:-linux-x64 linux-arm64}"
lock_directory="$(pwd)/$output_root/locks"
mkdir -p "$lock_directory"

for rid in $rids; do
  dotnet restore src/McpWorkbench/McpWorkbench.csproj -r "$rid" \
    -p:NuGetLockFilePath="$lock_directory/$rid.packages.lock.json" --force-evaluate
  dotnet publish src/McpWorkbench/McpWorkbench.csproj \
    -c Release -r "$rid" --self-contained true --no-restore \
    -p:PublishAot=true -o "$output_root/$rid"
done

(cd "$output_root" && find . -type f ! -name SHA256SUMS -print0 | sort -z | xargs -0 sha256sum > SHA256SUMS)
