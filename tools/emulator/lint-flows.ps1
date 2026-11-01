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
   - Slice 215: a `runFlow` that names a flow file (`runFlow: sub.yaml`, `runFlow: {file: sub.yaml}`, or `file:` under it) is followed (path relative to the flow) and its
     launchApp lines are judged as if inlined, so a hook launched from a sub-flow cannot escape the restart rule. The inline `launchApp: {...}` form is parsed
     with a tokenizer that respects quotes and nested braces. `# clearState:` may sit on the line above the `clearState` key or above its `- launchApp:` item,
     and the word inside a quoted string is not a clearState. An explicit `stopApp: true` is a restart like a missing `stopApp: false`.
   - a flow file may not hold a doubled CR (CR CR LF) or mixed line endings (CRLF and LF lines): a fixer left doubled CRs in two flows on 2026-10-30.
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
$script:DevAppIds = @('uk.sirous.readthestupidtext', 'uk.sirous.dinoreader.dbg')
# YAML's double-quoted escapes: 0 a b t n v f r e space " / \ N _ L P x u U (and a tab).
$script:ValidEscapes = '0abtnvfre "/\N_LPxuU' + "`t"
$script:ClearStateComment = '^#\s*clearState:\s*(.{20,})$'
$script:RestartComment = '^#\s*launchApp-restart:\s*(.{20,})$'
$script:LaunchCount = 0
$script:SubStack = @()
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

