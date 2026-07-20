# Production Deployment Checklist

This checklist ensures the DocumentClassifier is production-ready with all security features enabled and properly configured.

## Pre-Deployment Verification

### ✅ Security Features
- [ ] File validation enabled (magic number checking)
- [ ] Exception handler middleware active (safe error responses)
- [ ] Security headers present (CSP, HSTS, X-Frame-Options, etc.)
- [ ] CORS properly restricted (no wildcard origins)
- [ ] Logging configured (sensitive data redacted)
- [ ] Rate limiting enabled (if required)

### ✅ Authentication & Authorization
- [ ] Azure Entra ID application registered
- [ ] `Authentication:Enabled` set to `true`
- [ ] Tenant ID configured (`Authentication:TenantId`)
- [ ] Client ID configured (`Authentication:ClientId`)
- [ ] API permissions added in Entra ID
- [ ] Authorization policies tested

### ✅ Configuration Management
- [ ] All secrets moved to Key Vault (not in code)
- [ ] `appsettings.json` contains only safe defaults
- [ ] `appsettings.Development.json` in `.gitignore`
- [ ] Environment variables configured for production
- [ ] CORS origins updated for production domain(s)
- [ ] Database connection strings secured

### ✅ HTTPS & Transport Security
- [ ] HTTPS enforced (`HSTS` header enabled in production)
- [ ] Certificate valid and not expired
- [ ] TLS 1.2+ required
- [ ] Cipher suites hardened
- [ ] HTTP → HTTPS redirect enforced

### ✅ Secrets & Credentials
- [ ] No API keys in `appsettings.json`
- [ ] All secrets in Azure Key Vault
- [ ] Managed identities configured for Azure services
- [ ] Service principal credentials stored in Key Vault
- [ ] API keys rotated (if applicable)
- [ ] Database passwords changed from default

### ✅ Logging & Monitoring
- [ ] Application Insights configured
- [ ] Log retention policy set
- [ ] Alerts configured for errors/failures
- [ ] No sensitive data in logs (API keys, passwords)
- [ ] Correlation IDs enabled for tracing
- [ ] Structured logging in place

### ✅ Data Protection
- [ ] Uploaded documents encrypted at rest
- [ ] Document folder uses encryption
- [ ] Blob storage encryption enabled (if used)
- [ ] PII handling documented
- [ ] Data retention policy configured
- [ ] Backup strategy in place

### ✅ Testing & Validation
- [ ] All unit tests pass (44/44)
- [ ] Integration tests executed
- [ ] Security headers verified
- [ ] File upload validation tested
- [ ] Error handling verified (no info disclosure)
- [ ] CORS behavior validated

### ✅ Infrastructure
- [ ] Azure App Service plan configured
- [ ] Auto-scaling configured
- [ ] Health checks configured
- [ ] Resource monitoring enabled
- [ ] Backup strategy for database
- [ ] Disaster recovery plan reviewed

### ✅ Code Quality
- [ ] Code review completed
- [ ] OWASP Top 10 compliance verified
- [ ] Dependency security scanning passed
- [ ] No hardcoded credentials
- [ ] No debugging code left
- [ ] Performance optimizations applied

---

## Production Configuration Steps

### Step 1: Enable Authentication

Edit `appsettings.json` for production:

```json
{
  "Authentication": {
    "Enabled": true,
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "Audience": "api://document-classifier"
  }
}
```

### Step 2: Configure CORS

Update for production domain:

```json
{
  "Cors": {
    "AllowedOrigins": "https://yourdomain.com;https://app.yourdomain.com",
    "AllowedMethods": ["GET", "POST", "PUT", "DELETE"],
    "AllowCredentials": false
  }
}
```

### Step 3: Enable HTTPS

Ensure certificate is installed:

```bash
# Verify certificate
certutil -store MY | findstr /i "yourdomain"

# Force HTTPS
# Already enabled in Program.cs for non-development
```

### Step 4: Set Up Azure Key Vault

