# EF Core Migration Tools

This directory contains tools for validating and managing Entity Framework Core migrations in the Conduit project.

## Problem Statement

The EF Core migration validation was failing in GitHub Actions because:
1. The `DATABASE_URL` environment variable wasn't being passed to the `dotnet ef migrations script` command
2. Error messages weren't clear about what was wrong
3. Environment setup was inconsistent across workflow steps

## Solution

We've created a robust, maintainable solution using the existing .NET toolchain:

### 1. **GitHub Actions Workflow Fix** (`migration-validation.yml`)
- Moved `DATABASE_URL` to job-level environment variables for consistency
- Added pre-flight validation step to catch missing configuration early
- Removed duplicate environment variable declarations from individual steps

### 2. **EF Wrapper Script** (`ef-wrapper.sh`)
- Validates environment before running EF commands
- Provides clear, colored output with detailed error messages
- Tests database connectivity
- Analyzes common error patterns and suggests fixes

### 3. **Enhanced Validation Script** (`validate-migrations.sh`)
- Already worked well, now integrates with ef-wrapper for better error handling
- Validates migration files, checks for duplicates, and detects pending changes
- Falls back gracefully when database is unavailable

### 4. **Comprehensive Test Suite** (`test-migration-tools.sh`)
- Tests all components in various scenarios
- Validates error handling and edge cases
- Ensures scripts provide helpful feedback

## Usage

### Running Migration Validation
```bash
# Basic validation
./scripts/migrations/validate-migrations.sh

# Check for pending model changes (CI mode)
./scripts/migrations/validate-migrations.sh --check-pending

# Generate migration script
./scripts/migrations/validate-migrations.sh --generate-script
```

### Using the EF Wrapper
```bash
cd ConduitLLM.Configuration

# List migrations with enhanced error handling
../scripts/migrations/ef-wrapper.sh migrations list

# Generate migration script
../scripts/migrations/ef-wrapper.sh migrations script -o output.sql

# Add a new migration
../scripts/migrations/ef-wrapper.sh migrations add MigrationName
```

### Testing the Tools
```bash
# Run comprehensive test suite
./scripts/migrations/test-migration-tools.sh
```

## Environment Requirements

- `DATABASE_URL`: PostgreSQL connection string (required)
  - Format: `postgresql://user:password@host:port/database`
- .NET 9.0 SDK
- EF Core tools: `dotnet tool install --global dotnet-ef`

## Why Not Python?

When challenged to think critically about the solution, we determined that Python would be overengineering because:

1. **Existing Tools Work Well**: The bash scripts and dotnet-ef tools are sufficient
2. **Root Cause Was Simple**: Missing environment variable in one workflow step
3. **Stay in Ecosystem**: Adding Python introduces unnecessary complexity to a .NET project
4. **Better Error Handling**: We can enhance existing tools rather than rewrite them

## Key Features

1. **Consistent Environment Handling**: Job-level variables in GitHub Actions
2. **Clear Error Messages**: Wrapper script provides context and solutions
3. **Graceful Degradation**: Scripts work even when database is unavailable
4. **Comprehensive Testing**: Test suite validates all components
5. **No Over-Engineering**: Simple, maintainable solution using existing tools

## Troubleshooting

### "DATABASE_URL environment variable is not set"
Set the DATABASE_URL:
```bash
export DATABASE_URL="postgresql://user:password@localhost:5432/conduitdb"
```

### "Not in ConduitLLM.Configuration directory"
Navigate to the correct directory:
```bash
cd ConduitLLM.Configuration
```

### "EF Core tools not installed"
Install the tools:
```bash
dotnet tool install --global dotnet-ef
```

## Maintenance

The solution is designed to be maintainable:
- Scripts use clear variable names and comments
- Error messages guide users to solutions
- Test suite ensures changes don't break functionality
- No external dependencies beyond .NET ecosystem