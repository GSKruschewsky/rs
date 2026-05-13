# build.ps1 — Compile all three C# executables and place them at the repo root.
#
# Prefers 'dotnet build' when the .NET SDK is installed (uses Rmount.sln).
# Falls back to the csc.exe that ships with .NET Framework on every Windows 10/11
# machine — no extra installs required.
#
# Usage:  .\build.ps1              (default)
#         .\build.ps1 -Release     (optimised, smaller — only used with dotnet path)

param([switch]$Release)

$root = Split-Path -Parent $MyInvocation.MyCommand.Path

# ── Choose compiler ───────────────────────────────────────────────────────────
$dotnetExe  = Get-Command dotnet -ErrorAction SilentlyContinue
$hasSdk     = $dotnetExe -and (& dotnet --list-sdks 2>$null | Where-Object { $_ -match '\d' })
$csc        = Get-ChildItem "C:\Windows\Microsoft.NET\Framework64" -Filter csc.exe `
                  -Recurse -ErrorAction SilentlyContinue |
              Sort-Object FullName | Select-Object -Last 1

if ($hasSdk) {
    # ── dotnet SDK path ───────────────────────────────────────────────────────
    $config = if ($Release) { "Release" } else { "Debug" }
    Write-Host "Building with dotnet ($config)..."
    dotnet build (Join-Path $root "Rmount.sln") --configuration $config --nologo
    if ($LASTEXITCODE -ne 0) { Write-Host "Build failed." -ForegroundColor Red; exit 1 }

} elseif ($csc) {
    # ── csc.exe (built-in .NET Framework) fallback ────────────────────────────
    Write-Host "dotnet SDK not found — using csc.exe: $($csc.FullName)"
    Write-Host ""

    $jobs = @(
        @{
            Out  = Join-Path $root "rsm.exe"
            Src  = Join-Path $root "src\rsm\Program.cs"
            Refs = @("System.Windows.Forms.dll")
        },
        @{
            Out  = Join-Path $root "rsu.exe"
            Src  = Join-Path $root "src\rsu\Program.cs"
            Refs = @("System.Management.dll")
        },
        @{
            Out  = Join-Path $root "ssh-askpass.exe"
            Src  = Join-Path $root "src\ssh-askpass\Program.cs"
            Refs = @("System.Windows.Forms.dll", "System.Drawing.dll")
        }
    )

    $failed = $false
    foreach ($j in $jobs) {
        $refArgs = ($j.Refs | ForEach-Object { "/reference:$_" }) -join " "
        $cmd     = "& `"$($csc.FullName)`" /nologo /target:winexe /out:`"$($j.Out)`" $refArgs `"$($j.Src)`""
        Write-Host "Compiling $([System.IO.Path]::GetFileName($j.Out))..."
        Invoke-Expression $cmd
        if ($LASTEXITCODE -ne 0) {
            Write-Host "  FAILED" -ForegroundColor Red
            $failed = $true
        }
    }

    if ($failed) { Write-Host "Build failed." -ForegroundColor Red; exit 1 }

} else {
    Write-Host "No compiler found. Install the .NET SDK: https://aka.ms/dotnet/download" -ForegroundColor Red
    exit 1
}

# ── Report outputs ────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "Output:" -ForegroundColor Green
foreach ($name in @("rsm.exe", "rsu.exe", "ssh-askpass.exe")) {
    $exePath = Join-Path $root $name
    if (Test-Path $exePath) {
        Write-Host "  $exePath"
    } else {
        Write-Host "  MISSING: $exePath" -ForegroundColor Yellow
    }
}
