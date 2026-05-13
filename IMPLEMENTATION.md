# Rmount — C# Rewrite Implementation Spec
# Branch: feat/csharp-rewrite

## Goal

Replace the PowerShell + VBScript chain with three self-contained native Windows
executables built from C# (.NET Framework 4.8). No runtime dependencies beyond what
ships with Windows 10/11.

Before:
  rsm <hostname>  →  rsm.lnk → rsm.vbs → powershell rsm-script.ps1
  rsu <hostname>  →  rsu.lnk → rsu.vbs → powershell rsu-script.ps1
  SSH passphrase  →  ssh-askpass.cmd → powershell ssh-askpass.ps1

After:
  rsm <hostname>  →  rsm.lnk → rsm.exe
  rsu <hostname>  →  rsu.lnk → rsu.exe
  SSH passphrase  →  ssh-askpass.exe

---

## Repository layout (branch)

```
Rmount/
├── Rmount.sln                   # Visual Studio solution
├── build.ps1                    # Compile script (dotnet build)
├── install.ps1                  # PATH + shortcut setup (updated)
├── rclone.1                     # rclone man page (unchanged)
├── src/
│   ├── rsm/
│   │   ├── rsm.csproj
│   │   └── Program.cs
│   ├── rsu/
│   │   ├── rsu.csproj
│   │   └── Program.cs
│   └── ssh-askpass/
│       ├── ssh-askpass.csproj
│       └── Program.cs
├── pids/                        # Runtime: one .pid file per active mount
├── logs/                        # Runtime: one .log file per session
└── cache/                       # Runtime: rclone VFS cache per host
```

Compiled outputs land at the repo root (configured via `<OutputPath>..\..\</OutputPath>`
in each .csproj):

```
rsm.exe
rsu.exe
ssh-askpass.exe
```

---

## Build requirements

- .NET SDK (any version that supports net48 targeting)
  Install: https://dotnet.microsoft.com/download
- rclone.exe placed at repo root (not tracked by git)

One-time build:
```powershell
.\build.ps1          # Debug
.\build.ps1 -Release # Release (smaller binaries)
```

---

## Step-by-step implementation

### Step 1 — Git branch
```
git checkout -b feat/csharp-rewrite
```
Main branch is never modified.

