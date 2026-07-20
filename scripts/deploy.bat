@echo off
REM Windows deployment script for Document Classifier

setlocal enabledelayedexpansion

REM Configuration
set RESOURCE_GROUP_NAME=%1
if "!RESOURCE_GROUP_NAME!"=="" set RESOURCE_GROUP_NAME=rg-document-classifier-mcp

set LOCATION=%2
if "!LOCATION!"=="" set LOCATION=eastus

echo ======================================
echo  Document Classifier Azure Deployment
echo ======================================
echo.

REM Step 1: Create Resource Group
echo Creating resource group: !RESOURCE_GROUP_NAME!
call az group create ^
  --name "!RESOURCE_GROUP_NAME!" ^
  --location "!LOCATION!"

if errorlevel 1 (
  echo Failed to create resource group
  exit /b 1
)
echo.

REM Step 2: Build Docker images locally (optional)
echo Building Docker images...
call docker-compose build
if errorlevel 1 (
  echo Failed to build Docker images
  exit /b 1
)
echo.

REM Step 3: Deploy via Bicep
echo Deploying infrastructure...
call az deployment group create ^
  --resource-group "!RESOURCE_GROUP_NAME!" ^
  --template-file azure/infra/containers.bicep ^
  --parameters location="!LOCATION!" environmentName="prod"

if errorlevel 1 (
  echo Failed to deploy infrastructure
  exit /b 1
)
echo.

echo ======================================
echo  Deployment Complete!
echo ======================================
echo Resource Group: !RESOURCE_GROUP_NAME!
echo Location: !LOCATION!
echo.
echo Next steps:
echo   1. Configure Azure Key Vault with API keys
echo   2. Test endpoints
echo   3. Monitor via Azure Portal
echo.

endlocal
