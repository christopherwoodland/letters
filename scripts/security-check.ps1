#!/usr/bin/env pwsh
<#
.SYNOPSIS
Security scanner for DocumentClassifier - detects potential secrets in staged files.

.DESCRIPTION
This script checks staged Git files for common patterns that indicate secrets 
(API keys, passwords, connection strings, etc.). It can be used as a pre-commit hook.

.EXAMPLE
.\scripts\security-check.ps1

.NOTES
To install as a Git pre-commit hook on Windows:
1. Copy this script to .git/hooks/pre-commit (without .ps1 extension)
2. The script will run automatically before each commit
#>

param(
    [switch]$SkipConfirm
)

$ErrorActionPreference = 'Continue'

# Patterns that indicate secrets
$sensitivePatterns = @(
    @{ Pattern = 'api[_-]?key["\']?\s*[:=]'; Description = 'API Key' }
    @{ Pattern = 'password["\']?\s*[:=]'; Description = 'Password' }
    @{ Pattern = 'secret["\']?\s*[:=]'; Description = 'Secret' }
    @{ Pattern = 'token["\']?\s*[:=]'; Description = 'Token' }
    @{ Pattern = 'connection[_-]?string["\']?\s*[:=]'; Description = 'Connection String' }
    @{ Pattern = 'private[_-]?key'; Description = 'Private Key' }
    @{ Pattern = '-----BEGIN.*PRIVATE KEY'; Description = 'Private Key Block' }
    @{ Pattern = 'akia[0-9a-z]{16}'; Description = 'AWS Access Key' }
    @{ Pattern = 'sk_live_[0-9a-zA-Z]{24}'; Description = 'Stripe Secret Key' }
)

# Files that should never be committed
$forbiddenPatterns = @(
    '\.env',
    'secrets\.json',
    'appsettings\.Development\.json',
    'appsettings\.Local\.json',
    '\.aws',
    'azure-credentials\.json',
    '\.(pem|key|p12|pfx)$',
    'input/'
)

Write-Host "🔍 Running security checks..." -ForegroundColor Cyan
Write-Host "`n"

$issues = @()

# Get staged files
try {
    $stagedFiles = git diff --cached --name-only 2>$null
} catch {
    Write-Host "⚠️  Not a git repository or git not available" -ForegroundColor Yellow
    exit 0
}

if (-not $stagedFiles) {
    Write-Host "✅ No staged files to check" -ForegroundColor Green
    exit 0
}

foreach ($file in $stagedFiles) {
    # Check for forbidden file patterns
    foreach ($pattern in $forbiddenPatterns) {
        if ($file -match $pattern) {
            $issues += @{
                Type = 'Forbidden'
                File = $file
                Message = "Forbidden file pattern detected: $pattern"
            }
            continue
        }
    }
    
    # Check file content for sensitive patterns
    try {
        $content = git show ":$file" 2>$null
        
        if ($null -ne $content) {
            foreach ($sensitive in $sensitivePatterns) {
                if ($content -match $sensitive.Pattern) {
                    $issues += @{
                        Type = 'Sensitive'
                        File = $file
                        Message = "$($sensitive.Description) detected"
                    }
                }
            }
        }
    } catch {
        # Skip binary files or files that can't be read
    }
}

# Report findings
if ($issues.Count -gt 0) {
    Write-Host "⚠️  SECURITY ISSUES DETECTED:" -ForegroundColor Red
    Write-Host "`n"
    
    foreach ($issue in $issues) {
        Write-Host "   ❌ [$($issue.Type)] $($issue.File)" -ForegroundColor Red
        Write-Host "      → $($issue.Message)" -ForegroundColor Yellow
    }
    
    Write-Host "`n" + ("="*70) -ForegroundColor Red
    Write-Host "❌ COMMIT BLOCKED: Potential secrets detected!" -ForegroundColor Red
    Write-Host ("="*70) -ForegroundColor Red
    Write-Host "`nWhat to do:" -ForegroundColor Yellow
    Write-Host "  1. Remove sensitive data from the files"
    Write-Host "  2. Use 'dotnet user-secrets' for local development"
    Write-Host "  3. Use Azure Key Vault for production secrets"
    Write-Host "  4. See SECURITY.md for detailed guidelines`n" -ForegroundColor Cyan
    Write-Host "To override (NOT RECOMMENDED):" -ForegroundColor Red
    Write-Host "  git commit --no-verify`n"
    Write-Host ("="*70) -ForegroundColor Red
    
    exit 1
}

Write-Host "✅ No sensitive data detected`n" -ForegroundColor Green
exit 0
