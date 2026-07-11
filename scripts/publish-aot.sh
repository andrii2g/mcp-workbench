#!/usr/bin/env sh
set -eu

output_root="${OUTPUT_ROOT:-artifacts}"
rids="${*:-linux-x64 linux-arm64}"

for rid in $rids; do
  dotnet publish src/McpWorkbench/McpWorkbench.csproj \
    -c Release -r "$rid" --self-contained true --no-restore \
    -p:PublishAot=true -o "$output_root/$rid"
done

(cd "$output_root" && find . -type f ! -name SHA256SUMS -print0 | sort -z | xargs -0 sha256sum > SHA256SUMS)
