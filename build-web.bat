@echo off
rem Browser build for GitHub Pages: IL2CPP to WebAssembly, gzip with Unity's decompression fallback (Pages sends no
rem Content-Encoding header), the project's phone-framed page template. Moves no version number.
rem Output: Builds\Web\<version>-<build>\ (index.html at its root). Publish it with publish-web.bat.
rem   build-web.bat          release (no debug panel, no logging)
rem   build-web.bat -dev     development build with TW_DEBUG
setlocal
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
if not "%UNITY_PATH%"=="" set "UNITY=%UNITY_PATH%"
set "PROJECT=%~dp0"
set "EXTRA="
if /I "%~1"=="-dev" set "EXTRA=-twDev"
if not exist "%PROJECT%TestResults" mkdir "%PROJECT%TestResults"
"%UNITY%" -batchmode -projectPath "%PROJECT%." -buildTarget WebGL -executeMethod TillWinter.EditorTools.Build.BuildPipeline.BuildWeb %EXTRA% -logFile "%PROJECT%TestResults\build-web.log"
set EXIT=%ERRORLEVEL%
findstr /C:"[Build]" /C:"error CS" "%PROJECT%TestResults\build-web.log"
echo Exit code %EXIT%
exit /b %EXIT%
