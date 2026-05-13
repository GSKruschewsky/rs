using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Rmount
{
    static class Program
    {
        // ssh-askpass is called by OpenSSH with the prompt as argv[0].
        // We also honour SSH_ASKPASS_PROMPT for compatibility.
        // Output goes to stdout (inherited pipe handle); exit 0 = OK, 1 = cancel.
        //
        // Coordination with rsm.exe:
        //   RMOUNT_CANCEL_FILE  — we write this to tell rsm to abort the mount
        //   RMOUNT_RETRY_FILE   — we read/increment this to track attempt count

        const int MaxAttempts = 3;

        static void SignalCancel(string cancelFile)
        {
            if (cancelFile == null) return;
            try { File.WriteAllText(cancelFile, "1"); } catch { }
        }

        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string cancelFile = Environment.GetEnvironmentVariable("RMOUNT_CANCEL_FILE");
            string retryFile  = Environment.GetEnvironmentVariable("RMOUNT_RETRY_FILE");

            // ── Retry counter ─────────────────────────────────────────────────
            int attempt = 1;
            if (retryFile != null)
            {
                if (File.Exists(retryFile))
                {
                    int stored;
                    if (int.TryParse(File.ReadAllText(retryFile).Trim(), out stored))
                        attempt = stored + 1;
                }
                try { File.WriteAllText(retryFile, attempt.ToString()); } catch { }
            }

            // ── Too many failures ─────────────────────────────────────────────
            if (attempt > MaxAttempts)
            {
                MessageBox.Show(
                    "Incorrect passphrase " + MaxAttempts + " times in a row.\nCancelling mount.",
                    "SSH Authentication", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SignalCancel(cancelFile);
                return 1;
            }

            // ── Build display prompt ──────────────────────────────────────────
            string basePrompt =
                Environment.GetEnvironmentVariable("SSH_ASKPASS_PROMPT") ??
                (args.Length > 0 ? args[0] : null) ??
                "Enter passphrase:";

            string displayPrompt;
            if (attempt == 1)
            {
                displayPrompt = basePrompt;
            }
            else if (attempt < MaxAttempts)
            {
                displayPrompt = "Incorrect passphrase (attempt " + attempt + " of " + MaxAttempts + ").\n\n" + basePrompt;
            }
            else
            {
                displayPrompt = "Incorrect passphrase \u2014 last attempt (" + MaxAttempts + " of " + MaxAttempts + ").\n\n" + basePrompt;
            }

            // ── Dialog ────────────────────────────────────────────────────────
            using (var form = new Form())
            {
                form.Text             = "SSH Authentication";
                form.Size             = new Size(440, attempt == 1 ? 165 : 195);
                form.StartPosition    = FormStartPosition.CenterScreen;
                form.TopMost          = true;
                form.FormBorderStyle  = FormBorderStyle.FixedDialog;
                form.MaximizeBox      = false;
                form.MinimizeBox      = false;

                int labelHeight = attempt == 1 ? 36 : 66;
                int inputTop    = attempt == 1 ? 55 : 85;
                int btnTop      = attempt == 1 ? 90 : 120;

                var label = new Label
                {
                    Text     = displayPrompt,
                    AutoSize = false,
                    Width    = 400,
                    Height   = labelHeight,
                    Location = new Point(15, 12)
                };
                form.Controls.Add(label);

                var textBox = new TextBox
                {
                    UseSystemPasswordChar = true,
                    Location = new Point(15, inputTop),
                    Width    = 395
                };
                form.Controls.Add(textBox);

                var okButton = new Button
                {
                    Text         = "OK",
                    DialogResult = DialogResult.OK,
                    Location     = new Point(240, btnTop),
                    Width        = 80
                };
                form.Controls.Add(okButton);
                form.AcceptButton = okButton;

                var cancelButton = new Button
                {
                    Text         = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location     = new Point(330, btnTop),
                    Width        = 80
                };
                form.Controls.Add(cancelButton);
                form.CancelButton = cancelButton;

                DialogResult result = form.ShowDialog();

                if (result == DialogResult.OK)
                {
                    Console.WriteLine(textBox.Text);
                    return 0;
                }
            }

            // User cancelled or closed the window — signal rsm to abort
            SignalCancel(cancelFile);
            return 1;
        }
    }
}
