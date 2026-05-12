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

# ── 2. Create Start Menu shortcuts (enables Win+R → rsm / rsu) ────────────────
$startMenu = [Environment]::GetFolderPath("Programs")
$shell     = New-Object -ComObject WScript.Shell

foreach ($name in @("rsm", "rsu")) {
    $lnkPath = Join-Path $startMenu "$name.lnk"
    $target  = Join-Path $scriptDir "$name.vbs"

    $shortcut                  = $shell.CreateShortcut($lnkPath)
    $shortcut.TargetPath       = $target
    $shortcut.WorkingDirectory = $scriptDir
    $shortcut.Description      = if ($name -eq "rsm") { "Mount SFTP share via rclone" } else { "Unmount SFTP share" }
    $shortcut.Save()

    Write-Host "Shortcut created: $lnkPath"
}

Write-Host ""
Write-Host "Done. You can now run:"
Write-Host "  rsm.vbs <hostname>   (from any terminal or Win+R)"
Write-Host "  rsu.vbs <hostname>"
Write-Host ""
Write-Host "Restart open terminals for the PATH change to take effect."
