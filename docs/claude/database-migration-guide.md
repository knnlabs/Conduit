# Database Migration Guide

**🚨 CRITICAL**: You have made 50+ migration failures. This guide WILL prevent them. READ EVERY SECTION.

## Overview

Conduit uses Entity Framework Core with PostgreSQL exclusively. Migration failures cost hours of debugging. Most failures are from the SAME preventable mistakes repeated over and over.

## Before You Start

### Environment Setup
```bash
# Required: Set DATABASE_URL for EF Core tools
export DATABASE_URL='postgresql://user:password@localhost:5432/conduitdb'
```

## 🔴 STOP! Pre-Migration Checklist (PREVENTS 90% OF FAILURES)

### 1. CHECK FOR DUPLICATE ENTITY CONFIGURATIONS (Your #1 Failure)
**YOU KEEP CONFIGURING THE SAME ENTITY IN MULTIPLE PLACES!**

```bash
# MANDATORY: Run these checks BEFORE creating any migration
grep -r "modelBuilder.Entity<YourEntity>" ConduitLLM.Configuration/
grep -r "IEntityTypeConfiguration<YourEntity>" ConduitLLM.Configuration/
grep -r "modelBuilder.Entity.*YourEntity" ConduitLLM.Configuration/Data/
```

**Rule: Each entity gets ONE configuration location:**
- ✅ EITHER in a separate `IEntityTypeConfiguration<T>` class
- ✅ OR in `OnModelCreating()` directly
- ❌ NEVER BOTH! This causes "pending changes" errors every time!

### 2. UNDERSTAND THE "PENDING CHANGES" ERROR
When you see: **"The model for context has pending changes"**

EF is comparing THREE things:
1. **Current Code**: All your entity configurations
2. **Snapshot File**: `*ModelSnapshot.cs` 
3. **Database**: Actual PostgreSQL schema

If ANY mismatch exists → "pending changes" error → Hours wasted

### 3. DIAGNOSE BEFORE FIXING
```bash
# When you get "pending changes", DON'T randomly create migrations!
# Instead, diagnose first:

# Step 1: Create diagnostic migration to see what EF detected
dotnet ef migrations add DIAGNOSTIC --verbose > diagnostic.txt

# Step 2: Read what it generated
cat Migrations/*DIAGNOSTIC.cs

# Step 3: Look for these patterns:
# - DropIndex → You removed configuration but index exists
# - AddColumn → Duplicate configuration adding existing column
# - AlterColumn → Type mismatch in your configurations

# Step 4: FIX THE ROOT CAUSE, then remove diagnostic
dotnet ef migrations remove
```

### 4. CHECK DATABASE STATE FIRST
**NEVER assume what exists in the database!**

```bash
# Check if index exists BEFORE dropping
docker exec conduit-postgres-1 psql -U conduit -d conduitdb -c "\di *YourIndex*"

# Check table structure
docker exec conduit-postgres-1 psql -U conduit -d conduitdb -c "\d \"YourTable\""

# List ALL indexes on a table
docker exec conduit-postgres-1 psql -U conduit -d conduitdb -c \
  "SELECT indexname, indexdef FROM pg_indexes WHERE tablename = 'YourTable';"
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

## 🔴 Critical: Safe Drop Operations (Your #2 Failure)

**YOU KEEP TRYING TO DROP THINGS THAT DON'T EXIST!**

### NEVER Use Plain Drop Commands
```csharp
// ❌ WRONG - Fails if index doesn't exist
migrationBuilder.DropIndex(
    name: "IX_SomeIndex",
    table: "SomeTable");

