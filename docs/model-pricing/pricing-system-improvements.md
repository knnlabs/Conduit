# Model Pricing System Improvements

Based on analysis of OpenAI, Anthropic, and MiniMax pricing models compared to Conduit's current capabilities.

## Current Limitations

### 1. **Prompt Caching** (Anthropic)
- **Issue**: Cannot represent write vs read costs for cached prompts
- **Impact**: Unable to accurately price Anthropic models with prompt caching

### 2. **Context-Dependent Pricing** (MiniMax)
- **Issue**: Cannot set different prices based on token count thresholds
- **Impact**: Must create separate model entries for different context sizes

### 3. **Batch Processing Discounts**
- **Issue**: No way to apply 50% discounts for batch operations
- **Impact**: Cannot offer competitive batch pricing

### 4. **Video Pricing Complexity**
- **Issue**: Only supports per-second + resolution multipliers
- **Impact**: Cannot handle duration tiers or quality presets

### 5. **Multi-Dimensional Image Pricing**
- **Issue**: Single price per image regardless of size/quality
- **Impact**: Cannot differentiate DALL-E standard vs HD pricing

## Improvement Roadmap

### Phase 1: Low Complexity (1-2 weeks)
Quick wins that don't require major architectural changes.

#### 1.1 Enhanced Image Pricing
- Add resolution tiers to existing JSON multiplier pattern
- Minimal database changes required
```csharp
public string? ImageQualityMultipliers { get; set; } // JSON: {"standard": 1.0, "hd": 2.0}
```

#### 1.2 Audio Character-Based Pricing Fix
- Ensure `AudioCostPerKCharacters` is properly used for TTS
- Update CSV import to handle both per-minute and per-character pricing
- No database changes needed

#### 1.3 Video Duration Tiers
- Extend video multipliers to include duration ranges
```csharp
public string? VideoDurationMultipliers { get; set; } // JSON: {"0-5": 1.0, "5-10": 2.0}
```

### Phase 2: Medium Complexity (3-4 weeks)
Requires database migrations but uses existing patterns.

#### 2.1 Batch Processing Support
```csharp
public decimal? BatchProcessingMultiplier { get; set; } // 0.5 = 50% discount
public bool? SupportsBatchProcessing { get; set; }
```

#### 2.2 Basic Prompt Caching
```csharp
public decimal? CachedInputTokenCost { get; set; } // Read cost
public decimal? CachedInputWriteCost { get; set; } // Write cost
```

#### 2.3 Request-Based Minimums
```csharp
public decimal? MinimumCostPerRequest { get; set; }
```

### Phase 3: High Complexity (6-8 weeks)
Major architectural changes requiring significant refactoring.

#### 3.1 Context-Dependent Pricing
- New table: `ModelCostTiers`
- Supports unlimited pricing tiers based on token count
```csharp
public class ModelCostTier
{
    public int MinTokens { get; set; }
    public int? MaxTokens { get; set; }
    public decimal InputCostPerMillionTokens { get; set; }
    public decimal OutputCostPerMillionTokens { get; set; }
}
```

#### 3.2 Dynamic Pricing Rules Engine
- Support for complex pricing formulas
- Time-based pricing variations
- Custom pricing logic per provider

#### 3.3 Usage-Based Discounts
- Volume tiers
- Contract pricing
- Custom rates per virtual key

## Implementation Priority

### Immediate (Phase 1)
Start with these to address the most common gaps:
1. **Image quality multipliers** - Fixes DALL-E pricing
2. **Video duration tiers** - Fixes MiniMax video pricing
3. **Audio pricing clarity** - Ensures TTS/STT pricing works correctly

### Short-term (Phase 2)
Add these once Phase 1 is stable:
1. **Batch processing** - Major cost savings for users
2. **Basic prompt caching** - Critical for Anthropic competitiveness
3. **Request minimums** - Prevents undercharging on small requests

### Long-term (Phase 3)
Consider these for full pricing parity:
1. **Context tiers** - Full MiniMax support
2. **Dynamic pricing** - Future-proof the system
3. **Usage discounts** - Enterprise features

## Migration Strategy

### Phase 1 Migrations
```sql
ALTER TABLE ModelCosts ADD COLUMN ImageQualityMultipliers TEXT NULL;
ALTER TABLE ModelCosts ADD COLUMN VideoDurationMultipliers TEXT NULL;
```

### Phase 2 Migrations
```sql
ALTER TABLE ModelCosts ADD COLUMN BatchProcessingMultiplier DECIMAL(18,4) NULL;
ALTER TABLE ModelCosts ADD COLUMN SupportsBatchProcessing BIT NULL;
ALTER TABLE ModelCosts ADD COLUMN CachedInputTokenCost DECIMAL(18,8) NULL;
ALTER TABLE ModelCosts ADD COLUMN CachedInputWriteCost DECIMAL(18,8) NULL;
ALTER TABLE ModelCosts ADD COLUMN MinimumCostPerRequest DECIMAL(18,8) NULL;
```

### Phase 3 Migrations
```sql
CREATE TABLE ModelCostTiers (
    Id INT PRIMARY KEY,
    ModelCostId INT FOREIGN KEY REFERENCES ModelCosts(Id),
    MinTokens INT NOT NULL,
    MaxTokens INT NULL,
    InputCostPerMillionTokens DECIMAL(18,8) NOT NULL,
    OutputCostPerMillionTokens DECIMAL(18,8) NOT NULL
);
```

## CSV Import Format Evolution

### Phase 1 Addition
```csv
...,Image Quality Multipliers,Video Duration Multipliers
```

### Phase 2 Additions
```csv
...,Batch Multiplier,Cached Input Cost,Cached Write Cost,Min Request Cost
```

### Phase 3 Format
Would require a separate CSV for tier definitions or a more complex format.

## Testing Considerations

1. **Calculator Accuracy**: Ensure cost calculations remain accurate with new fields
2. **Backward Compatibility**: Existing model costs should continue working
3. **Import/Export**: CSV import must handle both old and new formats
4. **API Compatibility**: Changes should not break existing API consumers

## Recommended Starting Point

Begin with **Phase 1.1 (Image Pricing)** as it:
- Addresses an immediate need (DALL-E HD pricing)
- Uses existing patterns (JSON multipliers)
- Requires minimal code changes
- Can be completed quickly
- Provides a template for video duration tiers