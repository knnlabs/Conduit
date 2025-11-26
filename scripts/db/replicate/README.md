# Replicate Model Extraction Script

## Overview

This C# script extracts detailed model information from Replicate's public collections and generates SQL statements to populate the Conduit database with model metadata, capabilities, and parameter schemas.

## Purpose

Automates the process of:
1. Discovering models from Replicate's curated collections (text-to-image, text-to-video, language models)
2. Extracting detailed schema information including input parameters and token limits
3. Generating database-ready SQL insert statements
4. Creating proper relationships between Authors, Series, Models, and Provider Identifiers

## Usage

### Basic Usage

```bash
# Extract text/LLM models to console
./extract-models-detailed.cs text

# Extract image models to console
./extract-models-detailed.cs image

# Extract video models to console
./extract-models-detailed.cs video
```

### SQL Output

```bash
# Generate SQL file for text models
./extract-models-detailed.cs text --output sql --filename replicate-text-models.sql

# Generate SQL file for image models
./extract-models-detailed.cs image --output sql --filename replicate-image-models.sql

# Generate SQL file for video models
./extract-models-detailed.cs video --output sql --filename replicate-video-models.sql
```

### Executing SQL

```bash
# Load generated SQL into database
psql -h localhost -U conduit -d conduit_db < replicate-text-models.sql
```

## How It Works

### 1. Collection Discovery

The script fetches Replicate's curated collection pages:
- **Image Models**: `https://replicate.com/collections/text-to-image`
- **Video Models**: `https://replicate.com/collections/text-to-video`
- **Text Models**: `https://replicate.com/collections/language-models`

It parses the HTML to extract model URLs using regex patterns that find `streamController.enqueue()` calls.

### 2. Schema Extraction

For each discovered model, the script:
1. Fetches the model's schema page: `https://replicate.com/{owner}/{model}/api/schema`
2. Parses embedded OpenAPI schema from script tags
3. Extracts the `Input` schema's `properties` object
4. Converts Replicate's schema to Conduit's parameter format

### 3. Parameter Conversion

The script intelligently maps Replicate's OpenAPI schema types to Conduit parameter types:

| Replicate Type | Conduit Type | Notes |
|---------------|--------------|-------|
| `string` with `enum` | `select` | Creates dropdown with options |
| `string` with `format: uri` | `media-upload` | File upload (image/video) |
| `integer` with min/max | `slider` | Range slider control |
| `boolean` | `toggle` | Checkbox/toggle |
| `string` | `text` | Text input |
| `integer` or `number` | `number` | Numeric input |

**Special Handling:**
- Aspect ratios get enhanced labels: `"1:1"` � `"1:1 (Square)"`
- Width/height sliders use 32-pixel steps
- Prompt and seed fields are excluded (handled separately by UI)

### 4. Model Family Detection

The script detects model families by:
1. Removing variant suffixes (`-turbo`, `-pro`, `-dev`, `-lora`, etc.)
2. Extracting base name before version numbers (`v1`, `v2`, `1.5`, etc.)
3. Titlecasing the result

**Examples:**
- `flux-dev-lora` � `Flux`
- `sdxl-turbo-v1.0` � `Sdxl`
- `stable-diffusion-3-medium` � `Stable Diffusion`

### 5. Series Name Determination

Series names group related models:
- First model in a family: `"{Family} Series"` (e.g., "Flux Series")
- Same owner variants: Uses primary series name
- Different owner variants: `"{Family} Series ({Descriptor})"` (e.g., "Flux Series (Optimized)")

**Variant Descriptors:**
- `prunaai` � "Optimized"
- `fermatresearch` � "ControlNet"
- Other authors � Titlecased owner name

### 6. Token Limit Handling (Text Models)

For text/LLM models, the script:
1. Extracts `max_output_tokens` from schema (checks multiple parameter names)
2. Loads `replicate-token-limits.json` if available for `max_input_tokens` and `tokenizer_type`
3. Falls back to `NULL` for missing token limits (requires manual configuration)

**Token Limits Reference File Format:**
```json
{
  "models": {
    "meta/meta-llama-3.1-405b-instruct": {
      "maxInputTokens": 128000,
      "tokenizerType": "LLaMA3",
      "notes": "Llama 3.1 405B parameter model"
    }
  }
}
```

### 7. Pricing Extraction

The script automatically extracts pricing information from Replicate model pages:

