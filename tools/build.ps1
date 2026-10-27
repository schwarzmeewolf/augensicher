<#
.SYNOPSIS
    Build Augensicher.exe, run the tests, and pack the release archive.

.DESCRIPTION
    Uses the C# compiler that ships with Windows itself (.NET Framework 4.x),
    so building needs nothing installed: no Visual Studio, no SDK.

    Output:
      dist\Augensicher\                          the exe and the documents
      dist\Augensicher-<version>-windows.zip     the archive for the release page

    The tests are compiled into build\tests.exe and run first; a failing test
    stops the build.

.PARAMETER Screenshots
    Also redraw the README screenshots into assets\screenshots.
#>
param(
    [switch]$Screenshots
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$Csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $Csc)) { $Csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $Csc)) { throw "C# compiler not found: .NET Framework 4.x is missing" }

$version = (Select-String -Path (Join-Path $Root 'src\AppInfo.cs') -Pattern 'Version = "([^"]+)"').Matches[0].Groups[1].Value
Write-Host "Building Augensicher $version"

$Build = Join-Path $Root 'build'
$Dist = Join-Path $Root 'dist'
$Out = Join-Path $Dist 'Augensicher'
$Icon = Join-Path $Root 'assets\augensicher.ico'
New-Item -ItemType Directory -Force $Build, $Out | Out-Null

$sources = Get-ChildItem (Join-Path $Root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
$common = @('/nologo', '/codepage:65001', '/optimize+', '/warnaserror+', '/platform:anycpu',
            '/reference:System.dll', '/reference:System.Core.dll',
            '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll',
            "/resource:$Icon,augensicher.ico")

function Compile([string[]]$extra, [string[]]$files) {
    & $Csc @common @extra @files
    if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }
}

# 1. Tests
$tests = Get-ChildItem (Join-Path $Root 'tests') -Filter *.cs | ForEach-Object { $_.FullName }
$testExe = Join-Path $Build 'tests.exe'
Compile @('/target:exe', "/out:$testExe", '/main:Augensicher.Tests.TestMain') ($sources + $tests)
& $testExe
if ($LASTEXITCODE -ne 0) { throw "tests failed" }

# 2. The program
$exe = Join-Path $Out 'Augensicher.exe'
Compile @('/target:winexe', "/out:$exe", "/win32icon:$Icon") $sources
Write-Host "Built $exe"

# 3. Documents next to the exe. LICENSE gets .txt so a double-click opens it.
Copy-Item (Join-Path $Root 'LICENSE') (Join-Path $Out 'LICENSE.txt') -Force
foreach ($f in 'README.md', 'README.ru.md', 'DISCLAIMER.md', 'CHANGELOG.md') {
    Copy-Item (Join-Path $Root $f) $Out -Force
}

# 4. Archive
$zip = Join-Path $Dist "Augensicher-$version-windows.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $Out -DestinationPath $zip
Write-Host "Packed $zip"

if ($Screenshots) {
    $shots = Join-Path $Root 'assets\screenshots'
    $p = Start-Process -FilePath $exe -ArgumentList '--screenshots', "`"$shots`"" -Wait -PassThru
    if ($p.ExitCode -ne 0) { throw "screenshots failed" }
    Write-Host "Screenshots in $shots"
}
