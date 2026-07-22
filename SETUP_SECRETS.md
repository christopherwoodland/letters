# Developer Setup Guide: Secrets Configuration

This guide explains how to configure secrets and sensitive data locally for development.

## Quick Start

Bootstrap templates are included:
- `src/DocumentClassifier/appsettings.Development.example.json`
- `src/DocumentClassifier.MCP/local.settings.example.json`
- `.env.example`
- `src/DocumentClassifier.MCP/.env.example`

Copy these to local files and fill in your own values (do not commit filled files).

### Prerequisites
- .NET 9.0 SDK
- Git
- Access to Azure services (DocumentIntelligence, OpenAI, Search)

### 1. Initialize User Secrets (First Time Only)

```bash
cd src/DocumentClassifier
dotnet user-secrets init
```

This creates a local `secrets.json` file (excluded from Git) stored at:
- **Windows**: `%APPDATA%\Microsoft\UserSecrets\<ProjectId>\secrets.json`
- **macOS/Linux**: `~/.microsoft/usersecrets/<ProjectId>/secrets.json`

### 2. Configure Your Azure Services

Get the endpoints and keys from Azure:

```bash
# Document Intelligence
dotnet user-secrets set "DocumentIntelligence:Endpoint" "https://your-region.api.cognitive.microsoft.com"
dotnet user-secrets set "DocumentIntelligence:ApiKey" "your-api-key-here"
dotnet user-secrets set "DocumentIntelligence:ExtractionMethod" "auto"

# Azure OpenAI
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-4o"
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key-here"

# Azure AI Search
dotnet user-secrets set "Search:Endpoint" "https://your-search-service.search.windows.net"
dotnet user-secrets set "Search:ApiKey" "your-api-key-here"
dotnet user-secrets set "Search:IndexName" "court-documents"

# Tenant ID
dotnet user-secrets set "Azure:TenantId" "your-tenant-id"
```

### 3. (Optional) Azure Blob Storage

If using Azure Blob Storage instead of local storage:

```bash
dotnet user-secrets set "Storage:BlobConnectionString" "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net"
dotnet user-secrets set "Storage:BlobAccountUri" "https://your-account.blob.core.windows.net"
```

### 4. Verify Secrets Are Configured

```bash
# List all secrets (without values)
dotnet user-secrets list
```

### 5. Run the Application

```bash
dotnet run
```

The application will automatically load secrets from `appsettings.json` and merge them with user secrets.

## Environment Variables (Alternative)

Instead of user secrets, you can use environment variables:

```bash
# PowerShell
$env:DocumentIntelligence__Endpoint = "https://..."
$env:DocumentIntelligence__ApiKey = "..."
$env:AzureOpenAI__Endpoint = "https://..."
$env:AzureOpenAI__ApiKey = "..."
$env:Search__Endpoint = "https://..."
$env:Search__ApiKey = "..."

# Bash/Linux
export DocumentIntelligence__Endpoint="https://..."
export DocumentIntelligence__ApiKey="..."
export AzureOpenAI__Endpoint="https://..."
export AzureOpenAI__ApiKey="..."
export Search__Endpoint="https://..."
export Search__ApiKey="..."
```

## .env Files (Development Only)

For convenience, you can create a `.env` file in the project root:

```bash
# .env (NEVER commit this file)
DocumentIntelligence__Endpoint=https://...
DocumentIntelligence__ApiKey=your-key
AzureOpenAI__Endpoint=https://...
AzureOpenAI__ApiKey=your-key
Search__Endpoint=https://...
Search__ApiKey=your-key
Azure__TenantId=your-tenant-id
```

Then load it in your shell:

**PowerShell:**
```powershell
# Create function to load .env
function Load-Env {
    if (Test-Path .env) {
        Get-Content .env | ForEach-Object {
            if ($_ -match "^([^=]+)=(.*)$") {
                [System.Environment]::SetEnvironmentVariable($matches[1], $matches[2], "Process")
            }
        }
        Write-Host "✅ Loaded .env file"
    }
}
Load-Env
```

**Bash:**
```bash
# Load .env file
if [ -f .env ]; then
    export $(cat .env | xargs)
    echo "✅ Loaded .env file"
fi
```

## CI/CD Pipeline Secrets

For GitHub Actions, Azure DevOps, or other CI platforms:

1. **GitHub Actions**: Add secrets via Settings → Secrets → Actions
2. **Azure DevOps**: Use Pipelines → Library → Secure Files
3. **Azure Pipelines**: Use Variable Groups with secret variables
4. **Docker/Container**: Use `--secret` flag or secret management tools

Example GitHub Actions:
```yaml
- name: Build and Deploy
  env:
    DocumentIntelligence__ApiKey: ${{ secrets.DOCUMENT_INTELLIGENCE_KEY }}
    AzureOpenAI__ApiKey: ${{ secrets.AZURE_OPENAI_KEY }}
    Search__ApiKey: ${{ secrets.SEARCH_API_KEY }}
  run: dotnet build && dotnet publish
```

## Production: Azure Key Vault

In production, use **Azure Key Vault** with Managed Identity:

```csharp
// Program.cs
var keyVaultUrl = new Uri(builder.Configuration["KeyVault:Url"]!);
var credential = new DefaultAzureCredential();
builder.Configuration.AddAzureKeyVault(keyVaultUrl, credential);
```

This approach:
- ✅ No secrets in code or environment variables
- ✅ Automatic secret rotation
- ✅ Audit logging for all secret access
- ✅ Role-based access control (RBAC)

## Troubleshooting

### Secrets Not Loading?

1. Verify secrets are set:
   ```bash
   dotnet user-secrets list
   ```

2. Check Project ID matches:
   ```bash
   # Should show in .csproj
   cat src/DocumentClassifier/DocumentClassifier.csproj | grep UserSecretsId
   ```

3. Restart the application after adding secrets

### "An error occurred when trying to access the secrets file"

- Verify you ran `dotnet user-secrets init`
- Check file permissions on the secrets.json file
- Ensure the correct directory is being used

### Can't Connect to Azure Services?

1. Verify endpoints are correct (no trailing slashes)
2. Check API keys are valid and not expired
3. Verify network connectivity (firewall rules, VPN)
4. Check Azure resources are deployed and accessible

## Security Reminders

⚠️ **CRITICAL:**
- ❌ Never commit secrets to Git
- ❌ Never share API keys or connection strings
- ❌ Never hardcode secrets in code
- ✅ Use user-secrets for local development
- ✅ Use Key Vault or environment variables for production
- ✅ Rotate secrets regularly
- ✅ Review access logs quarterly

See [SECURITY.md](../SECURITY.md) for comprehensive security guidelines.

## References

- [Microsoft: User Secrets](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [Azure Key Vault Documentation](https://learn.microsoft.com/en-us/azure/key-vault/)
- [12-Factor App: Store Config](https://12factor.net/config)
- [OWASP: Secrets Management](https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html)
