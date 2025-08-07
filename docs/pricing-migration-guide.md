# Pricing System Migration Guide

## Overview

This guide helps you migrate from Conduit's legacy pattern-based pricing to the new polymorphic pricing system.

## What's Changing

### Old System (Pattern-Based)
- Used wildcard patterns to match model IDs (e.g., `gpt-4*`)
- Fixed to per-token pricing model
- Limited to standard token-based costs
- Provider-type based matching

### New System (Polymorphic)
- Explicit model-to-pricing mappings
- Supports 8+ different pricing models
- Flexible configuration via JSON
- Provider-instance based (supports multiple OpenAI configs)

## Migration Timeline

### Phase 1: Preparation (Before Migration)
- [ ] Audit existing ModelCost records
- [ ] Identify all model patterns in use
- [ ] Document custom pricing rules
- [ ] Test in development environment

### Phase 2: Migration (During Deployment)
- [ ] Run database migration scripts
- [ ] Create ModelProviderMappings
- [ ] Convert ModelCosts to new format
- [ ] Validate calculations match

### Phase 3: Verification (After Migration)
- [ ] Compare cost calculations
- [ ] Monitor for calculation errors
- [ ] Update documentation
- [ ] Train team on new system

## Pre-Migration Checklist

### 1. Audit Current Pricing

```sql
-- List all unique model patterns and their costs
SELECT 
    "ModelIdPattern",
    "ProviderType",
    "InputCostPerMillionTokens",
    "OutputCostPerMillionTokens",
    COUNT(*) as usage_count
FROM "ModelCosts"
WHERE "IsActive" = true
GROUP BY 
    "ModelIdPattern",
    "ProviderType",
    "InputCostPerMillionTokens",
    "OutputCostPerMillionTokens"
ORDER BY usage_count DESC;
```

### 2. Identify Affected Models

```sql
-- Find all models that match current patterns
SELECT DISTINCT 
    ml."ModelId",
    mc."ModelIdPattern",
    mc."ProviderType"
FROM "ModelLogs" ml
JOIN "ModelCosts" mc ON ml."ModelId" LIKE mc."ModelIdPattern"
WHERE ml."CreatedAt" > NOW() - INTERVAL '30 days';
```

### 3. Export Current Configuration

```bash
# Backup current model costs
pg_dump -h localhost -U conduit -d conduit \
  -t '"ModelCosts"' \
  -f model_costs_backup_$(date +%Y%m%d).sql
```

## Migration Steps

### Step 1: Database Schema Update

Run the migration to add new columns:

```sql
-- Add polymorphic pricing columns
ALTER TABLE "ModelCosts" 
ADD COLUMN IF NOT EXISTS "PricingModel" INTEGER DEFAULT 0,
ADD COLUMN IF NOT EXISTS "PricingConfiguration" TEXT,
ADD COLUMN IF NOT EXISTS "CostName" VARCHAR(255);

-- Add model mapping table if not exists
CREATE TABLE IF NOT EXISTS "ModelCostMappings" (
    "Id" SERIAL PRIMARY KEY,
    "ModelCostId" INTEGER NOT NULL REFERENCES "ModelCosts"("Id"),
    "ModelProviderMappingId" INTEGER NOT NULL,
    "IsActive" BOOLEAN DEFAULT true,
    "CreatedAt" TIMESTAMPTZ DEFAULT NOW()
);
```

### Step 2: Create Model Provider Mappings

For each model pattern, create explicit mappings:

```csharp
// Example: Migrating "gpt-4*" pattern
var gpt4Models = new[] {
    "gpt-4",
    "gpt-4-turbo",
    "gpt-4-turbo-preview",
    "gpt-4-1106-preview",
    "gpt-4-vision-preview"
};

foreach (var modelId in gpt4Models)
{
    var mapping = new ModelProviderMapping
    {
        ProviderId = openAiProviderId,
        ProviderModelId = modelId,
        ModelAlias = modelId,
        ModelType = ModelType.Chat,
        IsActive = true
    };
    await context.ModelProviderMappings.AddAsync(mapping);
}
```

