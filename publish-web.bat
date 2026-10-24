@echo off
rem Publishes the newest web build (Builds\Web\<version>-<build>\) to the gh-pages branch, which GitHub Pages
rem serves at https://emirsakal.github.io/TillWinter/. The branch holds only the build: it is rebuilt from scratch on
rem every publish (force push), in a scratch folder outside the repo, with no Git LFS (Pages cannot serve LFS files).
rem   publish-web.bat            the newest build
rem   publish-web.bat <folder>   a given build folder
setlocal
set "PROJECT=%~dp0"
set "SRC=%~1"
if "%SRC%"=="" for /f "delims=" %%d in (\'dir /b /ad /o-d "%PROJECT%Builds\Web"\') do if not defined SRC set "SRC=%PROJECT%Builds\Web\%%d"
if not exist "%SRC%\index.html" ( echo No web build found. Run build-web.bat first. & exit /b 1 )
for /f "delims=" %%u in ('git -C "%PROJECT%." remote get-url origin') do set "REMOTE=%%u"
set "STAGE=%TEMP%\tillwinter-pages"
if exist "%STAGE%" rmdir /s /q "%STAGE%"
xcopy "%SRC%" "%STAGE%\" /e /i /q /y >nul
pushd "%STAGE%"
git init -q -b gh-pages
git -c core.attributesFile=NUL lfs uninstall --local >nul 2>&1
git -c core.attributesFile=NUL add -A
git -c core.attributesFile=NUL commit -q -m "web build %~nx1"
git push -f -q "%REMOTE%" gh-pages
set EXIT=%ERRORLEVEL%
popd
rmdir /s /q "%STAGE%"
if %EXIT%==0 echo Published %SRC% to gh-pages: https://emirsakal.github.io/TillWinter/
exit /b %EXIT%
