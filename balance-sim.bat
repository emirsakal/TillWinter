@echo off
rem Headless balance run: the AutoPlayer plays N generations at a fixed seed and writes
rem TestResults\balance.csv and balance.txt, then prints the table. Usage: balance-sim.bat [seed] [generations]
rem Needs the project closed in the editor.
setlocal
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
if not "%UNITY_PATH%"=="" set "UNITY=%UNITY_PATH%"
set "PROJECT=%~dp0"
set SEED=%1
if "%SEED%"=="" set SEED=7
set GENS=%2
if "%GENS%"=="" set GENS=3
if not exist "%PROJECT%TestResults" mkdir "%PROJECT%TestResults"
del /q "%PROJECT%TestResults\balance.txt" 2>nul
"%UNITY%" -batchmode -nographics -projectPath "%PROJECT%." -executeMethod TillWinter.EditorTools.BalanceSim.Run -seed %SEED% -generations %GENS% -logFile "%PROJECT%TestResults\balance.log"
set EXIT=%ERRORLEVEL%
if exist "%PROJECT%TestResults\balance.txt" (
    type "%PROJECT%TestResults\balance.txt"
) else (
    echo No balance table written. See TestResults\balance.log
    findstr /C:"error CS" "%PROJECT%TestResults\balance.log"
)
exit /b %EXIT%
