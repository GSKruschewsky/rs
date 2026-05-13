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
            string pidsDir  = Path.Combine(scriptDir, "pids");
            string pidFile  = Path.Combine(pidsDir, hostName + ".pid");
            string cacheDir = Path.Combine(scriptDir, "cache", hostName);
            string logFile  = Path.Combine(scriptDir, "logs", hostName + ".log");

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

            // Open Explorer at the mount root
            Process.Start("explorer.exe", mountPath + @"\");
            return 0;
        }
    }
}
