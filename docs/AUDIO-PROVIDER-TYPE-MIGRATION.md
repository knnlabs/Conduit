# Audio Provider Type Migration Guide

## Overview

This document describes the migration of AudioCost and AudioUsageLog entities from string-based provider names to the strongly-typed ProviderType enum. This migration was completed as part of issue #654 to align the audio subsystem with the rest of the codebase.

## Migration Details

### Affected Entities

1. **AudioCost**
   - Changed: `string Provider` → `ProviderType Provider`
   - Stores audio pricing configuration per provider

2. **AudioUsageLog**
   - Changed: `string Provider` → `ProviderType Provider`
   - Tracks audio API usage and costs

### Database Migration

**Migration Name**: `MigrateAudioToProviderTypeEnum`
**Date**: 2025-01-27

The migration uses a safe approach with temporary columns:

1. Creates temporary integer columns
2. Converts string provider names to enum values
3. Drops old string columns
4. Renames temporary columns to Provider
5. Sets columns as non-nullable with default value

### Provider Mapping

| String Value | ProviderType Enum | Integer Value |
|--------------|-------------------|---------------|
| openai | OpenAI | 1 |
| anthropic | Anthropic | 2 |
| azureopenai | AzureOpenAI | 3 |
| gemini | Gemini | 4 |
| vertexai | VertexAI | 5 |
| cohere | Cohere | 6 |
| mistral | Mistral | 7 |
| groq | Groq | 8 |
| ollama | Ollama | 9 |
| replicate | Replicate | 10 |
| fireworks | Fireworks | 11 |
| bedrock | Bedrock | 12 |
| huggingface | HuggingFace | 13 |
| sagemaker | SageMaker | 14 |
| openrouter | OpenRouter | 15 |
| openaicompatible | OpenAICompatible | 16 |
| minimax | MiniMax | 17 |
| ultravox | Ultravox | 18 |
| elevenlabs | ElevenLabs | 19 |
| googlecloud | GoogleCloud | 20 |
| cerebras | Cerebras | 21 |
| awstranscribe | AWSTranscribe | 22 |

**Note**: Unknown provider names default to OpenAI (1) during migration.

## API Compatibility

### Backward Compatibility

The API maintains backward compatibility:

- **Endpoints still accept string provider names** in query parameters
- **Service layer handles conversion** from string to ProviderType
- **No breaking changes** for existing API consumers

### Example API Calls

```bash
# Still works - string provider parameter
GET /api/admin/audio/costs/by-provider/openai

# Still works - string provider in query
GET /api/admin/audio/costs/current?provider=anthropic&operationType=transcription
```

### Internal Changes

Internally, the system now uses ProviderType enum:
- Repository methods use ProviderType parameters
- LINQ queries compare enum values directly
- No more string conversions in data access layer

## Import/Export Compatibility

### CSV Import Format
```csv
Provider,OperationType,Model,CostUnit,CostPerUnit,MinimumCharge
openai,transcription,whisper-1,minute,0.006,0
elevenlabs,tts,eleven_monolingual_v1,character,0.00018,0
```

### JSON Import Format
```json
[
  {
    "provider": "openai",
    "operationType": "transcription",
    "model": "whisper-1",
    "costUnit": "minute",
    "costPerUnit": 0.006,
    "isActive": true
  }
]
```

The import/export functionality maintains string-based provider names for compatibility.

## Migration Rollback

If needed, the migration can be rolled back:

```bash
dotnet ef database update 20250726003113_InitialCreate
```

The Down() migration:
1. Creates temporary string columns
2. Converts enum values back to provider names
3. Restores original string columns
4. Preserves all data

## Testing the Migration

### Pre-Migration Checklist
- [ ] Backup database
- [ ] Note existing audio costs and usage logs
- [ ] Verify provider names are valid

### Post-Migration Verification
- [ ] All audio costs have valid ProviderType values
- [ ] All usage logs have valid ProviderType values
- [ ] API endpoints continue to work
- [ ] Import/export functionality works

### Sample Test Queries

```sql
-- Check AudioCosts after migration
SELECT "Provider", COUNT(*) 
FROM "AudioCosts" 
GROUP BY "Provider";

-- Check AudioUsageLogs after migration
SELECT "Provider", COUNT(*) 
FROM "AudioUsageLogs" 
GROUP BY "Provider";

-- Verify no null providers
SELECT COUNT(*) FROM "AudioCosts" WHERE "Provider" IS NULL;
SELECT COUNT(*) FROM "AudioUsageLogs" WHERE "Provider" IS NULL;
```

## SDK Updates

The TypeScript SDK will need regeneration to fully utilize ProviderType:

```typescript
// Future SDK usage
import { ProviderType } from '@conduit/admin-sdk';

const costs = await client.audio.getCostsByProvider(ProviderType.OpenAI);
```

Currently, the SDK continues to use string parameters for compatibility.

## Breaking Changes

### Database Level
- **BREAKING**: Direct SQL queries using string providers will fail
- **BREAKING**: Database dumps/restores require matching schema

### Application Level
- **Non-Breaking**: API maintains string parameters
- **Non-Breaking**: DTOs already use ProviderType
- **Non-Breaking**: Import/export uses strings

## Monitoring

After deployment, monitor:
1. Audio cost creation/updates
2. Audio usage logging
3. Provider-specific queries
4. Import/export operations

## Related Documentation

- [Provider Type Migration (Phase 3f)](#628)
- [CLAUDE.md](/CLAUDE.md) - Updated with ProviderType information
- [Database Migration Guide](/docs/claude/database-migration-guide.md)