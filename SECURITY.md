# Security Guidelines for DocumentClassifier

This document outlines security best practices and requirements for the DocumentClassifier project.

## 🔒 Critical Rules

### 1. Never Commit Secrets
**DO NOT commit:**
- API keys, connection strings, or any credentials
- `.env` files or environment-specific configuration with secrets
- `appsettings.Development.json` or similar local config files
- AWS/Azure credentials or access keys
- Database passwords
- Private certificates or keys

**What to do instead:**
- Use `appsettings.json` with EMPTY placeholder values
- Store secrets in:
  - **Local Development**: `dotnet user-secrets` or `.env` files (in `.gitignore`)
  - **Azure/Production**: Azure Key Vault, Managed Identity, or environment variables
  - **CI/CD**: GitHub Secrets or your CI platform's secret management

### 2. Input Folder Protection
The `input/` folder is **excluded from Git** because it may contain:
- Sensitive court documents
- Personal information
- Test data with PII (Personally Identifiable Information)

This folder should never be committed. Verify in `.gitignore`:
```
# Input test files (may contain large/sensitive PDFs)
input/
```

### 3. Review Queue & Temp Files
Never commit:
- `src/DocumentClassifier/review-queue.json` - Contains classified document metadata
- `src/DocumentClassifier/documents/` - Local document storage
- `src/DocumentClassifier/logs.txt` - May contain sensitive request details
- `.env` files or temporary configuration

## 🔐 Configuration Management

### Development Setup

1. **Initialize User Secrets** (one-time per project):
   ```bash
   cd src/DocumentClassifier
   dotnet user-secrets init
   ```

2. **Set Secrets Locally**:
   ```bash
   dotnet user-secrets set "DocumentIntelligence:Endpoint" "https://..."
   dotnet user-secrets set "DocumentIntelligence:ApiKey" "your-key"
   dotnet user-secrets set "AzureOpenAI:Endpoint" "https://..."
   dotnet user-secrets set "AzureOpenAI:ApiKey" "your-key"
   dotnet user-secrets set "Search:Endpoint" "https://..."
   dotnet user-secrets set "Search:ApiKey" "your-key"
   ```

3. **Verify Secrets Are NOT in Code**:
   ```bash
   dotnet user-secrets list
   ```

### Production Deployment

Use **Azure Key Vault** with Managed Identity:

```csharp
var keyVaultUrl = new Uri(builder.Configuration["KeyVault:Url"]!);
var credential = new DefaultAzureCredential();
builder.Configuration.AddAzureKeyVault(keyVaultUrl, credential);
```

All production secrets should be in Key Vault, NOT in code or config files.

## 🛡️ Code Security Best Practices

### 1. Input Validation
- Always validate file types, sizes, and content
- Use `ArgumentNullException.ThrowIfNull()` for parameters
- Validate API inputs with data annotations or FluentValidation

### 2. Error Messages
- Never expose internal error details to clients
- Log full errors server-side for debugging
- Return generic error messages to API consumers

```csharp
// ✅ Good: Generic public error
return BadRequest("Invalid request. Please check your input.");

// ❌ Bad: Exposes internal details
return BadRequest($"Failed to connect to {connection.ConnectionString}");
```

### 3. SQL/Database Queries
- Use parameterized queries (not string concatenation)
- Never trust user input in queries
- Use Entity Framework or Dapper with proper parameter binding

### 4. Authentication & Authorization
- Use **Azure AD (Entra ID)** for user authentication in production
- Validate JWT tokens in all API endpoints
- Implement role-based access control (RBAC)
- Never store passwords in code

### 5. Data at Rest & in Transit
- **HTTPS only**: Always use TLS/SSL in production
- **Encryption**: Encrypt sensitive data at rest using Azure Storage encryption
- **Database**: Use transparent data encryption (TDE) for SQL databases

### 6. Logging
- **Never log sensitive data**: API keys, connection strings, PII
- Use structured logging (ILogger) for diagnostics
- Redact sensitive values in logs

```csharp
// ✅ Good: Redacted
_logger.LogInformation("Connected to endpoint {Endpoint}", 
    endpoint.Substring(0, 20) + "...");

// ❌ Bad: Exposed
_logger.LogInformation("Connection string: {ConnectionString}", connectionString);
```

### 7. Dependency Management
- Keep NuGet packages updated
- Run `dotnet package audit` to check for known vulnerabilities
- Use specific version pinning in `.csproj` (avoid wildcards for production)

```xml
<!-- ✅ Good: Specific version -->
<PackageReference Include="Azure.Identity" Version="1.11.0" />

<!-- ❌ Bad: Wildcard allows breaking changes -->
<PackageReference Include="Azure.Identity" Version="1.*" />
```

### 8. API Security
- Implement rate limiting
- Validate file uploads (type, size, content)
- Use CORS appropriately (restrict to known origins)
- Implement request validation middleware

```csharp
if (file.Length > _storageOptions.MaxUploadBytes)
    return BadRequest($"File exceeds max size of {_storageOptions.MaxUploadBytes} bytes.");

if (!IsSupportedFileType(file.FileName, file.ContentType))
    return BadRequest("Unsupported file type.");
```

## 🔍 Pre-Commit Checklist

Before committing code:

- [ ] No API keys, connection strings, or credentials in code
- [ ] No `.env` files or `.Development` configuration files
- [ ] User secrets configured locally, not in version control
- [ ] No `input/` or `documents/` folders with real data
- [ ] No hardcoded endpoints or environment-specific values
- [ ] Passwords/tokens use `*` or are omitted in logs
- [ ] Error messages don't expose system details
- [ ] Dependencies are up-to-date and audited

## 🚨 Security Incident Response

If you accidentally commit a secret:

1. **Immediately rotate** the exposed key/credential
2. **Remove from history**:
   ```bash
   git filter-branch --force --index-filter \
     'git rm --cached --ignore-unmatch src/DocumentClassifier/appsettings.Development.json' \
     --prune-empty --tag-name-filter cat -- --all
   ```
3. **Force push** (coordinate with team):
   ```bash
   git push origin --force --all
   ```
4. **Document** the incident for audit trail
5. **Notify** security team if in production

## 📚 References

- [Microsoft: Secure coding guidelines](https://docs.microsoft.com/en-us/dotnet/standard/security/secure-coding-guidelines)
- [OWASP: Top 10 Web Application Security Risks](https://owasp.org/www-project-top-ten/)
- [Azure: Security best practices](https://docs.microsoft.com/en-us/azure/security/fundamentals/best-practices-and-patterns)
- [CWE: Most Dangerous Software Weaknesses](https://cwe.mitre.org/top25/)

## 🤝 Questions?

If you have questions about security practices, please:
1. Check the [CONTRIBUTING.md](CONTRIBUTING.md) guide
2. Open a discussion issue
3. Contact the security team

---

**Last Updated**: July 20, 2026  
**Status**: Active - All team members must comply
