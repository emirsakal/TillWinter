@echo off
rem Plays the mechanics end to end through the real input and UI paths (strike, water, reap, winter purchases,
rem next year, expansion, retire, album page, Heritage, new generation) and reports whether taps still land after
rem every screen change. Opens the real editor (play mode needs a Game view), so close the editor first.
rem The developer's save is set aside for the run and put back at the end.
rem Usage: mechanics-probe.bat [output folder]   (default TestResults\probe)
setlocal
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
if not "%UNITY_PATH%"=="" set "UNITY=%UNITY_PATH%"
set "PROJECT=%~dp0"
set "OUT=%~1"
if "%OUT%"=="" set "OUT=%PROJECT%TestResults\probe"
if not exist "%PROJECT%TestResults" mkdir "%PROJECT%TestResults"
start "" /wait "%UNITY%" -projectPath "%PROJECT%." -executeMethod TillWinter.EditorTools.MechanicsProbe.Run -probeOut "%OUT%" -logFile "%PROJECT%TestResults\mechanics-probe.log"
set EXIT=%ERRORLEVEL%
if exist "%OUT%\report.txt" type "%OUT%\report.txt"
echo Exit code %EXIT%
exit /b %EXIT%
