# Helpers for the check scripts. They run inside the VM, in the signed-in user's desktop session, under Windows
# PowerShell 5.1. Each check dot-sources this file and ends with Invoke-Check. (Slice 163)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, WindowsBase
Add-Type -AssemblyName System.Windows.Forms

$script:AutomationElement = [System.Windows.Automation.AutomationElement]
$script:TreeScope = [System.Windows.Automation.TreeScope]

Add-Type -Namespace Win32 -Name Mouse -MemberDefinition '[DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);' -ErrorAction SilentlyContinue

Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class NativeKeys {
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  public const uint KeyUp = 2;
  public static void Chord(params byte[] keys) {
    foreach (byte k in keys) keybd_event(k, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(150);
    for (int i = keys.Length - 1; i >= 0; i--) keybd_event(keys[i], 0, KeyUp, UIntPtr.Zero);
  }
}
'@

function Invoke-Check {
    # Runs the body; whatever it returns as a string is the pass message, any throw is a failure. Writes the result file.
    param([Parameter(Mandatory)] [string] $ResultFile, [Parameter(Mandatory)] [scriptblock] $Body)
    $result = @{ pass = $false; message = '' }
    try {
        $result.message = [string](& $Body)
        $result.pass = $true
    } catch {
        $result.message = $_.Exception.Message
    }
    # Write under a temp name and rename, so the runner never reads a half-written file.
    ($result | ConvertTo-Json -Compress) | Set-Content -Path "$ResultFile.tmp" -Encoding UTF8
    Move-Item -Path "$ResultFile.tmp" -Destination $ResultFile -Force
}

function Get-InstalledPackage {
    param([string] $Name)
    $pkg = Get-AppxPackage -Name $Name
    if (-not $pkg) { throw "Package $Name is not installed." }
    return $pkg
}

function Start-PackagedApp {
    param($Package)
    $manifest = Get-AppxPackageManifest -Package $Package
    $appId = @($manifest.Package.Applications.Application)[0].Id
    Start-Process "shell:AppsFolder\$($Package.PackageFamilyName)!$appId"
}

function Wait-Until {
    param([Parameter(Mandatory)] [scriptblock] $Condition, [int] $TimeoutSec = 30, [string] $Failure = 'condition not met')
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        $value = & $Condition
        if ($value) { return $value }
        Start-Sleep -Milliseconds 500
    }
    throw "$Failure (waited $TimeoutSec s)"
}

function Get-RtstLogText {
    # The Serilog system log and the input log under the package's TempState\logs folder.
    param($Package)
    $dir = Join-Path $env:LOCALAPPDATA "Packages\$($Package.PackageFamilyName)\TempState\logs"
    if (-not (Test-Path $dir)) { return '' }
    $text = Get-ChildItem $dir -Filter *.log -ErrorAction SilentlyContinue | ForEach-Object {
        try { [IO.File]::ReadAllText($_.FullName) } catch { '' }
    }
    return ($text -join "`n")
}

