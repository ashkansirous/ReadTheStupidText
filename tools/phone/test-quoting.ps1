<#
.SYNOPSIS
  Self-test for ConvertTo-WindowsArgument (quoting.ps1): quotes each tricky argument, splits the command line back with Windows' own
  CommandLineToArgvW and checks the arguments come out unchanged. Run it in both `powershell` (5.1) and `pwsh`. Needs no device.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'quoting.ps1')
Add-Type -Namespace RtstQuoting -Name Native -MemberDefinition @'
[DllImport("shell32.dll", SetLastError = true)]
public static extern System.IntPtr CommandLineToArgvW([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string cmd, out int count);
[DllImport("kernel32.dll")]
public static extern System.IntPtr LocalFree(System.IntPtr mem);
'@

function Split-WindowsCommandLine([string]$Line) {
    $count = 0
    $ptr = [RtstQuoting.Native]::CommandLineToArgvW($Line, [ref]$count)
    try { return @(0..($count - 1) | ForEach-Object { [Runtime.InteropServices.Marshal]::PtrToStringUni([Runtime.InteropServices.Marshal]::ReadIntPtr($ptr, $_ * [IntPtr]::Size)) }) }
    finally { $null = [RtstQuoting.Native]::LocalFree($ptr) }
}

$cases = @('plain', 'with space', 'C:\dir\', 'C:\dir with space\', 'say "hi"', 'ends with quote"', 'back\\"slash', "read text='it''s'", 'a\b\c', '', '\"', '\\')
$failed = 0
foreach ($case in $cases) {
    $line = 'prog.exe ' + (($case, 'next' | ForEach-Object { ConvertTo-WindowsArgument $_ }) -join ' ')
    $parsed = Split-WindowsCommandLine $line
    $ok = ($parsed.Count -eq 3) -and ($parsed[1] -ceq $case) -and ($parsed[2] -ceq 'next')
    if (-not $ok) { $failed++; Write-Host "FAIL: [$case] came back as [$($parsed[1..($parsed.Count - 1)] -join '|')]" }
}
if ($failed) { Write-Host "$failed of $($cases.Count) quoting cases failed (PowerShell $($PSVersionTable.PSVersion))."; exit 1 }
Write-Host "All $($cases.Count) quoting cases round-trip (PowerShell $($PSVersionTable.PSVersion))."
