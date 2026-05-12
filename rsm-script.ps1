param([string]$HostName)

if (-not $HostName) {
    [System.Windows.Forms.MessageBox]::Show("Usage: rsm <hostname>", "rsm") | Out-Null
    exit 1
} else {
    # Check if the hostname is pre-configured in .ssh/config right now!
    $sshConfigPath = Join-Path $env:USERPROFILE ".ssh\config"
    if (-not (Test-Path $sshConfigPath)) {
        [System.Windows.Forms.MessageBox]::Show("SSH config file not found at '$sshConfigPath'. Please create it and add a Host entry for '$HostName'.", "rsm") | Out-Null
        exit 1
    }
    $sshConfigContent = Get-Content $sshConfigPath -Raw
    if ($sshConfigContent -notmatch "Host\s+$HostName") {
        [System.Windows.Forms.MessageBox]::Show("Host '$HostName' not found in SSH config file at '$sshConfigPath'. Please add a Host entry for '$HostName'.", "rsm") | Out-Null
        exit 1
    }
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$mountPath  = "\\sftp\$HostName"
$pidsDir    = Join-Path $scriptDir "pids"
$pidFile    = Join-Path $pidsDir "$HostName.pid"
$cacheDir   = Join-Path $scriptDir "cache\$HostName"
$logFile    = Join-Path $scriptDir "logs\$HostName.log"

# Create required directories
foreach ($d in @($pidsDir, (Join-Path $scriptDir "logs"), $cacheDir)) {
    if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d | Out-Null }
}

# Empty logfile for this session
New-Item -Path $logFile -ItemType File -Force | Out-Null

# Route any SSH passphrase/host-key prompts through a GUI dialog instead of the console
$env:SSH_ASKPASS         = Join-Path $scriptDir "ssh-askpass.cmd"
$env:SSH_ASKPASS_REQUIRE = "force"

# Start rclone fully hidden in the background
$rclone = Join-Path $scriptDir "rclone.exe"
$proc = Start-Process -FilePath $rclone `
    -ArgumentList "mount `":sftp,ssh='ssh $HostName',shell_type=none,idle_timeout=0:`" `"\\sftp\$HostName`" --vfs-cache-mode full --log-level INFO --cache-dir `"$cacheDir`" --log-file `"$logFile`" --config NUL" `
    -WindowStyle Hidden -PassThru

if (-not $proc) { exit 1 }

# Persist the PID so rsu can stop it later
$proc.Id | Out-File -FilePath $pidFile -Encoding ascii -NoNewline

# Wait for the mount to become available; bail if rclone exits early (wrong passphrase, etc.)
while (-not ($objSelected = Get-PSDrive -ErrorAction SilentlyContinue | Where-Object { $_.DisplayRoot -eq $mountPath })) {
    if ($proc.HasExited) {
        Remove-Item $pidFile -Force -ErrorAction SilentlyContinue
        exit 1
    }
    Start-Sleep -Milliseconds 200
}

# Open explorer at 'objSelected.Root'
Start-Process explorer.exe $objSelected.Root