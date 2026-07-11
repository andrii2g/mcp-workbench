param(
    [string[]]$RuntimeIdentifiers = @('win-x64'),
    [string]$OutputRoot = 'artifacts'
)

$ErrorActionPreference = 'Stop'

foreach ($rid in $RuntimeIdentifiers) {
    $output = Join-Path $OutputRoot $rid
    dotnet publish src/McpWorkbench/McpWorkbench.csproj `
        -c Release -r $rid --self-contained true --no-restore `
        -p:PublishAot=true -o $output
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$outputPath = (Resolve-Path $OutputRoot).Path
$checksumFile = Join-Path $outputPath 'SHA256SUMS'
$checksums = Get-ChildItem $OutputRoot -Recurse -File |
    Where-Object { $_.FullName -ne (Join-Path (Resolve-Path $OutputRoot) 'SHA256SUMS') } |
    Sort-Object FullName |
    ForEach-Object {
        $relative = $_.FullName.Substring($outputPath.Length).TrimStart('\', '/').Replace('\', '/')
        "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $relative
    }
[System.IO.File]::WriteAllLines($checksumFile, $checksums, [System.Text.UTF8Encoding]::new($false))
