# Entity Framework Core Migration Strategy for Conduit

## Overview
This document outlines a robust migration strategy to prevent the recurring issues with EF Core migrations in the Conduit project.

## Current Issues
1. **Compiled Migration Conflicts**: Old migrations remain in compiled assemblies even after source files are deleted
2. **Migration Order Dependencies**: EF Core applies migrations in chronological order, making consolidation difficult
3. **Development vs Production Mismatch**: Different migration states between environments
4. **Docker Build Complexity**: Cached layers can retain old migration metadata

## Migration Development Workflow

### 1. Feature Branch Development
```bash
# Create feature branch
git checkout -b feature/your-feature

# Add migration
cd ConduitLLM.Configuration
dotnet ef migrations add YourMigrationName

# Test locally
docker-compose up -d
```

### 2. Migration Consolidation (Major Releases Only)
When consolidating migrations for a major release:

```bash
# 1. Export current schema
dotnet ef migrations script -o current-schema.sql

# 2. Remove all migrations
rm -rf Migrations/*

# 3. Clean build artifacts
find . -name "bin" -o -name "obj" | xargs rm -rf
dotnet nuget locals all --clear

# 4. Create new initial migration
dotnet ef migrations add InitialSchema

# 5. For existing databases, manually sync
# See migration-sync.sql template
```

### 3. Development Environment Reset
Use the provided script for development resets:
```bash
./scripts/reset-dev-migrations.sh
```

## Migration Policies

### DO:
- ✅ Create migrations in feature branches
- ✅ Test migrations with fresh database before merging
- ✅ Document breaking changes in migration comments
- ✅ Use idempotent migration scripts for production
- ✅ Backup databases before major migration changes

### DON'T:
- ❌ Delete migration files without proper cleanup
- ❌ Consolidate migrations in active development
- ❌ Mix schema and data migrations
- ❌ Apply migrations manually in production
- ❌ Share development databases between team members

## Production Migration Process

### 1. Generate Migration Bundle
```bash
dotnet ef migrations bundle --self-contained -r linux-x64
```

### 2. Test Migration
```bash
# Test on staging environment first
./efbundle --connection "your-staging-connection"
```

### 3. Production Deployment
```bash
# During maintenance window
./efbundle --connection "your-production-connection"
```

## Emergency Procedures

### Migration Conflict Resolution
If you encounter "relation already exists" errors:

1. **Check Migration History**:
```sql
SELECT * FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
```

2. **Compare with Pending Migrations**:
```bash
dotnet ef migrations list
```

3. **Manual Resolution**:
```sql
-- Mark specific migration as applied
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") 
VALUES ('MigrationId', '9.0.5');
```

### Rollback Procedure
```bash
# Generate down script
dotnet ef migrations script MigrationTo MigrationFrom -o rollback.sql

# Apply manually with transaction
psql -d conduitdb -f rollback.sql
```

## Monitoring and Health Checks

The system includes migration health checks at:
- `/health/migrations` - Migration status endpoint
- `/admin/migrations` - Migration management UI (coming soon)

## Team Guidelines

1. **Communication**: Announce migration changes in team chat
2. **Documentation**: Update this document with lessons learned
3. **Review**: All migrations require code review
4. **Testing**: Include migration tests in PR

## Tools and Scripts

Located in `/scripts/migrations/`:
- `reset-dev-migrations.sh` - Reset development environment
- `validate-migrations.sh` - CI/CD validation
- `generate-migration-report.sh` - Migration status report

## References
- [EF Core Migrations Documentation](https://docs.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [Migration Bundles](https://docs.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying#bundles)
- [Team Development](https://docs.microsoft.com/en-us/ef/core/managing-schemas/migrations/teams)