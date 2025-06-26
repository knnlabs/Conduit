# Secret Detection and Pre-commit Hooks

This guide explains how to set up and use Conduit's pre-commit hooks for secret detection to prevent sensitive information from being committed to the repository.

## Overview

Conduit uses [Gitleaks](https://github.com/gitleaks/gitleaks) to automatically scan commits for secrets and sensitive information before they are committed. This prevents API keys, passwords, and other credentials from being accidentally committed to the repository.

## What Gets Detected

The pre-commit hooks scan for:

### LLM Provider API Keys
- OpenAI API keys (`sk-...`)
- Anthropic API keys (`sk-ant-...`)
- Google/Gemini API keys
- Azure OpenAI keys
- Cohere API keys
- Mistral API keys
- Replicate API tokens
- MiniMax API keys

### Cloud & Infrastructure Secrets
- Cloudflare API tokens
- AWS access keys (`AKIA...`)
- Azure subscription keys
- Redis connection strings with passwords
- PostgreSQL/database connection strings
- Docker registry credentials

### Conduit-Specific Secrets
- `CONDUIT_MASTER_KEY` values (except sample values)
- `CONDUIT_WEBUI_AUTH_KEY` values (except sample values)
- JWT signing keys
- Webhook secrets

## Installation

### Prerequisites

```bash
# Install pre-commit
pip install pre-commit

# Or with homebrew on macOS
brew install pre-commit
```

### Setup

1. **Install pre-commit hooks**:
   ```bash
   pre-commit install
   ```

2. **Run on all files** (optional, for initial setup):
   ```bash
   pre-commit run --all-files
   ```

## Configuration Files

The secret detection is configured through two files:

### `.pre-commit-config.yaml`
Defines which tools run during pre-commit, including:
- **Gitleaks** - Secret detection
- **Standard checks** - Trailing whitespace, YAML validation, large files

### `.gitleaks.toml`
Configures Gitleaks behavior:
- **Custom rules** - Conduit-specific secret patterns
- **Allowlist** - Safe values that won't trigger alerts
- **File exclusions** - Documentation and example files

## Sample Values (Safe to Commit)

These sample values are allowlisted and won't trigger secret detection:

- `CONDUIT_MASTER_KEY: alpha` (from docker-compose.yml)
- `CONDUIT_WEBUI_AUTH_KEY: conduit123` (from docker-compose.yml)

**Important**: These are for Docker testing only. Use strong, unique keys in production.

## How It Works

1. **On every commit**: Pre-commit hooks run automatically
2. **Secret detection**: Gitleaks scans staged files for patterns
3. **Commit blocked**: If secrets are found, the commit is rejected
4. **Fix required**: Remove or replace secrets, then commit again

## Example: Blocked Commit

```bash
$ git commit -m "Add API configuration"

Detect secrets....................................................Failed
- hook id: gitleaks
- exit code: 1

Finding: CONDUIT_MASTER_KEY=sk-1234567890abcdef1234567890abcdef
Secret:  sk-1234567890abcdef1234567890abcdef
RuleID:  conduit-master-key
Entropy: 4.8
File:    config/production.env
Line:    3
```

## Fixing Secret Detection Issues

### 1. Remove the Secret
```bash
# Remove the secret from the file
vim config/production.env

# Stage the fix
git add config/production.env

# Commit again
git commit -m "Add API configuration"
```

### 2. Use Environment Variables
```bash
# Instead of hardcoding:
CONDUIT_MASTER_KEY=sk-1234567890abcdef1234567890abcdef

# Use reference:
CONDUIT_MASTER_KEY=${CONDUIT_MASTER_KEY}
```

### 3. Add to .gitignore
```bash
# Add sensitive files to .gitignore
echo "config/production.env" >> .gitignore
```

## Manual Testing

### Test Current Repository
```bash
# Install gitleaks locally
curl -sSfL https://raw.githubusercontent.com/gitleaks/gitleaks/master/scripts/install.sh | sh -s -- -b /usr/local/bin

# Run detection
gitleaks detect --config .gitleaks.toml --verbose
```

### Test Specific Files
```bash
# Scan specific files
gitleaks detect --config .gitleaks.toml --source=path/to/file.txt
```

## Bypassing (Not Recommended)

In rare cases where you need to commit a pattern that looks like a secret but isn't:

```bash
# Skip pre-commit hooks (NOT RECOMMENDED)
git commit --no-verify -m "Emergency fix"
```

**Warning**: Only use `--no-verify` in genuine emergencies. Always review what you're committing.

## Troubleshooting

### Hook Not Running
```bash
# Reinstall hooks
pre-commit uninstall
pre-commit install

# Verify installation
pre-commit --version
```

### False Positives
If legitimate code is being flagged:

1. **Check the pattern**: Ensure it's not actually a secret
2. **Update allowlist**: Add the pattern to `.gitleaks.toml`
3. **Use environment variables**: Reference secrets instead of hardcoding

### Performance Issues
```bash
# Skip hooks for large commits (temporary)
git commit --no-verify -m "Large refactor"

# Then run detection manually
pre-commit run --all-files
```

## Integration with CI/CD

The pre-commit hooks complement Conduit's existing security measures:

- **CodeQL scanning** - Analyzes code for security vulnerabilities
- **Build-time checks** - Blocks Docker builds with high-severity issues
- **Runtime security** - IP filtering, rate limiting, authentication

## Best Practices

1. **Use environment variables** for all secrets
2. **Never commit** `.env` files with real values
3. **Use sample values** in documentation and examples
4. **Rotate secrets** if accidentally committed
5. **Review git history** before pushing to remote

## Additional Resources

- [Gitleaks Documentation](https://github.com/gitleaks/gitleaks)
- [Pre-commit Framework](https://pre-commit.com/)
- [Conduit Security Guidelines](./Security-Guidelines.md)
- [Environment Variables Guide](./Environment-Variables.md)

## Contributing

When adding new secret patterns:

1. **Test thoroughly** to avoid false positives
2. **Update allowlist** for legitimate sample values
3. **Document new patterns** in this guide
4. **Consider entropy thresholds** for high-entropy detection