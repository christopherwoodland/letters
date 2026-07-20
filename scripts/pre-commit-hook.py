#!/usr/bin/env python3
"""
Git pre-commit hook to prevent committing sensitive information.
This script checks for common patterns that shouldn't be in version control.

Usage:
  1. Copy this file to .git/hooks/pre-commit
  2. Make it executable: chmod +x .git/hooks/pre-commit
  3. It will run automatically before each commit
"""

import re
import sys
import subprocess

# Patterns to detect sensitive information
PATTERNS = {
    r'api[_-]?key["\']?\s*[:=]': 'API Key',
    r'password["\']?\s*[:=]': 'Password',
    r'secret["\']?\s*[:=]': 'Secret',
    r'token["\']?\s*[:=]': 'Token',
    r'connection[_-]?string["\']?\s*[:=]': 'Connection String',
    r'private[_-]?key': 'Private Key',
    r'-----BEGIN (RSA|DSA|EC|PGP|OPENSSH) PRIVATE KEY': 'Private Key Block',
    r'akia[0-9a-z]{16}': 'AWS Access Key',
    r'[0-9a-z]{40}': 'Potential GitHub Token',
    r'SG\.[a-zA-Z0-9_-]{20,}': 'SendGrid API Key',
    r'sk_live_[0-9a-zA-Z]{24}': 'Stripe Secret Key',
}

# Files that should never be committed
FORBIDDEN_FILES = [
    r'.*\.env.*',
    r'.*secrets\.json',
    r'.*appsettings\.Development\.json',
    r'.*appsettings\.Local\.json',
    r'\.aws/credentials',
    r'\.aws/config',
    r'azure-credentials\.json',
    r'.*\.pem',
    r'.*\.key',
    r'.*\.p12',
    r'.*\.pfx',
    r'input/.*',
    r'\.vscode/settings\.json',
]

def check_staged_files():
    """Check staged files for sensitive content."""
    try:
        # Get staged files
        result = subprocess.run(
            ['git', 'diff', '--cached', '--name-only'],
            capture_output=True,
            text=True,
            check=True
        )
        staged_files = result.stdout.strip().split('\n')
        
        issues = []
        
        for filepath in staged_files:
            if not filepath:
                continue
                
            # Check if file matches forbidden patterns
            for pattern in FORBIDDEN_FILES:
                if re.match(pattern, filepath, re.IGNORECASE):
                    issues.append(f"❌ Forbidden file type detected: {filepath}")
                    continue
            
            # Check file content for sensitive patterns
            try:
                result = subprocess.run(
                    ['git', 'show', f':{filepath}'],
                    capture_output=True,
                    text=True,
                    check=False
                )
                
                if result.returncode == 0:
                    content = result.stdout
                    for pattern, description in PATTERNS.items():
                        if re.search(pattern, content, re.IGNORECASE):
                            issues.append(f"❌ {description} detected in {filepath}")
            except Exception as e:
                # Skip binary files or files that can't be read
                pass
        
        return issues

def main():
    """Main pre-commit hook logic."""
    print("🔍 Running security checks...\n")
    
    issues = check_staged_files()
    
    if issues:
        print("⚠️  SECURITY ISSUES DETECTED:\n")
        for issue in issues:
            print(f"   {issue}")
        
        print("\n" + "="*70)
        print("❌ COMMIT BLOCKED: Potential secrets detected!\n")
        print("What to do:")
        print("  1. Remove sensitive data from the files")
        print("  2. Use 'dotnet user-secrets' for local development")
        print("  3. Use Azure Key Vault for production secrets")
        print("  4. See SECURITY.md for detailed guidelines\n")
        print("To override (NOT RECOMMENDED):")
        print("  git commit --no-verify\n")
        print("="*70)
        return 1
    
    print("✅ No sensitive data detected\n")
    return 0

if __name__ == '__main__':
    sys.exit(main())
