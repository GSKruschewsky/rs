# Called by OpenSSH when a passphrase or interactive prompt is needed.
# Reads the prompt from SSH_ASKPASS_PROMPT (set by ssh-askpass.cmd to avoid quoting issues).
# Outputs the response to stdout and exits 0; exits 1 on cancel.

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$prompt = if ($env:SSH_ASKPASS_PROMPT) { $env:SSH_ASKPASS_PROMPT } else { "Enter passphrase:" }

$form                  = New-Object System.Windows.Forms.Form
$form.Text             = "SSH Authentication"
$form.Size             = New-Object System.Drawing.Size(440, 165)
$form.StartPosition    = "CenterScreen"
$form.TopMost          = $true
$form.FormBorderStyle  = "FixedDialog"
$form.MaximizeBox      = $false
$form.MinimizeBox      = $false

$label          = New-Object System.Windows.Forms.Label
$label.Text     = $prompt
$label.AutoSize = $false
$label.Width    = 400
$label.Height   = 36
$label.Location = New-Object System.Drawing.Point(15, 12)
$form.Controls.Add($label)

$textBox                       = New-Object System.Windows.Forms.TextBox
$textBox.UseSystemPasswordChar = $true
$textBox.Location              = New-Object System.Drawing.Point(15, 55)
$textBox.Width                 = 395
$form.Controls.Add($textBox)

$okButton              = New-Object System.Windows.Forms.Button
$okButton.Text         = "OK"
$okButton.DialogResult = [System.Windows.Forms.DialogResult]::OK
$okButton.Location     = New-Object System.Drawing.Point(240, 90)
$okButton.Width        = 80
$form.Controls.Add($okButton)
$form.AcceptButton = $okButton

$cancelButton              = New-Object System.Windows.Forms.Button
$cancelButton.Text         = "Cancel"
$cancelButton.DialogResult = [System.Windows.Forms.DialogResult]::Cancel
$cancelButton.Location     = New-Object System.Drawing.Point(330, 90)
$cancelButton.Width        = 80
$form.Controls.Add($cancelButton)
$form.CancelButton = $cancelButton

$result = $form.ShowDialog()

if ($result -eq [System.Windows.Forms.DialogResult]::OK) {
    Write-Output $textBox.Text
    exit 0
} else {
    exit 1
}