# Splits the body of an inline {...} mapping at its top-level commas; a comma inside quotes or nested braces/brackets stays in its entry.
function Split-TopLevel([string]$Body) {
    $parts = New-Object Collections.Generic.List[string]
    $depth = 0; $quote = $null; $start = 0
    for ($i = 0; $i -lt $Body.Length; $i++) {
        $c = $Body[$i]
        if ($quote) {
            if ($c -eq '\' -and $quote -eq '"') { $i++ }
            elseif ($c -eq $quote) { if ($quote -eq "'" -and $i + 1 -lt $Body.Length -and $Body[$i + 1] -eq "'") { $i++ } else { $quote = $null } }
        }
        elseif (($c -eq '"' -or $c -eq "'") -and (Test-QuoteOpensString $Body.Substring(0, $i))) { $quote = $c }
        elseif ($c -eq '{' -or $c -eq '[') { $depth++ }
        elseif ($c -eq '}' -or $c -eq ']') { $depth-- }
        elseif ($c -eq ',' -and $depth -eq 0) { $parts.Add($Body.Substring($start, $i - $start).Trim()); $start = $i + 1 }
    }
    $parts.Add($Body.Substring($start).Trim())
    return @($parts | Where-Object { $_ })
}

# The entries of an inline mapping as the lines the block form would have: `key: value`, and `key:` followed by the entries of a nested {...}.
function Get-InlineEntries([string]$Body) {
    foreach ($entry in (Split-TopLevel $Body)) {
        if ($entry -match '^([^:]+):\s*\{(.*)\}$') { $key = $Matches[1].Trim(); $inner = $Matches[2]; "${key}:"; Get-InlineEntries $inner }
        else { $entry }
    }
}

# The line with the text inside quotes emptied, so a word that only appears in a string is not read as a command or key.
function Remove-QuotedText([string]$Text) {
    $sb = New-Object Text.StringBuilder
    $quote = $null
    for ($i = 0; $i -lt $Text.Length; $i++) {
        $c = $Text[$i]
        if ($quote) {
            if ($c -eq '\' -and $quote -eq '"') { $i++ }
            elseif ($c -eq $quote) { if ($quote -eq "'" -and $i + 1 -lt $Text.Length -and $Text[$i + 1] -eq "'") { $i++ } else { $quote = $null; [void]$sb.Append($c) } }
        }
        else {
            [void]$sb.Append($c)
            if (($c -eq '"' -or $c -eq "'") -and (Test-QuoteOpensString $Text.Substring(0, $i))) { $quote = $c }
        }
    }
    return $sb.ToString()
}

# Slice 215: doubled CRs (CR CR LF) and mixed endings are what a whole-file rewrite by a tool leaves behind; a file is all CRLF or all LF.
function Test-LineEndings([System.IO.FileInfo]$File) {
    $raw = [Text.Encoding]::GetEncoding(28591).GetString([IO.File]::ReadAllBytes($File.FullName))
    $first = $null
    $number = 0
    foreach ($line in ($raw -split '(?<=\n)')) {
        $number++
        if (-not $line.EndsWith("`n")) { continue }  # the last line may have no ending
        $kind = if ($line.EndsWith("`r`r`n")) { 'doubled' } elseif ($line.EndsWith("`r`n")) { 'crlf' } else { 'lf' }
        if ($kind -eq 'doubled') { Add-Problem $File.FullName $number 'doubled CR (CR CR LF) in this line ending: restore the file and redo the edit'; return }
        if ($null -eq $first) { $first = $kind }
        elseif ($kind -ne $first) { Add-Problem $File.FullName $number "mixed line endings: this line is $($kind.ToUpper()) but the file starts with $($first.ToUpper())"; return }
    }
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

# The `- ` item line that holds the key on line $Index (the line itself when it is the item), or -1.
function Get-EnclosingItemIndex([string[]]$Lines, [int]$Index) {
    if ($Lines[$Index].TrimStart().StartsWith('-')) { return $Index }
    $indent = $Lines[$Index].Length - $Lines[$Index].TrimStart().Length
    for ($j = $Index - 1; $j -ge 0; $j--) {
        $t = $Lines[$j].TrimStart()
        if (-not $t -or $t.StartsWith('#')) { continue }
        if (($Lines[$j].Length - $t.Length) -lt $indent -and $t.StartsWith('-')) { return $j }
    }
    return -1
}

function Test-ClearState([string]$File, [string[]]$Lines, [int]$Index) {
    $above = if ($Index -gt 0) { $Lines[$Index - 1].Trim() } else { '' }
    $item = Get-EnclosingItemIndex $Lines $Index
    $aboveItem = if ($item -gt 0) { $Lines[$item - 1].Trim() } else { '' }
    if ($above -notmatch $script:ClearStateComment -and $aboveItem -notmatch $script:ClearStateComment) {
        Add-Problem $File ($Index + 1) 'clearState needs a comment on the line above it (or above its "- launchApp:" item) that starts "# clearState:" and says what it wipes (voice/OCR packs, own books) and why that is fine here'
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
    if ($Rest -match '^\{(.*)\}$') { return @(Get-InlineEntries $Matches[1]) }
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

# The block form's own keys: only the lines at the first child's indent, so a `file:` of a nested `- runFlow:` under `commands:` is not taken for this one's.
function Get-RunFlowChildLines([string[]]$Lines, [int]$Index, [int]$Indent) {
    $child = -1
    for ($j = $Index + 1; $j -lt $Lines.Count; $j++) {
        $line = $Lines[$j]
        if (-not $line.Trim()) { continue }
        $depth = $line.Length - $line.TrimStart().Length
        if ($depth -le $Indent) { break }
        if ($child -lt 0) { $child = $depth }
        if ($depth -eq $child) { $line.Trim() }
    }
}

# The flow file a `runFlow` line points at (relative path), or $null: the plain form, the inline {file: ...} form, or a `file:` line under the block form.
function Get-RunFlowPath([string[]]$Lines, [int]$Index, [int]$Indent, [string]$Rest) {
    $entries = if ($Rest -match '^\{(.*)\}$') { @(Get-InlineEntries $Matches[1]) } elseif ($Rest) { @("file: $Rest") } else { @(Get-RunFlowChildLines $Lines $Index $Indent) }
    $entry = $entries | Where-Object { $_ -match '^file:\s*\S' } | Select-Object -First 1
    if (-not $entry) { return $null }
    $path = (($(Read-FlowLine $entry).Code) -replace '^file:\s*', '').Trim().Trim('"', "'")
    if ($path -notmatch '\.ya?ml$') { return $null }
    return $path
}

# Slice 215: judges the launchApp lines of a sub-flow as if they were written where the runFlow is (it shares the launch count and the stack of files being followed).
function Test-SubFlow([string]$File, [int]$Index, [string]$Path, [string]$Dir) {
    if ($Path -match '\$\{' -or [IO.Path]::IsPathRooted($Path)) { return }  # a Maestro variable or an absolute path cannot be resolved here: skipped, not reported
    $full = Join-Path $Dir $Path
    if (-not (Test-Path $full)) { Add-Problem $File ($Index + 1) "runFlow file '$Path' does not exist (paths are relative to the flow)"; return }
    $full = (Resolve-Path $full).Path
    if ($script:SubStack -contains $full) { Add-Problem $File ($Index + 1) "runFlow '$Path' calls a flow that is already being followed (a loop)"; return }
    $sub = @(Get-Content $full)
    $separator = -1
    for ($i = 0; $i -lt $sub.Count; $i++) { if ($sub[$i] -match '^---\s*$') { $separator = $i; break } }
    $outer = $script:Problems
    $script:Problems = New-Object Collections.Generic.List[string]
    $script:SubStack += $full
    try { Test-FlowBody $full $sub $separator -LaunchOnly -Dir (Split-Path $full -Parent) }
    finally { $script:SubStack = @($script:SubStack | Where-Object { $_ -ne $full }); $inner = $script:Problems; $script:Problems = $outer }
    foreach ($problem in $inner) { Add-Problem $File ($Index + 1) "runFlow ${Path}: $problem" }
}

# -LaunchOnly (a followed sub-flow): only the launchApp and runFlow rules; the sub-flow file is linted in full on its own.
function Test-FlowBody([string]$File, [string[]]$Lines, [int]$Separator, [switch]$LaunchOnly, [string]$Dir) {
    $blocks = New-Object Collections.ArrayList
    $skipBelow = $null
    for ($i = $Separator + 1; $i -lt $Lines.Count; $i++) {
        $raw = $Lines[$i]
        if (-not $raw.Trim()) { continue }
        $indent = $raw.Length - $raw.TrimStart().Length
        if ($null -ne $skipBelow) { if ($indent -gt $skipBelow) { continue } else { $skipBelow = $null } }
        if ($raw.TrimStart().StartsWith('#')) { continue }
        $scan = Read-FlowLine $raw
        if (-not $LaunchOnly) { foreach ($bad in $scan.BadEscapes) { Add-Problem $File ($i + 1) "'$bad' is not a YAML escape in a double-quoted string: use single quotes, or write \\ for a backslash" } }
        $code = $scan.Code.TrimEnd()
        Remove-ClosedBlocks $blocks $indent $code
        if ($code -match '^\s*commands:\s*$') { $null = $blocks.Add(@{ KeyIndent = $indent; ItemIndent = $null }); continue }
        if ($code -match ':\s*[|>][+-]?\d?\s*$') { $skipBelow = $indent }
        if ($code -match '^\s*-\s+launchApp\s*(?::\s*(.*))?$') { Test-LaunchApp $File $Lines $i $indent ([string]$Matches[1]).Trim() }
        if ($code -match '^\s*-\s+runFlow\s*:\s*(.*)$') {
            $path = Get-RunFlowPath $Lines $i $indent ([string]$Matches[1]).Trim()
            if ($path) { Test-SubFlow $File $i $path $Dir }
        }
        if ($LaunchOnly) { continue }
        if ($code.TrimStart().StartsWith('-') -and (Test-IsCommandItem $indent $blocks)) { Test-CommandLine $File $Lines $i $code }
        $bare = Remove-QuotedText $code  # a "clearState" inside a string is not the command
        if ($bare -match '\bclearState\b' -and $bare -notmatch '\bclearState\s*:\s*false\b') { Test-ClearState $File $Lines $i }  # also the inline form {clearState: true}
    }
}

function Test-FlowFile([System.IO.FileInfo]$File) {
    $script:LaunchCount = 0
    $script:SubStack = @($File.FullName)
    Test-LineEndings $File
    $lines = @(Get-Content $File.FullName)
    $separator = -1
    for ($i = 0; $i -lt $lines.Count; $i++) { if ($lines[$i] -match '^---\s*$') { $separator = $i; break } }
    if ($separator -lt 0) { Add-Problem $File.FullName 1 'no --- line: the header (appId) must be separated from the commands by ---'; return }
    Test-Header $File.FullName $lines $separator
    Test-Needs $File.FullName $lines $separator
    Test-FlowBody $File.FullName $lines $separator -Dir (Split-Path $File.FullName -Parent)
}

if (-not (Test-Path $Path)) { Write-Host "lint-flows: $Path does not exist."; exit 2 }
$files = @(Get-ChildItem $Path -Include '*.yaml', '*.yml' -Recurse -File)
if ((Get-Item $Path).PSIsContainer -eq $false) { $files = @(Get-Item $Path) }
foreach ($file in $files) { Test-FlowFile $file }
foreach ($problem in $script:Problems) { Write-Host $problem }
Write-Host ("lint-flows: {0} flow(s) checked, {1} finding(s)." -f $files.Count, $script:Problems.Count)
if ($script:Problems.Count -gt 0) { exit 1 }
exit 0
