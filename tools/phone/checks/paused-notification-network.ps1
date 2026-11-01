<#
.SYNOPSIS
  paused-notification-network: "Download paused. Tap to finish." also posts when the backgrounded app only loses its network, with no dataSync time limit (Slice 233).
.DESCRIPTION
  Runs paused-notification.ps1 with -NetworkOnly: same pack move-aside and restore, but the network is cut by the Debug hook `drop-pack-network` (no netpolicy: a running dataSync service is exempt), no timeout set-up, so any Android version works. Passes on a
  `PACKDL service paused-notified pack=<id> reason=<offline|failed|transport>` marker, the notice in dumpsys notification, the app coming back from its content intent and the notice gone.
  Against the pre-Slice-233 Android build it fails: nothing is posted without the time limit. Wipes: as paused-notification (only the dev pack folder, moved aside and restored).
#>
param(
    [Parameter(Mandatory)][string]$Phone,
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App,
    [int]$PausedTimeoutSeconds = 150
)
$ErrorActionPreference = 'Stop'
return (& (Join-Path $PSScriptRoot 'paused-notification.ps1') -Phone $Phone -App $App -PausedTimeoutSeconds $PausedTimeoutSeconds -NetworkOnly)
