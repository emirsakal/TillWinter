@echo off
rem Screenshot tour of every screen and sheet: menu, splash, loading, HUD, debug panel, pause, settings, credits,
rem stats, winter, node sheet, retire confirm, heritage, away card. Opens the real editor (play mode needs a Game
rem view), so close the editor first. The save and settings are backed up and restored around the run.
rem Usage: ui-tour.bat [output folder] [game view preset]   (defaults TestResults\ui-tour and "1080x2340 (Portrait)")
rem Presets: "1080x2340 (Portrait)", "1080x1920 (Portrait)" (16:9), "1080x2400 (Portrait)", "1536x2048 (Tablet)"
setlocal
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
if not "%UNITY_PATH%"=="" set "UNITY=%UNITY_PATH%"
set "PROJECT=%~dp0"
set "OUT=%~1"
set "SIZE=%~2"
if "%SIZE%"=="" set "SIZE=1080x2340 (Portrait)"
if "%OUT%"=="" set "OUT=%PROJECT%TestResults\ui-tour"
if not exist "%PROJECT%TestResults" mkdir "%PROJECT%TestResults"
start "" /wait "%UNITY%" -projectPath "%PROJECT%." -executeMethod TillWinter.EditorTools.UiTour.Run -uiTourOut "%OUT%" -uiTourSize "%SIZE%" -logFile "%PROJECT%TestResults\ui-tour.log"
set EXIT=%ERRORLEVEL%
if exist "%OUT%\report.txt" type "%OUT%\report.txt"
echo Exit code %EXIT%
exit /b %EXIT%
