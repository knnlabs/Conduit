# Provider-Agnostic Entity Framework Core Migration Guide

## Problem
The current migrations contain SQLite-specific data types that cause failures when running on PostgreSQL:
- `INTEGER` type (PostgreSQL uses `integer` or `int4`)
- `TEXT` type for DateTime (PostgreSQL uses `timestamp`)
- `BLOB` type (PostgreSQL uses `bytea`)
- SQLite-specific annotations like `Sqlite:Autoincrement`

## Solution Approach

### 1. Remove Provider-Specific Column Attributes
Remove `[Column(TypeName = "...")]` attributes from entities and let EF Core handle the mapping:

```csharp
// Bad - Provider specific
[Column(TypeName = "text")]
public string? Payload { get; set; }

// Good - Provider agnostic
public string? Payload { get; set; }
```

### 2. Use Fluent API for Complex Configurations
In `OnModelCreating`, use provider-agnostic configurations:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Configure large text fields without specifying type
    modelBuilder.Entity<AsyncTask>(entity =>
    {
        // EF Core will map to appropriate text type for each provider
        entity.Property(e => e.Payload).HasMaxLength(null);
        entity.Property(e => e.Result).HasMaxLength(null);
        entity.Property(e => e.Error).HasMaxLength(null);
        entity.Property(e => e.Metadata).HasMaxLength(null);
    });
}
```

### 3. Handle Row Version Properly
For optimistic concurrency, use provider-agnostic approach:

```csharp
// Instead of byte[] with [Timestamp] attribute
[ConcurrencyCheck]
public int Version { get; set; }

// In OnModelCreating
entity.Property(e => e.Version)
    .IsConcurrencyToken()
    .ValueGeneratedOnAddOrUpdate();
```

### 4. Use Conditional Logic for Provider-Specific Features
When absolutely necessary, use conditional logic:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    var isPostgreSql = Database.IsNpgsql();
    var isSqlite = Database.IsSqlite();
    
    if (isPostgreSql)
    {
        // PostgreSQL-specific configurations
        modelBuilder.HasPostgresExtension("uuid-ossp");
    }
}
```

### 5. Migration Generation Best Practices

1. **Generate migrations for each provider separately:**
   ```bash
   # For SQLite
   dotnet ef migrations add InitialCreate --context ConfigurationDbContext --output-dir Migrations/Sqlite -- --provider sqlite
   
   # For PostgreSQL  
   dotnet ef migrations add InitialCreate --context ConfigurationDbContext --output-dir Migrations/PostgreSQL -- --provider postgresql
   ```

2. **Or use a single migration set with runtime checks:**
   ```csharp
   protected override void Up(MigrationBuilder migrationBuilder)
   {
       migrationBuilder.CreateTable(
           name: "VirtualKeys",
           columns: table => new
           {
               Id = table.Column<int>(nullable: false)
                   .Annotation("SqlServer:Identity", "1, 1")
                   .Annotation("Sqlite:Autoincrement", true)
                   .Annotation("Npgsql:ValueGenerationStrategy", 
                               NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
               // ... other columns
           });
   }
   ```

## Recommended Approach for Conduit

1. **Use EnsureCreated for Initial Setup**
   - For new deployments, use `context.Database.EnsureCreated()`
   - This creates schema based on current model without migrations
   - Works consistently across all providers

2. **Apply Migrations for Updates**
   - After initial setup, use migrations for schema changes
   - Generate provider-agnostic migrations going forward

3. **Update DatabaseInitializer**
   - Detect if database is empty
   - Use EnsureCreated for fresh databases
   - Use Migrate for existing databases with migration history

## Implementation Steps

1. Remove all `[Column(TypeName = "...")]` attributes from entities
2. Update `ConfigurationDbContext.OnModelCreating` to avoid provider-specific configurations
3. Regenerate migrations without provider-specific types
4. Update `DatabaseInitializer` to handle both SQLite and PostgreSQL gracefully