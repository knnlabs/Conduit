# API Key Migration Guide: From Single Key to Multi-Key Architecture

This guide helps developers migrate from the deprecated single `ApiKey` field to the new `ProviderKeyCredentials` collection in Conduit.

## Overview

The Conduit system is transitioning from a single API key per provider to a multi-key architecture that supports:
- Multiple keys per provider for load balancing
- Account-based failover and quota management  
- Better resilience and higher throughput
- Granular key management and monitoring

## Migration Timeline

- **Phase 1** (Current): Both systems work in parallel, `ApiKey` is deprecated
- **Phase 2** (Next Release): `ApiKey` field removed from DTOs and APIs
- **Phase 3** (Major Version): `ApiKey` database column dropped

## Before You Start

### What's Changing
- `ProviderCredential.ApiKey` → `ProviderKeyCredentials` collection
- Single key per provider → Multiple keys per provider
- Provider-level settings → Key-level overrides

### What's Not Changing
- Database connection strings
- Authentication mechanisms
- Core API endpoints
- SDK interfaces (they handle the transition transparently)

## Migration Steps

### Step 1: Update Your Data Access Code

#### Old Approach (Deprecated)
```csharp
// ❌ Deprecated - Single key access
var provider = await _providerRepo.GetByNameAsync("OpenAI");
var apiKey = provider.ApiKey; // This field is now obsolete
```

#### New Approach (Recommended)
```csharp
// ✅ Recommended - Multi-key access
var provider = await _providerRepo.GetByNameAsync("OpenAI");
var primaryKey = provider.ProviderKeyCredentials.FirstOrDefault(k => k.IsPrimary);
var apiKey = primaryKey?.ApiKey;

// Or use repository method for convenience
var primaryKey = await _keyRepo.GetPrimaryKeyAsync(provider.Id);
```

### Step 2: Creating New Provider Configurations

#### Old Approach (Deprecated)
```csharp
// ❌ Deprecated - Single key creation
var provider = new ProviderCredential
{
    ProviderName = "OpenAI",
    ProviderType = ProviderType.OpenAI,
    ApiKey = "sk-..." // Obsolete field
};
```

#### New Approach (Recommended)
```csharp
// ✅ Recommended - Multi-key creation
var provider = new ProviderCredential
{
    ProviderName = "OpenAI",  // Will be deprecated in Phase 2
    ProviderType = ProviderType.OpenAI,
    // Don't set ApiKey - it's obsolete
};

var keyCredential = new ProviderKeyCredential
{
    ProviderCredentialId = provider.Id,
    ApiKey = "sk-...",
    IsPrimary = true,
    ProviderAccountGroup = 0,
    KeyName = "Primary OpenAI Key"
};

provider.ProviderKeyCredentials.Add(keyCredential);
```

### Step 3: Key Management Operations

#### Adding Additional Keys
```csharp
// Add a second key for load balancing
var secondaryKey = new ProviderKeyCredential
{
    ProviderCredentialId = providerId,
    ApiKey = "sk-secondary...",
    IsPrimary = false,
    ProviderAccountGroup = 0, // Same account
    KeyName = "Secondary Key",
    IsEnabled = true
};

await _keyRepo.CreateAsync(secondaryKey);
```

#### Adding Keys from Different Accounts
```csharp
// Add key from different provider account for failover
var failoverKey = new ProviderKeyCredential
{
    ProviderCredentialId = providerId,
    ApiKey = "sk-different-account...",
    IsPrimary = false,
    ProviderAccountGroup = 1, // Different account group
    Organization = "different-org-id",
    KeyName = "Failover Account Key"
};

await _keyRepo.CreateAsync(failoverKey);
```

#### Switching Primary Keys
```csharp
// Make a different key primary
await _keyRepo.SetPrimaryKeyAsync(providerId, newPrimaryKeyId);
```

### Step 4: Update Configuration Loading

#### Old Approach
```csharp
// ❌ Deprecated
var config = new ProviderConfig
{
    ApiKey = provider.ApiKey,
    BaseUrl = provider.BaseUrl
};
```

#### New Approach
```csharp
// ✅ Recommended - Use primary key with fallbacks
var primaryKey = await _keyRepo.GetPrimaryKeyAsync(provider.Id);
var config = new ProviderConfig
{
    ApiKey = primaryKey.ApiKey,
    BaseUrl = primaryKey.BaseUrl ?? provider.BaseUrl, // Key-level override
    Organization = primaryKey.Organization // Key-specific setting
};
```

## Common Scenarios

### Scenario 1: Single Key Setup (Backward Compatible)
```csharp
// Works exactly like before, but uses new architecture
var provider = new ProviderCredential
{
    ProviderType = ProviderType.OpenAI
};

var key = new ProviderKeyCredential
{
    ApiKey = "sk-your-key",
    IsPrimary = true,
    ProviderAccountGroup = 0
};

provider.ProviderKeyCredentials.Add(key);
```

