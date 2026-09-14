@echo off
rem Token-cheap wrapper around smoke-test.bat: prints only failures and console-error count from
rem TestResults\smoke\report.txt. Screenshots are not opened. Exit code passes through.
setlocal
set "PROJECT=%~dp0"
call "%PROJECT%smoke-test.bat" >nul 2>&1
set EXIT=%ERRORLEVEL%
python "%PROJECT%.claude\hooks\summarize_smoke.py" "%PROJECT%TestResults\smoke\report.txt" %EXIT%
exit /b %EXIT%
