$ErrorActionPreference = 'Stop'

dotnet restore McpWorkbench.slnx
dotnet format McpWorkbench.slnx --verify-no-changes --no-restore
dotnet build McpWorkbench.slnx -c Release --no-restore
