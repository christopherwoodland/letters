#!/bin/bash
set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
RESOURCE_GROUP_NAME="${1:-rg-document-classifier-mcp}"
LOCATION="${2:-eastus}"
REGISTRY_NAME=""
ACR_LOGIN_SERVER=""

echo -e "${YELLOW}🚀 Document Classifier Deployment to Azure${NC}"
echo ""

# Step 1: Create Resource Group
echo -e "${YELLOW}📁 Creating resource group: $RESOURCE_GROUP_NAME${NC}"
az group create \
  --name "$RESOURCE_GROUP_NAME" \
  --location "$LOCATION"
echo -e "${GREEN}✓ Resource group created${NC}"
echo ""

# Step 2: Build and push Docker images
echo -e "${YELLOW}🐳 Building and pushing Docker images${NC}"

# Generate unique registry name
UNIQUE_SUFFIX=$(openssl rand -hex 4)
REGISTRY_NAME="dcmcp${UNIQUE_SUFFIX}"

# Create container registry
echo "Creating Container Registry: $REGISTRY_NAME"
az acr create \
  --resource-group "$RESOURCE_GROUP_NAME" \
  --name "$REGISTRY_NAME" \
  --sku Basic \
  --location "$LOCATION"

ACR_LOGIN_SERVER="${REGISTRY_NAME}.azurecr.io"

# Login to ACR
echo "Logging in to ACR..."
az acr login --name "$REGISTRY_NAME"

# Build and push main API
echo "Building and pushing DocumentClassifier image..."
az acr build \
  --registry "$REGISTRY_NAME" \
  --image document-classifier:latest \
  --file src/DocumentClassifier/Dockerfile \
  .

# Build and push MCP Functions
echo "Building and pushing DocumentClassifier.MCP image..."
az acr build \
  --registry "$REGISTRY_NAME" \
  --image document-classifier-mcp:latest \
  --file src/DocumentClassifier.MCP/Dockerfile \
  .

echo -e "${GREEN}✓ Docker images built and pushed${NC}"
echo ""

# Step 3: Deploy infrastructure
echo -e "${YELLOW}⚙️  Deploying infrastructure via Bicep${NC}"

az deployment group create \
  --resource-group "$RESOURCE_GROUP_NAME" \
  --template-file azure/infra/containers.bicep \
  --parameters \
    location="$LOCATION" \
    environmentName="prod"

echo -e "${GREEN}✓ Infrastructure deployed${NC}"
echo ""

# Step 4: Get deployment outputs
echo -e "${YELLOW}📊 Deployment Summary${NC}"
echo ""

DEPLOYMENT_OUTPUT=$(az deployment group show \
  --name containers \
  --resource-group "$RESOURCE_GROUP_NAME" \
  --query properties.outputs)

API_FQDN=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.apiContainerFQDN.value')
MCP_URL=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.mcpContainerAppUrl.value')

echo -e "${GREEN}✓ Deployment Complete!${NC}"
echo ""
echo "Resource Group: $RESOURCE_GROUP_NAME"
echo "Container Registry: $ACR_LOGIN_SERVER"
echo ""
echo "🔗 Endpoints:"
echo "  API: http://$API_FQDN"
echo "  MCP: $MCP_URL/api/mcp/tools/list_profiles"
echo ""
echo "Next steps:"
echo "  1. Configure Azure Key Vault with API keys"
echo "  2. Update appsettings in production"
echo "  3. Test endpoints"
echo ""
