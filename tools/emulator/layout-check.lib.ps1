<#
.SYNOPSIS
  Slice 297: the pure functions of the Slice 267 layout check (Get-LayoutProblems and its helpers), with no param() block and no top-level preference, so dot-sourcing it leaves the caller's scope alone.
.DESCRIPTION
  Dot-sourced by run-flows.ps1, test-layout-check.ps1 and the CLI wrapper layout-check.ps1. Rules for what it checks are in layout-check.ps1.
#>

$script:OverlapShareOfSmaller = 0.15   # a touching edge or a few pixels of shadow is not an overlap: the shared area must be this share of the smaller element
$script:EdgeTolerancePx = 2            # bounds are whole pixels and rounded: two pixels past the edge is not a cut-off

# Pure: the dump as an XmlDocument. The dump written to /dev/tty ends with a "UI hierarchy dumped to" line after the XML, which is cut off first.
function ConvertTo-UiDump([string]$Text) {
    $end = $Text.LastIndexOf('</hierarchy>')
    $start = $Text.IndexOf('<?xml')
    if ($start -lt 0) { $start = $Text.IndexOf('<hierarchy') }
    if ($end -lt 0 -or $start -lt 0) { return $null }
    $doc = New-Object System.Xml.XmlDocument
    $doc.LoadXml($Text.Substring($start, $end + '</hierarchy>'.Length - $start))
    return $doc
}

# Pure: "[l,t][r,b]" as @(l, t, r, b), or $null.
function ConvertTo-Bounds([string]$Text) {
    $m = [regex]::Match("$Text", '^\[(-?\d+),(-?\d+)\]\[(-?\d+),(-?\d+)\]$')
    if (-not $m.Success) { return $null }
    return @([int]$m.Groups[1].Value, [int]$m.Groups[2].Value, [int]$m.Groups[3].Value, [int]$m.Groups[4].Value)
}

function Get-Area($B) { return [math]::Max(0, $B[2] - $B[0]) * [math]::Max(0, $B[3] - $B[1]) }

function Get-IntersectionArea($A, $B) {
    $w = [math]::Min($A[2], $B[2]) - [math]::Max($A[0], $B[0])
    $h = [math]::Min($A[3], $B[3]) - [math]::Max($A[1], $B[1])
    if ($w -le 0 -or $h -le 0) { return 0 }
    return $w * $h
}

# Pure: every enabled clickable node with an area, as objects carrying its bounds, its name for the message and the chain of its ancestors' index paths.
function Get-TappableNodes([System.Xml.XmlNode]$Parent, [string]$Path, [System.Collections.Generic.List[object]]$Found, [System.Collections.Generic.List[object]]$Labels) {
    $i = 0
    foreach ($node in $Parent.ChildNodes) {
        if ($node.NodeType -ne 'Element' -or $node.Name -ne 'node') { continue }
        $here = "$Path/$i"
        $i++
        $bounds = ConvertTo-Bounds $node.GetAttribute('bounds')
        if ($node.GetAttribute('clickable') -eq 'true' -and $node.GetAttribute('enabled') -ne 'false' -and $bounds -and (Get-Area $bounds) -gt 0) {
            $name = $node.GetAttribute('resource-id'); if (-not $name) { $name = $node.GetAttribute('text') }; if (-not $name) { $name = $node.GetAttribute('content-desc') }; if (-not $name) { $name = $node.GetAttribute('class') }
            $hasText = [bool]($node.GetAttribute('text') -or $node.GetAttribute('content-desc'))
            $described = @($node.GetAttribute('text'), $node.GetAttribute('content-desc')) | Where-Object { $_ }
            $Found.Add([pscustomobject]@{ Path = $here; Bounds = $bounds; Name = "$name"; HasText = $hasText; Described = @($described) })
        }
        elseif ($null -ne $Labels -and $node.GetAttribute('clickable') -ne 'true' -and $bounds -and (Get-Area $bounds) -gt 0) {
            $label = $node.GetAttribute('text'); if (-not $label) { $label = $node.GetAttribute('content-desc') }
            if ($label) { $Labels.Add([pscustomobject]@{ Path = $here; Bounds = $bounds; Name = "$label" }) }
        }
        Get-TappableNodes $node $here $Found $Labels
    }
}

# Pure: a clickable node with no text or description whose bounds cover the whole window (the root node's bounds, within the edge tolerance).
function Test-IsScrim($Node, $Screen) {
    if (-not $Screen -or $Node.HasText) { return $false }
    $tol = $script:EdgeTolerancePx
    return ($Node.Bounds[0] -le $Screen[0] + $tol -and $Node.Bounds[1] -le $Screen[1] + $tol -and $Node.Bounds[2] -ge $Screen[2] - $tol -and $Node.Bounds[3] -ge $Screen[3] - $tol)
}

# Pure: does the button's own text or description name this label? The owning-button pattern (Slice 205, 325): a transparent button laid over its row or pill, named "Language: English", with the labels beside it in the tree
# (siblings, not children). A label the button's name spells out as a whole segment ("Language", "English") is the button's own caption, not text under someone else's button; "Try it" under "Skip" is still a problem.
function Test-ButtonNamesLabel($Tappable, [string]$LabelText) {
    $pattern = '(^|[\s:,;.|])' + [regex]::Escape($LabelText.Trim()) + '($|[\s:,;.|])'
    foreach ($d in @($Tappable.Described)) {
        if ($d -and [regex]::IsMatch("$d", $pattern, 'IgnoreCase')) { return $true }
    }
    return $false
}

