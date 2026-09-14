@echo off
rem Play-mode smoke test: opens the editor (not batchmode), plays through a year with a virtual mouse,
rem buys upgrades in the winter shop, starts year 2, spawns/taps a crow, and writes screenshots + report
rem to TestResults\smoke\. Exit code 0 = pass, 1 = a check failed or a console error was logged.
rem The editor must not already have this project open.
setlocal
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
if not "%UNITY_PATH%"=="" set "UNITY=%UNITY_PATH%"
set "PROJECT=%~dp0"
set "OUT=%PROJECT%TestResults\smoke"
if not exist "%OUT%" mkdir "%OUT%"
echo Running play-mode smoke test ...
"%UNITY%" -projectPath "%PROJECT%." -executeMethod TillWinter.EditorTools.SmokeTest.Run -logFile "%OUT%\smoke.log"
set EXIT=%ERRORLEVEL%
if exist "%OUT%\report.txt" type "%OUT%\report.txt"
echo Exit code %EXIT%
exit /b %EXIT%
