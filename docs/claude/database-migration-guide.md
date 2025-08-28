# Database Migration Guide

**CRITICAL**: This guide will save you hours of debugging. Read it BEFORE making any database changes.

## Overview

Conduit uses Entity Framework Core with PostgreSQL exclusively. We've had numerous migration failures due to SQL Server syntax sneaking into our PostgreSQL-only codebase. This guide ensures that never happens again.

## Before You Start

### Environment Setup
```bash
# Required: Set DATABASE_URL for EF Core tools
export DATABASE_URL='postgresql://user:password@localhost:5432/conduitdb'
```

## Creating a New Migration

### Step 1: Make Your Model Changes
Edit your entity classes or `ConfigurationDbContext.cs` as needed.

### Step 2: CRITICAL - Check Your Syntax
**PostgreSQL Boolean Syntax**:
```csharp
// ❌ WRONG - SQL Server syntax
entity.HasIndex(e => e.Region).IsUnique().HasFilter("IsActive = 1");

// ✅ CORRECT - PostgreSQL syntax
entity.HasIndex(e => e.Region).IsUnique().HasFilter("\"IsActive\" = true");
```

**Avoid Raw SQL in Filters**:
- EF Core's `HasFilter()` only accepts strings, not LINQ expressions
- Always use PostgreSQL-compatible syntax in filter strings
- Quote column names: `"ColumnName"` not `[ColumnName]`

### Step 3: Generate the Migration
```bash
cd ConduitLLM.Configuration
DATABASE_URL='postgresql://user:password@localhost:5432/conduitdb' dotnet ef migrations add YourMigrationName --no-build
```

### Step 4: IMMEDIATELY Validate the Migration
```bash
# From repository root
./scripts/migrations/validate-postgresql-syntax.sh
```

If validation fails, remove the migration and fix the issue:
```bash
DATABASE_URL='postgresql://user:password@localhost:5432/conduitdb' dotnet ef migrations remove --no-build
```

### Step 5: Review Generated Files
Check these files for SQL Server syntax:
1. `Migrations/[Timestamp]_YourMigrationName.cs`
2. `Migrations/ConfigurationDbContextModelSnapshot.cs`

Look for:
- `= 1` or `= 0` (should be `= true` or `= false`)
- `[ColumnName]` (should be `"ColumnName"`)
- `nvarchar`, `varchar(max)`, `bit` (SQL Server types)

### Step 6: Test Locally
```bash
# Start a local PostgreSQL container
docker run -d --name conduit-postgres \
  -e POSTGRES_USER=conduit \
  -e POSTGRES_PASSWORD=conduitpass \
  -e POSTGRES_DB=conduitdb \
  -p 5432:5432 \
  postgres:16

# Apply migrations
export DATABASE_URL='postgresql://conduit:conduitpass@localhost:5432/conduitdb'
cd ConduitLLM.Configuration
dotnet ef database update
```

### Step 7: Build and Verify
```bash
# From repository root
dotnet build
```

## Column Renaming

### CRITICAL: EF Core Column Rename Process

When renaming a column, EF Core CANNOT detect your intent and will generate DROP/ADD operations that cause DATA LOSS. You MUST manually intervene:

1. **Understand the Current State**:
   - Check database actual column name: `\d "TableName"` in psql
   - Check model's `[Column("name")]` attribute
   - Check snapshot's `.HasColumnName("name")` configuration
   - If these don't match, you have a synchronization issue

2. **Fix Synchronization Issues First**:
   ```csharp
   // If database has "ApiParameters" but model expects "Parameters":
   // Temporarily change model to match database
   [Column("ApiParameters")] // Match current database
   public string? ModelParameters { get; set; }
   ```

