$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ── 1. Add this folder to the user PATH (persistent) ──────────────────────────
$userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
$entries  = $userPath -split ";" | Where-Object { $_ -ne "" }

if ($entries -notcontains $scriptDir) {
    $newPath = ($entries + $scriptDir) -join ";"
    [Environment]::SetEnvironmentVariable("PATH", $newPath, "User")
    Write-Host "Added to PATH: $scriptDir"
} else {
    Write-Host "Already in PATH: $scriptDir"
}

# Also apply to the current session
$env:PATH = [Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" +
            [Environment]::GetEnvironmentVariable("PATH", "User")

# ── 2. Ensure executables are built ──────────────────────────────────────────
foreach ($exe in @("rsm.exe", "rsu.exe", "ssh-askpass.exe")) {
    if (-not (Test-Path (Join-Path $scriptDir $exe))) {
        Write-Host "'$exe' not found — running build.ps1 first..."
        & (Join-Path $scriptDir "build.ps1")
        break
    }
}

# ── 3. Create shortcuts in this folder (enables Win+R → rsm / rsu via PATH) ───
$shell = New-Object -ComObject WScript.Shell

foreach ($name in @("rsm", "rsu")) {
    $lnkPath = Join-Path $scriptDir "$name.lnk"
    $target  = Join-Path $scriptDir "$name.exe"

    $shortcut                  = $shell.CreateShortcut($lnkPath)
    $shortcut.TargetPath       = $target
    $shortcut.WorkingDirectory = $scriptDir
    $shortcut.Description      = if ($name -eq "rsm") { "Mount rclone SFTP share" } else { "Unmount rclone SFTP share" }
    $shortcut.Save()

    Write-Host "Shortcut created: $lnkPath"
}

Write-Host ""
Write-Host "Done. You can now run (from any terminal or Win+R):"
Write-Host "  rsm <hostname>"
Write-Host "  rsu <hostname>"
Write-Host ""
Write-Host "Restart open terminals for the PATH change to take effect."
