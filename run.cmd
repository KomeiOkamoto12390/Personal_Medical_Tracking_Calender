@echo off
rem Build (Debug) and launch the app. Uses .cmd so PowerShell execution policy never blocks it.
dotnet run --project "%~dp0src\MedicalCalendar\MedicalCalendar.csproj"
