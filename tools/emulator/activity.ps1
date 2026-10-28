# Shared "who is using the machine" helpers (Slice 180). Dot-sourced, not run.
# Every long device job (a build, a flows run, a VM run) registers an activity file for its lifetime; a timing run that needs a
# quiet machine (tools/phone/audio-timing.ps1) takes the quiet lock and waits for those jobs to end. While the lock is held, new
# jobs wait at their start instead of running, so a measurement never shares the CPU with a build.
$script:ToolsRepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$script:QuietMutexName = 'Global\RTST_QuietMachine'
$script:ActivityPollMs = 1000
$script:UnreadableActivitySeconds = 30

# The folder that holds every worktree of this repo (shared state and caches live there, not in any one worktree).
function Get-WorktreesRoot {
    $parent = Split-Path $script:ToolsRepoRoot -Parent
    if ((Split-Path $parent -Leaf) -like '*-worktrees') { return $parent }
    return Join-Path $parent ((Split-Path $script:ToolsRepoRoot -Leaf) + '-worktrees')
}
$script:ActivityDir = Join-Path (Get-WorktreesRoot) '.activity'

function Test-QuietHeld {
    $mutex = New-Object System.Threading.Mutex($false, $script:QuietMutexName)
    try {
        try { $got = $mutex.WaitOne(0) } catch [System.Threading.AbandonedMutexException] { $got = $true }
        if ($got) { $mutex.ReleaseMutex() }
        return (-not $got)
    } finally { $mutex.Dispose() }
}

# The registered job's process still runs: same pid and, when recorded, the same start time (a reused pid is a different process).
function Test-ActivityProcessAlive($Info) {
    if (-not $Info.pid) { return $false }
    $proc = Get-Process -Id $Info.pid -ErrorAction SilentlyContinue
    if (-not $proc) { return $false }
    if (-not $Info.procStart) { return $true }
    try { return ($proc.StartTime.ToFileTimeUtc() -eq [int64]$Info.procStart) } catch { return $true }
}

# Reads one activity file; $null when it cannot be read (still being written, or corrupt).
function Read-ActivityFile([string]$Path) {
    try { return (Get-Content $Path -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop) } catch { return $null }
}

# Live jobs only: an activity file whose process is gone (a crash) is deleted. An unreadable file counts as a live job (it is probably
# being written right now) until it is older than $script:UnreadableActivitySeconds, then it is deleted as corrupt.
function Get-LiveActivities {
    if (-not (Test-Path $script:ActivityDir)) { return @() }
    $live = foreach ($file in Get-ChildItem $script:ActivityDir -Filter '*.json' -ErrorAction SilentlyContinue) {
        $info = Read-ActivityFile $file.FullName
        if (-not $info) {
            if (((Get-Date) - $file.LastWriteTime).TotalSeconds -gt $script:UnreadableActivitySeconds) { Remove-Item $file.FullName -Force -ErrorAction SilentlyContinue }
            else { [pscustomobject]@{ pid = 0; kind = 'starting' } }
        }
        elseif (Test-ActivityProcessAlive $info) { $info }
        else { Remove-Item $file.FullName -Force -ErrorAction SilentlyContinue }
    }
    return @($live)
}

# Writes the activity file in one step (temp name, then rename), so a reader never sees half of it.
function Write-ActivityFile([string]$Kind) {
    $null = New-Item -ItemType Directory -Force $script:ActivityDir
    $path = Join-Path $script:ActivityDir ("{0}-{1}-{2}.json" -f $Kind, $PID, [guid]::NewGuid().ToString('N').Substring(0, 8))
    $start = (Get-Process -Id $PID).StartTime.ToFileTimeUtc()  # a number: ConvertFrom-Json would turn a date string into a DateTime
    @{ pid = $PID; procStart = $start; kind = $Kind; started = (Get-Date).ToString('o') } | ConvertTo-Json | Set-Content "$path.tmp"
    Move-Item "$path.tmp" $path -Force
    return $path
}

