# Helpers for the check scripts. They run inside the VM, in the signed-in user's desktop session, under Windows
# PowerShell 5.1. Each check dot-sources this file and ends with Invoke-Check. (Slice 163)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, WindowsBase
Add-Type -AssemblyName System.Windows.Forms

$script:AutomationElement = [System.Windows.Automation.AutomationElement]
$script:TreeScope = [System.Windows.Automation.TreeScope]

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
    # Finds a descendant by (partial, case-insensitive) Name; optional control type name such as 'Button'.
    param($Root = $script:AutomationElement::RootElement, [Parameter(Mandatory)] [string] $Name, [string] $ControlType)
    $all = $Root.FindAll($script:TreeScope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($e in $all) {
        $n = $e.Current.Name
        if ($n -and $n.IndexOf($Name, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            if (-not $ControlType -or $e.Current.ControlType.ProgrammaticName -eq "ControlType.$ControlType") { return $e }
        }
    }
    return $null
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
    Add-Type -Namespace Win32 -Name Mouse -MemberDefinition '[DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);' -ErrorAction SilentlyContinue
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
    Add-Type -Namespace Win32 -Name MouseClick -MemberDefinition '[DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);' -ErrorAction SilentlyContinue
    [Win32.MouseClick]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    [Win32.MouseClick]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
}

function Find-UiaById {
    param([Parameter(Mandatory)] $Root, [Parameter(Mandatory)] [string] $Id)
    return $Root.FindFirst($script:TreeScope::Descendants, (New-Object System.Windows.Automation.PropertyCondition($script:AutomationElement::AutomationIdProperty, $Id)))
}
