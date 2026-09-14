@echo off
rem Opens the TillWinter project in Unity 6000.3.22f1 (override with UNITY_PATH).
setlocal
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
if not "%UNITY_PATH%"=="" set "UNITY=%UNITY_PATH%"
start "" "%UNITY%" -projectPath "%~dp0."