# Pure: do the inner bounds lie inside the outer bounds (within the edge tolerance)? The owning button must really sit over its label: a same-named label elsewhere is not its caption.
function Test-BoundsInside($Inner, $Outer) {
    $tol = $script:EdgeTolerancePx
    return ($Inner[0] -ge $Outer[0] - $tol -and $Inner[1] -ge $Outer[1] - $tol -and $Inner[2] -le $Outer[2] + $tol -and $Inner[3] -le $Outer[3] + $tol)
}

function Test-IsAncestorPath([string]$Ancestor, [string]$Path) { return $Path.StartsWith($Ancestor + '/') }

# Pure: does the node at index path $A come before the node at $B in the dump (document order, so the one drawn first, underneath)? An ancestor comes before its descendants.
function Test-IsPathBefore([string]$A, [string]$B) {
    $left = @($A.Split('/', [System.StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { [int]$_ })
    $right = @($B.Split('/', [System.StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { [int]$_ })
    for ($i = 0; $i -lt [math]::Min($left.Count, $right.Count); $i++) {
        if ($left[$i] -ne $right[$i]) { return $left[$i] -lt $right[$i] }
    }
    return $left.Count -lt $right.Count
}

# Pure: a node with visible text (or a content description) drawn over a tappable node that is neither its ancestor nor its descendant. A label inside its own button is fine; a label under another button (Slice 267: "Try it" under "Skip") is not, even when the label itself is not clickable.
function Get-LabelOverlapProblems($Tappable, $Labels, [string]$Label, $Screen = $null) {
    $problems = @()
    foreach ($l in $Labels) {
        foreach ($t in $Tappable) {
            if ((Test-IsAncestorPath $t.Path $l.Path) -or (Test-IsAncestorPath $l.Path $t.Path)) { continue }
            if ((Test-ButtonNamesLabel $t $l.Name) -and (Test-BoundsInside $l.Bounds $t.Bounds)) { continue }   # Slice 325: the label is the caption of the button laid over it (it names the label and the label lies inside the button)
            if ((Test-IsScrim $t $Screen) -and (Test-IsPathBefore $t.Path $l.Path)) { continue }   # Slice 297: a scrim drawn under the label is a backdrop; one drawn over it swallows the label's taps
            $shared = Get-IntersectionArea $l.Bounds $t.Bounds
            $smaller = [math]::Min((Get-Area $l.Bounds), (Get-Area $t.Bounds))
            if ($smaller -gt 0 -and $shared -ge $script:OverlapShareOfSmaller * $smaller) {
                $problems += "${Label}: text '$($l.Name)' is covered by '$($t.Name)' (bounds [$($l.Bounds -join ',')] and [$($t.Bounds -join ',')])"
            }
        }
    }
    return $problems
}

# Pure: the problems of one dump, as strings ($Label first). Empty when the screen is fine.
function Get-LayoutProblems([string]$Text, [string]$Label = 'screen') {
    $doc = ConvertTo-UiDump $Text
    if (-not $doc) { return @("${Label}: no usable UI dump") }
    $rootNode = $doc.SelectSingleNode('//hierarchy/node')
    $screen = if ($rootNode) { ConvertTo-Bounds $rootNode.GetAttribute('bounds') } else { $null }
    $tappable = New-Object 'System.Collections.Generic.List[object]'
    $labels = New-Object 'System.Collections.Generic.List[object]'
    Get-TappableNodes $doc.DocumentElement '' $tappable $labels
    $problems = @()
    if ($screen) {
        foreach ($t in $tappable) {
            $b = $t.Bounds; $tol = $script:EdgeTolerancePx
            if ($b[0] -lt $screen[0] - $tol -or $b[1] -lt $screen[1] - $tol -or $b[2] -gt $screen[2] + $tol -or $b[3] -gt $screen[3] + $tol) {
                $problems += "${Label}: '$($t.Name)' is cut off by the screen edge (bounds [$($b -join ',')], screen [$($screen -join ',')])"
            }
        }
    }
    for ($a = 0; $a -lt $tappable.Count; $a++) {
        for ($c = $a + 1; $c -lt $tappable.Count; $c++) {
            $x = $tappable[$a]; $y = $tappable[$c]
            if (Test-IsScrim $x $screen) { continue }   # Slice 297: a full-screen tap-to-dismiss layer drawn first sits behind everything on purpose; one drawn after a button ($y) is on top of it and is still reported
            if ((Test-IsAncestorPath $x.Path $y.Path) -or (Test-IsAncestorPath $y.Path $x.Path)) { continue }
            $shared = Get-IntersectionArea $x.Bounds $y.Bounds
            $smaller = [math]::Min((Get-Area $x.Bounds), (Get-Area $y.Bounds))
            if ($smaller -gt 0 -and $shared -ge $script:OverlapShareOfSmaller * $smaller) {
                $problems += "${Label}: '$($x.Name)' and '$($y.Name)' overlap by $([int](100 * $shared / $smaller))% of the smaller one (bounds [$($x.Bounds -join ',')] and [$($y.Bounds -join ',')])"
            }
        }
    }
    $problems += Get-LabelOverlapProblems $tappable $labels $Label $screen
    return $problems
}
