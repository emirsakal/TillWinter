@echo off
rem Art pass setup (idempotent): FBX/PNG import settings, TW_Toon materials, prefabs from the Kenney kits and
rem primitives, node icon atlas, Palette / SeasonPalette / VisualCatalog assets, FarmDecor prefab links,
rem URP shadow/MSAA/HDR settings and the decal renderer feature.
setlocal
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
if not "%UNITY_PATH%"=="" set "UNITY=%UNITY_PATH%"
set "PROJECT=%~dp0"
if not exist "%PROJECT%TestResults" mkdir "%PROJECT%TestResults"
"%UNITY%" -batchmode -nographics -projectPath "%PROJECT%." -executeMethod TillWinter.EditorTools.ArtSetup.Run -logFile "%PROJECT%TestResults\art-setup.log"
set EXIT=%ERRORLEVEL%
findstr /C:"[ArtSetup]" /C:"error CS" /C:"Exception" "%PROJECT%TestResults\art-setup.log"
echo Exit code %EXIT%
exit /b %EXIT%
