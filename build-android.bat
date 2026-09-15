@echo off
rem Release-shaped Android build: IL2CPP, ARM64, Vulkan + GLES3, min API 24, version code bumped by the pipeline.
rem Output: Builds\Android\<version>-<code>\ (.apk always; .aab too when a keystore is provided).
rem Signing: set TW_KEYSTORE_PATH, TW_KEYSTORE_PASS, TW_KEY_ALIAS, TW_KEY_PASS (never commit them). Without them a
rem debug-signed APK is built for sideloading and the script prints how to create the keystore in Unity.
rem   build-android.bat          release (no debug panel, no logging)
rem   build-android.bat -dev     development build with TW_DEBUG (debug panel, probes, logs)
rem   build-android.bat -icons   re-render the app icon first
setlocal
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
if not "%UNITY_PATH%"=="" set "UNITY=%UNITY_PATH%"
set "PROJECT=%~dp0"
set "EXTRA="
:args
if "%~1"=="" goto run
if /I "%~1"=="-dev" set "EXTRA=%EXTRA% -twDev"
if /I "%~1"=="-icons" set "EXTRA=%EXTRA% -twIcons"
shift
goto args
:run
if not exist "%PROJECT%TestResults" mkdir "%PROJECT%TestResults"
"%UNITY%" -batchmode -projectPath "%PROJECT%." -buildTarget Android -executeMethod TillWinter.EditorTools.Build.BuildPipeline.BuildAndroid %EXTRA% -logFile "%PROJECT%TestResults\build-android.log"
set EXIT=%ERRORLEVEL%
findstr /C:"[Build]" /C:"error CS" "%PROJECT%TestResults\build-android.log"
echo Exit code %EXIT%
exit /b %EXIT%
