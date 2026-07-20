# Azure Entra ID Authentication Setup

This guide explains how to set up and configure Azure Entra ID (formerly Azure AD) authentication for the Document Classifier API.

## Overview

The Document Classifier supports optional JWT bearer token authentication using Azure Entra ID. When enabled, all API endpoints (except health checks) require a valid access token in the `Authorization: Bearer <token>` header.

## Prerequisites

- Azure subscription with Entra ID tenant access
- Admin privileges in Entra ID to register applications
- Azure CLI or Azure Portal access
- `dotnet user-secrets` or environment variables configured

## Step 1: Register Application in Entra ID

### Via Azure Portal

1. **Navigate to Entra ID**
   - Go to https://portal.azure.com
   - Search for "Entra ID" or "Azure Active Directory"

2. **Register New Application**
   - Click "App registrations" → "New registration"
   - Enter application name: `Document Classifier API`
   - Select "Accounts in this organizational directory only"
   - Click "Register"

3. **Note the IDs**
   - Copy "Application (client) ID" → `ClientId`
   - Copy "Directory (tenant) ID" → `TenantId`

### Via Azure CLI

```bash
az ad app create \
  --display-name "Document Classifier API" \
  --identifier-uris "api://document-classifier" \
  --query "{clientId: appId, tenantId: publisherDomain}"
```

## Step 2: Configure API Permissions

1. **Add API Permissions**
   - In App registration, click "API permissions" → "Add a permission"
   - Select "Microsoft Graph"
   - Choose "Application permissions"
   - Search for and add: `User.Read`, `Directory.Read.All` (if needed)
   - Click "Grant admin consent"

2. **Expose as API**
   - Click "Expose an API"
   - Click "Set" next to "Application ID URI"
   - Accept the suggested value: `api://document-classifier`
   - Click "Save"
   - Click "Add a scope" (for server-to-server scenarios if needed)

## Step 3: Create Client Secret (for Server-to-Server Auth)

**Skip this if using user authentication only.**

