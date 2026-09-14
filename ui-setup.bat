@echo off
rem One-off UI setup: imports TMP Essential Resources, builds Assets\Fonts\Resources\NunitoSDF.asset
rem (Latin + Turkish glyphs) from Assets\Fonts\Nunito-Variable.ttf, creates the TreeTheme asset. Idempotent.
setlocal
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
if not "%UNITY_PATH%"=="" set "UNITY=%UNITY_PATH%"
set "PROJECT=%~dp0"
if not exist "%PROJECT%TestResults" mkdir "%PROJECT%TestResults"
"%UNITY%" -batchmode -nographics -projectPath "%PROJECT%." -executeMethod TillWinter.EditorTools.UiSetup.Run -logFile "%PROJECT%TestResults\ui-setup.log"
set EXIT=%ERRORLEVEL%
findstr /C:"[UiSetup]" /C:"error CS" "%PROJECT%TestResults\ui-setup.log"
echo Exit code %EXIT%
exit /b %EXIT%