**Extracted Pricing Data:**
| Field | Description | Example |
|-------|-------------|---------|
| `CostPerSecond` | Compute time cost | $0.001525/sec |
| `CostPerImage` | Per-image generation cost | $0.025/image |
| `CostPerVideo` | Per-video generation cost | $0.50/video |
| `InputCostPerMillionTokens` | LLM input token cost | $9.50/M tokens |
| `OutputCostPerMillionTokens` | LLM output token cost | $9.50/M tokens |
| `MedianPredictionCost` | p50 prediction cost | $0.0047 |
| `Hardware` | GPU/CPU type | H100, A40, CPU |

**Pricing Models by Type:**
| Model Type | Pricing Model | Configuration |
|------------|--------------|-------------------|
| Text/LLM | Standard (0) | `InputCostPerMillionTokens`, `OutputCostPerMillionTokens` |
| Image | RulesBased (6) | `PricingConfiguration` JSON with quality rules |
| Video | RulesBased (6) | `PricingConfiguration` JSON with resolution rules |

**RulesBased Pricing:**
Video and image models use the flexible RulesBased pricing engine (PricingModel = 6) with `PricingConfiguration` JSON. This allows for:
- Resolution-based pricing for videos (e.g., 1080p costs more than 720p)
- Quality-based pricing for images (e.g., HD vs standard)
- Easy customization via the Admin UI without code changes

Example video pricing configuration:
```json
{
  "version": "1.0",
  "pricingType": "per_second",
  "unitField": "VideoDurationSeconds",
  "defaultRate": 0.05,
  "rules": [
    {"conditions": {"resolution": "1080p"}, "rate": 0.075, "priority": 2, "description": "1080p HD video"},
    {"conditions": {"resolution": "720p"}, "rate": 0.05, "priority": 1, "description": "720p video"}
  ]
}
```

**SQL Output:**
When generating SQL with `--output sql`, the script creates:
1. `ModelCosts` records with extracted pricing data
2. Links `ModelIdentifiers` to `ModelCosts` via `ModelCostId` foreign key

**Console Output:**
Pricing information is displayed for each model:
```
Pricing:
  - Hardware: H100
  - Cost per second: $0.001525
  - Cost per image: $0.0250
  - Median prediction cost: $0.0047
  - Description: or 40 images for $1
```

Plus a summary at the end showing:
- Total models with/without pricing
- Breakdown by pricing type (per-image, per-video, per-token, per-second)
- Hardware types and their frequency

### 8. SQL Generation

The script generates transactional SQL with CTEs (Common Table Expressions):

```sql
BEGIN;

-- For each model:
WITH author AS (
  INSERT INTO "ModelAuthors" ("Name", "Description", "WebsiteUrl")
  VALUES ('black-forest-labs', NULL, NULL)
  ON CONFLICT ("Name") DO UPDATE SET "Name" = EXCLUDED."Name"
  RETURNING "Id", "Name"
),
series AS (
  INSERT INTO "ModelSeries" ("Name", "Description", "AuthorId", "TokenizerType", "Parameters")
  SELECT 'Flux Series', NULL, author."Id", 0, '{...parameters...}'
  FROM author WHERE author."Name" = 'black-forest-labs'
  ON CONFLICT ("AuthorId", "Name") DO UPDATE SET "Name" = EXCLUDED."Name"
  RETURNING "Id", "Name"
),
model AS (
  INSERT INTO "Models" (
    "Name", "Version", "Description", "ModelCardUrl", "ModelSeriesId",
    "SupportsVision", "SupportsImageGeneration", "SupportsVideoGeneration",
    "SupportsEmbeddings", "SupportsChat", "SupportsFunctionCalling", "SupportsStreaming",
    "TokenizerType", "MaxInputTokens", "MaxOutputTokens",
    "IsActive", "Parameters", "CreatedAt", "UpdatedAt"
  )
  SELECT
    'flux-dev', NULL, NULL, 'https://replicate.com/black-forest-labs/flux-dev',
    series."Id",
    true, true, false, false, false, false, false,
    0, NULL, NULL,
    true, '{...parameters...}', NOW(), NOW()
  FROM series WHERE series."Name" = 'Flux Series'
  ON CONFLICT DO NOTHING
  RETURNING "Id", "Name"
),
modelcost AS (
  INSERT INTO "ModelCosts" (
    "CostName", "PricingModel", "InputCostPerMillionTokens", "OutputCostPerMillionTokens",
    "ImageCostPerImage", "VideoCostPerSecond", "PricingConfiguration", "ModelType", "IsActive",
    "EffectiveDate", "Description", "Priority", "SupportsBatchProcessing", "CreatedAt", "UpdatedAt"
  )
  VALUES (
    'Replicate - flux-dev',
    6,  -- PricingModel.RulesBased
    0, 0,
    NULL,  -- ImageCostPerImage (deprecated for RulesBased)
    NULL,  -- VideoCostPerSecond (deprecated for RulesBased)
    '{"version":"1.0","pricingType":"per_unit","unitField":"ImageCount","defaultRate":0.025,"rules":[{"conditions":{"quality":"hd"},"rate":0.05,"priority":1,"description":"HD quality image"},{"conditions":{"quality":"standard"},"rate":0.025,"priority":0,"description":"Standard quality image"}]}',
    'image',
    true,
    NOW(),
    'Auto-generated from Replicate (H100)',
    0,
    false,
    NOW(), NOW()
  )
  ON CONFLICT DO NOTHING
  RETURNING "Id"
)
INSERT INTO "ModelIdentifiers" (
  "ModelId", "Identifier", "Provider", "IsEnabled",
  "MaxInputTokens", "MaxOutputTokens", "IsPrimary", "ModelCostId"
)
SELECT
  model."Id",
  'black-forest-labs/flux-dev',
  3,  -- ProviderType.Replicate
  true, NULL, NULL, true,
  modelcost."Id"
FROM model, modelcost WHERE model."Name" = 'flux-dev'
ON CONFLICT ("Provider", "Identifier") DO NOTHING;

COMMIT;
```

