# Slice 320 item 5: the host stubs a flow's `# needs:` header asks for. run-flows.ps1 starts them before the flow and stops them after it. Dot-sourced, not run.
#   # needs: report-stub  -> node .maestro/dino/report-problem-stub.mjs   (report-problem-reaches-stub.yaml)
#   # needs: crash-stub   -> node tools/crashes/stub-server.mjs           (crash-report-reaches-stub.yaml, Dino and RTST)
# The emulator reaches the host as 10.0.2.2:8787, so the port is fixed. A stub already listening on the port is not ours: it is used and never stopped, but only when it answers as the right stub
# (every stub sends the header `X-Stub: <need>`); the two stubs share the port and answer 400 to each other's reports, so a listener of the other kind (or an older stub without the header) stops the run with a clear error.

$script:NeedStubScripts = [ordered]@{
    'report-stub' = '.maestro\dino\report-problem-stub.mjs'
    'crash-stub'  = 'tools\crashes\stub-server.mjs'
}
$script:NeedStubPort = 8787

# Pure: the `needs` values that name a host stub, in the order they appear.
function Get-StubNeeds([string[]]$Needs) {
    return @($Needs | Where-Object { $script:NeedStubScripts.Contains($_) })
}

# Pure: the stub script (relative to the repo root) for one `needs` value.
function Get-NeedStubScript([string]$Need) { return $script:NeedStubScripts[$Need] }

function Test-PortListening([int]$Port) {
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $connect = $client.BeginConnect('127.0.0.1', $Port, $null, $null)
        return ($connect.AsyncWaitHandle.WaitOne(500) -and $client.Connected)
    } catch { return $false }   # no-fallback: a refused connection is the answer "not listening"
    finally { $client.Close() }
}

# What the listener on $Port says it is: the value of its `X-Stub` response header, or 'an unknown listener' (not a stub of ours, no header, no answer). A plain socket, so it reads the same in Windows PowerShell 5.1 and pwsh and a 400 is not an error.
function Get-StubIdentity([int]$Port, [int]$Attempts = 3) {
    $unknown = 'an unknown listener'
    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        $answer = Read-StubHeaders $Port
        if ($answer.Answered) {
            if ($answer.Stub) { return $answer.Stub }
            return $script:StubWithoutHeader   # it answered with complete headers and no X-Stub: an older stub, or not ours at all
        }
        # no-fallback: a slow or failed probe is tried again (a stub busy for a moment still answers); after the last attempt the listener is reported as unknown
    }
    return $unknown
}

$script:StubWithoutHeader = 'a listener with no X-Stub header (an older stub: restart the stub, or stop the process holding the port)'

# One probe: sends a request and reads the response head line by line, so a slow stub gets the whole 3 s read timeout per line and a head that ends without X-Stub is seen as complete.
# Returns @{ Answered = <the head ended or X-Stub was seen>; Stub = <name or $null> }.
function Read-StubHeaders([int]$Port) {
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $client.ReceiveTimeout = 3000; $client.SendTimeout = 3000
        $client.Connect('127.0.0.1', $Port)
        $stream = $client.GetStream()
        $request = [System.Text.Encoding]::ASCII.GetBytes("GET /identify HTTP/1.0`r`nHost: localhost`r`nConnection: close`r`n`r`n")
        $stream.Write($request, 0, $request.Length)
        $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::ASCII)
        $sawStatus = $false
        while ($true) {
            $line = $reader.ReadLine()
            if ($null -eq $line) { return @{ Answered = $sawStatus; Stub = $null } }   # connection closed: complete only if a status line came
            $sawStatus = $true
            if ($line -match '^X-Stub:\s*(\S+)') { return @{ Answered = $true; Stub = $Matches[1] } }
            if ($line -eq '') { return @{ Answered = $true; Stub = $null } }   # end of the head, no X-Stub
        }
    } catch { return @{ Answered = $false; Stub = $null } }   # no-fallback: a probe that times out or is refused is retried by the caller, then reported as unknown
    finally { $client.Close() }
}

# Starts the stub for $Need and waits up to $TimeoutSeconds for its port. Returns @{ Process = <started process or $null>; Port; Script }.
# Process is $null when something already listened on the port (not ours: Stop-NeedStub leaves it alone). Throws when the stub never listens.
function Start-NeedStub([string]$Need, [string]$RepoRoot, [int]$Port = $script:NeedStubPort, [int]$TimeoutSeconds = 15) {
    $script = Join-Path $RepoRoot (Get-NeedStubScript $Need)
    if (-not (Test-Path $script)) { throw "The stub script for '# needs: $Need' is missing: $script" }
    if (Test-PortListening $Port) {
        $identity = Get-StubIdentity $Port
        if ($identity -ne $Need) { throw "Port $Port is held by '$identity' (not '$Need'): a stale or other stub is listening, and the flow would run against it. Stop it (or the process holding the port) and run again." }
        Write-Host "STUB $Need already listening on port $Port (not started by this run, left running)"
        return [pscustomobject]@{ Process = $null; Port = $Port; Script = $script }
    }
    $node = (Get-Command node -ErrorAction Stop).Source
    $process = Start-Process -FilePath $node -ArgumentList "`"$script`" --port $Port" -WindowStyle Hidden -PassThru
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline -and -not $process.HasExited) {
        if (Test-PortListening $Port) { Write-Host "STUB $Need started on port $Port (pid $($process.Id))"; return [pscustomobject]@{ Process = $process; Port = $Port; Script = $script } }
        Start-Sleep -Milliseconds 300
    }
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    throw "The stub for '# needs: $Need' did not listen on port $Port within $TimeoutSeconds s (node $script)."
}

# Stops only a stub this run started.
function Stop-NeedStub($Stub) {
    if ($null -eq $Stub -or $null -eq $Stub.Process) { return }
    Stop-Process -Id $Stub.Process.Id -Force -ErrorAction SilentlyContinue
    Write-Host "STUB stopped (pid $($Stub.Process.Id))"
}
