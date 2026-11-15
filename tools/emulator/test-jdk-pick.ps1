<#
.SYNOPSIS
  Self-test for Slice 389: the emulator preflight keeps a valid JAVA_HOME, else picks the newest installed JDK for the process only, else reports none.
  Pure logic over a temp folder of fake JDKs, no device. Run in pwsh and powershell 5.1.
#>
$ErrorActionPreference = 'Stop'
if (-not $env:ANDROID_HOME -and -not $env:ANDROID_SDK_ROOT) { $env:ANDROID_HOME = [IO.Path]::GetTempPath() }
. (Join-Path $PSScriptRoot 'common.ps1')
$script:failed = 0
function Assert-Equal([string]$Name, $Expected, $Actual) {
    if ("$Expected" -ne "$Actual") { $script:failed++; Write-Host "FAIL: $Name expected [$Expected] got [$Actual]" }
}

$tmp = Join-Path ([IO.Path]::GetTempPath()) ("jdkpick-" + [guid]::NewGuid().ToString('N'))
function New-FakeJdk([string]$Root, [string]$Name) {
    $bin = Join-Path (Join-Path $Root $Name) 'bin'
    New-Item -ItemType Directory -Force -Path $bin | Out-Null
    Set-Content -LiteralPath (Join-Path $bin 'java.exe') -Value 'x'
}
try {
    $a = Join-Path $tmp 'a'; $b = Join-Path $tmp 'b'
    New-FakeJdk $a 'openjdk-17.0.2'
    New-FakeJdk $a 'openjdk-26.0.1'
    New-FakeJdk $b 'jdk-21.0.11.10-hotspot'
    New-FakeJdk $b 'jdk-11.0.2'
    New-Item -ItemType Directory -Force -Path (Join-Path $a 'not-a-jdk') | Out-Null
    $roots = @($a, $b, (Join-Path $tmp 'missing'))
    $dead = Join-Path $tmp 'removed-jdk'
    $leaves = @(Get-InstalledJdkHomes $roots | ForEach-Object { Split-Path -Leaf $_ })

    Assert-Equal 'order: newest in 17..21 first, then newer, below 17 ignored' 'jdk-21.0.11.10-hotspot,openjdk-17.0.2,openjdk-26.0.1' ($leaves -join ',')
    Assert-Equal 'dead JAVA_HOME: the newest 17..21 JDK is picked over a newer 26' 'jdk-21.0.11.10-hotspot' (Split-Path -Leaf (Select-JdkHome $dead $roots))
    Assert-Equal 'unset JAVA_HOME: same pick' 'jdk-21.0.11.10-hotspot' (Split-Path -Leaf (Select-JdkHome '' $roots))
    $only = Join-Path $tmp 'c'
    New-FakeJdk $only 'openjdk-26.0.1'; New-FakeJdk $only 'jdk-8u300'
    Assert-Equal 'none in 17..21: falls back to the newest other >= 17' 'openjdk-26.0.1' (Split-Path -Leaf (Select-JdkHome $dead @($only)))
    $old = Join-Path $tmp 'd'
    New-FakeJdk $old 'jdk-11.0.2'
    Assert-Equal 'only a JDK below 17: none' $true ($null -eq (Select-JdkHome $dead @($old)))
    $good = Join-Path $b 'jdk-21.0.11.10-hotspot'
    Assert-Equal 'valid JAVA_HOME wins over a newer one' $good (Select-JdkHome $good $roots)
    Assert-Equal 'no JDK anywhere: null' $true ($null -eq (Select-JdkHome $dead @((Join-Path $tmp 'missing'))))

    $saved = $env:JAVA_HOME
    try {
        $env:JAVA_HOME = $good
        Assert-Equal 'Set-ProcessJavaHome keeps a valid JAVA_HOME' $true ([bool](Set-ProcessJavaHome 6>$null))
        Assert-Equal 'JAVA_HOME unchanged' $good $env:JAVA_HOME
        $env:JAVA_HOME = $dead
        Assert-Equal 'dead JAVA_HOME replaced' $true ([bool](Set-ProcessJavaHome $roots $false 6>$null))
        Assert-Equal 'dead JAVA_HOME now the 21' 'jdk-21.0.11.10-hotspot' (Split-Path -Leaf $env:JAVA_HOME)
        $env:JAVA_HOME = $dead
        Assert-Equal 'nothing found, no java on PATH: false' $false ([bool](Set-ProcessJavaHome @((Join-Path $tmp 'missing')) $false 6>$null))
        Assert-Equal 'JAVA_HOME left alone when nothing found' $dead $env:JAVA_HOME
        Assert-Equal 'nothing found but java on PATH: true' $true ([bool](Set-ProcessJavaHome @((Join-Path $tmp 'missing')) $true 6>$null))
        Assert-Equal 'dead JAVA_HOME removed so the launcher uses PATH java' '' "$($env:JAVA_HOME)"
        $env:JAVA_HOME = "`"$good `""
        Assert-Equal 'quoted and padded JAVA_HOME is accepted and cleaned' $good (Select-JdkHome $env:JAVA_HOME $roots)
        $env:JAVA_HOME = $null
        Assert-Equal 'unset, no java on PATH, nothing installed: false' $false ([bool](Set-ProcessJavaHome @() $false 6>$null))
        Assert-Equal 'search roots include ~/.jdks' $true (@(Get-JdkSearchRoots) -contains (Join-Path $HOME '.jdks'))
        $roots2 = @(Get-JdkSearchRoots)
        foreach ($v in @('Java', 'Amazon Corretto', 'Zulu')) {
            Assert-Equal "search roots include Program Files\$v" $true (@($roots2 | Where-Object { (Split-Path -Leaf $_) -eq $v }).Count -ge 1)
        }
        $pfJava = Join-Path $tmp 'pf\Java'
        New-FakeJdk $pfJava 'jdk-21'
        $env:JAVA_HOME = $dead
        Assert-Equal 'only JDK under a Java root is found' $true ([bool](Set-ProcessJavaHome @($pfJava) $false 6>$null))
        Assert-Equal 'and chosen' 'jdk-21' (Split-Path -Leaf $env:JAVA_HOME)
        Assert-Equal 'java -version "21.0.2" is 21' 21 (Get-JavaMajorFromVersionText 'openjdk version "21.0.2" 2026-01-20')
        Assert-Equal 'java -version "1.8.0_292" is 8' 8 (Get-JavaMajorFromVersionText 'java version "1.8.0_292"')
        Assert-Equal 'java -version "17" is 17' 17 (Get-JavaMajorFromVersionText 'openjdk version "17" 2021-09-14')
        Assert-Equal 'unreadable java -version is 0' 0 (Get-JavaMajorFromVersionText 'garbage')
    } finally { $env:JAVA_HOME = $saved }

    # Versions come from the release file, then the folder name; an odd name never throws.
    $r = Join-Path $tmp 'r'
    New-FakeJdk $r 'x64-jdk-17'; New-FakeJdk $r 'build-20261114123456789012345'; New-FakeJdk $r 'studio-jbr'; New-FakeJdk $r 'jdk-21'
    Set-Content -LiteralPath (Join-Path (Join-Path $r 'studio-jbr') 'release') -Value 'JAVA_VERSION="11.0.9"'
    Set-Content -LiteralPath (Join-Path (Join-Path $r 'jdk-21') 'release') -Value 'JAVA_VERSION="1.8.0_292"'
    Assert-Equal 'release file wins over the folder name (1.8 is 8)' 8 (Get-JdkInfo (Join-Path $r 'jdk-21') 'jdk-21').Major
    Assert-Equal 'jbr 11 from its release file' 11 (Get-JdkInfo (Join-Path $r 'studio-jbr') 'studio-jbr').Major
    Assert-Equal 'x64-jdk-17 is 17, not 64' 17 (Get-JdkInfo (Join-Path $r 'x64-jdk-17') 'x64-jdk-17').Major
    Assert-Equal 'a long digit run does not throw and is ignored' 0 (Get-JdkInfo (Join-Path $r 'build-20261114123456789012345') 'build-20261114123456789012345').Major
    Assert-Equal 'only the usable one is listed' 'x64-jdk-17' (@(Get-InstalledJdkHomes @($r) | ForEach-Object { Split-Path -Leaf $_ }) -join ',')
    $env:JAVA_HOME = Join-Path $r 'studio-jbr'
    try { Assert-Equal 'JAVA_HOME at JDK 11 is not kept' (Join-Path $r 'x64-jdk-17') (Select-JdkHome $env:JAVA_HOME @($r)) } finally { $env:JAVA_HOME = $saved }
} finally {
    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
if ($script:failed) { Write-Host "test-jdk-pick: $($script:failed) check(s) failed."; exit 1 }
Write-Host 'test-jdk-pick: all checks passed.'
exit 0