function Find-UiaByName {
    # Finds a descendant by Name (case-insensitive): an exact match wins, else the first partial match; optional control type name such as 'Button'.
    # ToolTip popups and everything inside one are skipped: a button's tooltip carries its name too ("Next sentence (Right arrow)", with a child Text of the same name) and comes first in the tree.
    param($Root = $script:AutomationElement::RootElement, [Parameter(Mandatory)] [string] $Name, [string] $ControlType)
    $partial = $null
    $all = $Root.FindAll($script:TreeScope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($e in $all) {
        # An element that vanished mid-read (a tooltip closing, ElementNotAvailableException or any COM error) is skipped, never a throw.
        try {
            $n = $e.Current.Name
            if (-not $n -or $n.IndexOf($Name, [StringComparison]::OrdinalIgnoreCase) -lt 0) { continue }
            if ($ControlType -and $e.Current.ControlType.ProgrammaticName -ne "ControlType.$ControlType") { continue }
            if (Test-InsideToolTip $e) { continue }
            if ($n.Equals($Name, [StringComparison]::OrdinalIgnoreCase)) { return $e }
            if (-not $partial) { $partial = $e }
        } catch { continue }
    }
    return $partial
}

function Test-InsideToolTip {
    # True for a ToolTip element or one whose UIA parent is a ToolTip (its text child).
    # An element that vanished while being read counts as a tooltip (skipped by the caller), never a throw.
    param($Element)
    try {
        if ($Element.Current.ControlType.ProgrammaticName -eq 'ControlType.ToolTip') { return $true }
        $parent = [System.Windows.Automation.TreeWalker]::RawViewWalker.GetParent($Element)
        return ($null -ne $parent) -and $parent.Current.ControlType.ProgrammaticName -eq 'ControlType.ToolTip'
    } catch { return $true }
}

function Invoke-Uia {
    param($Element)
    if (-not $Element) { throw 'Invoke-Uia: element not found (the UIA lookup returned nothing).' }
    $pattern = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) { $pattern.Invoke(); return }
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$pattern)) { $pattern.Toggle(); return }
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) { $pattern.Select(); return }
    $rect = $Element.Current.BoundingRectangle
    if ($rect.IsEmpty) { throw "Element '$($Element.Current.Name)' has no pattern and no bounds." }
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point([int]($rect.X + $rect.Width / 2), [int]($rect.Y + $rect.Height / 2))
    Start-Sleep -Milliseconds 200
    [Win32.Mouse]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); [Win32.Mouse]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
}

function Get-AppWindows {
    # Top-level UIA windows owned by the processes of the given executable base name.
    param([Parameter(Mandatory)] [string] $ProcessName)
    $ids = @(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | ForEach-Object Id)
    $cond = New-Object System.Windows.Automation.PropertyCondition($script:AutomationElement::ControlTypeProperty, [System.Windows.Automation.ControlType]::Window)
    $wins = $script:AutomationElement::RootElement.FindAll($script:TreeScope::Children, $cond)
    return @($wins | Where-Object { $ids -contains $_.Current.ProcessId })
}

Add-Type -Namespace Win32 -Name Windows -MemberDefinition '[DllImport("user32.dll")] public static extern IntPtr FindWindow(string cls, string title);' -ErrorAction SilentlyContinue

function Get-TaskbarElement {
    # AutomationElement.RootElement does not list the taskbar as a child, so go through its window handle.
    $handle = [Win32.Windows]::FindWindow('Shell_TrayWnd', $null)
    if ($handle -eq [IntPtr]::Zero) { throw 'No taskbar (Shell_TrayWnd) in this session.' }
    return $script:AutomationElement::FromHandle($handle)
}

function Save-Screen {
    param([Parameter(Mandatory)] [string] $Path)
    Add-Type -AssemblyName System.Drawing
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try { $g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size); $bmp.Save($Path) } finally { $g.Dispose(); $bmp.Dispose() }
}

function Set-TrayIconPromoted {
    # Win11 hides a new tray icon in the overflow flyout; promoting it puts it on the taskbar where it can be seen and clicked.
    param([Parameter(Mandatory)] [string] $ExeName)
    $entries = @(Get-ChildItem 'HKCU:\Control Panel\NotifyIconSettings' -ErrorAction SilentlyContinue | Where-Object { (Get-ItemProperty $_.PSPath).ExecutablePath -like "*$ExeName" })
    if (-not $entries.Count) { throw "Windows has no tray-icon entry for $ExeName (the app never registered its icon)." }
    $entries | ForEach-Object { Set-ItemProperty $_.PSPath -Name IsPromoted -Value 1 -Type DWord }
}

