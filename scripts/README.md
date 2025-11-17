# Scripts Directory

This directory contains utility scripts for development, testing, and maintenance of the Conduit LLM project.

## Directory Structure

Scripts are organized into the following subdirectories:

- **`dev/`** - Development workflow and environment setup scripts
- **`test/`** - Testing, validation, and code quality scripts
- **`setup/`** - Service setup and initialization scripts
- **`security/`** - Security scanning and analysis tools
- **`migrations/`** - Database migration utilities

## Script Categories

### 🚀 Development & Build (`dev/`)

- **`start-dev.sh`** - **PRIMARY**: Start development environment with hot reloading and proper user permissions
  ```bash
  ./scripts/dev/start-dev.sh              # Standard startup
  ./scripts/dev/start-dev.sh --clean      # Clean restart
  ./scripts/dev/start-dev.sh --build      # Force rebuild
  ./scripts/dev/start-dev.sh --webui      # Rebuild WebUI container
  ```
- **`dev-workflow.sh`** - Advanced development workflow commands (logs, shell, build-webui, lint-fix-webui)
- **`fix-sdk-errors.sh`** - **CONSOLIDATED**: Fix ESLint errors and build SDK clients
  - All SDKs: `./scripts/dev/fix-sdk-errors.sh`
  - Admin only: `./scripts/dev/fix-sdk-errors.sh admin`
  - Core only: `./scripts/dev/fix-sdk-errors.sh core`
- **`fix-webadmin-errors.sh`** - Fix WebUI-specific lint and build errors
- **`setup-r2-dev.sh`** - Setup Cloudflare R2 storage for development

### 🔑 Virtual Key Management (`dev/`)

- **`get-webadmin-virtual-key.sh`** - **PRIMARY**: Get existing WebUI key or regenerate if needed
- **`create-webadmin-key.sh`** - Create new WebUI virtual key (if none exists)
- **`create-test-virtual-key.sh`** - Create temporary virtual keys for testing
- **`clear-blocked-ips.sh`** - Clear blocked IP addresses from Redis and database

### 🧪 Testing & Validation (`test/`)

- **`tests.sh`** - Run all project test suites
- **`quick-verify-tests.sh`** - Fast test verification for CI/CD
- **`ci-build-test.sh`** - CI/CD build and test pipeline
- **`cleanup-test-data.sh`** - Clean up test data from database
- **`test-codeql.sh`** - CodeQL security testing (local validation)
- **`check-typescript.sh`** - Comprehensive TypeScript error checking across all projects

### 🔧 Code Quality & Linting (`test/`)

- **`validate-eslint.sh`** - **UNIFIED**: ESLint validation with normal/strict modes
  - Normal mode: `./scripts/test/validate-eslint.sh`
  - Strict mode: `./scripts/test/validate-eslint.sh --strict`
- **`validate-eslint-strict.sh`** - **WRAPPER**: Calls unified script in strict mode (for CI/CD compatibility)
- **`validate-workflows.sh`** - Validate GitHub Actions workflow files

### 📊 Code Coverage & Metrics (`test/`)

- **`check-coverage-info.sh`** - Check code coverage information (non-blocking)
- **`check-coverage-thresholds.sh`** - Validate coverage meets thresholds
- **`coverage-dashboard.sh`** - Generate comprehensive coverage dashboard
  - `./scripts/test/coverage-dashboard.sh run` - Run tests and generate reports
  - `./scripts/test/coverage-dashboard.sh summary` - Show summary from existing reports
- **`generate-coverage-badges.sh`** - Generate coverage badges for README

### 🔐 Security & Code Analysis (`security/`)

- **`run-codeql-security-scan.sh`** - Interactive CodeQL security analysis tool
  - Full security scan with comprehensive reporting
  - Quick log injection scan
  - Database creation and management

### ⚙️ Setup & Initialization (`setup/`)

- **`wait-for-services.sh`** - Wait for dependent services to be ready
  - Used by other scripts to ensure services are healthy before operations

### 🗄️ Database Migrations (`migrations/`)

- **`ef-wrapper.sh`** - Entity Framework wrapper script
- **`validate-migrations.sh`** - Validate migration files
- **`reset-dev-migrations.sh`** - Reset development migrations
- **`fix-production-migrations.sh`** - Fix production migration issues
- **`clean-build-artifacts.sh`** - Clean build artifacts
- **`test-migration-tools.sh`** - Test migration tooling
- **`seed-model-data-updated.sql`** - SQL script for seeding model data

## Usage Guidelines

### Development Workflow

**Always use `start-dev.sh` for development:**
```bash
# Start development environment
./scripts/dev/start-dev.sh

# Clean restart if issues
./scripts/dev/start-dev.sh --clean

# Force rebuild
./scripts/dev/start-dev.sh --build

# Rebuild WebUI container only
./scripts/dev/start-dev.sh --webui
```

