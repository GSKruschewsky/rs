using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Rmount
{
    static class Program
    {
        // ── Win32 console signal API ──────────────────────────────────────────
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll")]
        static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCtrlHandler(IntPtr handler, bool add);

        // ── WNet unmount API ─────────────────────────────────────────────────
        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        static extern int WNetCancelConnection2(string lpName, uint dwFlags, bool fForce);

        // ─────────────────────────────────────────────────────────────────────

        static void SendCtrlC(int targetPid)
        {
            FreeConsole();
            AttachConsole((uint)targetPid);
            SetConsoleCtrlHandler(IntPtr.Zero, true);
            GenerateConsoleCtrlEvent(0, 0);
            Thread.Sleep(200);
            FreeConsole();
            SetConsoleCtrlHandler(IntPtr.Zero, false);
        }

        static void StopRclone(int targetPid)
        {
            Process proc;
            try { proc = Process.GetProcessById(targetPid); }
            catch { return; }

            SendCtrlC(targetPid);

            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(500);
                try { Process.GetProcessById(targetPid); }
                catch { return; } // process gone — clean exit
            }

            // Force-kill fallback
            try { proc.Kill(); } catch { }
        }

        static int Main(string[] args)
        {
            if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
                return 1;

            string hostName = args[0].Trim();
            string scriptDir = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);
            string pidsDir   = Path.Combine(scriptDir, "pids");
            string pidFile   = Path.Combine(pidsDir, hostName + ".pid");
            string driveFile = Path.Combine(pidsDir, hostName + ".drive");

            // Remove drive letter mapping first so Explorer releases the handle
            if (File.Exists(driveFile))
            {
                string driveLetter = File.ReadAllText(driveFile).Trim();
                WNetCancelConnection2(driveLetter, 0, true);
                try { File.Delete(driveFile); } catch { }
            }

            // Stop rclone via PID file
            if (File.Exists(pidFile))
            {
                int targetPid;
                if (int.TryParse(File.ReadAllText(pidFile).Trim(), out targetPid))
                    StopRclone(targetPid);
                try { File.Delete(pidFile); } catch { }
                return 0;
            }

            // Fallback: find rclone by WMI process scan
            using (var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='rclone.exe'"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    string cmdLine = obj["CommandLine"] != null
                        ? obj["CommandLine"].ToString()
                        : string.Empty;
                    if (cmdLine.IndexOf(hostName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int pid = Convert.ToInt32(obj["ProcessId"]);
                        StopRclone(pid);
                        break;
                    }
                }
            }

            return 0;
        }
    }
}
