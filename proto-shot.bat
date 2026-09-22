@echo off
rem Core loop v3 prototype screenshots: opens Proto.unity in play mode, drives a few strikes, a reap and a winter
rem through DigProtoBootstrap's debug hooks, writes 00-05 PNGs to the folder given (default TestResults\proto).
rem Close the editor first (the project lock).   proto-shot.bat <folder>
setlocal
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
if not "%UNITY_PATH%"=="" set "UNITY=%UNITY_PATH%"
set "PROJECT=%~dp0"
set "OUT=%~1"
if "%OUT%"=="" set "OUT=%PROJECT%TestResults\proto"
if not exist "%PROJECT%TestResults" mkdir "%PROJECT%TestResults"
start "" /wait "%UNITY%" -projectPath "%PROJECT%." -executeMethod TillWinter.EditorTools.DigProtoShot.Run -protoOut "%OUT%" -logFile "%OUT%\proto-shot.log"
set EXIT=%ERRORLEVEL%
dir /b "%OUT%\*.png" 2>nul
echo Exit code %EXIT%
exit /b %EXIT%