3. **Create Migration with Manual Edit**:
   ```bash
   # Generate migration
   DATABASE_URL='...' dotnet ef migrations add RenameColumnName --no-build
   
   # Migration will likely be empty or have DROP/ADD
   # MANUALLY replace with RenameColumn:
   ```
   
   ```csharp
   protected override void Up(MigrationBuilder migrationBuilder)
   {
       migrationBuilder.RenameColumn(
           name: "OldColumnName",
           table: "TableName",
           newName: "NewColumnName");
   }
   
   protected override void Down(MigrationBuilder migrationBuilder)
   {
       migrationBuilder.RenameColumn(
           name: "NewColumnName",
           table: "TableName",
           newName: "OldColumnName");
   }
   ```

4. **Update Model After Creating Migration**:
   ```csharp
   // Now update model to final state
   [Column("Parameters")] // Final desired name
   public string? ModelParameters { get; set; }
   ```

5. **Validate and Apply**:
   ```bash
   # Validate syntax
   ./scripts/migrations/validate-postgresql-syntax.sh
   
   # Apply migration
   DATABASE_URL='...' dotnet ef database update
   
   # Verify in database
   docker exec conduit-postgres-1 psql -U conduit -d conduitdb -c "\d \"TableName\""
   ```

### Common Pitfalls to Avoid

1. **Never trust EF Core to detect renames** - It will DROP and recreate, losing data
2. **Snapshot out of sync** - If someone edits the snapshot without a migration, future migrations break
3. **NotMapped properties accessing navigation properties** - Can cause EF to generate invalid SQL
4. **Column attribute mismatch** - `[Column("name")]` must match actual database column

## Common PostgreSQL-Specific Patterns

### Boolean Columns
```csharp
// Entity definition
public bool IsActive { get; set; } = true;

// Index filter - PostgreSQL syntax
.HasFilter("\"IsActive\" = true");
```

### String Length
```csharp
// PostgreSQL uses 'text' for unlimited strings
entity.Property(e => e.Description).HasColumnType("text");

// Or character varying for limited length
entity.Property(e => e.Name).HasMaxLength(100); // becomes character varying(100)
```

### Timestamps
```csharp
// Always use timestamp with time zone for PostgreSQL
entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");
```

### Arrays (PostgreSQL-specific feature)
```csharp
// PostgreSQL supports array columns
entity.Property(e => e.Tags).HasColumnType("text[]");
```

## Pre-Push Validation

A git pre-push hook automatically validates migrations before they can be pushed:
- Location: `.git/hooks/pre-push`
- Runs: `./scripts/migrations/validate-postgresql-syntax.sh`
- Prevents: Pushing migrations with SQL Server syntax

## CI/CD Validation

The GitHub Actions workflow `migration-validation.yml`:
1. Spins up a PostgreSQL container
2. Applies all migrations
3. Validates PostgreSQL syntax
4. Generates migration scripts for review

## Troubleshooting

### "column does not exist" Error
Usually means you're using SQL Server case-insensitive syntax. PostgreSQL is case-sensitive:
```sql
-- ❌ Wrong
WHERE IsActive = 1

-- ✅ Correct  
WHERE "IsActive" = true
```

### "syntax error at or near" Error
Check for SQL Server specific syntax:
- Square brackets: `[Table].[Column]`
- Integer booleans: `1` or `0` instead of `true`/`false`
- TOP clause instead of LIMIT

### Migration Already Applied
If a migration with bad syntax was already applied:
1. Create a new migration to fix the issue
2. Drop and recreate the affected index/constraint
3. Never modify existing migration files

## Golden Rules

1. **Always validate migrations** before committing
2. **Never use SQL Server syntax** - we're PostgreSQL only
3. **Test against real PostgreSQL** - not just build verification
4. **Use the validation script** - it catches common mistakes
5. **Quote identifiers** when using raw SQL in filters

## Quick Reference

```bash
# Create migration
DATABASE_URL='...' dotnet ef migrations add MigrationName --no-build

# Validate syntax
./scripts/migrations/validate-postgresql-syntax.sh

# Remove bad migration
DATABASE_URL='...' dotnet ef migrations remove --no-build

# Apply migrations
DATABASE_URL='...' dotnet ef database update

# Generate SQL script
DATABASE_URL='...' dotnet ef migrations script
```

Remember: A few minutes of validation saves hours of debugging deployment failures!