### Step 3: Convert Model Costs

Transform existing costs to polymorphic format:

```csharp
public async Task MigrateModelCosts()
{
    var oldCosts = await context.ModelCosts
        .Where(mc => mc.PricingModel == null)
        .ToListAsync();
    
    foreach (var oldCost in oldCosts)
    {
        // Set pricing model to Standard for all legacy costs
        oldCost.PricingModel = PricingModel.Standard;
        
        // Generate descriptive name from pattern
        oldCost.CostName = GenerateCostName(oldCost.ModelIdPattern);
        
        // Convert per-token to per-million-tokens if needed
        if (oldCost.InputCostPerMillionTokens < 1)
        {
            oldCost.InputCostPerMillionTokens *= 1_000_000;
            oldCost.OutputCostPerMillionTokens *= 1_000_000;
        }
        
        // Map to model provider mappings
        await CreateCostMappings(oldCost);
    }
    
    await context.SaveChangesAsync();
}

private string GenerateCostName(string pattern)
{
    // Remove wildcards and create readable name
    var name = pattern.Replace("*", "").Trim();
    return $"{name} - Migrated from Legacy";
}
```

### Step 4: Validate Migration

Run validation queries to ensure correctness:

```sql
-- Check all costs have pricing model
SELECT COUNT(*) as unmigrated_count
FROM "ModelCosts"
WHERE "PricingModel" IS NULL;

-- Verify cost calculations match
WITH old_calc AS (
    SELECT 
        model_id,
        (prompt_tokens::decimal / 1000000) * mc."InputCostPerMillionTokens" +
        (completion_tokens::decimal / 1000000) * mc."OutputCostPerMillionTokens" as old_cost
    FROM usage_logs ul
    JOIN "ModelCosts" mc ON ul.model_id LIKE mc."ModelIdPattern"
    WHERE ul.created_at > NOW() - INTERVAL '1 day'
),
new_calc AS (
    SELECT 
        model_id,
        calculated_cost as new_cost
    FROM cost_calculations
    WHERE created_at > NOW() - INTERVAL '1 day'
)
SELECT 
    old_calc.model_id,
    old_calc.old_cost,
    new_calc.new_cost,
    ABS(old_calc.old_cost - new_calc.new_cost) as difference
FROM old_calc
JOIN new_calc ON old_calc.model_id = new_calc.model_id
WHERE ABS(old_calc.old_cost - new_calc.new_cost) > 0.001;
```

## Migration Script

Complete migration script for production:

```bash
#!/bin/bash
# pricing-migration.sh

set -e  # Exit on error

echo "Starting Conduit Pricing Migration..."

# 1. Backup database
echo "Creating backup..."
pg_dump -h $DB_HOST -U $DB_USER -d $DB_NAME \
  -f backup_pre_migration_$(date +%Y%m%d_%H%M%S).sql

# 2. Run schema migration
echo "Updating schema..."
psql -h $DB_HOST -U $DB_USER -d $DB_NAME <<EOF
BEGIN;

-- Add new columns
ALTER TABLE "ModelCosts" 
ADD COLUMN IF NOT EXISTS "PricingModel" INTEGER DEFAULT 0,
ADD COLUMN IF NOT EXISTS "PricingConfiguration" TEXT,
ADD COLUMN IF NOT EXISTS "CostName" VARCHAR(255);

-- Create mapping table
CREATE TABLE IF NOT EXISTS "ModelCostMappings" (
    "Id" SERIAL PRIMARY KEY,
    "ModelCostId" INTEGER NOT NULL,
    "ModelProviderMappingId" INTEGER NOT NULL,
    "IsActive" BOOLEAN DEFAULT true,
    "CreatedAt" TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE("ModelCostId", "ModelProviderMappingId")
);

-- Migrate existing costs
UPDATE "ModelCosts" 
SET "PricingModel" = 0,
    "CostName" = CONCAT("ModelIdPattern", ' - Legacy')
WHERE "PricingModel" IS NULL;

-- Fix token costs if needed (convert to per-million)
UPDATE "ModelCosts"
SET "InputCostPerMillionTokens" = "InputCostPerMillionTokens" * 1000000,
    "OutputCostPerMillionTokens" = "OutputCostPerMillionTokens" * 1000000
WHERE "InputCostPerMillionTokens" < 1 
  AND "InputCostPerMillionTokens" > 0;

COMMIT;
EOF

# 3. Run application migration
echo "Running application migration..."
dotnet run --project MigrationTool \
  --task migrate-pricing \
  --validate

# 4. Clear caches
echo "Clearing caches..."
redis-cli FLUSHDB

# 5. Verify migration
echo "Verifying migration..."
psql -h $DB_HOST -U $DB_USER -d $DB_NAME <<EOF
SELECT 
    COUNT(*) as total_costs,
    COUNT(CASE WHEN "PricingModel" IS NOT NULL THEN 1 END) as migrated_costs,
    COUNT(CASE WHEN "PricingModel" IS NULL THEN 1 END) as unmigrated_costs
FROM "ModelCosts";
EOF

echo "Migration complete!"
```