function Find-TrayIconCenter {
    # The taskbar is not visible to UI Automation in this VM, so look for the icon's own blue (#2556A8) in the tray strip.
    # Returns @{X;Y} or $null. The VM is frozen at its checkpoint, so the strip holds the same few icons every run.
    $shot = Join-Path $env:TEMP 'rtst-tray.png'
    Save-Screen $shot
    $bmp = [System.Drawing.Bitmap]::FromFile($shot)
    try {
        $h = $bmp.Height; $w = $bmp.Width
        $xs = @(); $ys = @()
        for ($x = $w - 300; $x -lt $w; $x++) {
            for ($y = $h - 46; $y -lt $h - 8; $y++) {
                $c = $bmp.GetPixel($x, $y)
                if ($c.R -lt 70 -and $c.G -ge 60 -and $c.G -le 115 -and $c.B -ge 140 -and $c.B -le 190) { $xs += $x; $ys += $y }
            }
        }
        if ($xs.Count -lt 10) { return $null }
        return @{ X = [int](($xs | Measure-Object -Average).Average); Y = [int](($ys | Measure-Object -Average).Average) }
    } finally { $bmp.Dispose(); Remove-Item $shot -ErrorAction SilentlyContinue }
}

function Click-Screen {
    param([int] $X, [int] $Y)
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point($X, $Y)
    Start-Sleep -Milliseconds 300
    [Win32.Mouse]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    [Win32.Mouse]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
}

function Move-MouseTo {
    # A real pointer move (mouse_event MOUSEEVENTF_MOVE | ABSOLUTE | VIRTUALDESK); setting Cursor.Position alone raises no WinUI pointer move in the VM (Slice 235, Slice 269).
    param([int] $X, [int] $Y)
    $screen = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $ax = [uint32][Math]::Round(($X - $screen.Left) * 65535.0 / [Math]::Max(1, $screen.Width - 1))
    $ay = [uint32][Math]::Round(($Y - $screen.Top) * 65535.0 / [Math]::Max(1, $screen.Height - 1))
    [Win32.Mouse]::mouse_event(0x8001 -bor 0x4000, $ax, $ay, 0, [UIntPtr]::Zero)
}

function Find-UiaById {
    param([Parameter(Mandatory)] $Root, [Parameter(Mandatory)] [string] $Id)
    return $Root.FindFirst($script:TreeScope::Descendants, (New-Object System.Windows.Automation.PropertyCondition($script:AutomationElement::AutomationIdProperty, $Id)))
}

function Find-UiaExact {
    # Finds a visible descendant whose Name is exactly $Name (case-sensitive), optionally of one control type such as 'Button'.
    # The match runs inside UIA (a Name condition), so a poll does not walk the whole tree in PowerShell.
    param([Parameter(Mandatory)] $Root, [Parameter(Mandatory)] [string] $Name, [string] $ControlType)
    $cond = New-Object System.Windows.Automation.PropertyCondition($script:AutomationElement::NameProperty, $Name)
    foreach ($e in $Root.FindAll($script:TreeScope::Descendants, $cond)) {
        if ($e.Current.BoundingRectangle.IsEmpty) { continue }
        if (-not $ControlType -or $e.Current.ControlType.ProgrammaticName -eq "ControlType.$ControlType") { return $e }
    }
    return $null
}

function Find-UiaVisibleById {
    param([Parameter(Mandatory)] $Root, [Parameter(Mandatory)] [string] $Id)
    $e = Find-UiaById $Root $Id
    if ($e -and -not $e.Current.BoundingRectangle.IsEmpty) { return $e }
    return $null
}

function Find-FirstTileMore {
    # Slice 420: the dots button (AutomationId TileMore) of the first visible book, in reading order. A tap on a cover reads the book (Slice 418 decision 6); a book's sheet opens from this button, shown only while the shelf is unlocked.
    param([Parameter(Mandatory)] $Root)
    $cond = New-Object System.Windows.Automation.PropertyCondition($script:AutomationElement::AutomationIdProperty, 'TileMore')
    $dots = foreach ($e in $Root.FindAll($script:TreeScope::Descendants, $cond)) { if (-not $e.Current.BoundingRectangle.IsEmpty) { $e } }
    return @($dots | Sort-Object { [int]($_.Current.BoundingRectangle.Y / 20) }, { $_.Current.BoundingRectangle.X })[0]
}

