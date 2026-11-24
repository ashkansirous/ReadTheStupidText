# Slice 433 (review pass 2, finding 4): the store frame sizes, shared by store-captions.ps1 (cuts the frame) and store-screenshots.ps1 (sizes the emulator from the same numbers).
# No param block and no side effect but its own names, so dot-sourcing it cannot reset a caller's variable.

# The frames: output size, caption band (fraction of the output height; 0 = no caption), the caption's start font size (fraction of the band height), the default status bar and navigation bar heights of a raw shot (px), and how much of the raw shot's height
# may be cropped at the bottom beyond the navigation bar (the home indicator zone on an iPhone; an Android shot is made to fit exactly, so 1%).
$script:FrameSpecs = @{
    'phone'      = @{ Width = 1080; Height = 1920; Band = 0.15; Start = 0.28; StatusPx = 63; NavPx = 0; MaxBottom = 0.01; Target = 'play-phone' }
    'tablet'     = @{ Width = 1440; Height = 2560; Band = 0.0;  Start = 0.28; StatusPx = 63; NavPx = 0; MaxBottom = 0.01; Target = 'play-tablet' }
    'iphone-6.9' = @{ Width = 1320; Height = 2868; Band = 0.10; Start = 0.28; StatusPx = 186; NavPx = 0; MaxBottom = 0.06; Target = 'appstore-iphone-6.9' }
    # iPad: the raw shot is 2752 x 2064, so the band height is the crop; 5% (103 px) cuts 48 px of status bar and 55 px at the bottom, a little over the 40 px home-indicator zone, and MaxBottom (3%, 62 px) stops anything bigger.
    'ipad-13'    = @{ Width = 2752; Height = 2064; Band = 0.05; Start = 0.5; StatusPx = 48; NavPx = 0; MaxBottom = 0.03; Target = 'appstore-ipad-13' }
}

# Pure: the frame spec of a form (throws on an unknown form).
function Get-FrameSpec([string]$Form) {
    if (-not $script:FrameSpecs.ContainsKey($Form)) { throw "No frame for form '$Form'." }
    return $script:FrameSpecs[$Form]
}

# Pure: the band height in pixels for an image height and a band fraction.
function Get-BandHeight([int]$ImageHeight, [double]$Fraction) { return [int][math]::Round($ImageHeight * $Fraction) }