## Rollback Plan

If issues occur, rollback procedure:

```sql
-- Restore from backup
psql -h localhost -U conduit -d conduit < backup_pre_migration_[timestamp].sql

-- Or manually revert
BEGIN;

-- Remove new columns
ALTER TABLE "ModelCosts" 
DROP COLUMN IF EXISTS "PricingModel",
DROP COLUMN IF EXISTS "PricingConfiguration",
DROP COLUMN IF EXISTS "CostName";

-- Drop mapping table
DROP TABLE IF EXISTS "ModelCostMappings";

-- Restore original token costs if changed
UPDATE "ModelCosts"
SET "InputCostPerMillionTokens" = "InputCostPerMillionTokens" / 1000000,
    "OutputCostPerMillionTokens" = "OutputCostPerMillionTokens" / 1000000
WHERE "InputCostPerMillionTokens" > 100;

COMMIT;
```

## Common Migration Scenarios

### Scenario 1: Simple Token-Based Models

**Before**: 
```json
{
  "ModelIdPattern": "gpt-3.5*",
  "InputCostPerMillionTokens": 0.5,
  "OutputCostPerMillionTokens": 1.5
}
```

**After**:
```json
{
  "CostName": "GPT-3.5 Turbo",
  "PricingModel": 0,
  "InputCostPerMillionTokens": 0.5,
  "OutputCostPerMillionTokens": 1.5,
  "ModelProviderMappingIds": [1, 2, 3]
}
```

### Scenario 2: Adding Batch Processing

**Before**: No batch support

**After**:
```json
{
  "CostName": "Claude 3 Sonnet (Batch)",
  "PricingModel": 0,
  "SupportsBatchProcessing": true,
  "BatchProcessingMultiplier": 0.5,
  "InputCostPerMillionTokens": 3.0,
  "OutputCostPerMillionTokens": 15.0
}
```

### Scenario 3: Video Model Migration

**Before**: Trying to fit video into token model

**After**:
```json
{
  "CostName": "Replicate SDXL Video",
  "PricingModel": 2,
  "PricingConfiguration": {
    "baseRate": 0.09,
    "resolutionMultipliers": {
      "720p": 1.0,
      "1080p": 1.5
    }
  }
}
```

## Testing Migration

### Unit Tests

```csharp
[Test]
public async Task Migration_ConvertsLegacyCosts()
{
    // Arrange
    var legacyCost = new ModelCost
    {
        ModelIdPattern = "gpt-4*",
        InputCostPerMillionTokens = 0.00001m,
        OutputCostPerMillionTokens = 0.00003m
    };
    
    // Act
    await migrator.MigrateCost(legacyCost);
    
    // Assert
    Assert.AreEqual(PricingModel.Standard, legacyCost.PricingModel);
    Assert.AreEqual(10m, legacyCost.InputCostPerMillionTokens);
    Assert.AreEqual(30m, legacyCost.OutputCostPerMillionTokens);
    Assert.IsNotNull(legacyCost.CostName);
}
```