### Step 2 — Solution scaffold
- `Rmount.sln` references all three projects with stable GUIDs
- Each `.csproj` targets `net48`, `OutputType=WinExe`
- `OutputPath` set to `..\..\` so exes appear at repo root without a copy step
- `AppendTargetFrameworkToOutputPath=false` prevents net48/ subfolder

### Step 3 — rsm/Program.cs
Replaces: `rsm-script.ps1` + `rsm.vbs`

Logic (in order):
1. Read `args[0]` as hostname; if empty → MessageBox "Usage: rsm <hostname>" + exit 1
2. Read `~/.ssh/config`; missing → MessageBox + exit 1
3. Regex `Host\s+<hostname>` against config content; not found → MessageBox + exit 1
4. Compute paths from `Assembly.GetExecutingAssembly().Location` (same as `$MyInvocation.MyCommand.Path`)
5. `Directory.CreateDirectory` for pids/, logs/, cache/<hostname>/
6. `File.WriteAllText(logFile, "")` — truncate for this session
7. Clear stale `driveFile` / `cancelFile` / `retryFile`, then set `SSH_ASKPASS=<scriptDir>\ssh-askpass.exe`, `SSH_ASKPASS_REQUIRE=force`, `RMOUNT_CANCEL_FILE=<cancelFile>`, `RMOUNT_RETRY_FILE=<retryFile>`
8. Start rclone with `CreateNoWindow=true, WindowStyle=Hidden, UseShellExecute=false`
9. Write `proc.Id` to pidFile
10. Poll loop (200ms): enumerate network drives, resolve each drive letter back to its remote path with `WNetGetConnection`, and stop when one matches `@"\\sftp\<hostname>"`
11. If `cancelFile` appears, stop the full rclone process tree with `taskkill /T /F`, delete `pidFile`, and leave the askpass state files in place so late `ssh-askpass` launches exit silently
12. If a matching drive is found, persist its drive letter to `driveFile` for `rsu`
13. If rclone exits early, delete `pidFile` / `driveFile` and exit 1; stale askpass state is cleared on the next launch
14. On mount ready: delete askpass state files, then `Process.Start("explorer.exe", driveLetter + @"\")`

### Step 4 — rsu/Program.cs
Replaces: `rsu-script.ps1` + `rsu.vbs`

P/Invoke declarations (kernel32.dll):
- `FreeConsole`
- `AttachConsole(uint dwProcessId)`
- `GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId)`
- `SetConsoleCtrlHandler(IntPtr handler, bool add)`

P/Invoke declaration (mpr.dll):
- `WNetCancelConnection2(string lpName, uint dwFlags, bool fForce)`

Logic:
1. `args[0]` hostname; empty → exit 1
2. Paths: scriptDir from Assembly location, pidsDir, pidFile, driveFile
3. If `.drive` file exists: `WNetCancelConnection2(driveLetter, 0, true)` + delete file
4. If `.pid` file exists: `StopRclone(pid)` + delete file → exit 0
5. Fallback WMI: `SELECT ProcessId,CommandLine FROM Win32_Process WHERE Name='rclone.exe'`
   → find entry where CommandLine contains hostname → `StopRclone(pid)`

`SendCtrlC(int pid)`:
  FreeConsole → AttachConsole(pid) → SetConsoleCtrlHandler(null, true)
  → GenerateConsoleCtrlEvent(0,0) → Sleep(200ms)
  → FreeConsole → SetConsoleCtrlHandler(null, false)

`StopRclone(int pid)`:
  GetProcessById → SendCtrlC → poll HasExited (500ms intervals, 10s deadline)
  → force-kill fallback

References: `System.Management` (for WMI)

### Step 5 — ssh-askpass/Program.cs
Replaces: `ssh-askpass.cmd` + `ssh-askpass.ps1`

Compiled as WinExe so no console window flashes. OpenSSH captures stdout through the
inherited pipe handle regardless of subsystem.

Logic:
1. Prompt = `SSH_ASKPASS_PROMPT` env var, then `args[0]`, then `"Enter passphrase:"`
2. If `RMOUNT_CANCEL_FILE` already exists, exit 1 immediately without showing UI
3. Track retries through `RMOUNT_RETRY_FILE`; after 3 failures, show the final error,
  then create `RMOUNT_CANCEL_FILE` so the message stays visible until dismissed while any
  later askpass launch still exits silently
4. WinForms dialog (440×165, FixedDialog, TopMost):
   - Label (400×36 at 15,12) showing prompt
   - Masked TextBox (UseSystemPasswordChar=true, 395wide at 15,55)
   - OK button (80wide at 240,90) — AcceptButton
   - Cancel button (80wide at 330,90) — CancelButton
5. ShowDialog → OK: Console.WriteLine(password) + return 0
6. Cancel/close: create `RMOUNT_CANCEL_FILE`, then return 1

Note: SSH_ASKPASS_PROMPT compatibility is retained so any tooling that previously
set that env var continues to work without change.

### Step 6 — build.ps1
```powershell
.\build.ps1          # dotnet build Rmount.sln --configuration Debug
.\build.ps1 -Release # dotnet build Rmount.sln --configuration Release
```
Prints paths of produced exes or warnings if any are missing.

### Step 7 — install.ps1 changes
- Added Step 2: check if rsm.exe/rsu.exe/ssh-askpass.exe exist; if not, auto-call build.ps1
- Changed shortcut target from `$name.vbs` → `$name.exe`

### Step 8 — Deleted files (on this branch)
```
rsm.vbs
rsu.vbs
rsm-script.ps1
rsu-script.ps1
ssh-askpass.cmd
ssh-askpass.ps1
```

---

## Design decisions

| Decision | Choice | Reason |
|---|---|---|
| Target framework | net48 | Pre-installed on all Win 10/11; no runtime download |
| OutputType | WinExe | No console window (same effect as VBS `0, False` / `-WindowStyle Hidden`) |
| OutputPath | `..\..\` in .csproj | Exes land at repo root; install.ps1 unchanged path assumptions |
| Mount detection | `Directory.Exists(@"\\sftp\host")` poll | Equivalent to `Get-PSDrive` loop; no WMI overhead during hot path |
| ssh-askpass stdout | `Console.WriteLine` from WinExe | Inherited pipe handle from SSH is still valid in WinExe subsystem |
| Ctrl+C signal | Direct P/Invoke (FreeConsole→AttachConsole→GenerateCtrlEvent) | Identical mechanism to the embedded C# in the old rsu-script.ps1 |
| WMI fallback in rsu | `System.Management.ManagementObjectSearcher` | Mirrors `Get-CimInstance Win32_Process` behaviour exactly |

---

## Verification checklist

- [ ] `git log --oneline main` — no new commits on main
- [ ] `.\build.ps1` — exits 0, rsm.exe/rsu.exe/ssh-askpass.exe present at repo root
- [ ] `rsm` (no args) — MessageBox "Usage: rsm <hostname>"
- [ ] `rsm unknownhost` — MessageBox "Host not found in SSH config"
- [ ] `rsm <valid-host>` — mount appears at \\sftp\<host>, Explorer opens, pids/<host>.pid created
- [ ] `rsu <valid-host>` — graceful Ctrl+C sent, rclone exits cleanly, pids/<host>.pid deleted
- [ ] SSH key with passphrase — ssh-askpass.exe dialog appears, passphrase accepted, mount succeeds
- [ ] Cancel in ssh-askpass — rsm detects early rclone exit, cleans up pidFile, exits 1
