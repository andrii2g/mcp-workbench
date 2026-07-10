#!/usr/bin/env sh
set -eu

dotnet test McpWorkbench.slnx -c Release --no-build
