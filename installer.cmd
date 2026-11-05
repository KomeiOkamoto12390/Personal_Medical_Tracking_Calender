@echo off
rem Builds the installer (Setup.exe) and update packages with Velopack.
rem Output: artifacts\releases\  (MedicalCalendar-win-Setup.exe, *.nupkg, releases.win.json, ...)
rem
rem Version comes from <Version> in src\MedicalCalendar\MedicalCalendar.csproj.
rem Keep earlier releases in artifacts\releases so vpk can build small delta updates.
setlocal
cd /d "%~dp0"

for /f "usebackq delims=" %%v in (`dotnet msbuild src\MedicalCalendar\MedicalCalendar.csproj -nologo -getProperty:Version`) do set VERSION=%%v
if "%VERSION%"=="" (echo Could not read the version from the project file. & exit /b 1)
echo Building Medical Calendar %VERSION% installer...

dotnet tool restore || exit /b 1

rem Velopack packs a folder of files, so publish without single-file bundling.
if exist artifacts\publish rmdir /s /q artifacts\publish
dotnet publish src\MedicalCalendar\MedicalCalendar.csproj -c Release -p:PublishSingleFile=false -o artifacts\publish || exit /b 1

rem packId decides the install folder (%LOCALAPPDATA%\KomeiOkamoto.MedicalCalendar).
rem It must NEVER be "MedicalCalendar": that is the data folder, and uninstall deletes the install folder.
dotnet vpk pack ^
  --packId KomeiOkamoto.MedicalCalendar ^
  --packVersion %VERSION% ^
  --packDir artifacts\publish ^
  --mainExe MedicalCalendar.exe ^
  --packTitle "Medical Calendar" ^
  --packAuthors "Komei Okamoto" ^
  --outputDir artifacts\releases || exit /b 1

echo.
echo Installer: %~dp0artifacts\releases\KomeiOkamoto.MedicalCalendar-win-Setup.exe
