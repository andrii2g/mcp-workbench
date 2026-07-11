#!/usr/bin/env sh
set -eu

executable="${1:-artifacts/linux-x64/mcp-workbench}"
temporary_directory="$(mktemp -d)"
registry="$temporary_directory/servers.json"
port="${MCP_WORKBENCH_SMOKE_PORT:-$(python3 -c 'import socket; s=socket.socket(); s.bind(("127.0.0.1", 0)); print(s.getsockname()[1]); s.close()')}"
base_url="http://127.0.0.1:$port"
pid=''

cleanup() {
  if [ -n "$pid" ]; then kill "$pid" 2>/dev/null || true; wait "$pid" 2>/dev/null || true; fi
  rm -rf "$temporary_directory"
}
trap cleanup EXIT INT TERM

"$executable" --urls="$base_url" --McpWorkbench:RegistryPath="$registry" >"$temporary_directory/stdout.log" 2>"$temporary_directory/stderr.log" &
pid=$!

attempt=0
until curl --fail --silent "$base_url/health/live" >/dev/null; do
  attempt=$((attempt + 1))
  if [ "$attempt" -ge 100 ]; then cat "$temporary_directory/stdout.log" "$temporary_directory/stderr.log"; exit 1; fi
  sleep 0.2
done
curl --fail --silent "$base_url/health/ready" >/dev/null
curl --fail --silent -X POST -H 'Content-Type: application/json' \
  -d '{"name":"smoke-http","enabled":true,"transport":"http","http":{"endpoint":"http://127.0.0.1:1","mode":"auto","headers":{}},"operationTimeoutSeconds":5}' \
  "$base_url/api/v1/servers" >/dev/null
test -s "$registry"
printf 'Native smoke passed: %s\n' "$executable"
