# CSV Import/Export Format for Model Costs

## Overview

The CSV import/export functionality supports bulk management of model cost configurations. This document describes the current CSV format including the new pricing fields added in Phase 1.

## CSV Format

### Required Headers

The following columns are required:
- `Model Pattern` - The model ID pattern (supports wildcards with *)
- `Provider` - The provider name (e.g., OpenAI, Anthropic, MiniMax)
- `Model Type` - The type of model: chat, embedding, image, audio, or video

### Cost Columns

Costs are entered per 1,000 tokens/units for convenience (automatically converted to per-million internally):
- `Input Cost (per 1K tokens)` - Cost for input tokens (chat/completion models)
- `Output Cost (per 1K tokens)` - Cost for output tokens (chat/completion models)
- `Embedding Cost (per 1K tokens)` - Cost for embedding tokens
- `Image Cost (per image)` - Cost per generated image
- `Audio Cost (per minute)` - Cost per minute of audio processing
- `Video Cost (per second)` - Cost per second of video generation

### New Pricing Fields (Phase 1)

#### Batch Processing
- `Batch Processing Multiplier` - Decimal value between 0 and 1 (e.g., 0.5 for 50% discount)
- `Supports Batch Processing` - Yes/No or true/false

#### Quality/Resolution Multipliers
- `Image Quality Multipliers` - JSON object with quality-based multipliers
- `Video Resolution Multipliers` - JSON object with resolution-based multipliers

### Metadata Columns
- `Priority` - Integer priority for pattern matching (higher values match first)
- `Active` - Yes/No or true/false to enable/disable the cost configuration
- `Description` - Optional description of the model/pricing

## Examples

### Chat Model with Batch Processing
```csv
Model Pattern,Provider,Model Type,Input Cost (per 1K tokens),Output Cost (per 1K tokens),Batch Processing Multiplier,Supports Batch Processing,Priority,Active
gpt-4o,OpenAI,chat,0.005,0.015,0.5,Yes,10,Yes
```

### Image Model with Quality Tiers
```csv
Model Pattern,Provider,Model Type,Image Cost (per image),Image Quality Multipliers,Priority,Active
dall-e-3,OpenAI,image,0.04,"{""standard"": 1.0, ""hd"": 2.0}",10,Yes
```

### Video Model with Resolution Multipliers
```csv
Model Pattern,Provider,Model Type,Video Cost (per second),Video Resolution Multipliers,Priority,Active
video-01,MiniMax,video,0.3,"{""720p"": 1.0, ""1080p"": 1.5, ""4k"": 3.0}",10,Yes
```

## JSON Format for Multipliers

Both image quality and video resolution multipliers use JSON objects:

```json
{
  "standard": 1.0,
  "hd": 2.0,
  "ultra": 4.0
}
```

**Important**: In CSV files, JSON must be properly escaped:
- Use double quotes for the JSON string
- Escape internal quotes by doubling them: `"{""`

## Validation Rules

1. **Batch Processing Multiplier**:
   - Must be between 0 and 1 (exclusive of 0, inclusive of 1)
   - Values > 1 are rejected (would increase cost instead of discount)

2. **Image Quality/Video Resolution Multipliers**:
   - Must be valid JSON
   - Must be an object (not an array)
   - All values must be positive numbers
   - Values > 10 generate a warning (unreasonably high multiplier)

3. **Model Pattern**:
   - Required field
   - Supports wildcards (e.g., `gpt-4*` matches all GPT-4 variants)
   - Must be unique within the CSV

4. **Costs**:
   - All cost values must be non-negative
   - Values > 1000 per 1K tokens generate a warning

## Backward Compatibility

The CSV import maintains backward compatibility:
- Old CSV files without the new columns will import successfully
- Missing batch processing fields default to: no batch support, no multiplier
- Missing quality/resolution multipliers are left empty

## Sample Files

See the following sample CSV files in the repository:
- `openai-model-costs-updated.csv` - OpenAI models with batch processing
- `anthropic-model-costs-updated.csv` - Anthropic models with batch API support
- `minimax-model-costs-updated.csv` - MiniMax models with image quality tiers

## Error Handling

Import errors are reported with row numbers:
- Invalid JSON: "Row X: Image quality multipliers must be valid JSON"
- Out of range: "Row X: Batch processing multiplier cannot be greater than 1"
- Duplicate patterns: "Row X: Duplicate model pattern: [pattern]"

The import process validates all rows before importing any data, ensuring atomic operations.