// ✅ CORRECT - Always use IF EXISTS for PostgreSQL
migrationBuilder.Sql(@"
    DROP INDEX IF EXISTS ""IX_SomeIndex"";
");
```

### When EF Generates a DropIndex
1. **STOP! Don't trust it exists**
2. **CHECK the database first:**
   ```bash
   docker exec conduit-postgres-1 psql -U conduit -d conduitdb -c "\di *IndexName*"
   ```
3. **If it doesn't exist:** The index was never created (configuration was removed before migration)
4. **Fix:** Replace with `DROP INDEX IF EXISTS`

### Common Drop Failures
- **Index doesn't exist**: Configuration removed before index was created
- **Column doesn't exist**: Model changed before migration ran
- **Table doesn't exist**: Entity removed before migration

**Rule: ALWAYS use IF EXISTS for drops in PostgreSQL**

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

## 🔴 Emergency Recovery (When Everything Is Broken)

### Container Won't Start - "Pending Model Changes"
**This is YOUR most common failure!**

1. **Check the actual error:**
   ```bash
   docker logs conduit-admin-1 --tail 100 | grep -A5 -B5 "pending"
   ```

2. **Find duplicate configurations:**
   ```bash
   grep -r "modelBuilder.Entity.*ModelProviderTypeAssociation" ConduitLLM.Configuration/
   ```

3. **Fix steps:**
   - Remove duplicate configuration from `OnModelCreating()`
   - Keep only the `IEntityTypeConfiguration<T>` class
   - Create new migration
   - Rebuild with `./scripts/start-dev.sh --build`

### Container Won't Start - "Index does not exist"
1. **Find the failing migration:**
   ```bash
   docker logs conduit-api-1 2>&1 | grep "does not exist"
   ```

2. **Fix the migration:**
   ```csharp
   // Replace DropIndex with:
   migrationBuilder.Sql("DROP INDEX IF EXISTS \"IndexName\";");
   ```

3. **Rebuild and restart**

### Your Migration Failure Patterns to Recognize

**Empty Migration = You Don't Understand the Problem**
- Files like `TempSnapshotSync` with empty Up/Down
- Multiple "Fix" migrations in a row
- Sign you're guessing, not diagnosing

**"Pending Changes" After Migration = Duplicate Configuration**
- You configured entity in multiple places
- EF sees both, database has one
- Always check for duplicate configurations first

**"Does not exist" = You Didn't Check Database**
- Trying to drop non-existent objects
- Always verify with psql before dropping

## Golden Rules

1. **Check for duplicate configurations FIRST** - Causes 90% of failures
2. **Diagnose "pending changes" before creating migrations** - Don't guess
3. **Always use IF EXISTS for drops** - PostgreSQL safety
4. **Never trust EF to detect renames** - Manual intervention required
5. **Check database state before operations** - Never assume

## Quick Reference

```bash
# BEFORE creating any migration - Check for duplicates
grep -r "modelBuilder.Entity<EntityName>" ConduitLLM.Configuration/

# Diagnose "pending changes" error
dotnet ef migrations add DIAGNOSTIC --verbose
cat Migrations/*DIAGNOSTIC.cs  # See what EF detected
dotnet ef migrations remove     # Remove diagnostic

# Check database state
docker exec conduit-postgres-1 psql -U conduit -d conduitdb -c "\d \"TableName\""
docker exec conduit-postgres-1 psql -U conduit -d conduitdb -c "\di *IndexName*"

# Create migration (AFTER checks)
DATABASE_URL='...' dotnet ef migrations add MigrationName --no-build

# Validate syntax
./scripts/migrations/validate-postgresql-syntax.sh

# Fix drop operations
# Replace: migrationBuilder.DropIndex(...)
# With: migrationBuilder.Sql("DROP INDEX IF EXISTS \"IndexName\";")

# Apply migrations
DATABASE_URL='...' dotnet ef database update

# Rebuild containers (when migrations added)
./scripts/start-dev.sh --build
```

## 🚨 REMEMBER: Your Top 3 Failures

1. **Duplicate entity configuration** → "Pending changes" → Hours wasted
2. **Dropping non-existent objects** → "Does not exist" → Container crash  
3. **Creating empty "fix" migrations** → Not understanding root cause → More failures

**ALWAYS check for duplicate configurations FIRST. This alone prevents 90% of your failures.**