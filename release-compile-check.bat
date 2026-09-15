@echo off
rem Fast CI check: compiles the Android and iOS *release* player scripts (no TW_DEBUG) without building a player,
rem and fails if the debug panel or the allocation probes are still in TillWinter.Unity.dll.
setlocal
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
if not "%UNITY_PATH%"=="" set "UNITY=%UNITY_PATH%"
set "PROJECT=%~dp0"
if not exist "%PROJECT%TestResults" mkdir "%PROJECT%TestResults"
"%UNITY%" -batchmode -nographics -projectPath "%PROJECT%." -executeMethod TillWinter.EditorTools.Build.BuildPipeline.CompileReleaseCheck -logFile "%PROJECT%TestResults\release-compile.log"
set EXIT=%ERRORLEVEL%
findstr /C:"[Build]" /C:"error CS" "%PROJECT%TestResults\release-compile.log"
echo Exit code %EXIT%
exit /b %EXIT%
