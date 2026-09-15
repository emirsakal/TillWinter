@echo off
rem Feel setup (idempotent): VFX particle prefabs + VfxCatalog and the Master/SFX/Ambience mixer.
setlocal
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
if not "%UNITY_PATH%"=="" set "UNITY=%UNITY_PATH%"
set "PROJECT=%~dp0"
if not exist "%PROJECT%TestResults" mkdir "%PROJECT%TestResults"
"%UNITY%" -batchmode -nographics -projectPath "%PROJECT%." -executeMethod TillWinter.EditorTools.FeelSetup.Run -logFile "%PROJECT%TestResults\feel-setup.log"
set EXIT=%ERRORLEVEL%
findstr /C:"[FeelSetup]" /C:"error CS" /C:"Exception" "%PROJECT%TestResults\feel-setup.log"
echo Exit code %EXIT%
exit /b %EXIT%
