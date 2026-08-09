@echo off
setlocal
chcp 65001 >nul

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0backup-github-source.ps1" -RepoDir "%~dp0." -TargetBranch "main" -CommitMessage "自动备份"
set "EXIT_CODE=%ERRORLEVEL%"

pause
exit /b %EXIT_CODE%
