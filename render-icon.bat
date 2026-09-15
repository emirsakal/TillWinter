@echo off
rem Renders the app icon from the IconRenderer scene (needs a GPU: no -nographics), applies it and the splash logo to
rem Player Settings, and exports every store size to Builds\Icons. The build scripts do this automatically when missing.
setlocal
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
if not "%UNITY_PATH%"=="" set "UNITY=%UNITY_PATH%"
set "PROJECT=%~dp0"
if not exist "%PROJECT%TestResults" mkdir "%PROJECT%TestResults"
"%UNITY%" -batchmode -projectPath "%PROJECT%." -buildTarget Android -executeMethod TillWinter.EditorTools.Build.IconRenderer.RenderBatch -logFile "%PROJECT%TestResults\render-icon.log"
set EXIT=%ERRORLEVEL%
findstr /C:"[Build]" /C:"error CS" "%PROJECT%TestResults\render-icon.log"
echo Exit code %EXIT%
exit /b %EXIT%
