# check: Apps=rtst EstimateSeconds=200 NeedsPack=true Variant=debug
<#
.SYNOPSIS
  pack-stall: a pack download whose socket goes silent mid-way must end in a `PACKDL stall` log line within 90 s, and then retry (Slice 241 item 7).
.DESCRIPTION
  Runs paused-notification.ps1 with -Stall: same pack move-aside and restore, throttled download. Once a `PACKDL progress` line shows the body moving, the Debug hook `stall-pack-network` hangs the next body read
  like a dead socket (the read never returns and ignores its token). Passes on `PACKDL stall pack=<id> phase=body bytes=<n>` within 90 s of the hook (the stall guard's timeout is 60 s) followed by `PACKDL retry` or a new `PACKDL start`.
  Against the build before Slice 241 it fails at the first step: there is no `PACKDL progress` line and no `stall-pack-network` hook. Wipes: as paused-notification (only the dev pack folder, moved aside and restored).
  The 2026-11-01 phone run saw a kokoro-en .partial stuck at 9,011,200 bytes for 7 min with no PACKDL line; this check is how the next run proves the stall is now logged.
#>
param(
    [Parameter(Mandatory)][string]$Phone,
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App
)
$ErrorActionPreference = 'Stop'
return (& (Join-Path $PSScriptRoot 'paused-notification.ps1') -Phone $Phone -App $App -Stall)