**Key Features:**
- Uses `ON CONFLICT` to prevent duplicate inserts
- Preserves manual changes to existing records
- Creates proper foreign key relationships
- Sets appropriate capabilities based on model type
- **Creates ModelCosts records with RulesBased pricing configurations**
- **Generates flexible PricingConfiguration JSON for video and image models**
- **Links ModelIdentifiers to ModelCosts for cost tracking**

## Model Capabilities

### Image Models
- `SupportsImageGeneration`: `true`
- `SupportsVision`: `true` (can take image inputs for img2img)
- All other capabilities: `false`

### Video Models
- `SupportsVideoGeneration`: `true`
- `SupportsVision`: `true` (can take image inputs for first frame)
- All other capabilities: `false`

### Text/LLM Models
- `SupportsChat`: `true`
- `SupportsStreaming`: `true` (most text models support streaming)
- All other capabilities: `false`
- Note: Vision support varies by model (requires manual configuration)

## Output Formats

### Console Output

Shows detailed information per model:
```
Owner: black-forest-labs
Model Name: flux-dev
Model Type: image
Model Family: Flux
Series Name: Flux Series
URL: https://replicate.com/black-forest-labs/flux-dev
Replicate ID: black-forest-labs/flux-dev
Schema URL: https://replicate.com/black-forest-labs/flux-dev/api/schema
Capabilities:
  - Image Generation: True
  - Vision: True
Parameters: {
  "aspect_ratio": {
    "type": "select",
    "name": "aspect_ratio",
    "label": "Aspect Ratio",
    "options": [
      {"value": "1:1", "label": "1:1 (Square)"},
      {"value": "16:9", "label": "16:9 (Landscape)"}
    ],
    "default": "1:1"
  }
}
---
```

Plus summary statistics:
- Unique authors count
- Model families distribution
- Series breakdown

### SQL Output

- Transaction-wrapped SQL statements
- UTF-8 encoded `.sql` file
- Ready for PostgreSQL execution
- Includes helpful comments with model identifiers

## Requirements

### Runtime
- .NET 8.0 or later
- Internet connection (fetches from Replicate API)

### Dependencies
- **AngleSharp 1.1.2**: HTML parsing
- Built-in .NET libraries: HTTP, JSON, Regex

### Permissions
- Read access to local directory (for token limits JSON)
- Write access for SQL output files

## Token Limits Configuration (Text Models Only)

### Why Manual Configuration?

Token limits are **not** available in Replicate's public API schemas. They must be:
1. Looked up from official model documentation
2. Manually configured in a reference file
3. Kept up-to-date as models change

### Creating Token Limits Reference

Create `replicate-token-limits.json` in the same directory as the script:

```json
{
  "models": {
    "meta/meta-llama-3.1-405b-instruct": {
      "maxInputTokens": 128000,
      "tokenizerType": "LLaMA3",
      "notes": "Llama 3.1 405B parameter model"
    },
    "mistralai/mixtral-8x7b-instruct-v0.1": {
      "maxInputTokens": 32768,
      "tokenizerType": "Mistral",
      "notes": "Mixtral 8x7B MoE model"
    }
  }
}
```

