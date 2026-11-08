# check: Apps=rtst EstimateSeconds=300 NeedsPack=true Variant=debug
<#
.SYNOPSIS
  paused-notification-repost: a repeated failed automatic resume posts the paused notice again, but silently: the first post alerts, the re-post does not (Slice 233, via Slice 316).
.DESCRIPTION
  Runs paused-notification.ps1 with -Repost (which implies -NetworkOnly): same pack move-aside, backgrounding and `drop-pack-network` cut as paused-notification-network. After the first
  `PACKDL service paused-notified` marker the notice's flags must NOT carry FLAG_ONLY_ALERT_ONCE (0x8: the first post of the episode alerts). The app is then opened (an automatic restart, which keeps the
  episode), the download must log `PACKDL start` again, the app goes to the background and the connection is dropped a second time. The second marker must appear and the new notice's flags MUST carry 0x8
  (`SetOnlyAlertOnce(true)`, the silent re-post of `PackDownloadHost.ClaimAlert`). Not yet run on a phone (none available when written); it should fail against a build that alerts on every re-post.
  Wipes: as paused-notification (the dev app's data via reset-dev-app -Level clear, installed packs kept aside and restored; only the dev pack folder, moved aside and restored).
#>
param(
    [Parameter(Mandatory)][string]$Phone,
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App,
    [int]$PausedTimeoutSeconds = 150
)
$ErrorActionPreference = 'Stop'
return (& (Join-Path $PSScriptRoot 'paused-notification.ps1') -Phone $Phone -App $App -PausedTimeoutSeconds $PausedTimeoutSeconds -Repost)
