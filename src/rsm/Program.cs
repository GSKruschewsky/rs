using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Rmount
{
    static class Program
    {
        static void StopProcessTree(Process proc)
        {
            if (proc == null) return;

            try
            {
                if (proc.HasExited) return;
            }
            catch
            {
                return;
            }

            try
            {
                var killPsi = new ProcessStartInfo("taskkill.exe", "/PID " + proc.Id + " /T /F")
                {
                    WindowStyle     = ProcessWindowStyle.Hidden,
                    CreateNoWindow  = true,
                    UseShellExecute = false
                };

                using (var killProc = Process.Start(killPsi))
                {
                    if (killProc != null)
                        killProc.WaitForExit(5000);
                }
            }
            catch
            {
                try { if (!proc.HasExited) proc.Kill(); } catch { }
            }

            try { proc.WaitForExit(5000); } catch { }
        }

        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            {
                MessageBox.Show("Usage: rsm <hostname>", "rsm",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 1;
            }

            string hostName = args[0].Trim();

            // Validate SSH config
            string sshConfigPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".ssh", "config");

            if (!File.Exists(sshConfigPath))
            {
                MessageBox.Show(
                    "SSH config file not found at '" + sshConfigPath + "'.\n" +
                    "Please create it and add a Host entry for '" + hostName + "'.",
                    "rsm", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }

            string sshConfigContent = File.ReadAllText(sshConfigPath);
            if (!Regex.IsMatch(sshConfigContent, @"Host\s+" + Regex.Escape(hostName),
                RegexOptions.IgnoreCase))
            {
                MessageBox.Show(
                    "Host '" + hostName + "' not found in SSH config file at '" + sshConfigPath + "'.\n" +
                    "Please add a Host entry for '" + hostName + "'.",
                    "rsm", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }

            // Paths
            string scriptDir = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);
            string pidsDir    = Path.Combine(scriptDir, "pids");
            string pidFile    = Path.Combine(pidsDir, hostName + ".pid");
            string cacheDir   = Path.Combine(scriptDir, "cache", hostName);
            string logFile    = Path.Combine(scriptDir, "logs", hostName + ".log");
            string cancelFile = Path.Combine(pidsDir, hostName + ".askpass-cancel");
            string retryFile  = Path.Combine(pidsDir, hostName + ".askpass-retry");

            // Ensure directories exist
            Directory.CreateDirectory(pidsDir);
            Directory.CreateDirectory(Path.Combine(scriptDir, "logs"));
            Directory.CreateDirectory(cacheDir);

            // Clear log for this session
            File.WriteAllText(logFile, string.Empty);

            // Route SSH passphrase prompts through the GUI askpass exe
            string askpassExe = Path.Combine(scriptDir, "ssh-askpass.exe");
            Environment.SetEnvironmentVariable("SSH_ASKPASS", askpassExe);
            Environment.SetEnvironmentVariable("SSH_ASKPASS_REQUIRE", "force");

            // Pass coordination files to ssh-askpass; clean up any stale state first
            try { File.Delete(cancelFile); } catch { }
            try { File.Delete(retryFile);  } catch { }
            Environment.SetEnvironmentVariable("RMOUNT_CANCEL_FILE", cancelFile);
            Environment.SetEnvironmentVariable("RMOUNT_RETRY_FILE",  retryFile);

            // Build rclone arguments
            string mountPath = @"\\sftp\" + hostName;
            string rcloneArgs =
                "mount \":sftp,ssh='ssh " + hostName + "',shell_type=none,idle_timeout=0:\" " +
                "\"" + mountPath + "\" " +
                "--vfs-cache-mode full --log-level INFO " +
                "--cache-dir \"" + cacheDir + "\" " +
                "--log-file \"" + logFile + "\" " +
                "--config NUL";

            string rcloneExe = Path.Combine(scriptDir, "rclone.exe");
            var psi = new ProcessStartInfo(rcloneExe, rcloneArgs)
            {
                WindowStyle    = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            Process proc = Process.Start(psi);
            if (proc == null) return 1;

            // Persist PID
            File.WriteAllText(pidFile, proc.Id.ToString());

            // Wait for the mount to become visible
            while (true)
            {
                if (proc.HasExited)
                {
                    try { File.Delete(pidFile);    } catch { }
                    return 1;
                }

                // User cancelled or exhausted retries in the askpass dialog
                if (File.Exists(cancelFile))
                {
                    StopProcessTree(proc);
                    try { File.Delete(pidFile); } catch { }
                    return 1;
                }

                try
                {
                    if (Directory.Exists(mountPath))
                        break;
                }
                catch { /* share not ready yet */ }

                System.Threading.Thread.Sleep(200);
            }

            // Clean up askpass state — mount succeeded
            try { File.Delete(cancelFile); } catch { }
            try { File.Delete(retryFile); } catch { }

            // Open Explorer at the mount root
            Process.Start("explorer.exe", mountPath + @"\");
            return 0;
        }
    }
}