function Test-LockedShelfShown {
    # Slice 418: there is one shelf. The lock (AutomationId GalleryLock) is on it always; the countdown chip (LockChip) shows only while it is unlocked. Locked means the lock without the chip.
    param([Parameter(Mandatory)] $Root)
    return [bool]((Find-UiaVisibleById $Root 'GalleryLock') -and -not (Find-UiaVisibleById $Root 'LockChip'))
}

function Enter-KidsShelf {
    # Slice 418: a check that starts from the locked shelf taps the lock when the shelf is unlocked (the chip shows): a tap on the lock locks at once. Does nothing when it is locked already.
    $find = { $w = Get-AppWindows 'DinoReader.App' | Select-Object -First 1; if ($w) { Find-UiaVisibleById $w 'LockChip' } }
    if (-not (Get-AppWindows 'DinoReader.App')) { throw 'FAIL Enter-KidsShelf: Dino Reader has no window (not running, or closed)' }
    if (-not (& $find)) { return }
    $lock = Find-UiaVisibleById (Get-AppWindows 'DinoReader.App' | Select-Object -First 1) 'GalleryLock'
    if (-not $lock) { throw 'FAIL Enter-KidsShelf: the shelf is unlocked but no lock (GalleryLock) is showing' }
    Invoke-Uia $lock
    $null = Wait-Until { -not (& $find) } 15 'FAIL a tap on the lock did not lock the shelf'   # Wait-Until throws when the time runs out, so a stuck unlocked shelf fails the check here
}

function Get-ConsentDialogAbove {
    # The Shell_SystemDialog window above an element (RawViewWalker), or $null: the system microphone prompt is not under the UIA root, only reachable as the focused element (tools/desktop-vm/CLAUDE.md, dino-record).
    param($Element)
    $walker = [System.Windows.Automation.TreeWalker]::RawViewWalker
    $node = $Element
    for ($depth = 0; $node -and $depth -lt 12; $depth++) {
        if ($node.Current.ClassName -eq 'Shell_SystemDialog') { return $node }
        $node = $walker.GetParent($node)
    }
    return $null
}

function Find-ConsentButtonFocused {
    # The focused Button of the microphone consent prompt (a Button outside the app's process under a Shell_SystemDialog), or $null while the focus is anywhere else.
    param([Parameter(Mandatory)] [int] $AppProcessId)
    try {
        $focused = $script:AutomationElement::FocusedElement
        if (-not $focused -or $focused.Current.ControlType -ne [System.Windows.Automation.ControlType]::Button) { return $null }
        if ($focused.Current.ProcessId -eq $AppProcessId) { return $null }
        if (Get-ConsentDialogAbove $focused) { return $focused }
    } catch { $null = $_ }   # no-fallback: a focus read that fails this poll is "no prompt yet"; the caller polls again
    return $null
}

function Get-ConsentDialogButtons {
    # The dialog's buttons, walked with RawViewWalker (FindAll on the dialog returns nothing; at most 200 nodes).
    param($Dialog)
    $walker = [System.Windows.Automation.TreeWalker]::RawViewWalker
    $buttons = @(); $queue = New-Object System.Collections.Queue; $queue.Enqueue($Dialog); $seen = 0
    while ($queue.Count -gt 0 -and $seen -lt 200) {
        $node = $queue.Dequeue(); $seen++
        if ($node.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button) { $buttons += $node }
        for ($child = $walker.GetFirstChild($node); $child; $child = $walker.GetNextSibling($child)) { $queue.Enqueue($child) }
    }
    return $buttons
}