# Measured limit (2026-10-28, i9-11950H, 32 GB): two emulators running flows plus the VM is too much (VM 0/3, one emulator flow failed);
# one emulator + the VM, or two emulators without the VM, are fine. Returns why a new job of $Kind must wait now, or $null.
function Get-ConcurrencyConflict([string]$Kind) {
    $live = @(Get-LiveActivities | Where-Object { $_.pid -ne $PID })
    $flows = @($live | Where-Object { $_.kind -like 'flows-*' })
    $vm = @($live | Where-Object { $_.kind -like 'vm-*' })
    if ($Kind -like 'vm-*' -and $flows.Count -ge 2) { return "two emulators are running flows ($(($flows | ForEach-Object { $_.kind }) -join ', ')); with the VM that is too much for this PC" }
    if ($Kind -like 'flows-*' -and $vm.Count -gt 0 -and $flows.Count -ge 1) { return "the VM and another emulator's flows are running ($(($vm + $flows | ForEach-Object { $_.kind }) -join ', ')); a second emulator would overload this PC" }
    return $null
}

# Runs a build at BelowNormal priority so flows and the VM keep the CPU; child processes (dotnet, msbuild) inherit it.
# Returns the previous priority for Restore-ProcessPriority.
function Set-BuildPriority {
    $proc = Get-Process -Id $PID
    $previous = $proc.PriorityClass
    try { $proc.PriorityClass = [Diagnostics.ProcessPriorityClass]::BelowNormal } catch { Write-Host "Could not lower the build priority: $($_.Exception.Message)" }
    return $previous
}

function Restore-ProcessPriority($Previous) {
    if ($null -eq $Previous) { return }
    try { (Get-Process -Id $PID).PriorityClass = $Previous } catch { }
}

# Registers a job; waits first while a quiet-machine run holds the lock or the concurrency limit would be broken (and says why).
# Checking and registering happen under one mutex, so two jobs starting together never both slip past the limit.
# Pass the returned path to Stop-Activity in a finally.
function Start-Activity([string]$Kind, [int]$WaitMinutes = 90) {
    $deadline = (Get-Date).AddMinutes($WaitMinutes)
    $admit = New-Object System.Threading.Mutex($false, 'Global\RTST_ActivityAdmit')
    try {
        while ($true) {
            $why = if (Test-QuietHeld) { 'a quiet-machine timing run is in progress' } else { $null }
            if (-not $why) {
                try { $got = $admit.WaitOne([TimeSpan]::FromSeconds(30)) } catch [System.Threading.AbandonedMutexException] { $got = $true }
                if (-not $got) { $why = 'another job is registering' }
                if ($got) {
                    try {
                        $why = Get-ConcurrencyConflict $Kind
                        if (-not $why) {
                            # Register first, then look at the lock again: a timing run that took the lock in between sees this file or this job backs out.
                            $path = Write-ActivityFile $Kind
                            if (-not (Test-QuietHeld)) { return $path }
                            Remove-Item $path -Force -ErrorAction SilentlyContinue
                            $why = 'a quiet-machine timing run is in progress'
                        }
                    } finally { $admit.ReleaseMutex() }
                }
            }
            if ((Get-Date) -gt $deadline) { throw "Not starting ${Kind}: still waiting after $WaitMinutes minutes because $why." }
            Write-Host "Waiting before starting ${Kind}: $why..."
            Start-Sleep -Seconds 15
        }
    } finally { $admit.Dispose() }
}

function Stop-Activity([string]$Path) {
    if ($Path) { Remove-Item $Path -Force -ErrorAction SilentlyContinue }
}

# Takes the quiet lock, then waits until no other job runs (both emulators' flows, the VM, builds). Returns the mutex for Exit-QuietMachine.
function Enter-QuietMachine([int]$WaitMinutes = 60) {
    $mutex = New-Object System.Threading.Mutex($false, $script:QuietMutexName)
    try { $got = $mutex.WaitOne([TimeSpan]::FromMinutes($WaitMinutes)) } catch [System.Threading.AbandonedMutexException] { $got = $true }
    if (-not $got) { throw "Another quiet-machine timing run held the machine for over $WaitMinutes minutes." }
    $deadline = (Get-Date).AddMinutes($WaitMinutes)
    try {
        while (@(Get-LiveActivities | Where-Object { $_.pid -ne $PID }).Count -gt 0) {
            if ((Get-Date) -gt $deadline) { throw "Builds or device runs were still busy after $WaitMinutes minutes." }
            $busy = (Get-LiveActivities | ForEach-Object { $_.kind }) -join ', '
            Write-Host "Waiting for the machine to go quiet (busy: $busy)..."
            Start-Sleep -Seconds 15
        }
    } catch { $mutex.ReleaseMutex(); throw }
    return $mutex
}

function Exit-QuietMachine($Mutex) {
    if ($Mutex) { $Mutex.ReleaseMutex() }
}