### Scenario 2: Load Balancing Setup
```csharp
// Multiple keys from same account for load balancing
var keys = new[]
{
    new ProviderKeyCredential
    {
        ApiKey = "sk-key-1",
        IsPrimary = true,
        ProviderAccountGroup = 0,
        KeyName = "Primary Key"
    },
    new ProviderKeyCredential
    {
        ApiKey = "sk-key-2", 
        IsPrimary = false,
        ProviderAccountGroup = 0, // Same account
        KeyName = "Load Balance Key"
    }
};

provider.ProviderKeyCredentials.AddRange(keys);
```

### Scenario 3: Multi-Account Failover
```csharp
// Keys from different accounts for quota failover
var keys = new[]
{
    new ProviderKeyCredential
    {
        ApiKey = "sk-account-1-key",
        IsPrimary = true,
        ProviderAccountGroup = 0,
        Organization = "org-account-1"
    },
    new ProviderKeyCredential
    {
        ApiKey = "sk-account-2-key",
        IsPrimary = false,
        ProviderAccountGroup = 1, // Different account
        Organization = "org-account-2"
    }
};
```

## Repository Methods

### IProviderKeyCredentialRepository
```csharp
// Get all keys for a provider
var keys = await _keyRepo.GetByProviderIdAsync(providerId);

// Get only enabled keys
var enabledKeys = await _keyRepo.GetEnabledKeysAsync(providerId);

// Get the primary key
var primary = await _keyRepo.GetPrimaryKeyAsync(providerId);

// Set a new primary key
await _keyRepo.SetPrimaryKeyAsync(providerId, keyId);

// Count keys per provider
var keyCount = await _keyRepo.CountKeysAsync(providerId);
```

## Database Schema

The new schema maintains backward compatibility:

```sql
-- ProviderCredential table (existing)
CREATE TABLE ProviderCredential (
    Id int PRIMARY KEY,
    ProviderName varchar(100), -- Will be deprecated
    ProviderType int,
    ApiKey varchar(500), -- DEPRECATED but still exists
    BaseUrl varchar(500),
    ApiVersion varchar(50), -- DEPRECATED
    IsEnabled boolean
);

-- ProviderKeyCredential table (new)
CREATE TABLE ProviderKeyCredential (
    Id int PRIMARY KEY,
    ProviderCredentialId int REFERENCES ProviderCredential(Id),
    ProviderAccountGroup smallint CHECK (ProviderAccountGroup BETWEEN 0 AND 32),
    ApiKey varchar(500),
    BaseUrl varchar(500), -- Overrides provider default
    ApiVersion varchar(50), -- DEPRECATED
    Organization varchar(100),
    KeyName varchar(100),
    IsPrimary boolean,
    IsEnabled boolean
);
```

## Testing Your Migration

### 1. Unit Tests
```csharp
[Test]
public async Task Should_Use_Primary_Key_When_Available()
{
    // Arrange
    var provider = CreateProviderWithMultipleKeys();
    
    // Act
    var config = await _service.GetProviderConfigAsync(provider.Id);
    
    // Assert
    Assert.That(config.ApiKey, Is.EqualTo(primaryKey.ApiKey));
}
```

### 2. Integration Tests
```csharp
[Test]
public async Task Should_Fallback_To_Secondary_Key_On_Primary_Failure()
{
    // Test failover logic
    // Disable primary key and verify secondary is used
}
```

## Troubleshooting

### Common Issues

1. **No Primary Key Set**
   - Error: Multiple keys but none marked as primary
   - Solution: Use `SetPrimaryKeyAsync()` to designate one

2. **All Keys Disabled**
   - Error: Provider exists but no enabled keys
   - Solution: Enable at least one key or disable the provider

3. **Obsolete Warnings**
   - Warning: Usage of deprecated `ApiKey` field
   - Solution: Update code to use `ProviderKeyCredentials` collection

### Migration Verification
```csharp
// Verify migration completed successfully
public async Task<bool> VerifyProviderMigration(int providerId)
{
    var provider = await _providerRepo.GetByIdAsync(providerId);
    
    // Should have at least one key
    if (!provider.ProviderKeyCredentials.Any())
        return false;
        
    // Should have exactly one primary key
    var primaryCount = provider.ProviderKeyCredentials.Count(k => k.IsPrimary);
    if (primaryCount != 1)
        return false;
        
    // All keys should have valid API keys
    return provider.ProviderKeyCredentials.All(k => !string.IsNullOrEmpty(k.ApiKey));
}
```

## Additional Resources

- [Entity Naming Conventions](entity-naming-conventions.md)
- [Database Migration Guide](../claude/database-migration-guide.md)
- [Provider Models Documentation](../claude/provider-models.md)
- [Entity Cleanup Epic #619](https://github.com/knnlabs/Conduit/issues/619)

## Support

If you encounter issues during migration:
1. Check this guide first
2. Review the unit tests for examples
3. File an issue on GitHub with details about your specific scenario