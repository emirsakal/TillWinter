@echo off
rem Token-cheap wrapper around run-tests.bat: prints compile errors, pass/fail counts and the
rem first lines of each failure. Capped at 100 lines. Exit code is run-tests.bat's exit code.
setlocal
set "PROJECT=%~dp0"
call "%PROJECT%run-tests.bat" >nul 2>&1
set EXIT=%ERRORLEVEL%
python "%PROJECT%.claude\hooks\summarize_tests.py" "%PROJECT%TestResults\EditMode.xml" "%PROJECT%TestResults\EditMode.log" %EXIT%
exit /b %EXIT%
