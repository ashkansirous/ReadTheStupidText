# Windows command-line quoting for Windows PowerShell 5.1, which has no ProcessStartInfo.ArgumentList. Dot-sourced, not run.
# The rules of CommandLineToArgvW / the C runtime: backslashes are literal unless they come right before a quote, where each one is doubled
# (and the quote itself is escaped); the same goes for backslashes at the end, which sit before the closing quote we add.
function ConvertTo-WindowsArgument([string]$Value) {
    $escaped = $Value -replace '(\\*)"', '$1$1\"'
    $escaped = $escaped -replace '(\\+)$', '$1$1'
    return '"' + $escaped + '"'
}