function Answer-MicConsentPrompt {
    # Answers the first-use Windows microphone prompt with its Allow ("Yes", "Allow") or Deny ("No", "Don't allow", "Deny", "Block") button (the VM runs in English). Polls up to $Seconds (short: the prompt opens right after the key press,
    # and a run with no prompt must not wait long); returns a note naming the path taken, also when no prompt appeared (consent already decided). A prompt with no matching button throws with the buttons it saw.
    param([Parameter(Mandatory)] [int] $AppProcessId, [Parameter(Mandatory)] [ValidateSet('Allow', 'Deny')] [string] $Choice, [int] $Seconds = 6, [scriptblock] $NoPromptNeeded)
    # Only "No" is seen on a real run; the Allow names (Yes, Allow ...) are a guess no run has proven (a wrong guess throws naming the buttons seen).
    $buttonPattern = if ($Choice -eq 'Allow') { '^(Yes|Allow|Allow once|Allow this time)$' } else { '^(No|Don.t allow|Deny|Block)$' }
    $script:ConsentPrompt = $null
    # $NoPromptNeeded (optional): a condition that shows the app got past the prompt (recording started); the wait ends at once when it is true, so a slow prompt is not missed and a run with no prompt does not wait the full time.
    try { Wait-Until { $script:ConsentPrompt = Find-ConsentButtonFocused $AppProcessId; [bool]$script:ConsentPrompt -or ($NoPromptNeeded -and (& $NoPromptNeeded)) } $Seconds 'the microphone consent prompt' | Out-Null } catch { $null = $_ }
    $prompt = $script:ConsentPrompt
    if (-not $prompt) { return "no microphone consent prompt appeared in $Seconds s (consent already decided)" }
    $dialog = Get-ConsentDialogAbove $prompt
    $buttons = if ($dialog) { @(Get-ConsentDialogButtons $dialog) } else { @() }
    $button = if ($prompt.Current.Name -match $buttonPattern) { $prompt } else { $buttons | Where-Object { $_.Current.Name -match $buttonPattern } | Select-Object -First 1 }
    $names = @($buttons | ForEach-Object { $_.Current.Name } | Where-Object { $_ })
    if (-not $button) { throw "FAIL the microphone consent prompt showed no $Choice button (Name matching $buttonPattern); focused: '$($prompt.Current.Name)'; buttons seen: $($names -join ' / ')" }
    $clicked = $button.Current.Name
    Invoke-Uia $button
    Start-Sleep -Milliseconds 800   # fixed-wait: the prompt closes and focus returns to the app
    return "microphone consent prompt answered $Choice through '$clicked' (buttons seen: $($names -join ' / '))"
}

