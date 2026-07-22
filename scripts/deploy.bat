@echo off
setlocal

set RESOURCE_GROUP_NAME=%1
if "%RESOURCE_GROUP_NAME%"=="" set RESOURCE_GROUP_NAME=rg-document-classifier-mcp

set LOCATION=%2
if "%LOCATION%"=="" set LOCATION=eastus

where pwsh >nul 2>nul
if %ERRORLEVEL%==0 (
  pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0Deploy-ToAzure.ps1" -ResourceGroup "%RESOURCE_GROUP_NAME%" -Location "%LOCATION%"
  exit /b %ERRORLEVEL%
)

where powershell >nul 2>nul
if %ERRORLEVEL%==0 (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Deploy-ToAzure.ps1" -ResourceGroup "%RESOURCE_GROUP_NAME%" -Location "%LOCATION%"
  exit /b %ERRORLEVEL%
)

echo Neither pwsh nor powershell was found. Install PowerShell to run scripts\Deploy-ToAzure.ps1.
exit /b 1