1. **Generate Secret**
   - Click "Certificates & secrets"
   - Click "New client secret"
   - Enter description: `Development Secret`
   - Select expiration: 12 months (or your preference)
   - Click "Add"
   - **IMPORTANT**: Copy the secret VALUE immediately (you can't see it again)

2. **Store Securely**
   ```bash
   # Using dotnet user-secrets
   dotnet user-secrets set "Authentication:ClientSecret" "your-secret-value"
   
   # Or set environment variable
   export Authentication__ClientSecret="your-secret-value"
   ```

## Step 4: Enable Authentication in Application

### Configuration

Edit `appsettings.json` or set via environment variables:

```json
{
  "Authentication": {
    "Enabled": true,
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "Audience": "api://document-classifier"
  },
  "Azure": {
    "TenantId": "your-tenant-id"
  }
}
```

Or using environment variables:
```bash
# Windows PowerShell
$env:Authentication__Enabled = "true"
$env:Authentication__TenantId = "your-tenant-id"
$env:Authentication__ClientId = "your-client-id"

# Linux/Mac bash
export Authentication__Enabled=true
export Authentication__TenantId="your-tenant-id"
export Authentication__ClientId="your-client-id"
```

### Restart Application

```bash
dotnet run --project src/DocumentClassifier/DocumentClassifier.csproj
```

## Step 5: Test Authentication

### Obtain Access Token

#### Option A: Using PowerShell (Interactive)

```powershell
$token = (az account get-access-token --resource "api://document-classifier").accessToken
Write-Host "Token: $token"
```

#### Option B: Using Python

```python
import subprocess
import json

result = subprocess.run([
    "az", "account", "get-access-token",
    "--resource", "api://document-classifier"
], capture_output=True, text=True)

token = json.loads(result.stdout)["accessToken"]
print(f"Token: {token}")
```

#### Option C: Using OAuth 2.0 Client Credentials Flow

```bash
curl -X POST \
  "https://login.microsoftonline.com/{tenant-id}/oauth2/v2.0/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id={client-id}" \
  -d "scope=api://document-classifier/.default" \
  -d "grant_type=client_credentials" \
  -d "client_secret={client-secret}"
```

### Call Protected Endpoint

```bash
# Using curl
curl -X GET "http://localhost:5000/api/profiles" \
  -H "Authorization: Bearer $TOKEN"

# Using PowerShell
$headers = @{
    "Authorization" = "Bearer $token"
}
Invoke-RestMethod -Uri "http://localhost:5000/api/profiles" -Headers $headers
```

### Expected Responses

**With valid token:**
```json
{
  "status": 200,
  "data": [...]
}
```

**Without token:**
```json
{
  "status": 401,
  "message": "Unauthorized"
}
```

**With invalid token:**
```json
{
  "status": 401,
  "message": "Invalid token"
}
```

## Step 6: Configure for Production

### Enable HTTPS Only

```json
{
  "Authentication": {
    "Enabled": true,
    ...
  }
}
```

The application automatically:
- Forces HSTS headers in production
- Requires HTTPS for all requests
- Sets secure CORS headers

### Manage Secrets in Production

#### Option A: Azure Key Vault

```bash
# Create Key Vault
az keyvault create --resource-group mygroup --name mykeyvault

# Add secrets
az keyvault secret set --vault-name mykeyvault \
  --name "Authentication--ClientId" --value "your-client-id"
az keyvault secret set --vault-name mykeyvault \
  --name "Authentication--TenantId" --value "your-tenant-id"

# Application will automatically read from Key Vault via DefaultAzureCredential
```

#### Option B: Azure App Service Environment Variables

1. Go to Azure Portal → App Service → Configuration
2. Add application settings:
   - `Authentication__Enabled`: `true`
   - `Authentication__ClientId`: `your-client-id`
   - `Authentication__TenantId`: `your-tenant-id`

#### Option C: Environment Variables

```bash
export Authentication__Enabled="true"
export Authentication__ClientId="your-client-id"
export Authentication__TenantId="your-tenant-id"
```

### Configure CORS for Production

```json
{
  "Cors": {
    "AllowedOrigins": "https://yourdomain.com;https://app.yourdomain.com",
    "AllowedMethods": ["GET", "POST", "PUT", "DELETE"],
    "AllowCredentials": false
  }
}
```

## Authorization Policies

The application supports fine-grained authorization via Entra ID roles:

### DocumentProcessing (Default)
- **Required roles**: Any authenticated user
- **Endpoints**: All document processing endpoints
- **Use case**: Any user who can log in

### AdminOnly
- **Required roles**: `Admin` or `DocumentClassifierAdmin`
- **Endpoints**: Profile management (CRUD)
- **Use case**: Administrators only

### Adding Custom Roles

1. **Create role in Entra ID**
   ```bash
   az ad app role create \
     --id {app-id} \
     --role-id "unique-guid" \
     --display-name "DocumentClassifierAdmin" \
     --description "Full admin access to document classifier"
   ```

2. **Assign role to users**
   - In Azure Portal → App registration → App roles
   - Select user/group and assign role

3. **Use in code**
   ```csharp
   [Authorize(Roles = "Admin,DocumentClassifierAdmin")]
   public IActionResult AdminEndpoint() { }
   ```

## Troubleshooting

### Token Validation Fails

**Error**: `InvalidOperationException: Unable to validate bearer token`

**Solution**:
1. Verify `TenantId` matches your Entra ID directory ID
2. Verify `ClientId` matches the registered application
3. Check token is not expired: `jwt.ms` decode the token
4. Verify `aud` (audience) claim matches `api://document-classifier`

### CORS Errors After Enabling Auth

**Error**: `Access to XMLHttpRequest has been blocked by CORS policy`

**Solution**:
1. Update `Cors:AllowedOrigins` to include frontend domain
2. Ensure `Cors:AllowCredentials` is set correctly
3. Browser preflight requests must succeed (OPTIONS method)

### Clients Can't Get Token

**Error**: `invalid_client_id` or `invalid_scope`

**Solution**:
1. Verify `ClientId` is registered in Entra ID
2. Verify scope is: `api://document-classifier/.default`
3. If using client secret, ensure it hasn't expired
4. Check secret value is stored correctly (no extra spaces/newlines)

### Can't Disable Authentication

**Error**: All requests return 401 Unauthorized even though `Authentication:Enabled` is false

**Solution**:
1. Ensure you've restarted the application after config change
2. Check environment variables don't override (env vars take precedence)
3. Verify config file has correct JSON format
4. Check application startup logs:
   ```bash
   dotnet run --project src/DocumentClassifier/DocumentClassifier.csproj 2>&1 | grep -i auth
   ```

## References

- [Microsoft Identity Web Documentation](https://github.com/AzureAD/microsoft-identity-web)
- [JWT Bearer Token Authorization in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/introduction)
- [Register an application with Entra ID](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app)
- [OAuth 2.0 Client Credentials Flow](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-client-creds-grant-flow)

## Quick Disable

If authentication causes issues in development:

```json
{
  "Authentication": {
    "Enabled": false
  }
}
```

All endpoints become public (development only).

## Next Steps

- [Review Security Architecture](./ARCHITECTURE.md#security-architecture)
- [Check Contributing Guidelines](./CONTRIBUTING.md)
- [Read Security Policy](./SECURITY.md)
