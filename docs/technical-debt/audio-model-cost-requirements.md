# Technical Debt: Audio Model Cost and Capability Requirements

## Summary
The current `ModelCost` and `Usage` entities are insufficient for properly calculating costs for audio operations, particularly for realtime sessions that have separate input and output audio durations with different pricing.

## Current State

### Problems Identified
1. **Realtime Audio Cost Calculation**: The `Usage` class only has a single `AudioDurationSeconds` property, but realtime audio sessions have:
   - Input audio duration (user speaking)
   - Output audio duration (AI responding)
   - Each with different per-minute rates

2. **Missing Audio-Specific Cost Fields**: The `ModelCost` entity lacks fields for:
   - Input audio cost per minute
   - Output audio cost per minute
   - Minimum billing duration (some providers bill in minimum increments)
   - Different rates for different quality levels (e.g., OpenAI's tts-1 vs tts-1-hd)

3. **Capability Detection**: While `ModelCapabilities` has boolean flags for audio support, it lacks:
   - Supported audio codecs/formats per model
   - Maximum audio duration limits
   - Real-time vs batch processing capabilities
   - Supported sampling rates
   - Voice cloning capabilities
   - Language-specific capabilities

## Required Database Schema Enhancements

### 1. Enhanced ModelCost Table
```sql
-- Add audio-specific cost fields
ALTER TABLE ModelCost ADD COLUMN InputAudioCostPerMinute DECIMAL(10,6);
ALTER TABLE ModelCost ADD COLUMN OutputAudioCostPerMinute DECIMAL(10,6);
ALTER TABLE ModelCost ADD COLUMN MinimumBillingSeconds INT DEFAULT 1;
ALTER TABLE ModelCost ADD COLUMN AudioQualityTier VARCHAR(20); -- 'standard', 'hd', 'ultra'
```

### 2. Enhanced Usage Model
```csharp
public class Usage
{
    // Existing fields...
    
    // Separate input/output audio for realtime sessions
    public decimal? InputAudioDurationSeconds { get; set; }
    public decimal? OutputAudioDurationSeconds { get; set; }
    
    // Quality tier used (affects pricing)
    public string? AudioQualityTier { get; set; }
}
```

### 3. Audio-Specific Capabilities Table
```sql
CREATE TABLE AudioModelCapabilities (
    Id INT PRIMARY KEY,
    ModelId INT FOREIGN KEY REFERENCES Models(Id),
    
    -- Format support
    SupportedInputFormats JSON, -- ["mp3", "wav", "flac", "ogg", "m4a"]
    SupportedOutputFormats JSON, -- ["mp3", "opus", "aac", "flac"]
    
    -- Technical limits
    MaxAudioDurationSeconds INT,
    SupportedSampleRates JSON, -- [8000, 16000, 24000, 48000]
    MaxFileSizeMB INT,
    
    -- Feature support
    SupportsStreaming BIT,
    SupportsVoiceCloning BIT,
    SupportsEmotionControl BIT,
    SupportsSpeakerDiarization BIT,
    SupportsBackgroundNoiseRemoval BIT,
    
    -- Language capabilities
    TranscriptionLanguages JSON, -- ["en", "es", "fr", ...]
    TTSLanguages JSON,
    
    -- Voice options
    AvailableVoices JSON, -- [{"id": "alloy", "gender": "neutral", "languages": ["en"]}]
    
    -- Quality tiers
    QualityTiers JSON -- ["standard", "hd"]
);
```

## Implementation Recommendations

### 1. Update Cost Calculation Service
```csharp
public interface ICostCalculationService
{
    // New method for audio-specific costs
    Task<decimal> CalculateAudioCostAsync(
        string modelId, 
        decimal? inputAudioSeconds,
        decimal? outputAudioSeconds,
        string? qualityTier,
        CancellationToken cancellationToken = default);
}
```

### 2. Migration Path
1. Add new database columns with sensible defaults
2. Migrate existing hardcoded costs to database
3. Update cost calculation service to use new fields
4. Remove all hardcoded cost calculations

### 3. Configuration Priority
- Store all provider-specific costs in database
- No hardcoded fallbacks
- Require explicit cost configuration before enabling audio models

## Benefits
- Accurate billing for all audio operations
- Support for different pricing tiers
- Flexibility for provider-specific pricing models
- No code deployments needed for price changes
- Better cost tracking and reporting

## Risks
- Migration complexity for existing deployments
- Need to backfill historical cost data
- Performance impact of additional database queries (mitigate with caching)

## Related Issues
- #763: Technical Debt: Migrate Hardcoded Provider Configurations to Database
- Audio routing strategies need actual metrics, not arbitrary scores
- Provider capability detection should use database, not hardcoded switches

## Next Steps
1. Design and implement new database schema
2. Create migration scripts
3. Update cost calculation service
4. Remove remaining hardcoded audio costs
5. Add comprehensive tests for audio cost calculations