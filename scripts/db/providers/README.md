# Provider Models SQL Generator

This directory contains the canonical provider model catalogs plus an optional SQL generator for offline maintenance.

The JSON files and `provider-config.json` are embedded in the shared Configuration
assembly during every release build. A clean database imports that snapshot after
migrations, and administrators can merge entries from WebAdmin's Models page.
Runtime imports refresh provider-owned metadata while preserving associations marked
as `Manual` and canonical models marked `Curated` or `Manual`; generated SQL is not
used by the Docker images.

## Overview

This unified approach consolidates the previously separate provider-specific scripts (cerebras, sambanova) into a single, maintainable, configurable system. The pattern uses **static JSON + generator** because:

- Providers have small, stable model sets (4-15 models typically)
- All models use standard OpenAI-compatible parameters
- Model specifications are clearly documented
- Pricing information is well-documented
- Manual maintenance is faster than building/maintaining web scrapers
- Centralized configuration reduces duplication

## Files

- **provider-config.json** - Provider metadata (ProviderType enum, URLs, capabilities)
- **generate-provider-sql.cs** - Unified C# script that generates PostgreSQL SQL
- **fetch-openrouter-models.cs** - Fetches OpenRouter models from API → generates openrouter-models.json
- **cerebras-models.json** - Hand-maintained list of Cerebras models
- **groq-models.json** - Hand-maintained list of Groq models
- **sambanova-models.json** - Hand-maintained list of SambaNova models
- **openrouter-models.json** - Auto-generated from OpenRouter API (do not hand-edit)
- **UPDATING-MODELS.md** - Comprehensive guide for updating model JSON files
- **README.md** - This file

## Usage

### Basic Usage

```bash
# Generate SQL for Cerebras (outputs to cerebras-models.sql)
./generate-provider-sql.cs cerebras

# Generate SQL for Groq (outputs to groq-models.sql)
./generate-provider-sql.cs groq

# Generate SQL for SambaNova (outputs to sambanova-models.sql)
./generate-provider-sql.cs sambanova

# Generate SQL for OpenRouter (requires fetch step first — see below)
./generate-provider-sql.cs openrouter

# Generate SQL with custom output filename
./generate-provider-sql.cs cerebras my-cerebras-models.sql
```

### OpenRouter (API-Fetched)

OpenRouter has 300+ models from 50+ upstream providers — too many to hand-maintain.
Use the fetch script to pull the latest model catalog from OpenRouter's API:

```bash
# Step 1: Fetch models from OpenRouter API → generates openrouter-models.json
dotnet run fetch-openrouter-models.cs

# Step 2: Generate SQL from the fetched JSON (same as other providers)
dotnet run generate-provider-sql.cs -- openrouter
```

The fetch script:
- Calls `GET https://openrouter.ai/api/v1/models` (no auth required)
- Converts per-token pricing to per-million-tokens
- Captures OpenRouter's exact `input_modalities` and `output_modalities`
- Infers operation support such as function calling from `supported_parameters`
- Maps OpenRouter tokenizer names to Conduit's tokenizer enum
- Derives model family/series from naming patterns
- Retains free-tier variants so the bundled snapshot matches the public catalog
- Outputs standard `openrouter-models.json` format

### Executing Generated SQL

```bash
# Execute against local database
psql -h localhost -U conduit -d conduit_db < cerebras-models.sql
psql -h localhost -U conduit -d conduit_db < groq-models.sql
psql -h localhost -U conduit -d conduit_db < sambanova-models.sql
psql -h localhost -U conduit -d conduit_db < openrouter-models.sql

# Execute via Docker
docker exec -i conduit-postgres psql -U conduit -d conduit_db < cerebras-models.sql
docker exec -i conduit-postgres psql -U conduit -d conduit_db < groq-models.sql
docker exec -i conduit-postgres psql -U conduit -d conduit_db < sambanova-models.sql
docker exec -i conduit-postgres psql -U conduit -d conduit_db < openrouter-models.sql
```

## Provider Configuration

The `provider-config.json` file centralizes provider metadata:

