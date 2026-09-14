@echo off
rem Runs the TillWinter EditMode test suite in Unity batchmode and writes NUnit XML results.
rem Usage:  run-tests.bat            (uses the default Unity 6000.3.22f1 Hub install)
rem         set UNITY_PATH=C:\path\to\Unity.exe && run-tests.bat
rem Exit codes: 0 = all tests passed, 2 = at least one test failed, 3 = Unity failed to run (compile error, project already open, ...).
setlocal
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
if not "%UNITY_PATH%"=="" set "UNITY=%UNITY_PATH%"
set "PROJECT=%~dp0"
set "OUT=%PROJECT%TestResults"
if not exist "%OUT%" mkdir "%OUT%"
del /q "%OUT%\EditMode.xml" 2>nul

echo Running EditMode tests with "%UNITY%" ...
"%UNITY%" -batchmode -nographics -projectPath "%PROJECT%." -runTests -testPlatform EditMode -testResults "%OUT%\EditMode.xml" -logFile "%OUT%\EditMode.log"
set EXIT=%ERRORLEVEL%

if exist "%OUT%\EditMode.xml" (
    echo.
    findstr /C:"<test-run" "%OUT%\EditMode.xml"
    echo Results: %OUT%\EditMode.xml
) else (
    echo No results file written. See %OUT%\EditMode.log
    findstr /C:"error CS" "%OUT%\EditMode.log"
)
echo Exit code %EXIT%
exit /b %EXIT%
