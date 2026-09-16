@echo off
rem Screenshot tour of every screen and sheet: menu, splash, loading, HUD, debug panel, pause, settings, credits,
rem stats, winter, node sheet, retire confirm, heritage, away card. Opens the real editor (play mode needs a Game
rem view), so close the editor first. The save and settings are backed up and restored around the run.
rem Usage: ui-tour.bat [output folder]   (default TestResults\ui-tour)
setlocal
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
if not "%UNITY_PATH%"=="" set "UNITY=%UNITY_PATH%"
set "PROJECT=%~dp0"
set "OUT=%~1"
if "%OUT%"=="" set "OUT=%PROJECT%TestResults\ui-tour"
if not exist "%PROJECT%TestResults" mkdir "%PROJECT%TestResults"
start "" /wait "%UNITY%" -projectPath "%PROJECT%." -executeMethod TillWinter.EditorTools.UiTour.Run -uiTourOut "%OUT%" -logFile "%PROJECT%TestResults\ui-tour.log"
set EXIT=%ERRORLEVEL%
if exist "%OUT%\report.txt" type "%OUT%\report.txt"
echo Exit code %EXIT%
exit /b %EXIT%
