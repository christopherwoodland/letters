#!/bin/bash
set -euo pipefail

YELLOW='\033[1;33m'
GREEN='\033[0;32m'
NC='\033[0m'

RESOURCE_GROUP_NAME="${1:-rg-document-classifier-mcp}"
LOCATION="${2:-eastus}"
ENVIRONMENT_NAME="${3:-prod}"

echo -e "${YELLOW}Document Classifier deployment (Container Apps)${NC}"

if ! command -v az >/dev/null 2>&1; then
  echo "Azure CLI is required. Install from https://aka.ms/azure-cli"
  exit 1
fi

echo -e "${YELLOW}1) Creating resource group${NC}"
az group create --name "$RESOURCE_GROUP_NAME" --location "$LOCATION" >/dev/null

echo -e "${YELLOW}2) Creating ACR and building images in cloud${NC}"
if command -v openssl >/dev/null 2>&1; then
  UNIQUE_SUFFIX=$(openssl rand -hex 4)
else
  UNIQUE_SUFFIX=$(date +%s | tail -c 5)
fi
REGISTRY_NAME="dcmcp${UNIQUE_SUFFIX}"

az acr create \
  --resource-group "$RESOURCE_GROUP_NAME" \
  --name "$REGISTRY_NAME" \
  --sku Basic \
  --location "$LOCATION" >/dev/null

az acr build \
  --registry "$REGISTRY_NAME" \
  --image document-classifier:latest \
  --file src/DocumentClassifier/Dockerfile \
  .

az acr build \
  --registry "$REGISTRY_NAME" \
  --image document-classifier-mcp:latest \
  --file src/DocumentClassifier.MCP/Dockerfile \
  .

echo -e "${YELLOW}3) Preparing deployment parameters${NC}"
az acr update --resource-group "$RESOURCE_GROUP_NAME" --name "$REGISTRY_NAME" --admin-enabled true >/dev/null
ACR_LOGIN_SERVER=$(az acr show --resource-group "$RESOURCE_GROUP_NAME" --name "$REGISTRY_NAME" --query loginServer -o tsv)
ACR_USERNAME=$(az acr credential show --resource-group "$RESOURCE_GROUP_NAME" --name "$REGISTRY_NAME" --query username -o tsv)
ACR_PASSWORD=$(az acr credential show --resource-group "$RESOURCE_GROUP_NAME" --name "$REGISTRY_NAME" --query passwords[0].value -o tsv)

PARAMS_FILE=$(mktemp)
cat > "$PARAMS_FILE" <<EOF
{
  "\$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "location": { "value": "$LOCATION" },
    "environmentName": { "value": "$ENVIRONMENT_NAME" },
    "containerRegistryName": { "value": "$REGISTRY_NAME" },
    "containerRegistryLoginServer": { "value": "$ACR_LOGIN_SERVER" },
    "containerRegistryUsername": { "value": "$ACR_USERNAME" },
    "containerRegistryPassword": { "value": "$ACR_PASSWORD" }
  }
}
EOF

DEPLOYMENT_NAME="containers-$(date +%Y%m%d%H%M%S)"

echo -e "${YELLOW}4) Running what-if validation${NC}"
az deployment group what-if \
  --name "$DEPLOYMENT_NAME" \
  --resource-group "$RESOURCE_GROUP_NAME" \
  --template-file azure/infra/containers.bicep \
  --parameters "$PARAMS_FILE" >/dev/null

echo -e "${YELLOW}5) Deploying infrastructure${NC}"
az deployment group create \
  --name "$DEPLOYMENT_NAME" \
  --resource-group "$RESOURCE_GROUP_NAME" \
  --template-file azure/infra/containers.bicep \
  --parameters "$PARAMS_FILE" \
  --query properties.outputs \
  --output json >/dev/null

rm -f "$PARAMS_FILE"

API_URL=$(az deployment group show --name "$DEPLOYMENT_NAME" --resource-group "$RESOURCE_GROUP_NAME" --query properties.outputs.apiContainerAppUrl.value -o tsv)
MCP_URL=$(az deployment group show --name "$DEPLOYMENT_NAME" --resource-group "$RESOURCE_GROUP_NAME" --query properties.outputs.mcpContainerAppUrl.value -o tsv)

echo -e "${GREEN}Deployment complete${NC}"
echo "Resource Group: $RESOURCE_GROUP_NAME"
echo "Container Registry: $ACR_LOGIN_SERVER"
echo "API: $API_URL"
echo "MCP list_profiles: $MCP_URL/api/mcp/tools/list_profiles"
