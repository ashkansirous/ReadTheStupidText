# The Windows path-length check for builds. Dot-sourced, not run (tools/phone/common.ps1 and tools/emulator/build-cache.ps1 both load it; Slice 266 moved it here).
# Item 3 (Slice 216): the build fails early when its intermediate folders would pass the Windows path limit (APT2264 came minutes into a build). The deepest MAUI intermediate
# below the artifacts folder is an ESTIMATE (the ML Kit asset paths are ~230 characters from the project folder): tune it when a build fails with APT2264 although this passed.
$script:WindowsPathLimit = 260
$script:DeepestMauiIntermediateChars = 150   # unmeasured estimate (as before Slice 266), used by every build without a measured depth (phone builds, the RTST emulator build): over it is only a WARNING
$script:MeasuredDinoEmulatorIntermediateChars = 192  # Slice 266: measured only for .artifacts\emu-dino (the longest file under it was 247 characters with a 35-character worktree prefix); a longer artifacts path certainly fails with APT2264
$script:KnownMinimumIntermediateChars = 100  # a floor any MAUI intermediate path certainly has below the artifacts folder: over it the build cannot work (FAIL)
# $null when fine, else @{ Level = 'Warn' | 'Fail'; Message }.
function Test-BuildPathLength([string]$ArtifactsPath, [int]$MinimumIntermediateChars = $script:KnownMinimumIntermediateChars) {
    $floor = $ArtifactsPath.Length + $MinimumIntermediateChars
    if ($floor -gt $script:WindowsPathLimit) {
        return [pscustomobject]@{ Level = 'Fail'; Message = "path too long: even the shortest intermediate path would be about $floor characters (limit $($script:WindowsPathLimit)). Move the worktree or shorten '$ArtifactsPath' by $($floor - $script:WindowsPathLimit) characters." }
    }
    $estimate = $ArtifactsPath.Length + $script:DeepestMauiIntermediateChars
    if ($estimate -le $script:WindowsPathLimit) { return $null }
    return [pscustomobject]@{ Level = 'Warn'; Message = "path may be too long: the deepest intermediate path is estimated at $estimate characters (limit $($script:WindowsPathLimit)). If the build fails with APT2264, move the worktree or shorten '$ArtifactsPath'." }
}

# -MinimumIntermediateChars: a depth the caller has measured (the Dino emulator build only: $script:MeasuredDinoEmulatorIntermediateChars), so a path that certainly fails stops now.
function Assert-BuildPathLength([string]$ArtifactsPath, [int]$MinimumIntermediateChars = $script:KnownMinimumIntermediateChars) {
    $problem = Test-BuildPathLength $ArtifactsPath $MinimumIntermediateChars
    if (-not $problem) { return }
    if ($problem.Level -eq 'Fail') { throw $problem.Message }
    Write-Warning $problem.Message
}
