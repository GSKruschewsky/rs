# rs — rclone SFTP mount helpers for Windows

Silently mount and unmount any SSH host from your `~/.ssh/config` as a Windows network share (`\\sftp\<hostname>`) using [rclone](https://rclone.org/). No console windows, no manual setup per host — just two commands.

## How it works

| Command | What it does |
|---|---|
| `rsm <hostname>` | Mounts the SFTP share, opens it in Explorer |
| `rsu <hostname>` | Gracefully unmounts and stops rclone |

**`rsm`** starts rclone fully hidden in the background. If SSH needs a passphrase or host-key confirmation, a native GUI dialog pops up (via `SSH_ASKPASS`) instead of a terminal prompt. Once the share is ready it opens automatically in Explorer.

**`rsu`** sends `Ctrl+C` to rclone for a graceful flush-and-exit, then waits up to 10 seconds. If rclone hasn't exited by then it force-kills the process. The PID file is always cleaned up.

## Requirements

- Windows 10/11
- [rclone](https://rclone.org/downloads/) **v1.74.1** — place `rclone.exe` in this folder
  > Built and tested against rclone `v1.74.1` (commit `076fb2bc`, 2026-05-08). Compatibility with newer versions is not guaranteed.
- OpenSSH client (ships with Windows 10+)
- Host entry in `~/.ssh/config` for every hostname you want to mount

## Setup

**1. Clone / download this repo into a folder of your choice.**

**2. Download `rclone.exe`** from [rclone.org/downloads](https://rclone.org/downloads/) and put it next to the scripts. Then unblock it so Windows allows direct execution:

```powershell
Unblock-File .\rclone.exe
```

**3. Add your SSH host to `~/.ssh/config`** (if not already there):

```
Host myserver
    HostName 1.2.3.4
    User ubuntu
    IdentityFile ~/.ssh/id_ed25519
```

Connect once manually (`ssh myserver`) to accept the host key into `known_hosts`.

**4. Run the installer** to add the folder to your PATH and create Start Menu shortcuts (enables Win+R → `rsm` / `rsu`):

```powershell
powershell -ExecutionPolicy Bypass -File install.ps1
```

This is safe to run multiple times — it won't duplicate the PATH entry.

## Usage

Double-click `rsm.vbs` (or run from a prompt — the `.vbs` wrapper suppresses the console):

```
rsm myserver
rsu myserver
```

If your SSH key has a passphrase, a dialog will appear asking for it. Enter it and click OK — rclone remains invisible throughout.

## File structure

```
rs/
├── install.ps1          # One-time setup: adds to PATH, creates Start Menu shortcuts
├── rsm.vbs              # Mount launcher (silent, no console window)
├── rsm-script.ps1       # Mount logic
├── rsu.vbs              # Unmount launcher (silent, no console window)
├── rsu-script.ps1       # Unmount logic
├── ssh-askpass.cmd      # Called by OpenSSH to request passphrases
├── ssh-askpass.ps1      # GUI passphrase / host-key dialog
├── rclone.exe           # ← you provide this (not tracked by git)
├── pids/                # Runtime: PID files per active mount
├── logs/                # Runtime: rclone log per session
└── cache/               # Runtime: rclone VFS cache per host
```

## How the graceful unmount works

`rsu-script.ps1` uses the Win32 console API (`FreeConsole` → `AttachConsole` → `GenerateConsoleCtrlEvent`) to inject a `Ctrl+C` signal directly into rclone's console group. This triggers rclone's built-in shutdown path (cache flush, FUSE unmount) without touching Explorer or leaving the network share in a broken state. Force-kill is only a fallback.

## License

MIT
