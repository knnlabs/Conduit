# Polymorphic Pricing Quick Reference

## Pricing Model Enum Values

```csharp
public enum PricingModel
{
    Standard = 0,              // Token-based
    PerVideo = 1,              // Flat rate video
    PerSecondVideo = 2,        // Duration-based video
    InferenceSteps = 3,        // Step-based image
    TieredTokens = 4,          // Context-based tiers
    PerImage = 5,              // Per image
    PerMinuteAudio = 6,        // Audio duration
    PerThousandCharacters = 7  // Character count
}
```

## Configuration Templates

### Standard (No Config Needed)
```csharp
new ModelCost {
    PricingModel = PricingModel.Standard,
    InputCostPerMillionTokens = 10.0m,
    OutputCostPerMillionTokens = 30.0m
}
```

### Per Video (MiniMax)
```json
{
  "rates": {
    "512p_6": 0.10,
    "768p_6": 0.28,
    "1080p_6": 0.49
  }
}
```

### Per Second Video (Replicate)
```json
{
  "baseRate": 0.09,
  "resolutionMultipliers": {
    "720p": 1.0,
    "1080p": 1.5
  }
}
```

### Inference Steps (Fireworks)
```json
{
  "costPerStep": 0.00035,
  "defaultSteps": 20,
  "modelSteps": {
    "model-fast": 10
  }
}
```

### Tiered Tokens (MiniMax)
```json
{
  "tiers": [
    {
      "maxContext": 200000,
      "inputCost": 400,
      "outputCost": 2200
    },
    {
      "maxContext": null,
      "inputCost": 1300,
      "outputCost": 2200
    }
  ]
}
```

### Per Image
```json
{
  "baseRate": 0.04,
  "qualityMultipliers": {
    "standard": 1.0,
    "hd": 1.5
  }
}
```

### Per Minute Audio
```json
{
  "ratePerMinute": 0.15
}
```

### Per Thousand Characters
```json
{
  "ratePerThousand": 0.015
}
```

## API Endpoints

### Create Pricing
```http
POST /api/admin/model-costs
{
  "costName": "GPT-4 Turbo",
  "modelProviderMappingIds": [1, 2],
  "pricingModel": 0,
  "inputCostPerMillionTokens": 10.0
}
```

### Update Pricing
```http
PUT /api/admin/model-costs/{id}
{
  "pricingModel": 1,
  "pricingConfiguration": "{...}"
}
```

### Get Pricing
```http
GET /api/admin/model-costs/{id}
```

### List Pricing
```http
GET /api/admin/model-costs?page=1&pageSize=20&isActive=true
```

### Delete Pricing
```http
DELETE /api/admin/model-costs/{id}
```

## C# Usage Examples

### Calculate Standard Cost
```csharp
var cost = await costCalculationService.CalculateCostAsync(
    modelId: "gpt-4-turbo",
    usage: new Usage {
        PromptTokens = 1000,
        CompletionTokens = 500
    }
);
```

### Calculate Video Cost
```csharp
var cost = await costCalculationService.CalculateCostAsync(
    modelId: "video-01",
    usage: new Usage {
        VideoResolution = "768p",
        VideoDuration = 6
    }
);
```

### Calculate with Batch Discount
```csharp
var cost = await costCalculationService.CalculateCostAsync(
    modelId: "claude-3-sonnet",
    usage: new Usage {
        PromptTokens = 10000,
        CompletionTokens = 2000,
        BatchProcessing = true  // Applies multiplier
    }
);
```

## TypeScript/React Usage

### Import Types
```typescript
import { 
  PricingModel, 
  ModelCostDto, 
  CreateModelCostDto 
} from '@knn_labs/conduit-admin-client';
```

### Create Pricing
```typescript
const { createModelCost } = useModelCostsApi();

await createModelCost({
  costName: "GPT-4 Turbo",
  pricingModel: PricingModel.Standard,
  modelProviderMappingIds: [1, 2],
  inputCostPerMillionTokens: 10.0,
  outputCostPerMillionTokens: 30.0
});
```

### Use Pricing Selector Component
```tsx
import { PricingModelSelector } from './PricingModelSelector';

<PricingModelSelector
  pricingModel={pricingModel}
  pricingConfiguration={config}
  onPricingModelChange={setPricingModel}
  onConfigurationChange={setConfig}
/>
```