```bash
# Create Key Vault
az keyvault create --resource-group mygroup --name mykeyvault

# Add secrets
az keyvault secret set --vault-name mykeyvault \
  --name "Authentication--TenantId" --value "your-tenant-id"
az keyvault secret set --vault-name mykeyvault \
  --name "Authentication--ClientId" --value "your-client-id"
az keyvault secret set --vault-name mykeyvault \
  --name "DocumentIntelligence--ApiKey" --value "your-api-key"
az keyvault secret set --vault-name mykeyvault \
  --name "AzureOpenAI--ApiKey" --value "your-openai-key"
```

### Step 5: Configure Managed Identity

```bash
# Enable managed identity on App Service
az webapp identity assign --resource-group mygroup --name myapp

# Grant Key Vault access
az keyvault set-policy --name mykeyvault \
  --object-id <app-identity-object-id> \
  --secret-permissions get list
```

### Step 6: Enable Application Insights

```bash
# Create Application Insights
az monitor app-insights component create \
  --resource-group mygroup \
  --app myapp-insights \
  --location eastus

# Configure in app settings
APPINSIGHTS_INSTRUMENTATIONKEY=<key>
ApplicationInsightsAgent_EXTENSION_VERSION=~3
```

### Step 7: Configure Logging

Set production log level:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.Hosting": "Information"
    }
  }
}
```

### Step 8: Test Authentication

```bash
# Get token
$token = (az account get-access-token --resource "api://document-classifier").accessToken

# Test protected endpoint
curl -H "Authorization: Bearer $token" \
  https://yourdomain.com/api/profiles
```

---

## Post-Deployment Verification

### ✅ Security Headers Check

```bash
curl -i https://yourdomain.com | grep -i "X-"
```

Expected headers:
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `X-XSS-Protection: 1; mode=block`
- `Strict-Transport-Security: ...`

### ✅ HTTPS Verification

```bash
# Check certificate
curl -vI https://yourdomain.com 2>&1 | grep -i "ssl\|tls"

# Verify HSTS
curl -i https://yourdomain.com | grep -i "strict-transport"
```

### ✅ Authentication Test

```bash
# Test without token (should fail)
curl https://yourdomain.com/api/documents/process -X POST
# Expected: 401 Unauthorized

# Test with token (should work)
curl -H "Authorization: Bearer <token>" \
  https://yourdomain.com/api/documents/process -X POST
# Expected: 200 or 400 (depending on request)
```

### ✅ Error Handling Test

```bash
# Test invalid file upload (should not expose details)
curl -X POST https://yourdomain.com/api/documents/process \
  -F "file=@malicious.exe"
# Expected: 400 Bad Request with safe error message (no path info)
```

### ✅ Monitor Logs

```bash
# Check Application Insights
az monitor app-insights metrics show --app myapp-insights \
  --metrics requests/count requests/failed

# Check security events in logs
# Look for: "File upload", "validation failed", "Authentication"
```

---

## Rollback Plan

If issues occur in production:

1. **Switch to previous version**: `az webapp deployment slot swap ...`
2. **Disable authentication**: Set `Authentication:Enabled: false`
3. **Review logs**: Check Application Insights for errors
4. **Notify users**: If critical functionality impacted
5. **Investigate**: Root cause analysis

---

## Post-Deployment Tasks

- [ ] Notify stakeholders of production deployment
- [ ] Document production URLs and endpoints
- [ ] Train support team on new features
- [ ] Schedule security audit (30 days)
- [ ] Review and update incident response plan
- [ ] Set up on-call rotation for monitoring
- [ ] Plan quarterly security reviews
- [ ] Document configuration decisions

---

## References

- [AUTHENTICATION_SETUP.md](./AUTHENTICATION_SETUP.md) - Entra ID setup guide
- [SECURITY.md](./SECURITY.md) - Security best practices
- [ARCHITECTURE.md](./ARCHITECTURE.md) - System architecture
- [SETUP_SECRETS.md](./SETUP_SECRETS.md) - Secrets management

---

**Last Updated**: July 20, 2026
**Prepared By**: DocumentClassifier Security Team
**Next Review**: 30 days after production deployment
