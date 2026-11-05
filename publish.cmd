@echo off
rem Build a self-contained, ReadyToRun Release exe into .\dist
rem The result runs on any Windows 10/11 x64 PC with no .NET install needed.
dotnet publish "%~dp0src\MedicalCalendar\MedicalCalendar.csproj" -c Release -o "%~dp0dist"
if errorlevel 1 exit /b 1
echo.
echo Built: %~dp0dist\MedicalCalendar.exe
