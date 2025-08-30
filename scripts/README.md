# Scripts Directory

This directory contains utility scripts for development, testing, and maintenance of the Conduit LLM project.

## Script Categories

### 🚀 Development & Build

- **`start-dev.sh`** - **PRIMARY**: Start development environment with hot reloading and proper user permissions
- **`dev-workflow.sh`** - Advanced development workflow commands (logs, shell, build-webui, lint-fix-webui)
- **`docker-build.sh`** - Build Docker images for production
- **`ci-build-test.sh`** - CI/CD build and test pipeline
- **`auto-version.sh`** - Automatic version management
- **`wait-for-services.sh`** - Wait for dependent services to be ready

### 🔧 Code Quality & Linting

- **`ts-lint.sh`** - **QUICK**: Fast lint check for WebUI and SDKs (no fixes, just status)
- **`validate-eslint.sh`** - **UNIFIED**: ESLint validation with normal/strict modes
  - Normal mode: `./scripts/validate-eslint.sh`
  - Strict mode: `./scripts/validate-eslint.sh --strict`
- **`validate-eslint-strict.sh`** - **WRAPPER**: Calls unified script in strict mode (for CI/CD compatibility)
- **`fix-sdk-errors.sh`** - **CONSOLIDATED**: Fix ESLint errors and build SDK clients
  - All SDKs: `./scripts/fix-sdk-errors.sh`
  - Admin only: `./scripts/fix-sdk-errors.sh admin`
  - Core only: `./scripts/fix-sdk-errors.sh core`
- **`fix-lint-errors.sh`** - Fix ESLint errors across all projects
- **`fix-webui-errors.sh`** - Fix WebUI-specific errors
- **`fix-all-log-injection.sh`** - Fix log injection vulnerabilities
- **`fix-log-injection-inline.sh`** - Fix log injection inline

### 🔐 Security & Code Analysis

- **`run-codeql-security-scan.sh`** - Run CodeQL security analysis
- **`verify-codeql-fix.md`** - CodeQL fix verification guide
- **`analyze-codeql-pattern.md`** - CodeQL pattern analysis guide
- **`clear-blocked-ips.sh`** - Clear blocked IP addresses

### 🔑 Virtual Key Management

- **`get-master-key.sh`** - Retrieve the master API key
- **`get-webui-virtual-key.sh`** - **PRIMARY**: Get existing WebUI key or regenerate if needed
- **`create-webui-key.sh`** - Create new WebUI virtual key (if none exists)
- **`fix-webui-virtual-key.sh`** - **EMERGENCY**: Force create new WebUI key and update database
- **`create-test-virtual-key.sh`** - Create temporary virtual keys for testing

### 🧪 Testing & Connectivity

- **`test-signalr-connection.sh`** - Full Node.js SignalR client connection test
- **`test-signalr-negotiate.sh`** - Simple SignalR negotiate endpoint test
- **`test-webui-connection.sh`** - Test WebUI connectivity
- **`tests.sh`** - Run project test suites

### 📊 Code Coverage & Metrics

- **`check-coverage-info.sh`** - Check code coverage information
- **`check-coverage-thresholds.sh`** - Validate coverage meets thresholds
- **`coverage-dashboard.sh`** - Generate coverage dashboard
- **`generate-coverage-badges.sh`** - Generate coverage badges
- **`find-large-source-files.sh`** - Find source files exceeding size limits

### 🔄 API Client Generation

- **`generate-api-clients.sh`** - Generate API clients from OpenAPI specs

### 🗄️ Database Migrations

Located in `scripts/migrations/`:

- **`ef-wrapper.sh`** - Entity Framework wrapper script
- **`validate-migrations.sh`** - Validate migration files
- **`validate-postgresql-syntax.sh`** - **CRITICAL**: Validate PostgreSQL syntax
- **`reset-dev-migrations.sh`** - Reset development migrations
- **`fix-production-migrations.sh`** - Fix production migration issues
- **`clean-build-artifacts.sh`** - Clean build artifacts
- **`test-migration-tools.sh`** - Test migration tooling

### 📂 Data & SQL Scripts

- **`seed-audio-costs-providertype.sql`** - Seed audio costs with provider types
- **`test-audio-provider-migration.sql`** - Test audio provider migration
- **`validate-audio-provider-data.sql`** - Validate audio provider data

## Usage Guidelines

### Development Workflow

**Always use `start-dev.sh` for development:**
```bash
# Start development environment
./scripts/start-dev.sh

# Clean restart if issues
./scripts/start-dev.sh --clean

# Force rebuild
./scripts/start-dev.sh --build
```

### Code Quality Checks

**Quick status check:**
```bash
# Fast lint status for WebUI and SDKs
./scripts/ts-lint.sh
```

**Before committing:**
```bash
# Check for lint errors (normal mode)
./scripts/validate-eslint.sh

# Fix auto-fixable issues
./scripts/fix-lint-errors.sh

# Strict validation (CI/CD mode)
./scripts/validate-eslint.sh --strict
```

### SDK Development

**After API changes:**
```bash
# Fix and build all SDKs
./scripts/fix-sdk-errors.sh

# Fix specific SDK only
./scripts/fix-sdk-errors.sh admin
./scripts/fix-sdk-errors.sh core
```

### Virtual Key Management

**WebUI key management flow:**
```bash
# Get existing key (or regenerate if missing)
./scripts/get-webui-virtual-key.sh

# Create new key (fails if exists)
./scripts/create-webui-key.sh

# Force fix key and database (emergency)
./scripts/fix-webui-virtual-key.sh
```

### Database Migrations

**Always validate PostgreSQL syntax:**
```bash
# After creating migrations
./scripts/migrations/validate-postgresql-syntax.sh
```

## Script Naming Conventions

- **Primary scripts**: Named for their main function (`start-dev.sh`, `validate-eslint.sh`)
- **Wrappers**: Maintain backward compatibility (`validate-eslint-strict.sh`)
- **Specialized scripts**: Clear purpose indication (`fix-webui-virtual-key.sh`)
- **Test scripts**: Prefixed with `test-` (`test-signalr-connection.sh`)

## Deprecated Scripts

The following scripts have been removed or consolidated:

- ❌ `fix-admin-SDK-errors.sh` → Use `fix-sdk-errors.sh admin`
- ❌ `fix-core-SDK-errors.sh` → Use `fix-sdk-errors.sh core`

## CI/CD Integration

Scripts used by CI/CD pipelines:

- **`ci-build-test.sh`** - Main CI/CD build and test
- **`validate-eslint-strict.sh`** - Lint validation (fails on any errors)
- **`run-codeql-security-scan.sh`** - Security scanning
- **Pre-push hook** - Uses `validate-eslint-strict.sh`

## Security Notes

- Scripts that interact with databases require running services
- Master key retrieval scripts should only be used in secure environments
- Virtual key scripts handle sensitive authentication data
- Log injection fix scripts address security vulnerabilities

## Troubleshooting

### Permission Issues
- Use `start-dev.sh --clean` to fix Docker volume permissions
- Ensure scripts are executable: `chmod +x scripts/*.sh`

### Service Dependencies
- Many scripts require services to be running via `start-dev.sh`
- Use `wait-for-services.sh` when scripting dependent operations

### Database Issues
- Always run PostgreSQL syntax validation after creating migrations
- Use migration validation scripts before deploying changes

---

**Last Updated**: 2025-08-01
**Maintained By**: Development Team

For issues or questions about these scripts, see the main project documentation or create an issue in the repository.