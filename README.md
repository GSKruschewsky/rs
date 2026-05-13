# rs — rclone SFTP mount helpers for Windows

Silently mount and unmount any SSH host from your `~/.ssh/config` as a Windows network drive using [rclone](https://rclone.org/). No console windows, no manual setup per host — just two commands.

## How it works

| Command | What it does |
|---|---|
| `rsm <hostname>` | Mounts the SFTP share, then opens the mapped drive in Explorer |
| `rsu <hostname>` | Gracefully unmounts and stops rclone |

**`rsm`** starts `rclone.exe` fully hidden in the background through the compiled `rsm.exe` launcher. If SSH needs a passphrase or host-key confirmation, a native GUI dialog pops up (via `ssh-askpass.exe`) instead of a terminal prompt. Once the share is ready it opens automatically in Explorer at the mapped drive letter.

**`rsu`** sends `Ctrl+C` to rclone for a graceful flush-and-exit, then waits up to 10 seconds. If rclone hasn't exited by then it force-kills the process. It also removes the mapped drive letter and cleans up the PID state.

## Requirements

- Windows 10/11
- [rclone](https://rclone.org/downloads/) **v1.74.1** — place `rclone.exe` in this folder
  > Built and tested against rclone `v1.74.1` (commit `076fb2bc`, 2026-05-08). Compatibility with newer versions is not guaranteed.
- OpenSSH client (ships with Windows 10+)
- Host entry in `~/.ssh/config` for every hostname you want to mount

## Setup

**1. Clone / download this repo into a folder of your choice.**

**2. Download `rclone.exe`** from [rclone.org/downloads](https://rclone.org/downloads/) and put it in the repo root. Then unblock it so Windows allows direct execution:

```powershell
Unblock-File .\rclone.exe
```

**3. Build the executables** so `rsm.exe`, `rsu.exe`, and `ssh-askpass.exe` exist in the repo root:

```powershell
.\build.ps1
```

If the .NET SDK is installed, the script builds the full solution with `dotnet build`. If not, it falls back to the built-in .NET Framework compiler on Windows.

**4. Add your SSH host to `~/.ssh/config`** (if not already there):

```
Host myserver
    HostName 1.2.3.4
    User ubuntu
    IdentityFile ~/.ssh/id_ed25519
```

Connect once manually (`ssh myserver`) to accept the host key into `known_hosts`.

**5. Run the installer** to add the folder to your PATH and create Start Menu shortcuts (enables Win+R → `rsm` / `rsu`):

```powershell
powershell -ExecutionPolicy Bypass -File install.ps1
```

This is safe to run multiple times — it won't duplicate the PATH entry.

## Usage

Run the compiled launchers from a prompt, Win+R, or the Start Menu shortcuts:

```
rsm myserver
rsu myserver
```

If your SSH key has a passphrase, `ssh-askpass.exe` shows a GUI dialog for it. Enter it and click OK — rclone remains invisible throughout.

## File structure

```
rs/
├── build.ps1            # Compile rsm.exe, rsu.exe, and ssh-askpass.exe
├── install.ps1          # One-time setup: adds to PATH, creates Start Menu shortcuts
├── rsm.exe              # Mount launcher (silent, no console window)
├── rsu.exe              # Unmount launcher (silent, no console window)
├── ssh-askpass.exe      # GUI passphrase / host-key dialog for OpenSSH
├── src/                 # C# source projects
├── rclone.exe           # ← you provide this (not tracked by git)
├── pids/                # Runtime: PID, drive-letter, and askpass coordination files
├── logs/                # Runtime: rclone log per session
└── cache/               # Runtime: rclone VFS cache per host
```

## How the graceful unmount works

`rsu.exe` uses the Win32 console API (`FreeConsole` → `AttachConsole` → `GenerateConsoleCtrlEvent`) to inject a `Ctrl+C` signal directly into rclone's console group. This triggers rclone's built-in shutdown path (cache flush, unmount) without touching Explorer or leaving the network drive in a broken state. Force-kill is only a fallback.

## License

MIT