### Integration Tests

```csharp
[Test]
public async Task CostCalculation_MatchesLegacySystem()
{
    // Arrange
    var usage = new Usage
    {
        ModelId = "gpt-4-turbo",
        PromptTokens = 1000,
        CompletionTokens = 500
    };
    
    // Act
    var legacyCost = await legacyCalculator.Calculate(usage);
    var newCost = await polymorphicCalculator.Calculate(usage);
    
    // Assert
    Assert.AreEqual(legacyCost, newCost, 0.001m);
}
```

## Post-Migration Tasks

### 1. Update Documentation
- [ ] Update API documentation
- [ ] Update user guides
- [ ] Create training materials

### 2. Monitor Performance
```sql
-- Monitor calculation errors
SELECT 
    DATE(created_at) as date,
    pricing_model,
    COUNT(*) as error_count,
    array_agg(DISTINCT error_message) as errors
FROM cost_calculation_logs
WHERE status = 'error'
  AND created_at > NOW() - INTERVAL '7 days'
GROUP BY DATE(created_at), pricing_model
ORDER BY date DESC;
```

### 3. Optimize Queries
```sql
-- Add indexes for performance
CREATE INDEX idx_model_costs_pricing_model 
ON "ModelCosts"("PricingModel", "IsActive");

CREATE INDEX idx_model_cost_mappings_active 
ON "ModelCostMappings"("ModelCostId", "IsActive");
```

### 4. Clean Up Legacy Data
After confirming migration success (recommended: 30 days):

```sql
-- Remove deprecated columns
ALTER TABLE "ModelCosts" 
DROP COLUMN IF EXISTS "ModelIdPattern",
DROP COLUMN IF EXISTS "ProviderType";
```

## Troubleshooting Guide

### Issue: Costs Don't Match After Migration

**Check**:
1. Token multiplication factor (should be 1,000,000)
2. Model mappings are complete
3. Priority settings for overlapping costs

**Fix**:
```sql
-- Find mismatched costs
SELECT 
    old.model_id,
    old.calculated_cost as old_cost,
    new.calculated_cost as new_cost,
    (new.calculated_cost - old.calculated_cost) as difference
FROM old_calculations old
JOIN new_calculations new ON old.request_id = new.request_id
WHERE ABS(old.calculated_cost - new.calculated_cost) > 0.01;
```

### Issue: Some Models Not Found

**Check**:
```sql
-- Find unmapped models
SELECT DISTINCT model_id
FROM usage_logs
WHERE model_id NOT IN (
    SELECT DISTINCT mpm."ProviderModelId"
    FROM "ModelProviderMappings" mpm
)
AND created_at > NOW() - INTERVAL '7 days';
```

**Fix**: Create missing ModelProviderMappings

### Issue: JSON Configuration Invalid

**Check**: Validate JSON format
```bash
echo '$PRICING_CONFIG' | jq .
```

**Fix**: Correct JSON syntax and re-save

## Support Resources

- **Migration Hotline**: Set up during migration window
- **Slack Channel**: #pricing-migration
- **Documentation**: [Polymorphic Pricing Guide](./polymorphic-pricing.md)
- **Rollback Procedure**: See "Rollback Plan" section above

## Migration Success Criteria

✅ All ModelCosts have PricingModel set
✅ No calculation errors in past 24 hours  
✅ Cost calculations match within 0.1% tolerance
✅ All active models have pricing configured
✅ Performance metrics within baseline
✅ No user-reported issues for 48 hours

## Sign-Off Checklist

- [ ] Database backup completed
- [ ] Migration script tested in staging
- [ ] Rollback procedure verified
- [ ] Team trained on new system
- [ ] Monitoring alerts configured
- [ ] Documentation updated
- [ ] Customer communication sent
- [ ] Go-live approval received

---

**Last Updated**: 2024-01-07
**Version**: 1.0.0
**Owner**: Platform Team