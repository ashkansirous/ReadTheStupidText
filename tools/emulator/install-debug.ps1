<#
.SYNOPSIS
  Builds the x86_64 Debug APK of RTST or Dino Reader, installs it on the warm emulator and launches it.
#>
param(
    [Parameter(Mandatory)][ValidateSet('rtst', 'dino')][string]$App
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$apps = @{
    rtst = @{ Project = 'src\ReadTheStupidText.Mobile\ReadTheStupidText.Mobile.csproj'; Package = 'uk.sirous.readthestupidtext' }
    dino = @{ Project = 'src\DinoReader.App\DinoReader.App.csproj'; Package = 'uk.sirous.dinoreader' }
}
$cfg = $apps[$App]
$project = Join-Path $script:RepoRoot $cfg.Project

& (Join-Path $PSScriptRoot 'start-warm.ps1')

$sw = [Diagnostics.Stopwatch]::StartNew()
$apkDir = Join-Path (Split-Path $project) 'bin\Debug\net10.0-android\android-x64'
# Remove old signed APKs first, so the one found afterwards is certainly from this build.
Get-ChildItem $apkDir -Filter '*-Signed.apk' -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force
dotnet build $project -f net10.0-android -c Debug -p:RuntimeIdentifier=android-x64 `
    -p:EmbedAssembliesIntoApk=true -p:IncludeWindows=false -p:AndroidPackageFormat=apk
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$apk = Get-ChildItem $apkDir -Filter '*-Signed.apk' -Recurse |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $apk) { throw 'Signed APK not found.' }
Write-Host ("Built in {0:n1} s: {1}" -f $sw.Elapsed.TotalSeconds, $apk.FullName)

& $script:Adb -s $script:Serial install -r $apk.FullName
if ($LASTEXITCODE -ne 0) { throw 'adb install failed.' }
$ErrorActionPreference = 'Continue'  # monkey writes its banner to stderr
& $script:Adb -s $script:Serial shell monkey -p $cfg.Package -c android.intent.category.LAUNCHER 1 2>&1 | Out-Null
$abi = & $script:Adb -s $script:Serial shell getprop ro.product.cpu.abi
Write-Host "Launched $($cfg.Package) on $($script:Serial) ($abi)."
