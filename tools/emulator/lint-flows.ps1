<#
.SYNOPSIS
  Text lint for the Maestro flows under .maestro (Slice 207). Needs no device and no Maestro; runs in about a second.
.DESCRIPTION
  Exit 0 when clean, 1 with one line per finding as `file:line: message`. Rules:
   - every command after the `---` line is a Maestro command name (https://docs.maestro.dev/api-reference/commands), also inside the `commands:` of
     runFlow, repeat and retry. An unknown name (openNotifications) is an error.
   - in a double-quoted YAML string a backslash escape outside YAML's set is an error (`\+`). Single-quoted and plain strings are not checked.
   - a `clearState` line needs a comment on the line above that starts `# clearState:` and says what it wipes and why that is fine for this flow.
   - a `launchApp` that carries `arguments` with a `cmd` and is not the flow's first `launchApp` must have `stopApp: false` (a hook sent to the
     running app must not restart it, Slice 212), or a `# launchApp-restart:` comment above it or inside it saying why a restart is wanted.
   - a header line `# needs: <value>[, <value>]` may only name a known value (cellular-only: run-flows.ps1 turns Wi-Fi off for that flow, Slice 212).
   - the header has an `appId:` that is one of the two dev packages.
  Used by run-flows.ps1 (before any build). Its self-test, test-lint-flows.ps1, runs in CI (tools-selftests.yml, Slice 212) and by hand.
#>
param(
    [string]$Path = (Join-Path $PSScriptRoot '..\..\.maestro')
)
$ErrorActionPreference = 'Stop'

# The 48 command names of https://docs.maestro.dev/api-reference/commands (2026-10-30).
$script:MaestroCommands = @(
    'addMedia', 'assertDarkMode', 'assertLightMode', 'assertNoDefectsWithAI', 'assertNotVisible', 'assertScreenshot', 'assertTrue', 'assertVisible',
    'assertWithAI', 'back', 'clearKeychain', 'clearState', 'copyTextFrom', 'doubleTapOn', 'eraseText', 'evalScript', 'extendedWaitUntil',
    'extractTextWithAI', 'hideKeyboard', 'inputText', 'killApp', 'launchApp', 'longPressOn', 'openLink', 'pasteText', 'pressKey', 'repeat', 'retry',
    'runFlow', 'runScript', 'scroll', 'scrollUntilVisible', 'setAirplaneMode', 'setClipboard', 'setDarkMode', 'setLocation', 'setOrientation',
    'setPermissions', 'startRecording', 'stopApp', 'stopRecording', 'swipe', 'takeScreenshot', 'tapOn', 'toggleAirplaneMode', 'toggleDarkMode',
    'travel', 'waitForAnimationToEnd'
)
$script:NeedsValues = @('cellular-only')
$script:DevAppIds = @('uk.sirous.readthestupidtext', 'uk.sirous.dinoreader')
# YAML's double-quoted escapes: 0 a b t n v f r e space " / \ N _ L P x u U (and a tab).
$script:ValidEscapes = '0abtnvfre "/\N_LPxuU' + "`t"
$script:ClearStateComment = '^#\s*clearState:\s*(.{20,})$'
$script:RestartComment = '^#\s*launchApp-restart:\s*(.{20,})$'
$script:LaunchCount = 0
$script:Problems = New-Object Collections.Generic.List[string]

function Add-Problem([string]$File, [int]$LineNumber, [string]$Message) {
    $script:Problems.Add(('{0}:{1}: {2}' -f $File, $LineNumber, $Message))
}

# True when a quote at the end of $Before opens a YAML string: only a key, a list dash or a flow indicator comes before it.
function Test-QuoteOpensString([string]$Before) {
    $t = $Before.TrimEnd()
    return ($t.Length -eq 0 -or ':-[{,?'.Contains($t.Substring($t.Length - 1)))
}

# Walks one line: returns the text without its trailing comment and the bad escapes found in double-quoted strings.
function Read-FlowLine([string]$Text) {
    $state = 'plain'
    $badEscapes = New-Object Collections.Generic.List[string]
    for ($i = 0; $i -lt $Text.Length; $i++) {
        $c = $Text[$i]
        if ($state -eq 'plain') {
            if ($c -eq '#' -and ($i -eq 0 -or [char]::IsWhiteSpace($Text[$i - 1]))) { return [pscustomobject]@{ Code = $Text.Substring(0, $i); BadEscapes = $badEscapes } }
            if (($c -eq '"' -or $c -eq "'") -and (Test-QuoteOpensString $Text.Substring(0, $i))) { $state = if ($c -eq '"') { 'double' } else { 'single' } }
        }
        elseif ($state -eq 'single') {
            if ($c -eq "'") { if ($i + 1 -lt $Text.Length -and $Text[$i + 1] -eq "'") { $i++ } else { $state = 'plain' } }
        }
        elseif ($c -eq '\') {
            if ($i + 1 -ge $Text.Length) { break }
            $next = $Text[$i + 1]
            if (-not $script:ValidEscapes.Contains([string]$next)) { $badEscapes.Add("\$next") }
            $i++
        }
        elseif ($c -eq '"') { $state = 'plain' }
    }
    return [pscustomobject]@{ Code = $Text; BadEscapes = $badEscapes }
}

