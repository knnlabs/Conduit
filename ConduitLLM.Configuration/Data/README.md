# Database Initialization Service

This directory contains a robust solution for handling database initialization in a cross-provider environment, addressing the limitations of Entity Framework Core migrations when used with different database providers.

## Problem

Entity Framework Core migrations are specific to the database provider they are created with. When a migration is created with one provider (e.g., SQLite) but then applied to another provider (e.g., PostgreSQL), tables may not be created correctly due to provider-specific SQL syntax differences. This can result in errors such as:

```
PG Error: relation 'IpFilters' does not exist
```

## Solution

The `DatabaseInitializer` provides a comprehensive solution that:

1. Centralizes initialization logic for different database providers (SQLite, PostgreSQL)
2. Applies standard EF Core migrations when possible
3. Falls back to direct table creation when migrations fail
4. Ensures critical database tables exist across providers
5. Provides robust retry and error recovery

## Key Components

- **DatabaseInitializer**: Implementation that handles migrations across different providers
- **DatabaseInitializationExtensions**: Extension methods for using the initializer in applications

## Usage

### Registration

In `Program.cs` or `Startup.cs`:

```csharp
// Add database initialization services to DI container
services.AddDatabaseInitialization();
```

### Initializing the Database

```csharp
// Apply migrations and create tables
await serviceProvider.InitializeDatabaseAsync();

// Ensure specific tables exist
await serviceProvider.EnsureTablesExistAsync("TableName1", "TableName2");
```

## Features

- **Provider Detection**: Automatically detects SQLite vs PostgreSQL
- **Table Verification**: Ensures tables exist after migrations
- **Direct Table Creation**: Creates tables directly when migrations fail
- **Comprehensive Error Handling**: With customizable retry logic
- **Index Creation**: Creates appropriate indexes for performance

## Benefits

1. **Reliability**: More reliable database initialization across providers
2. **Consistency**: Tables are created with consistent schema regardless of provider
3. **Robustness**: Better error recovery and retry logic
4. **Maintainability**: Centralized and modular database initialization code
5. **Diagnostics**: Improved logging and error reporting