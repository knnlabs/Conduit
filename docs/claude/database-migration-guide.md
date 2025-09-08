# Database Migration Guide

## ⛔ STOP: Basic Tooling Check Required

Before attempting any migrations, verify that basic EF Core tools work:

```bash
# Test 1: Can you run dotnet ef?
dotnet ef --version
```

**If this fails, STOP immediately. Do not attempt migrations. Do not create workarounds.**

```bash
# Test 2: Can you see the DbContext?
cd ConduitLLM.Configuration
dotnet ef dbcontext info
```

**If this fails, STOP immediately. Fix the tooling issue first.**

## Standard EF Core Migration Workflow

### 1. Make Your Model Changes
Edit your entity classes or configurations as needed.

### 2. Create Migration
```bash
cd ConduitLLM.Configuration
dotnet ef migrations add YourMigrationName
```

### 3. Review the Generated Migration
Check the generated files in `Migrations/` folder:
- Look for PostgreSQL compatibility issues
- Ensure column names use double quotes: `"ColumnName"`
- Boolean filters should use `true`/`false`, not `1`/`0`

### 4. Apply Migration
```bash
dotnet ef database update
```

### 5. Build and Test
```bash
cd .. # back to repository root
dotnet build
```

## PostgreSQL-Specific Notes

### Boolean Syntax
```csharp
// ✅ Correct
entity.HasIndex(e => e.Region).HasFilter("\"IsActive\" = true");

// ❌ Wrong (SQL Server syntax)
entity.HasIndex(e => e.Region).HasFilter("IsActive = 1");
```

### Safe Drop Operations
If EF generates drop operations that might fail:
```csharp
// Replace generated DropIndex with:
migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_IndexName\";");
```

## When Things Go Wrong

### "Pending Model Changes" Error
This usually means duplicate entity configurations. Check:
```bash
grep -r "modelBuilder.Entity<YourEntity>" ConduitLLM.Configuration/
```

Each entity should only be configured in ONE place:
- Either in a separate `IEntityTypeConfiguration<T>` class
- OR directly in `OnModelCreating()`
- Never both!

### "Index/Column does not exist" Error
EF might try to drop something that doesn't exist. Use `IF EXISTS` patterns or check the database first:
```bash
docker exec conduit-postgres-1 psql -U conduit -d conduitdb -c "\d \"TableName\""
```

## Emergency Rule

**If you encounter repeated failures or complex errors:**

1. STOP creating migrations
2. STOP trying workarounds
3. Tell the user: "I'm having trouble with EF Core migrations. Please handle this manually using standard EF Core tools."

**Do not create elaborate diagnostic scripts, validation tools, or manual SQL workarounds.**

The standard EF Core tooling should handle 95% of migration scenarios. If it doesn't work, fix the tooling - don't work around it.