### Supported Tokenizer Types

Map to Conduit's `TokenizerType` enum:
- `None` (0) - Default for image/video models
- `Cl100KBase` (1) - GPT-3.5, GPT-4
- `O200KBase` (5) - GPT-4o
- `Claude3` (7) - Claude 3/4 family
- `Gemini` (8) - Google models
- `LLaMA` (10) - LLaMA 1
- `LLaMA2` (11) - LLaMA 2
- `LLaMA3` (12) - LLaMA 3/3.1
- `Mistral` (13) - Mistral family
- See script for complete enum mapping

## Common Issues

### Schema Extraction Failures

**Symptom:** `� Schema parsing error` or `(schema extraction failed)`

**Causes:**
- Replicate changed their HTML structure
- Model schema page is malformed
- Network timeout

**Solution:** Script continues with remaining models. Review console output for affected models.

### Missing Token Limits (Text Models)

**Symptom:** `MaxInputTokens: null` in output

**Causes:**
- No `replicate-token-limits.json` file found
- Model not configured in reference file
- Token limit not extractable from schema

**Solution:** Create/update `replicate-token-limits.json` with official values from model docs.

### SQL Conflicts

**Symptom:** `ON CONFLICT DO NOTHING` prevents insertion

**Causes:**
- Model already exists in database
- Script re-run on same models

**Solution:** This is **expected behavior** to preserve manual edits. To force updates, edit SQL to remove `ON CONFLICT` clauses.

## Rate Limiting

The script includes a 100ms delay between requests to be respectful of Replicate's servers:

```csharp
await Task.Delay(100);
```

For large collections (50+ models), expect ~5-10 seconds total execution time.

## Example Workflow

### 1. Extract and Review
```bash
# See what models will be extracted
./extract-models-detailed.cs text
```

### 2. Create Token Limits Reference
```bash
# Create reference file with official token limits
cat > replicate-token-limits.json << 'EOF'
{
  "models": {
    "meta/meta-llama-3.1-405b-instruct": {
      "maxInputTokens": 128000,
      "tokenizerType": "LLaMA3"
    }
  }
}
EOF
```

### 3. Generate SQL
```bash
./extract-models-detailed.cs text --output sql --filename replicate-text-models.sql
```

### 4. Review Generated SQL
```bash
less replicate-text-models.sql
```

### 5. Execute in Database
```bash
psql -h localhost -U conduit -d conduit_db < replicate-text-models.sql
```

### 6. Verify in Database
```sql
-- Check inserted models
SELECT m."Name", ms."Name" as "Series", ma."Name" as "Author"
FROM "Models" m
JOIN "ModelSeries" ms ON m."ModelSeriesId" = ms."Id"
JOIN "ModelAuthors" ma ON ms."AuthorId" = ma."Id"
WHERE EXISTS (
  SELECT 1 FROM "ModelIdentifiers" mi
  WHERE mi."ModelId" = m."Id" AND mi."Provider" = 3
);
```

## Maintenance

### Updating for New Models

Replicate regularly adds new models. To refresh:

```bash
# Re-run extraction (safe with ON CONFLICT)
./extract-models-detailed.cs image --output sql --filename replicate-image-models.sql
psql -h localhost -U conduit -d conduit_db < replicate-image-models.sql
```

### Updating Token Limits

When models update their context windows:

1. Update `replicate-token-limits.json`
2. Regenerate SQL
3. Manually update database (script uses `ON CONFLICT DO NOTHING`)

```sql
-- Manual token limit update
UPDATE "Models"
SET "MaxInputTokens" = 128000, "TokenizerType" = 12  -- LLaMA3
WHERE "Name" = 'meta-llama-3.1-405b-instruct';
```

## Architecture Integration

This script populates the following Conduit database tables:
- **ModelAuthors**: Model creators/organizations
- **ModelSeries**: Model family groupings with shared parameters
- **Models**: Individual model versions with capabilities
- **ModelIdentifiers**: Provider-specific identifiers (Replicate IDs)

See [docs/architecture/provider-system/](../../docs/architecture/provider-system/) for complete architecture details.

## Related Documentation

- [Provider Architecture](../../docs/architecture/provider-system/provider-architecture.md)
- [Model and Cost Mapping](../../docs/architecture/provider-system/model-and-cost-mapping.md)
- [Repository and Data Access](../../docs/architecture/patterns/repository-and-data-access.md)
