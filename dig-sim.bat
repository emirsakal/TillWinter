@echo off
rem Core loop v3 prototype (GDD section 2v3): runs the dumb and smart bots for N years and writes TestResults\dig.txt.
rem   dig-sim.bat [seed] [years]      defaults: seed 1, 8 years
setlocal
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
if not "%UNITY_PATH%"=="" set "UNITY=%UNITY_PATH%"
set "PROJECT=%~dp0"
set SEED=%1
if "%SEED%"=="" set SEED=1
set YEARS=%2
if "%YEARS%"=="" set YEARS=8
if not exist "%PROJECT%TestResults" mkdir "%PROJECT%TestResults"
del /q "%PROJECT%TestResults\dig.txt" 2>nul
"%UNITY%" -batchmode -nographics -projectPath "%PROJECT%." -executeMethod TillWinter.EditorTools.DigSimRunner.Run -seed %SEED% -years %YEARS% -logFile "%PROJECT%TestResults\dig.log"
set EXIT=%ERRORLEVEL%
type "%PROJECT%TestResults\dig.txt" 2>nul
echo Exit code %EXIT%
exit /b %EXIT%
