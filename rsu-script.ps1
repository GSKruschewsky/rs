param([string]$HostName)

if (-not $HostName) { exit 1 }

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class ConsoleSignal {
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FreeConsole();
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool AttachConsole(uint dwProcessId);
    [DllImport("kernel32.dll")]
    public static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
    [DllImport("kernel32.dll")]
    public static extern bool SetConsoleCtrlHandler(IntPtr handler, bool add);
}
"@

function Send-CtrlC([int]$TargetPid) {
    # Detach from our own console, attach to rclone's console
    [ConsoleSignal]::FreeConsole() | Out-Null
    [ConsoleSignal]::AttachConsole([uint32]$TargetPid) | Out-Null
    # Suppress Ctrl+C for this process so only rclone gets it
    [ConsoleSignal]::SetConsoleCtrlHandler([IntPtr]::Zero, $true) | Out-Null
    [ConsoleSignal]::GenerateConsoleCtrlEvent(0, 0) | Out-Null
    Start-Sleep -Milliseconds 200
    # Restore: detach from rclone's console, re-enable Ctrl+C for ourselves
    [ConsoleSignal]::FreeConsole() | Out-Null
    [ConsoleSignal]::SetConsoleCtrlHandler([IntPtr]::Zero, $false) | Out-Null
}

function Stop-Rclone([int]$TargetPid) {
    $proc = Get-Process -Id $TargetPid -ErrorAction SilentlyContinue
    if (-not $proc) { return }

    # Write-Host "Sending Ctrl+C to rclone (PID: $TargetPid)..."
    Send-CtrlC $TargetPid

    # Wait up to 10 seconds for graceful exit
    $deadline = (Get-Date).AddSeconds(10)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
        if (-not (Get-Process -Id $TargetPid -ErrorAction SilentlyContinue)) { return }
    }

    Stop-Process -Id $TargetPid -Force -ErrorAction SilentlyContinue
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$pidsDir   = Join-Path $scriptDir "pids"
$pidFile   = Join-Path $pidsDir "$HostName.pid"
$driveFile = Join-Path $pidsDir "$HostName.drive"

# Remove the drive letter mapping first so Explorer releases the handle
if (Test-Path $driveFile) {
    $driveLetter = (Get-Content $driveFile -Raw).Trim()
    net use $driveLetter /delete /yes 2>&1 | Out-Null
    Remove-Item $driveFile -Force -ErrorAction SilentlyContinue
}

if (Test-Path $pidFile) {
    $targetPid = [int](Get-Content $pidFile -Raw).Trim()
    Stop-Rclone $targetPid
    Remove-Item $pidFile -Force -ErrorAction SilentlyContinue
} else {
    $procInfo = Get-CimInstance -ClassName Win32_Process |
        Where-Object { $_.Name -eq 'rclone.exe' -and $_.CommandLine -like "*$HostName*" } |
        Select-Object -First 1

    if ($procInfo) {
        Stop-Rclone $procInfo.ProcessId
    }
}
