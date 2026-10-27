<#
.SYNOPSIS
    Run the acceptance test in Windows Sandbox and wait for the result.

.DESCRIPTION
    Copies the release archive and run-test.ps1 into a host folder, starts a
    sandbox that runs the test at logon, waits for DONE.txt and closes the
    sandbox. Results: <HostFolder>\results\progress.log (PASS/FAIL lines),
    screenshots and the program's log.

    Needs the Windows Sandbox feature. Run tools\build.ps1 first.

    The sandbox is closed through its window, not by killing the service:
    killing it leaves a "logging off the remote session" window behind.
#>
param(
    [string]$HostFolder = 'C:\augensicher-sandbox',
    [int]$TimeoutMinutes = 30
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$zip = Get-ChildItem (Join-Path $Root 'dist') -Filter 'Augensicher-*-windows.zip' | Sort-Object LastWriteTime | Select-Object -Last 1
if (-not $zip) { throw 'No archive in dist: run tools\build.ps1 first' }

if (Get-Process WindowsSandbox* -ErrorAction SilentlyContinue) { throw 'A sandbox is already running: only one at a time' }

# On a machine where several projects share the sandbox, a lock file says
# who has it. Younger than 4 hours = busy; older = left over, taken over.
$Lock = 'C:\dev\_shared\sandbox.lock'
if ((Test-Path $Lock) -and ((Get-Date) - (Get-Item $Lock).LastWriteTime).TotalHours -lt 4) {
    throw "The sandbox is busy: $(Get-Content $Lock -Raw)"
}
if (Test-Path (Split-Path $Lock)) {
    Set-Content $Lock -Encoding UTF8 -Value "$Root (Augensicher acceptance run) since $((Get-Date).ToString('yyyy-MM-dd HH:mm'))"
}

New-Item -ItemType Directory -Force $HostFolder | Out-Null
Remove-Item (Join-Path $HostFolder 'results') -Recurse -Force -ErrorAction SilentlyContinue
Get-ChildItem $HostFolder -Filter 'Augensicher-*.zip' | Remove-Item -Force
Copy-Item $zip.FullName $HostFolder
Copy-Item (Join-Path $PSScriptRoot 'run-test.ps1') $HostFolder -Force

$wsb = Join-Path $HostFolder 'test.wsb'
@"
<Configuration>
  <MemoryInMB>4096</MemoryInMB>
  <Networking>Disable</Networking>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$HostFolder</HostFolder>
      <SandboxFolder>C:\host</SandboxFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>powershell -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File C:\host\run-test.ps1</Command>
  </LogonCommand>
</Configuration>
"@ | Set-Content $wsb -Encoding UTF8

# The previous sandbox's virtual machine lives on for a minute or two after
# its window closes; a new sandbox started meanwhile never comes up.
$wait = (Get-Date).AddMinutes(5)
while ((Get-Process vmmemWindowsSandbox -ErrorAction SilentlyContinue) -and (Get-Date) -lt $wait) { Start-Sleep -Seconds 5 }

Write-Host "Testing $($zip.Name) in Windows Sandbox..."
Start-Process $wsb
$up = (Get-Date).AddMinutes(2)
while (-not (Get-Process WindowsSandbox* -ErrorAction SilentlyContinue) -and (Get-Date) -lt $up) { Start-Sleep -Seconds 3 }
if (-not (Get-Process WindowsSandbox* -ErrorAction SilentlyContinue)) { throw 'Windows Sandbox did not start' }
$done = Join-Path $HostFolder 'results\DONE.txt'
$end = (Get-Date).AddMinutes($TimeoutMinutes)
while ((Get-Date) -lt $end -and -not (Test-Path $done)) { Start-Sleep -Seconds 15 }
$finished = Test-Path $done

Start-Sleep -Seconds 3
# Closing asks "really close?"; press its OK through UI Automation.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$okNames = @('OK', ([string][char]0x41E + [char]0x41A))   # OK, and the Russian "ОК"
$mine = Get-CimInstance Win32_Process -Filter "Name='WindowsSandboxRemoteSession.exe'" | Where-Object { $_.CommandLine -like "*$wsb*" }
foreach ($m in $mine) {
    $c = Get-Process -Id $m.ProcessId -ErrorAction SilentlyContinue
    if (-not $c -or $c.MainWindowHandle -eq [IntPtr]::Zero) { continue }
    $c.CloseMainWindow() | Out-Null
    Start-Sleep -Seconds 3
    $w = [System.Windows.Automation.AutomationElement]::FromHandle($c.MainWindowHandle)
    $ok = $w.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition) |
        Where-Object { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.Button' -and $okNames -contains $_.Current.Name } |
        Select-Object -First 1
    if ($ok) { $ok.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
}
$gone = $false
for ($i = 0; $i -lt 36 -and -not $gone; $i++) {
    Start-Sleep -Seconds 5
    $gone = -not (Get-Process vmmemWindowsSandbox, WindowsSandboxRemoteSession -ErrorAction SilentlyContinue)
}
# A sandbox left open still holds the machine: keep the lock then.
if ($gone) { Remove-Item $Lock -Force -ErrorAction SilentlyContinue } else { Write-Host 'The sandbox did not close: close it by hand' }

if (-not $finished) { Write-Host "TIMEOUT after $TimeoutMinutes min" }
Get-Content (Join-Path $HostFolder 'results\progress.log') -ErrorAction SilentlyContinue
if ($finished -and -not (Select-String (Join-Path $HostFolder 'results\progress.log') -Pattern '^\S+ FAIL' -Quiet)) { exit 0 } else { exit 1 }