function Test-Header([string]$File, [string[]]$Lines, [int]$Separator) {
    $range = if ($Separator -gt 0) { 0..($Separator - 1) } else { @() }
    $appLine = $range | Where-Object { $Lines[$_] -match '^appId:\s*(.*?)\s*$' } | Select-Object -First 1
    if ($null -eq $appLine) { Add-Problem $File ($Separator + 1) 'the header has no appId:'; return }
    $id = ($Lines[$appLine] -replace '^appId:\s*', '' -replace '\s+#.*$', '').Trim().Trim('"', "'")  # a trailing YAML comment is not part of the id
    if ($script:DevAppIds -cnotcontains $id) { Add-Problem $File ($appLine + 1) "appId '$id' is not a dev package ($($script:DevAppIds -join ', '))" }
}

# Slice 212: every `# needs:` header comment must name only known values, so a typo never silently runs a flow on the wrong network.
function Test-Needs([string]$File, [string[]]$Lines, [int]$Separator) {
    for ($i = 0; $i -lt $Separator; $i++) {
        if ($Lines[$i] -notmatch '^#\s*needs:\s*(.*?)\s*$') { continue }
        $values = @($Matches[1] -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
        if ($values.Count -eq 0) { Add-Problem $File ($i + 1) "'# needs:' names no value (allowed: $($script:NeedsValues -join ', '))" }
        foreach ($v in $values) {
            if ($script:NeedsValues -cnotcontains $v) { Add-Problem $File ($i + 1) "'# needs: $v' is not a known value (allowed: $($script:NeedsValues -join ', '))" }
        }
    }
}

function Test-ClearState([string]$File, [string[]]$Lines, [int]$Index) {
    $above = if ($Index -gt 0) { $Lines[$Index - 1].Trim() } else { '' }
    if ($above -notmatch $script:ClearStateComment) {
        Add-Problem $File ($Index + 1) 'clearState needs a comment on the line above that starts "# clearState:" and says what it wipes (voice/OCR packs, own books) and why that is fine here'
    }
}

# A command item: a dash at the flow's top level, or at the item level of the nearest `commands:` list.
function Test-IsCommandItem([int]$Indent, $Blocks) {
    if ($Indent -eq 0) { return $true }
    if ($Blocks.Count -eq 0) { return $false }
    $top = $Blocks[$Blocks.Count - 1]
    if ($null -eq $top.ItemIndent -and $Indent -ge $top.KeyIndent) { $top.ItemIndent = $Indent }
    return ($Indent -eq $top.ItemIndent)
}

function Remove-ClosedBlocks($Blocks, [int]$Indent, [string]$Code) {
    $isItem = $Code.TrimStart().StartsWith('-')
    while ($Blocks.Count -gt 0) {
        $top = $Blocks[$Blocks.Count - 1]
        if ($Indent -lt $top.KeyIndent -or ($Indent -eq $top.KeyIndent -and -not $isItem)) { $Blocks.RemoveAt($Blocks.Count - 1) } else { break }
    }
}

function Test-CommandLine([string]$File, [string[]]$Lines, [int]$Index, [string]$Code) {
    if ($Code.TrimStart() -match '^-\s+([^\s:#]+)\s*(:|$)') {
        $name = $Matches[1]
        if ($script:MaestroCommands -cnotcontains $name) { Add-Problem $File ($Index + 1) "'$name' is not a Maestro command (see the list in lint-flows.ps1)" }
    }
}

# The lines of the block under a `- launchApp:` line: everything indented deeper than the dash (blank lines included).
function Get-LaunchBlock([string[]]$Lines, [int]$Index, [int]$Indent) {
    $block = New-Object Collections.Generic.List[string]
    for ($j = $Index + 1; $j -lt $Lines.Count; $j++) {
        $line = $Lines[$j]
        if ($line.Trim() -and ($line.Length - $line.TrimStart().Length) -le $Indent) { break }
        $block.Add($line.Trim())
    }
    return $block
}

# The text a launchApp item carries after its dash, as the lines to judge: the block form's deeper lines, or the inline {...} form's body split on commas.
function Get-LaunchLines([string[]]$Lines, [int]$Index, [int]$Indent, [string]$Rest) {
    if ($Rest -match '^\{(.*)\}$') { return @($Matches[1] -split '[,{}]' | ForEach-Object { $_.Trim() }) }
    if ($Rest) { return @() }
    return @(Get-LaunchBlock $Lines $Index $Indent)
}

# Slice 212: a hook sent to the running app with launchApp restarts it (Maestro's stopApp defaults to true) and the restart can swallow the hook.
# Every launchApp form (plain, block, inline) counts toward "the first launchApp".
function Test-LaunchApp([string]$File, [string[]]$Lines, [int]$Index, [int]$Indent, [string]$Rest) {
    $script:LaunchCount++
    if ($script:LaunchCount -eq 1) { return }
    $block = @(Get-LaunchLines $Lines $Index $Indent $Rest)
    $codeLines = @($block | ForEach-Object { ($(Read-FlowLine $_).Code).Trim() })
    if (-not ($codeLines -match '^arguments:') -or -not ($codeLines -match '^(arguments:\s*\{?\s*)?cmd:\s*\S')) { return }
    if ($codeLines -match '^(arguments:.*[{,]\s*)?stopApp:\s*false\s*$') { return }
    $above = if ($Index -gt 0) { $Lines[$Index - 1].Trim() } else { '' }
    if ($above -match $script:RestartComment -or ($block -match $script:RestartComment)) { return }
    Add-Problem $File ($Index + 1) 'a hook launchApp after the first one needs "stopApp: false" (it must not restart the running app), or a "# launchApp-restart:" comment saying why a restart is wanted'
}

function Test-FlowBody([string]$File, [string[]]$Lines, [int]$Separator) {
    $blocks = New-Object Collections.ArrayList
    $skipBelow = $null
    for ($i = $Separator + 1; $i -lt $Lines.Count; $i++) {
        $raw = $Lines[$i]
        if (-not $raw.Trim()) { continue }
        $indent = $raw.Length - $raw.TrimStart().Length
        if ($null -ne $skipBelow) { if ($indent -gt $skipBelow) { continue } else { $skipBelow = $null } }
        if ($raw.TrimStart().StartsWith('#')) { continue }
        $scan = Read-FlowLine $raw
        foreach ($bad in $scan.BadEscapes) { Add-Problem $File ($i + 1) "'$bad' is not a YAML escape in a double-quoted string: use single quotes, or write \\ for a backslash" }
        $code = $scan.Code.TrimEnd()
        Remove-ClosedBlocks $blocks $indent $code
        if ($code -match '^\s*commands:\s*$') { $null = $blocks.Add(@{ KeyIndent = $indent; ItemIndent = $null }); continue }
        if ($code -match ':\s*[|>][+-]?\d?\s*$') { $skipBelow = $indent }
        if ($code -match '^\s*-\s+launchApp\s*(?::\s*(.*))?$') { Test-LaunchApp $File $Lines $i $indent ([string]$Matches[1]).Trim() }
        if ($code.TrimStart().StartsWith('-') -and (Test-IsCommandItem $indent $blocks)) { Test-CommandLine $File $Lines $i $code }
        if ($code -match '\bclearState\b' -and $code -notmatch '\bclearState\s*:\s*false\b') { Test-ClearState $File $Lines $i }  # also the inline form {clearState: true}
    }
}

function Test-FlowFile([System.IO.FileInfo]$File) {
    $script:LaunchCount = 0
    $lines = @(Get-Content $File.FullName)
    $separator = -1
    for ($i = 0; $i -lt $lines.Count; $i++) { if ($lines[$i] -match '^---\s*$') { $separator = $i; break } }
    if ($separator -lt 0) { Add-Problem $File.FullName 1 'no --- line: the header (appId) must be separated from the commands by ---'; return }
    Test-Header $File.FullName $lines $separator
    Test-Needs $File.FullName $lines $separator
    Test-FlowBody $File.FullName $lines $separator
}

if (-not (Test-Path $Path)) { Write-Host "lint-flows: $Path does not exist."; exit 2 }
$files = @(Get-ChildItem $Path -Include '*.yaml', '*.yml' -Recurse -File)
if ((Get-Item $Path).PSIsContainer -eq $false) { $files = @(Get-Item $Path) }
foreach ($file in $files) { Test-FlowFile $file }
foreach ($problem in $script:Problems) { Write-Host $problem }
Write-Host ("lint-flows: {0} flow(s) checked, {1} finding(s)." -f $files.Count, $script:Problems.Count)
if ($script:Problems.Count -gt 0) { exit 1 }
exit 0
