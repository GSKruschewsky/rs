Set oShell = CreateObject("WScript.Shell")
Dim scriptDir, psScript, args, i
scriptDir = Left(WScript.ScriptFullName, InStrRev(WScript.ScriptFullName, "\"))
psScript  = scriptDir & "rsm-script.ps1"
args = ""
For i = 0 To WScript.Arguments.Count - 1
    args = args & " """ & WScript.Arguments(i) & """"
Next
oShell.Run "powershell -WindowStyle Hidden -ExecutionPolicy Bypass -File """ & psScript & """" & args, 0, False