### Code Quality Checks

**Before committing:**
```bash
# Check for lint errors (normal mode)
./scripts/test/validate-eslint.sh

# Strict validation (CI/CD mode)
./scripts/test/validate-eslint.sh --strict

# Check TypeScript errors across all projects
./scripts/test/check-typescript.sh
```

### SDK Development

**After API changes:**
```bash
# Fix and build all SDKs
./scripts/dev/fix-sdk-errors.sh

# Fix specific SDK only
./scripts/dev/fix-sdk-errors.sh admin
./scripts/dev/fix-sdk-errors.sh core
```

### Virtual Key Management

**WebUI key management flow:**
```bash
# Get existing key (or regenerate if missing)
./scripts/dev/get-webadmin-virtual-key.sh

# Create new key (fails if exists)
./scripts/dev/create-webadmin-key.sh

# Create test keys for development
./scripts/dev/create-test-virtual-key.sh
```

### Testing

**Run tests:**
```bash
# Run all tests
./scripts/test/tests.sh

# Quick CI-style verification
./scripts/test/quick-verify-tests.sh

# Full CI/CD build and test
./scripts/test/ci-build-test.sh
```

**Coverage analysis:**
```bash
# Run tests and generate coverage reports
./scripts/test/coverage-dashboard.sh run

# Show coverage summary
./scripts/test/coverage-dashboard.sh summary

# Generate badges
./scripts/test/generate-coverage-badges.sh
```

### Security Scanning

**Run CodeQL analysis:**
```bash
# Interactive security scan
./scripts/security/run-codeql-security-scan.sh

# Follow prompts to choose scan type and options
```

### Database Migrations

**Follow standard EF Core workflow:**
```bash
# Create migration
cd ConduitLLM.Configuration  # or from root
dotnet ef migrations add MigrationName

# Apply migration
dotnet ef database update

# Validate migrations
./scripts/migrations/validate-migrations.sh
```

## Script Naming Conventions

- **Primary scripts**: Named for their main function (`start-dev.sh`, `validate-eslint.sh`)
- **Wrappers**: Maintain backward compatibility (`validate-eslint-strict.sh`)
- **Specialized scripts**: Clear purpose indication (`fix-webadmin-errors.sh`)
- **Test scripts**: Prefixed with `test-` (`test-codeql.sh`)

## Path Updates

**IMPORTANT**: Scripts have been reorganized into subdirectories. Update your references:

### Old Paths → New Paths
```bash
# Development
./scripts/start-dev.sh → ./scripts/dev/start-dev.sh
./scripts/dev-workflow.sh → ./scripts/dev/dev-workflow.sh
./scripts/fix-sdk-errors.sh → ./scripts/dev/fix-sdk-errors.sh
./scripts/fix-webadmin-errors.sh → ./scripts/dev/fix-webadmin-errors.sh

# Testing
./scripts/tests.sh → ./scripts/test/tests.sh
./scripts/ci-build-test.sh → ./scripts/test/ci-build-test.sh
./scripts/validate-eslint.sh → ./scripts/test/validate-eslint.sh
./scripts/coverage-dashboard.sh → ./scripts/test/coverage-dashboard.sh

# Setup
./scripts/wait-for-services.sh → ./scripts/setup/wait-for-services.sh

# Security
./scripts/run-codeql-security-scan.sh → ./scripts/security/run-codeql-security-scan.sh
```

## CI/CD Integration

Scripts used by CI/CD pipelines:

- **`test/ci-build-test.sh`** - Main CI/CD build and test
- **`test/validate-eslint-strict.sh`** - Lint validation (fails on any errors)
- **`security/run-codeql-security-scan.sh`** - Security scanning
- **Pre-push hook** - Uses `test/validate-eslint-strict.sh`

## Security Notes

- Scripts that interact with databases require running services
- Virtual key scripts handle sensitive authentication data
- Use security scanning tools regularly to detect vulnerabilities
- CodeQL analysis should be run before major releases

## Troubleshooting

### Permission Issues
- Use `./scripts/dev/start-dev.sh --clean` to fix Docker volume permissions
- Ensure scripts are executable: `chmod +x scripts/**/*.sh`

### Service Dependencies
- Many scripts require services to be running via `./scripts/dev/start-dev.sh`
- Use `./scripts/setup/wait-for-services.sh` when scripting dependent operations

### Database Issues
- Always run PostgreSQL syntax validation after creating migrations
- Use migration validation scripts before deploying changes

### Path Issues
- If a script references another script, update paths to use subdirectories
- Check for hardcoded script paths in GitHub Actions workflows

---

**Last Updated**: 2025-11-08
**Maintained By**: Development Team

For issues or questions about these scripts, see the main project documentation or create an issue in the repository.
