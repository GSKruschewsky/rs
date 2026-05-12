@echo off
setlocal
rem SSH passes the prompt text as the first argument.
rem We relay it via an env var so there are no quoting issues with the PowerShell call.
set "SSH_ASKPASS_PROMPT=%~1"
powershell -WindowStyle Hidden -NoProfile -ExecutionPolicy Bypass -File "%~dp0ssh-askpass.ps1"
