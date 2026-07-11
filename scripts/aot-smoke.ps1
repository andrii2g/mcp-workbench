param(
    [string]$Executable = 'artifacts/win-x64/mcp-workbench.exe'
)

$ErrorActionPreference = 'Stop'
$executablePath = (Resolve-Path $Executable).Path
$temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("mcp-workbench-smoke-" + [guid]::NewGuid().ToString('N'))
$registryPath = Join-Path $temporaryDirectory 'servers.json'
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()
$baseUrl = "http://127.0.0.1:$port"
$process = $null
$standardOutput = ''
$standardError = ''

New-Item $temporaryDirectory -ItemType Directory | Out-Null
try {
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $executablePath
    $startInfo.Arguments = "--urls=$baseUrl --McpWorkbench:RegistryPath=`"$registryPath`""
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = [System.Diagnostics.Process]::Start($startInfo)

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(20)
    do {
        try {
            $live = Invoke-WebRequest "$baseUrl/health/live" -UseBasicParsing -TimeoutSec 2
            if ($live.StatusCode -eq 200) { break }
        } catch {
            Start-Sleep -Milliseconds 200
        }
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    if ($null -eq $live -or $live.StatusCode -ne 200) { throw 'Native executable did not become live.' }
    $ready = Invoke-WebRequest "$baseUrl/health/ready" -UseBasicParsing -TimeoutSec 2
    if ($ready.StatusCode -ne 200) { throw 'Native executable did not become ready.' }

    $body = '{"name":"smoke-http","enabled":true,"transport":"http","http":{"endpoint":"http://127.0.0.1:1","mode":"auto","headers":{}},"operationTimeoutSeconds":5}'
    $created = Invoke-WebRequest "$baseUrl/api/v1/servers" -Method Post -ContentType 'application/json' -Body $body -UseBasicParsing
    if ($created.StatusCode -ne 201 -or -not (Test-Path $registryPath)) { throw 'Registry persistence smoke check failed.' }
    Write-Output "Native smoke passed: $Executable"
} catch {
    if ($null -ne $process -and $process.HasExited) {
        $standardOutput = $process.StandardOutput.ReadToEnd()
        $standardError = $process.StandardError.ReadToEnd()
    }
    if ($standardOutput) { Write-Output $standardOutput }
    if ($standardError) { Write-Error $standardError }
    throw
} finally {
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit(5000) | Out-Null
    }
    Remove-Item $temporaryDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
