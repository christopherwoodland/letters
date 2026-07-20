#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploy Document Classifier to Azure with containerization

.PARAMETER ResourceGroup
    Name of the resource group (default: rg-document-classifier-mcp)

.PARAMETER Location
    Azure region (default: eastus)

.PARAMETER SkipDockerBuild
    Skip local Docker build and go straight to ACR build

.EXAMPLE
    .\Deploy-ToAzure.ps1 -ResourceGroup "rg-doc-classifier" -Location "westus"
#>

param(
    [string]$ResourceGroup = "rg-document-classifier-mcp",
    [string]$Location = "eastus",
    [switch]$SkipDockerBuild = $false
)

$ErrorActionPreference = "Stop"

function Invoke-Az {
    param(
        [Parameter(Mandatory=$true)]
        [string[]]$Args
    )

    & az @Args
    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI command failed: az $($Args -join ' ')"
    }
}

Write-Host "
==============================================================
   Document Classifier - Azure Deployment with Containers
==============================================================
" -ForegroundColor Cyan

# Verify Azure CLI is installed
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Host "[ERROR] Azure CLI not found. Install from https://aka.ms/azure-cli" -ForegroundColor Red
    exit 1
}

# Step 1: Create Resource Group
Write-Host "`n[INFO] Creating resource group: $ResourceGroup in $Location" -ForegroundColor Yellow
Invoke-Az -Args @("group", "create", "--name", $ResourceGroup, "--location", $Location)
Write-Host "[OK] Resource group created" -ForegroundColor Green

# Step 2: Create Container Registry
Write-Host "`n[INFO] Creating Azure Container Registry..." -ForegroundColor Yellow
$suffix = -join ((65..90) + (97..122) | Get-Random -Count 4 | ForEach-Object {[char]$_})
$registryName = "dcmcp$suffix".ToLower()

Invoke-Az -Args @("acr", "create", "--resource-group", $ResourceGroup, "--name", $registryName, "--sku", "Basic", "--location", $Location)

Write-Host "[OK] Container Registry created: $registryName" -ForegroundColor Green

# Step 3: Login to ACR (skip if Docker daemon unavailable)
Write-Host "`n[INFO] Skipping ACR login - using cloud builds..." -ForegroundColor Yellow

# Step 4: Build and push images via ACR
Write-Host "`n[INFO] Building and pushing Docker images to ACR..." -ForegroundColor Yellow

Write-Host "   Building DocumentClassifier image..." -ForegroundColor Cyan
Invoke-Az -Args @("acr", "build", "--registry", $registryName, "--image", "document-classifier:latest", "--file", "src/DocumentClassifier/Dockerfile", ".")

Write-Host "   Building DocumentClassifier.MCP image..." -ForegroundColor Cyan
Invoke-Az -Args @("acr", "build", "--registry", $registryName, "--image", "document-classifier-mcp:latest", "--file", "src/DocumentClassifier.MCP/Dockerfile", ".")

Write-Host "[OK] Docker images pushed to ACR" -ForegroundColor Green

# Step 5: Deploy infrastructure
Write-Host "`n[INFO] Deploying infrastructure via Bicep..." -ForegroundColor Yellow

# Get ACR credentials
Write-Host "[INFO] Getting ACR credentials..." -ForegroundColor Yellow

# Enable admin user if not already enabled
az acr update --resource-group $ResourceGroup --name $registryName --admin-enabled true | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Failed to enable ACR admin user"
}

# Get ACR login server and credentials
$acrLoginServer = az acr show --resource-group $ResourceGroup --name $registryName --query loginServer --output tsv
$acrCreds = az acr credential show --resource-group $ResourceGroup --name $registryName --query "{username:username, password:passwords[0].value}" --output json | ConvertFrom-Json

if ($LASTEXITCODE -ne 0) {
    throw "Failed to get ACR credentials"
}

$deploymentName = "containers-$(Get-Date -Format 'yyyyMMddHHmmss')"

# Create a temporary parameters file to handle special characters in password
$paramsFile = New-TemporaryFile
$paramsContent = @{
    schema = "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#"
    contentVersion = "1.0.0.0"
    parameters = @{
        location = @{ value = $Location }
        environmentName = @{ value = "prod" }
        containerRegistryName = @{ value = $registryName }
        containerRegistryLoginServer = @{ value = $acrLoginServer }
        containerRegistryUsername = @{ value = $acrCreds.username }
        containerRegistryPassword = @{ value = $acrCreds.password }
    }
} | ConvertTo-Json

Set-Content -Path $paramsFile -Value $paramsContent -Encoding UTF8

try {
    $deploymentJson = az deployment group create `
        --name $deploymentName `
        --resource-group $ResourceGroup `
        --template-file azure/infra/containers.bicep `
        --parameters $paramsFile `
        --query properties.outputs `
        --output json
    if ($LASTEXITCODE -ne 0) {
        throw "Azure deployment failed"
    }
}
finally {
    Remove-Item -Path $paramsFile -Force -ErrorAction SilentlyContinue
}

Write-Host "[OK] Infrastructure deployed" -ForegroundColor Green

# Step 6: Get deployment outputs
Write-Host "`n[INFO] Getting deployment outputs..." -ForegroundColor Yellow
$outputs = $deploymentJson | ConvertFrom-Json

$apiContainerAppUrl = $outputs.apiContainerAppUrl.value
$mcpContainerAppUrl = $outputs.mcpContainerAppUrl.value

Write-Host "`n
==============================================================
                   DEPLOYMENT COMPLETE!
==============================================================

Resource Group:    $ResourceGroup
Container Registry: $registryName.azurecr.io
Location:          $Location

[ENDPOINTS]
   API:  $apiContainerAppUrl
   MCP:  $mcpContainerAppUrl/api/mcp/tools/list_profiles

[NEXT STEPS]
   1. Update API keys in Azure Key Vault
   2. Test endpoints:
      curl http://$apiContainerFqdn/api/profiles
   3. Configure custom domain (optional)
   4. Set up monitoring and alerts

[USEFUL COMMANDS]
   View logs:
     az container logs --resource-group $ResourceGroup --name document-classifier-api
   
   Cleanup:
     az group delete --name $ResourceGroup --yes --no-wait

" -ForegroundColor Green

Write-Host "For more info, see: DEPLOYMENT.md" -ForegroundColor Cyan
