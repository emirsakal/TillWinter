@echo off
rem Taps every plot (centre and its four visible corners) through the real input path at every field size, after
rem every screen change that has broken taps before, and reports UI over the field, wrong plot mapping, missed
rem strikes and stray plot views. Opens the real editor (play mode), so close the editor first. The save is set
rem aside for the run and put back at the end.
rem Usage: field-tap-probe.bat [output folder] [screen preset, e.g. "1080x1920 (Portrait)"]
setlocal
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
if not "%UNITY_PATH%"=="" set "UNITY=%UNITY_PATH%"
set "PROJECT=%~dp0"
set "OUT=%~1"
if "%OUT%"=="" set "OUT=%PROJECT%TestResults\tapprobe"
set "PRESET=%~2"
if "%PRESET%"=="" set "PRESET=1080x2340 (Portrait)"
if not exist "%PROJECT%TestResults" mkdir "%PROJECT%TestResults"
if not exist "%OUT%" mkdir "%OUT%"
start "" /wait "%UNITY%" -projectPath "%PROJECT%." -executeMethod TillWinter.EditorTools.FieldTapProbe.Run -probeOut "%OUT%" -probePreset "%PRESET%" -logFile "%PROJECT%TestResults\field-tap-probe.log"
set EXIT=%ERRORLEVEL%
if exist "%OUT%\report.txt" type "%OUT%\report.txt"
echo Exit code %EXIT%
exit /b %EXIT%