## Database Queries

### Find Costs by Model
```sql
SELECT mc.* 
FROM "ModelCosts" mc
JOIN "ModelCostMappings" mcm ON mc."Id" = mcm."ModelCostId"
JOIN "ModelProviderMappings" mpm ON mcm."ModelProviderMappingId" = mpm."Id"
WHERE mpm."ProviderModelId" = 'gpt-4-turbo'
  AND mc."IsActive" = true
ORDER BY mc."Priority" DESC;
```

### Get Configuration
```sql
SELECT 
    "CostName",
    "PricingModel",
    "PricingConfiguration"::json
FROM "ModelCosts"
WHERE "PricingModel" != 0;
```

### Cost Summary
```sql
SELECT 
    mc."CostName",
    COUNT(ul.id) as usage_count,
    SUM(ul.calculated_cost) as total_cost,
    AVG(ul.calculated_cost) as avg_cost
FROM usage_logs ul
JOIN "ModelCosts" mc ON ul.model_cost_id = mc."Id"
WHERE ul.created_at > NOW() - INTERVAL '30 days'
GROUP BY mc."CostName"
ORDER BY total_cost DESC;
```

## Redis Cache Keys

```bash
# Parsed configuration cache
modelcost:parsed:{costId}

# Cost calculation cache
cost:calc:{modelId}:{usageHash}

# Model mapping cache
model:mapping:{modelId}
```

### Clear Cache
```bash
# Clear specific cost
redis-cli DEL "modelcost:parsed:123"

# Clear all cost caches
redis-cli --scan --pattern "modelcost:*" | xargs redis-cli DEL
```

## Common Validation Rules

### JSON Configuration
- Must be valid JSON
- Required fields per pricing model
- Positive numeric values only
- Non-empty arrays/objects

### Model Costs
- `CostName` required, max 255 chars
- At least one model mapping required
- `Priority` >= 0
- Costs >= 0

### Batch Processing
- `BatchProcessingMultiplier` between 0 and 1
- Only applies when `SupportsBatchProcessing = true`

## Error Codes

| Code | Description | Solution |
|------|-------------|----------|
| PC001 | Invalid pricing model | Check enum value |
| PC002 | Missing configuration | Add required JSON config |
| PC003 | Invalid JSON format | Validate JSON syntax |
| PC004 | No matching rate | Add rate to config |
| PC005 | Negative cost value | Use positive values |
| PC006 | Model not found | Check model mapping |
| PC007 | Tier not found | Add appropriate tier |
| PC008 | Invalid multiplier | Check value range |

## Performance Tips

1. **Cache parsed configurations** - Already done in Redis
2. **Batch cost calculations** - Process multiple in one call
3. **Use appropriate indexes** - On PricingModel, IsActive
4. **Pre-warm cache** - Load common configs on startup
5. **Monitor calculation time** - Alert if > 100ms

## Testing Checklist

- [ ] Each pricing model calculates correctly
- [ ] Batch discounts apply properly
- [ ] Invalid configurations rejected
- [ ] Cache invalidation works
- [ ] Priority ordering respected
- [ ] Inactive costs ignored
- [ ] Migration preserves values
- [ ] API endpoints return correct data
- [ ] UI displays all pricing models
- [ ] Performance within limits

## Environment Variables

```bash
# Redis cache for configurations
REDIS_CONNECTION=localhost:6379

# Cache expiration (seconds)
PRICING_CACHE_TTL=3600

# Enable debug logging
PRICING_DEBUG=true

# Batch processing default
DEFAULT_BATCH_MULTIPLIER=0.5
```

## Monitoring Metrics

```prometheus
# Cost calculation duration
conduit_cost_calculation_duration_seconds

# Pricing model usage
conduit_pricing_model_usage_total{model="Standard"}

# Calculation errors
conduit_cost_calculation_errors_total{error="InvalidConfig"}

# Cache hit rate
conduit_pricing_cache_hit_ratio
```

## Support Links

- [Full Documentation](./polymorphic-pricing.md)
- [WebUI Guide](./webui-pricing-guide.md)
- [Migration Guide](./pricing-migration-guide.md)
- [API Reference](/api/swagger)
- [GitHub Issues](https://github.com/knnlabs/Conduit/issues)