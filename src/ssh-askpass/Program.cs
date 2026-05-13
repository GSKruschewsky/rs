using System;
using System.Drawing;
using System.Windows.Forms;

namespace Rmount
{
    static class Program
    {
        // ssh-askpass is called by OpenSSH with the prompt as argv[0].
        // We also honour SSH_ASKPASS_PROMPT (set by the old .cmd shim) for
        // compatibility if anything still uses it.
        // Output goes to stdout (inherited pipe handle); exit 0 = OK, 1 = cancel.

        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string prompt =
                Environment.GetEnvironmentVariable("SSH_ASKPASS_PROMPT") ??
                (args.Length > 0 ? args[0] : null) ??
                "Enter passphrase:";

            using (var form = new Form())
            {
                form.Text             = "SSH Authentication";
                form.Size             = new Size(440, 165);
                form.StartPosition    = FormStartPosition.CenterScreen;
                form.TopMost          = true;
                form.FormBorderStyle  = FormBorderStyle.FixedDialog;
                form.MaximizeBox      = false;
                form.MinimizeBox      = false;

                var label = new Label
                {
                    Text     = prompt,
                    AutoSize = false,
                    Width    = 400,
                    Height   = 36,
                    Location = new Point(15, 12)
                };
                form.Controls.Add(label);

                var textBox = new TextBox
                {
                    UseSystemPasswordChar = true,
                    Location = new Point(15, 55),
                    Width    = 395
                };
                form.Controls.Add(textBox);

                var okButton = new Button
                {
                    Text         = "OK",
                    DialogResult = DialogResult.OK,
                    Location     = new Point(240, 90),
                    Width        = 80
                };
                form.Controls.Add(okButton);
                form.AcceptButton = okButton;

                var cancelButton = new Button
                {
                    Text         = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location     = new Point(330, 90),
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

            return 1;
        }
    }
}
