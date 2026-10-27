# Acceptance run inside Windows Sandbox: a clean Windows the program has
# never seen. Started by the sandbox's LogonCommand (see start.ps1); writes
# progress.log, screenshots and the program's log to C:\host\results and
# DONE.txt at the end.
#
# Keep this file ASCII: Windows PowerShell 5.1 reads a file without a BOM in
# the ANSI code page, and any Cyrillic here would arrive as garbage.
$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'

# Our own start.ps1 maps the host folder as C:\host; a shared harness that
# maps input as C:\in and output as C:\out (and dot-sources this file) works too.
$In = if (Test-Path 'C:\in') { 'C:\in' } else { 'C:\host' }
$R = if (Test-Path 'C:\out') { 'C:\out' } else { 'C:\host\results' }
$global:Failures = 0
New-Item -ItemType Directory -Force $R | Out-Null
$progress = Join-Path $R 'progress.log'
function Log([string]$m) { Add-Content $progress ("{0:HH:mm:ss} {1}" -f (Get-Date), $m) }
function Step([string]$m) { Log ''; Log ('== ' + $m) }
function Verdict([string]$name, [bool]$ok) {
    if (-not $ok) { $global:Failures++ }
    Log ($(if ($ok) { 'PASS ' } else { 'FAIL ' }) + $name)
}

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
function Shot([string]$name) {
    try {
        $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
        $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
        $bmp.Save((Join-Path $R "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
        $g.Dispose(); $bmp.Dispose(); Log "shot $name"
    } catch { Log "shot $name FAILED: $_" }
}

# Simulated keyboard input, so the program sees someone working.
# F15 exists in Windows but no program reacts to it.
function Poke { try { [System.Windows.Forms.SendKeys]::SendWait('{F15}') } catch { } }

$data = Join-Path $env:APPDATA 'Augensicher'
$applog = Join-Path $data 'augensicher.log'
$settings = Join-Path $data 'settings.ini'

function AppCount { (Get-Process Augensicher -ErrorAction SilentlyContinue | Measure-Object).Count }
function StopApp { Get-Process Augensicher -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep 2 }
function LogHas([string]$pattern) { (Test-Path $applog) -and (Select-String -Path $applog -Pattern $pattern -Quiet) }
function LogCount([string]$pattern) { if (Test-Path $applog) { @(Select-String -Path $applog -Pattern $pattern).Count } else { 0 } }

# Waits for the N-th occurrence of a log line, poking the keyboard meanwhile.
function WaitLog([string]$pattern, [int]$count, [int]$seconds, [bool]$poke) {
    $end = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $end) {
        if ((LogCount $pattern) -ge $count) { return $true }
        if ($poke) { Poke }
        Start-Sleep -Seconds 3
    }
    return $false
}

function WriteSettings([string]$text) {
    New-Item -ItemType Directory -Force $data | Out-Null
    Set-Content -Path $settings -Value $text -Encoding ASCII
}

# ---------------------------------------------------------------- setup
Step 'setup'
$k = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
Log ("Windows: {0} {1} build {2}.{3}" -f $k.ProductName, $k.DisplayVersion, $k.CurrentBuild, $k.UBR)
Log ("VBScript engine present: " + (Test-Path "$env:WINDIR\System32\vbscript.dll"))
Log ("Screen: " + [System.Windows.Forms.Screen]::PrimaryScreen.Bounds)

# Unpack into a folder with Cyrillic letters and a space, as a Russian user
# would: "Папка с пробелом", built from character codes to keep this file ASCII.
$cyr = -join [char[]]@(0x41F,0x430,0x43F,0x43A,0x430,0x20,0x441,0x20,0x43F,0x440,0x43E,0x431,0x435,0x43B,0x43E,0x43C)
$base = Join-Path $env:USERPROFILE $cyr
$zip = Get-ChildItem (Join-Path $In 'Augensicher-*-windows.zip') | Select-Object -First 1
Expand-Archive $zip.FullName $base -Force
$exe = Join-Path $base 'Augensicher\Augensicher.exe'
Verdict "unpacked to $exe" (Test-Path $exe)

# ---------------------------------------------------------------- A
Step 'A: first start with default settings'
Start-Process $exe
Verdict 'starts and writes its log' (WaitLog 'started' 1 30 $false)
Start-Sleep 3
Shot 'A1-started'
Verdict 'one copy running' ((AppCount) -eq 1)

$p = Start-Process $exe -PassThru
$exited = $p.WaitForExit(15000)
Start-Sleep 2
Shot 'A2-second-start'
Verdict 'a second start exits at once' $exited
Verdict 'still one copy running' ((AppCount) -eq 1)

$p = Start-Process $exe -ArgumentList '--autostart', 'on' -PassThru
$p.WaitForExit(15000) | Out-Null
$lnk = Join-Path ([Environment]::GetFolderPath('Startup')) 'Augensicher.lnk'
Verdict 'autostart shortcut created' (Test-Path $lnk)
StopApp
Start-Process $lnk
Start-Sleep 8
Verdict 'the autostart shortcut starts the program' ((AppCount) -eq 1)
$p = Start-Process $exe -ArgumentList '--autostart', 'off' -PassThru
$p.WaitForExit(15000) | Out-Null
Verdict 'autostart off removes the shortcut' (-not (Test-Path $lnk))
StopApp

# ---------------------------------------------------------------- B
Step 'B: a break comes after 1 minute of work, ends by itself after 1 minute'
WriteSettings "work_minutes = 1`r`nbreak_minutes = 1`r`nwarn_seconds = 30`r`nlanguage = en`r`n"
Start-Process $exe
WaitLog 'started' 2 30 $false | Out-Null
$warnShot = $false
$end = (Get-Date).AddSeconds(120)
while ((Get-Date) -lt $end -and (LogCount 'break start') -lt 1) {
    Poke
    Start-Sleep 3
    if (-not $warnShot -and (Get-Date) -gt $end.AddSeconds(-85)) { Shot 'B1-warning'; $warnShot = $true }
}
Verdict 'break started after 1 minute of simulated work' ((LogCount 'break start') -ge 1)
Start-Sleep 3
Shot 'B2-break-en'
Verdict 'break ended by itself' (WaitLog 'break end: finished' 1 90 $false)
Start-Sleep 2
Shot 'B3-after-break'
StopApp

# ---------------------------------------------------------------- B2
Step 'B2: break while typing in another window; Esc must reach the break'
WriteSettings "work_minutes = 1`r`nbreak_minutes = 1`r`nwarn_seconds = 0`r`nlanguage = en`r`n"
Start-Process $exe
Start-Sleep 5
# Notepad is not always there (24H2 ships it as a Store app), so the test
# opens its own window with a text box and keeps typing into it.
$pad = Join-Path $R 'typing-window.ps1'
Set-Content $pad -Encoding ASCII -Value @'
Add-Type -AssemblyName System.Windows.Forms
$f = New-Object System.Windows.Forms.Form
$f.Text = 'Typing window'
$f.Width = 600; $f.Height = 400
$t = New-Object System.Windows.Forms.TextBox
$t.Multiline = $true; $t.Dock = 'Fill'
$f.Controls.Add($t)
$f.Add_Shown({ $f.Activate() })
[System.Windows.Forms.Application]::Run($f)
'@
$np = Start-Process powershell -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $pad -PassThru
Start-Sleep 3
Verdict 'break started while the typing window had the focus' (WaitLog 'break start' 2 120 $true)
Start-Sleep 2
Shot 'B4-break-over-typing'
[System.Windows.Forms.SendKeys]::SendWait('{ESC}')
Verdict 'Esc skips a break that started on its own' (WaitLog 'break end: skipped' 1 10 $false)
$np | Stop-Process -Force -ErrorAction SilentlyContinue
StopApp

# ---------------------------------------------------------------- C
Step 'C: Russian interface, a break from the command line, Esc skips it'
WriteSettings "language = ru`r`n"
Start-Process $exe -ArgumentList '--break-now'
Verdict 'break started' (WaitLog 'break start' 3 30 $false)
Start-Sleep 3
Shot 'C1-break-ru'
[System.Windows.Forms.SendKeys]::SendWait('{ESC}')
Verdict 'Esc skips the break (the break window has the focus)' (WaitLog 'break end: skipped' 2 10 $false)
Start-Sleep 2
StopApp

# ---------------------------------------------------------------- D
Step 'D: short break after 5 minutes of work'
WriteSettings "work_minutes = 30`r`nmini_breaks = true`r`nmini_every_minutes = 5`r`nmini_seconds = 15`r`nlanguage = en`r`n"
Start-Process $exe
WaitLog 'started' 4 30 $false | Out-Null
$ok = WaitLog 'short break' 1 360 $true
Verdict 'short break shown' $ok
if ($ok) { Start-Sleep 2; Shot 'D1-short-break' }
Start-Sleep 20
Shot 'D2-after-short-break'
Verdict 'no long break during the short one' ((LogCount 'break start') -eq 3)
StopApp

# ---------------------------------------------------------------- E
Step 'E: windows drawn on a clean system'
$p = Start-Process $exe -ArgumentList '--screenshots', "$R\render" -PassThru
Verdict 'windows drawn' ($p.WaitForExit(30000) -and $p.ExitCode -eq 0)

# ---------------------------------------------------------------- F
Step 'F: the log'
Copy-Item $applog $R -ErrorAction SilentlyContinue
Verdict 'no errors in the log' ((LogCount 'ERROR|FATAL') -eq 0)

if ($global:Failures -gt 0) { $global:ScenarioExit = 1 }
if ($In -eq 'C:\host') {
    Log ''
    Log 'DONE'
    Set-Content (Join-Path $R 'DONE.txt') 'done'
}