function Get-VisibleNames {
    # "Type:Name" of the first $Max named, visible elements: the UI dump a failing check appends to its message.
    param([Parameter(Mandatory)] $Root, [int] $Max = 40)
    $all = $Root.FindAll($script:TreeScope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    $names = foreach ($e in $all) {
        if ($e.Current.Name -and -not $e.Current.BoundingRectangle.IsEmpty) { $e.Current.ControlType.ProgrammaticName.Substring(12) + ':' + $e.Current.Name }
    }
    return @($names | Select-Object -First $Max)
}

function Invoke-PressAndHold {
    # Presses the left mouse button on the element's centre and holds it for $HoldMs (default 3500), nudging the pointer by
    # one pixel every 250 ms so the app keeps seeing pointer moves. Used for Dino's real grown-up gate (hold 3 s, no bypass).
    param([Parameter(Mandatory)] $Element, [int] $HoldMs = 3500)
    $r = $Element.Current.BoundingRectangle
    if ($r.IsEmpty) { throw "Invoke-PressAndHold: '$($Element.Current.Name)' has no bounds." }
    $cx = [int]($r.X + $r.Width / 2); $cy = [int]($r.Y + $r.Height / 2)
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point($cx, $cy)
    Start-Sleep -Milliseconds 300
    [Win32.Mouse]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    try {
        $steps = [int][Math]::Ceiling($HoldMs / 250)
        foreach ($i in 1..$steps) { Start-Sleep -Milliseconds 250; [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point(($cx + ($i % 2)), $cy) }
    } finally { [Win32.Mouse]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero) }
}

function Get-GateHoldResetCount {
    # Lines in the app's window-trace.log saying a gate hold was reset while still holding (GatePage.ResetHold).
    param([string] $PackageName)
    $pkg = Get-AppxPackage -Name $PackageName
    if (-not $pkg) { return 0 }
    $path = Join-Path $env:LOCALAPPDATA "Packages\$($pkg.PackageFamilyName)\LocalState\window-trace.log"
    if (-not (Test-Path $path)) { return 0 }
    return @(Get-Content -Path $path | Where-Object { $_ -match 'gate hold reset while holding' }).Count
}

function Find-GateHoldSum {
    foreach ($e in (Get-DinoWindow).FindAll($script:TreeScope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
        if ($e.Current.Name -match 'What is (\d+) plus (\d+)') { return [int]$Matches[1] + [int]$Matches[2] }
    }
    return $null
}

function Invoke-GateHold {
    # Holds Dino's grown-up gate ring and returns the sum it then shows. The VM's synthetic mouse sometimes delivers a release ~7 ms after the press
    # while the button is still held (Slice 325), so when no sum appears the hold is repeated, up to $MaxAttempts times in all; only then it throws.
    param(
        [Parameter(Mandatory)] [scriptblock] $FindRing,
        [Parameter(Mandatory)] [string] $PackageName,
        [int] $HoldMs = 3500,
        [int] $MaxAttempts = 3,
        [int] $SumTimeoutSec = 10
    )
    foreach ($attempt in 1..$MaxAttempts) {
        $resetsBefore = Get-GateHoldResetCount $PackageName
        Invoke-PressAndHold (& $FindRing) $HoldMs
        try { return (Wait-Until { Find-GateHoldSum } $SumTimeoutSec 'gate sum not shown') } catch { Write-Verbose $_.Exception.Message }
        if ($attempt -eq $MaxAttempts) { break }
        $confirmed = if ((Get-GateHoldResetCount $PackageName) -gt $resetsBefore) { ' (window-trace.log shows "gate hold reset while holding")' } else { '' }
        Write-Host "gate hold retried: the VM delivered an early release (attempt $($attempt + 1))$confirmed"
    }
    throw "FAIL the gate sum did not appear after the hold ($MaxAttempts attempts)"
}

function Initialize-NativeDisplay {
    # Compiled on first use only, so checks that never change the resolution pay nothing; a compile error is thrown, not swallowed.
    if ('NativeDisplay' -as [type]) { return }
    Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class NativeDisplay {
  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
  public struct DEVMODE {
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
    public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
    public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
    public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
    public short dmLogPixels; public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
    public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
  }
  [DllImport("user32.dll", CharSet = CharSet.Ansi)] public static extern int EnumDisplaySettings(string dev, int mode, ref DEVMODE dm);
  [DllImport("user32.dll", CharSet = CharSet.Ansi)] public static extern int ChangeDisplaySettings(ref DEVMODE dm, int flags);
  public static int Set(int w, int h) {
    DEVMODE dm = new DEVMODE(); dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
    if (EnumDisplaySettings(null, -1, ref dm) == 0) return -100;
    dm.dmPelsWidth = w; dm.dmPelsHeight = h; dm.dmFields = 0x80000 | 0x100000;
    return ChangeDisplaySettings(ref dm, 0);
  }
}
'@
}

function Set-ScreenResolution {
    # Sets the primary display to $Width x $Height (the harness resets the guest to 1024x768); throws when Windows refuses it.
    param([int] $Width = 1920, [int] $Height = 1080)
    Initialize-NativeDisplay
    $code = [NativeDisplay]::Set($Width, $Height)
    if ($code -ne 0) { throw "Windows refused ${Width}x${Height} (ChangeDisplaySettings returned $code)." }
    Start-Sleep -Seconds 2
}

function Save-WindowScreenshot {
    # Saves only the given UIA window's rectangle as a PNG (the window must be fully on screen and not covered).
    param([Parameter(Mandatory)] $Window, [Parameter(Mandatory)] [string] $Path)
    Add-Type -AssemblyName System.Drawing
    $r = $Window.Current.BoundingRectangle
    if ($r.IsEmpty) { throw 'Save-WindowScreenshot: the window has no bounds.' }
    $bmp = New-Object System.Drawing.Bitmap ([int]$r.Width), ([int]$r.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, $bmp.Size)
        $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $g.Dispose(); $bmp.Dispose() }
}