```json
{
  "cerebras": {
    "providerType": 9,
    "websiteUrl": "https://cerebras.ai",
    "modelCardUrl": "https://inference-docs.cerebras.ai/models/overview",
    "supportsAudio": false
  },
  "groq": {
    "providerType": 2,
    "websiteUrl": "https://groq.com",
    "modelCardUrl": "https://console.groq.com/docs/models",
    "supportsAudio": false
  },
  "sambanova": {
    "providerType": 10,
    "websiteUrl": "https://sambanova.ai",
    "modelCardUrl": "https://docs.sambanova.ai/docs/en/models/sambacloud-models",
    "supportsAudio": true
  }
}
```

### Configuration Fields

- **providerType**: Database ProviderType enum value (must match C# enum)
- **websiteUrl**: Provider's main website (used in ModelAuthors table)
- **modelCardUrl**: URL to model documentation (used in Models table)
- **supportsAudio**: Whether provider has audio transcription models

## Model Data Structure

Each model in `{provider}-models.json` includes:

```json
{
  "model-id": {
    "name": "model-name",
    "family": "Model Family",
    "series": "Model Series Name",
    "owner": "company-name",
    "maxInputTokens": 128000,
    "maxOutputTokens": 65536,
    "tokenizerType": "LLaMA3|Cl100KBase|Tiktoken|Mistral",
    "supportsChat": true,
    "supportsStreaming": true,
    "inputModalities": ["text", "image", "video"],
    "outputModalities": ["text"],
    "capabilitySource": "ProviderApi",
    "capabilitiesLastVerifiedAt": "2026-07-23T00:00:00Z",
    "supportsFunctionCalling": true,
    "supportsEmbeddings": false,
    "supportsImageGeneration": false,
    "supportsVideoGeneration": false,
    "supportsSpeechToText": false,
    "supportsTextToSpeech": false,
    "supportsRerank": false,
    "inputPricePerMillion": 0.85,
    "outputPricePerMillion": 1.20,
    "speedTokensPerSec": 2100,
    "notes": "Model description with cross-references to other providers"
  }
}
```

`inputModalities` and `outputModalities` are directional. For example, a model can
accept `video` and return `text` without supporting `supportsVideoGeneration`.
Use `null`/omission for unknown metadata and `[]` only when explicitly unsupported.
Legacy `supportsVision` is retained as a compatibility alias for image input.

## Generated SQL Structure

The script generates SQL that creates/updates:

1. **ModelAuthors** - Model creators (Meta, DeepSeek, Qwen, OpenAI, etc.)
2. **ModelSeries** - Model series/families (Llama 3.3 Series, DeepSeek V3 Series, etc.)
3. **Models** - Individual model records with capabilities and token limits
4. **ModelIdentifiers** - Maps provider model IDs to Models (with provider-specific ProviderType)
5. **ModelCosts** - Pricing information per model

## Current Providers

### Cerebras (ProviderType = 9)

**Production Models (4):**
- llama3.1-8b (32K context, 2200 tokens/sec)
- llama-3.3-70b (128K context, 2100 tokens/sec)
- gpt-oss-120b (131K context, 3000 tokens/sec - fastest!)
- qwen-3-32b (131K context, 2600 tokens/sec)

**Key Features:**
- Ultra-fast inference powered by CS-3 wafer-scale chips
- 20-30x faster than GPU-based inference
- Free tier: 1M tokens/day, 30 req/min
- Paid tier: Higher context windows, increased output limits

**Data Sources:**
- Official Docs: https://inference-docs.cerebras.ai/models/overview
- Pricing: https://www.cerebras.ai/pricing
- Performance Analysis: https://artificialanalysis.ai/providers/cerebras

### Groq (ProviderType = 2)

**Production Models (5):**
- llama-3.1-8b-instant (131K context, 560 tokens/sec)
- llama-3.3-70b-versatile (131K context, 280 tokens/sec)
- meta-llama/llama-guard-4-12b (131K context, 1200 tokens/sec, multimodal)
- openai/gpt-oss-120b (131K context, 500 tokens/sec, function calling)
- openai/gpt-oss-20b (131K context, 1000 tokens/sec, function calling)

**Preview Models (7):**
- meta-llama/llama-4-maverick-17b-128e-instruct (multimodal MoE with vision, 17B params, 128 experts)
- meta-llama/llama-4-scout-17b-16e-instruct (multimodal MoE with vision, 17B params, 16 experts)
- meta-llama/llama-prompt-guard-2-22m (prompt injection/jailbreak detection, 22M params)
- meta-llama/llama-prompt-guard-2-86m (prompt injection/jailbreak detection, 86M params)
- moonshotai/kimi-k2-instruct-0905 (1T total params, 32B active, 256K context, MoE with 384 experts)
- openai/gpt-oss-safeguard-20b (safety classification with reasoning)
- qwen/qwen3-32b (dual-mode reasoning, 32B parameters)

**Key Features:**
- Ultra-fast inference powered by proprietary LPU (Language Processing Unit) architecture
- 10x faster than GPU-based inference for language workloads
- 131K context windows across most models
- Special capabilities: Vision (3 models), Function calling (5 models), Content moderation (3 models)
- Pricing: $0.03-$3.00 per million tokens

**Data Sources:**
- Official Docs: https://console.groq.com/docs/models
- Pricing: https://groq.com/pricing
- Performance Analysis: https://artificialanalysis.ai/providers/groq

### SambaNova (ProviderType = 10)

**Production Models (6):**
- DeepSeek-R1-0528 (128K context, reasoning model)
- DeepSeek-V3-0324 (128K context, 250 tokens/sec)
- DeepSeek-V3.1 (128K context, latest iteration)
- DeepSeek-R1-Distill-Llama-70B (128K context, distilled reasoning)
- Meta-Llama-3.3-70B-Instruct (128K context, 70B parameters)
- Meta-Llama-3.1-8B-Instruct (16K context, fast inference)

**Preview Models (6):**
- Llama-4-Maverick-17B-128E-Instruct (multimodal with vision)
- gpt-oss-120b (128K context)
- Whisper-Large-v3 (audio transcription)
- Qwen3-32B (8K context)
- Llama-3.3-Swallow-70B-Instruct-v0.4 (16K context)
- E5-Mistral-7B-Instruct (embedding-optimized)

**Key Features:**
- Ultra-fast inference powered by custom RDU chips
- 10x faster than GPU-based inference
- Special capabilities: Vision (1 model), Audio (1 model), Embeddings (1 model)

**Data Sources:**
- Official Docs: https://docs.sambanova.ai/docs/en/models/sambacloud-models
- Pricing: https://cloud.sambanova.ai/plans/pricing
- Performance Analysis: https://artificialanalysis.ai/providers/sambanova

### OpenRouter (ProviderType = 13)

**Auto-fetched from API (~316 paid models from 50+ upstream providers)**

Unlike other providers, OpenRouter models are **not hand-maintained**. The `fetch-openrouter-models.cs` script pulls the latest catalog from OpenRouter's API. Run it periodically to pick up new models.

**Key Features:**
- Meta-provider routing to 300+ models from OpenAI, Anthropic, Google, Meta, Mistral, and more
- Models use `provider/model-name` format (e.g., `openai/gpt-4o`, `anthropic/claude-3.5-sonnet`)
- Pricing varies by upstream provider
- Capabilities (vision, tools, etc.) detected from API metadata

**Data Source:**
- API: https://openrouter.ai/api/v1/models (public, no auth required)
- Docs: https://openrouter.ai/docs

## Updating Models

When a provider releases new models or updates specifications:

1. **Check official documentation** for new model announcements
2. **Read UPDATING-MODELS.md** for comprehensive update guide (LLM-friendly)
3. **Edit {provider}-models.json** to add/update model data
4. **Build the Configuration/Admin projects** to validate and embed the catalog
5. **Optionally run ./generate-provider-sql.cs {provider}** when standalone SQL is needed
6. **Use WebAdmin or POST /api/Model/bundled-catalog/import** to merge a rebuilt release snapshot

The UPDATING-MODELS.md guide includes:
- Data source URLs for model specs and pricing
- Step-by-step decision rules for each field
- Examples for every model family
- Common pitfalls to avoid
- Validation checklist

## Tokenizer Types

Common tokenizer types used across providers:

- **LLaMA3** (12) - Llama 3.x and DeepSeek models
- **Cl100KBase** (1) - OpenAI GPT models
- **Tiktoken** (23) - Qwen and Whisper models
- **Mistral** (13) - Mistral-based models
- **Cerebras** (18) - Cerebras-specific tokenizer
- **None** (0) - Default/unknown

See `MapTokenizerTypeToEnum()` in the script for complete mapping (supports 18+ tokenizer types).

## Adding New Providers

To add a new provider to this system:

1. **Add provider configuration** to `provider-config.json`:
   ```json
   "newprovider": {
     "providerType": 11,
     "websiteUrl": "https://newprovider.ai",
     "modelCardUrl": "https://docs.newprovider.ai/models",
     "supportsAudio": false
   }
   ```

2. **Create model JSON file**: `newprovider-models.json` with model specifications

3. **Build the release**: the wildcard embedded-resource rule includes the new JSON automatically

4. **Verify**: run the bundled catalog tests and import through the Admin API

No code changes required - the script is fully configuration-driven!

## Architecture Benefits

### Previous Approach (Deprecated)
- Separate scripts for each provider (`generate-cerebras-sql.cs`, `generate-sambanova-sql.cs`)
- 99% code duplication between scripts
- Provider metadata hardcoded in each script
- Adding new provider = copy entire script + search/replace

### New Unified Approach
- Single script (`generate-provider-sql.cs`)
- Zero code duplication
- Provider metadata in centralized config
- Adding new provider = edit JSON config only

### Maintainability Wins
- Bug fixes apply to all providers automatically
- Tokenizer mapping maintained in one place
- SQL generation logic unified
- Easy to extend for new providers

## Notes

- Script uses `ON CONFLICT DO NOTHING` to preserve manual changes to authors/series
- Script uses conditional INSERT/UPDATE for models and costs
- All models are marked as `IsActive = true` by default
- All providers use standard OpenAI-compatible chat completion parameters
- Pricing is per million tokens (input/output)
- The script is idempotent - safe to run multiple times
- Generated SQL is wrapped in BEGIN/COMMIT transaction

## Performance Characteristics

Both Cerebras and SambaNova are known for **ultra-fast inference**:

| Provider | Technology | Speed Range | Key Advantage |
|----------|------------|-------------|---------------|
| Cerebras | CS-3 wafer-scale chip | 2100-3000 tokens/sec | Single chip, FP16 precision |
| SambaNova | RDU chips | Up to 250 tokens/sec | Custom AI accelerators |

Both providers are 10-30x faster than traditional GPU-based inference.

## Comparison of Approaches

| Aspect | Hand-Maintained JSON | OpenRouter API Fetch | Replicate Scraper |
|--------|---------------------|---------------------|-------------------|
| Data Source | Static JSON files | Live API | Web scraping |
| Providers | Cerebras, Groq, SambaNova | OpenRouter | Replicate |
| Model Count | 4-15 per provider | 300+ models | 20-50+ models |
| Parameters | Standard (OpenAI) | Standard (OpenAI) | Unique per model |
| Update Process | Edit JSON manually | Run fetch script | Update scraping logic |
| Complexity | Low | Low (~200 lines) | High (~1400 lines) |
| Extensibility | Add JSON config | N/A (OpenRouter-specific) | Duplicate scraper |

## Future Enhancements

If providers significantly expand their model catalogs, consider:

- **Web scraping** if any provider exceeds 15-20 models
- **API-based extraction** if providers offer public model APIs
- **Automated pricing updates** from pricing pages
- **Preview model tracking** with automated promotion to production
- **Multi-provider comparison** tool using the unified config

For now, manual JSON maintenance with centralized configuration is the most efficient approach.

## Migration from Old Scripts

The old provider-specific scripts (`scripts/db/cerebras/generate-cerebras-sql.cs` and `scripts/db/sambanova/generate-sambanova-sql.cs`) have been consolidated into this unified system. Generated SQL is identical, but the new approach is more maintainable and extensible.

**Old usage:**
```bash
cd scripts/db/cerebras
./generate-cerebras-sql.cs
```

**New usage:**
```bash
cd scripts/db/providers
./generate-provider-sql.cs cerebras
```
