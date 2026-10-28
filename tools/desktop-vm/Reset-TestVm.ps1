<#
.SYNOPSIS
  Puts the desktop-test VM back at its 'clean' checkpoint (Slice 163). -Start also boots it to the saved desktop.
#>
[CmdletBinding()]
param([switch] $Start)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"
Enter-VmLock
try { Restore-CleanCheckpoint -Start:$Start } finally { Exit-VmLock }
