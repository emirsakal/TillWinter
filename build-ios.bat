@echo off
rem iOS: generates the Xcode project (IL2CPP, Metal, iPhone only, iOS 15+, requires full screen, deferred home-indicator
rem gestures) into Builds\iOS\<version>-<build>\Xcode. Signing, archiving and upload happen in Xcode on a Mac.
rem   build-ios.bat          release
rem   build-ios.bat -dev     development build with TW_DEBUG
rem   build-ios.bat -icons   re-render the app icon first
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
"%UNITY%" -batchmode -projectPath "%PROJECT%." -buildTarget iOS -executeMethod TillWinter.EditorTools.Build.BuildPipeline.BuildIos %EXTRA% -logFile "%PROJECT%TestResults\build-ios.log"
set EXIT=%ERRORLEVEL%
findstr /C:"[Build]" /C:"error CS" "%PROJECT%TestResults\build-ios.log"
echo Exit code %EXIT%
exit /b %EXIT%
