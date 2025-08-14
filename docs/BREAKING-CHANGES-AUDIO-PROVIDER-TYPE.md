# BREAKING CHANGE: Audio Provider Type Migration

**Date**: 2025-01-27  
**Version**: Next Release  
**Impact**: Database Schema Change  
**Issue**: #654

## Summary

The `AudioCost` and `AudioUsageLog` entities have been migrated from string-based provider names to the strongly-typed `ProviderType` enum. This is a database schema change that requires migration.

**Important Note**: ProviderType is used for categorization only. Provider.Id remains the canonical identifier for Provider records, supporting multiple providers of the same type.

## Impact Assessment

### ⚠️ Breaking Changes

1. **Database Schema**
   - `AudioCosts.Provider` column changed from `varchar(100)` to `integer`
   - `AudioUsageLogs.Provider` column changed from `varchar(100)` to `integer`
   - Direct SQL queries using string providers will fail

2. **Database Operations**
   - Database backups from before migration are incompatible
   - Database restores require matching schema version
   - Cross-database replication may fail without schema update

### ✅ Non-Breaking (Backward Compatible)

1. **API Endpoints** - Continue accepting string provider names
2. **Import/Export** - CSV/JSON formats unchanged
3. **SDK Usage** - String parameters still supported

## Required Actions

### For System Administrators

1. **Before Deployment**
   ```bash
   # Backup your database
   pg_dump -U conduit -d conduitdb > backup_before_audio_migration.sql
   ```

2. **Run Migration**
   ```bash
   # Migration runs automatically on startup or manually:
   dotnet ef database update
   ```

3. **Verify Migration**
   ```sql
   -- Check provider values are integers 1-22
   SELECT DISTINCT "Provider" FROM "AudioCosts";
   SELECT DISTINCT "Provider" FROM "AudioUsageLogs";
   ```

### For Developers

1. **Direct SQL Queries**
   ```sql
   -- OLD (will fail)
   SELECT * FROM "AudioCosts" WHERE "Provider" = 'openai';
   
   -- NEW (use integer values)
   SELECT * FROM "AudioCosts" WHERE "Provider" = 1; -- OpenAI
   ```

2. **Entity Framework Queries**
   ```csharp
   // Automatically handled - use ProviderType enum
   var costs = await repository.GetByProviderAsync(ProviderType.OpenAI);
   ```

### For API Consumers

**No action required** - API maintains backward compatibility:

```bash
# These continue to work
GET /api/admin/audio/costs/by-provider/openai
GET /api/admin/audio/costs/current?provider=anthropic&operationType=tts
POST /api/admin/audio/costs/import
```

## Rollback Procedure

If issues occur, rollback to previous migration:

```bash
# Rollback command
dotnet ef database update 20250726003113_InitialCreate

# Restore from backup if needed
psql -U conduit -d conduitdb < backup_before_audio_migration.sql
```

## Provider Type Reference

| Provider String | Enum Value | Integer |
|----------------|------------|---------|
| openai | ProviderType.OpenAI | 1 |
| anthropic | ProviderType.Anthropic | 2 |
| elevenlabs | ProviderType.ElevenLabs | 19 |
| googlecloud | ProviderType.GoogleCloud | 20 |
| awstranscribe | ProviderType.AWSTranscribe | 22 |

[Full mapping table in migration documentation]

## Timeline

- **Migration Available**: Immediately after deployment
- **Grace Period**: N/A - Schema migration required
- **Old Schema Removal**: Already removed in migration

## Support

For migration issues:
1. Check migration logs in application startup
2. Verify PostgreSQL compatibility
3. Contact support with error messages

## Related Changes

- All audio-related repositories updated
- Service layer maintains string compatibility
- DTOs use ProviderType enum internally