# Cerebras Models SQL Generator

This directory contains scripts for generating SQL to populate Cerebras models in the Conduit database.

## Overview

Like the Groq and SambaNova approaches, the Cerebras implementation uses a **static JSON + generator** pattern because:

- Cerebras has a small, stable set of models (4 production models currently)
- All models use standard OpenAI-compatible parameters
- Model specifications are clearly documented in Cerebras Inference API docs
- Pricing information is well-documented and stable
- Manual maintenance of JSON is faster than building/maintaining a web scraper
- Preview models excluded until pricing is finalized

## Files

- **cerebras-models.json** - Hand-maintained list of Cerebras production models with specifications
- **generate-cerebras-sql.cs** - C# script that reads JSON and generates PostgreSQL SQL
- **README.md** - This file

## Usage

### Basic Usage

```bash
# Generate SQL (outputs to cerebras-models.sql)
./generate-cerebras-sql.cs

# Generate SQL with custom filename
./generate-cerebras-sql.cs my-cerebras-models.sql
```

### Executing Generated SQL

```bash
# Execute against local database
psql -h localhost -U conduit -d conduit_db < cerebras-models.sql

# Execute via Docker
docker exec -i conduit-postgres psql -U conduit -d conduit_db < cerebras-models.sql
```

## Model Data Structure

Each model in `cerebras-models.json` includes:

```json
{
  "model-id": {
    "name": "model-name",
    "family": "Model Family",
    "series": "Model Series Name",
    "owner": "company-name",
    "maxInputTokens": 128000,
    "maxOutputTokens": 65536,
    "tokenizerType": "LLaMA3|Cl100KBase|Tiktoken",
    "supportsChat": true,
    "supportsStreaming": true,
    "supportsVision": false,
    "supportsFunctionCalling": true,
    "supportsEmbeddings": false,
    "inputPricePerMillion": 0.85,
    "outputPricePerMillion": 1.20,
    "speedTokensPerSec": 2100,
    "notes": "Model description with cross-references to other providers"
  }
}
```

## Generated SQL Structure

The script generates SQL that creates:

1. **ModelAuthors** - Model creators (Meta, OpenAI, Qwen, etc.)
2. **ModelSeries** - Model series/families (Llama 3.1 Series, GPT OSS Series, etc.)
3. **Models** - Individual model records with capabilities and token limits
4. **ModelIdentifiers** - Maps Cerebras model IDs to Models (ProviderType.Cerebras = 9)
5. **ModelCosts** - Pricing information per model

## Current Models (Production Only)

### 1. Llama 3.1 8B
- **ID**: `llama3.1-8b`
- **Context**: 32K tokens (paid tier)
- **Output**: 8K tokens
- **Speed**: ~2200 tokens/sec
- **Pricing**: $0.10/M input, $0.10/M output
- **Capabilities**: Chat, streaming, tool calling, structured outputs
- **Cross-Reference**: Same as Groq `llama-3.1-8b-instant`, SambaNova `Meta-Llama-3.1-8B-Instruct`

### 2. Llama 3.3 70B
- **ID**: `llama-3.3-70b`
- **Context**: 128K tokens (paid tier)
- **Output**: 65K tokens (paid tier)
- **Speed**: ~2100 tokens/sec
- **Pricing**: $0.85/M input, $1.20/M output
- **Capabilities**: Chat, streaming
- **Cross-Reference**: Same as Groq `llama-3.3-70b-versatile`, SambaNova `Meta-Llama-3.3-70B-Instruct`

### 3. OpenAI GPT OSS 120B
- **ID**: `gpt-oss-120b`
- **Context**: 131K tokens (paid tier)
- **Output**: 40K tokens (paid tier)
- **Speed**: ~3000 tokens/sec (fastest!)
- **Pricing**: $0.35/M input, $0.75/M output
- **Capabilities**: Reasoning, streaming, tool calling, structured outputs
- **Cross-Reference**: Same as Groq `openai/gpt-oss-120b`, SambaNova `gpt-oss-120b`

### 4. Qwen 3 32B
- **ID**: `qwen-3-32b`
- **Context**: 131K tokens (paid tier)
- **Output**: 8K tokens
- **Speed**: ~2600 tokens/sec
- **Pricing**: $0.40/M input, $0.80/M output
- **Capabilities**: Hybrid reasoning mode, streaming, tool calling, structured outputs
- **Cross-Reference**: Same as Groq `qwen/qwen3-32b`, SambaNova `Qwen3-32B`

## Preview Models (Not Included)

The following preview models are documented but excluded from the seed script until pricing is finalized:

- **Qwen 3 235B Instruct** (`qwen-3-235b-a22b-instruct-2507`) - 131K context, MoE model
- **Z.ai GLM 4.6** (`zai-glm-4.6`) - 357B parameters, unique to Cerebras

When these models move to production and pricing is available, they can be added to `cerebras-models.json`.

## Cerebras Competitive Advantages

### Ultra-Fast Inference
Cerebras runs on custom CS-3 wafer-scale chips delivering unprecedented speed:

- **llama3.1-8b**: 2200 tokens/sec (20-30x faster than GPUs)
- **llama-3.3-70b**: 2100 tokens/sec
- **gpt-oss-120b**: 3000 tokens/sec (fastest production model)
- **qwen-3-32b**: 2600 tokens/sec

### Free Tier
- **1 million tokens/day** across all models
- **30 requests/minute**, 60K input tokens/minute
- Community support via Discord

### Paid Tier Benefits
- **Higher context windows** (8K → 32K for Llama 3.1 8B, 65K → 131K for others)
- **Increased output limits** (8K → 65K for Llama 3.3 70B, 32K → 40K for GPT OSS)
- **1000 requests/minute**, 1M input tokens/minute
- Priority support

## Updating Models

When Cerebras releases new models or updates specifications:

1. **Check official documentation** at https://inference-docs.cerebras.ai/models/overview
2. **Verify pricing** at https://www.cerebras.ai/pricing
3. **Edit cerebras-models.json** to add/update model data
4. **Run ./generate-cerebras-sql.cs** to generate new SQL
5. **Review the generated SQL**
6. **Execute against your database**

### Data Sources for Updates

- **Model Specs**: Individual model pages (e.g., https://inference-docs.cerebras.ai/models/llama-31-8b)
- **Pricing**: https://www.cerebras.ai/pricing (official pricing table)
- **Performance Benchmarks**: https://artificialanalysis.ai/providers/cerebras
- **Release Notes**: Cerebras blog and Discord announcements

## Provider Type

Cerebras uses **ProviderType = 9** in the database.

## Tokenizer Types

Tokenizer types used by Cerebras models:

- **LLaMA3** (12) - Llama 3.1 and 3.3 models
- **Cl100KBase** (1) - GPT OSS models
- **Tiktoken** (23) - Qwen models

See `MapTokenizerTypeToEnum()` in the script for complete mapping.

## Pricing Notes

Cerebras pricing is competitive and varies by model:

- **Llama 3.1 8B**: $0.10/M (combined input/output)
- **Llama 3.3 70B**: $0.85/M input, $1.20/M output
- **GPT OSS 120B**: $0.35/M input, $0.75/M output
- **Qwen 3 32B**: $0.40/M input, $0.80/M output

All pricing verified from: https://www.cerebras.ai/pricing

## Performance Characteristics

Cerebras inference is powered by the **CS-3 wafer-scale chip**:

- **Single chip architecture** - No multi-chip bottlenecks
- **FP16 precision** - Quality preservation vs INT8 quantization
- **20-30x faster than GPUs** - Consistent low latency
- **Deterministic performance** - Predictable throughput

## Model Overlaps with Other Providers

All 4 Cerebras production models are available on other providers, but with different identifiers:

| Cerebras ID | Groq ID | SambaNova ID | Notes |
|-------------|---------|--------------|-------|
| llama3.1-8b | llama-3.1-8b-instant | Meta-Llama-3.1-8B-Instruct | Same underlying model |
| llama-3.3-70b | llama-3.3-70b-versatile | Meta-Llama-3.3-70B-Instruct | Same underlying model |
| gpt-oss-120b | openai/gpt-oss-120b | gpt-oss-120b | Same underlying model |
| qwen-3-32b | qwen/qwen3-32b | Qwen3-32B | Same underlying model |

**Note**: While the underlying models are the same, each provider may have different:
- Context window limits (free vs paid tiers)
- Pricing structures
- Rate limits
- Performance characteristics

## Notes

- Script uses `ON CONFLICT DO NOTHING` to preserve manual changes
- All models are marked as `IsActive = true` by default
- Cerebras models use standard OpenAI-compatible chat completion parameters
- Pricing is per million tokens (input/output)
- The script is idempotent - safe to run multiple times
- Context and output limits reflect **paid tier maximums** (free tier has lower limits)

## Comparison with Other Providers

| Aspect | Cerebras Approach | Groq Approach | SambaNova Approach | Replicate Approach |
|--------|------------------|---------------|-------------------|-------------------|
| Data Source | Static JSON file | Static JSON file | Static JSON file | Web scraping |
| Model Count | 4 production models | 5-7 models | 12-15 models | 20-50+ models per type |
| Parameters | Standard (OpenAI) | Standard (OpenAI) | Standard (OpenAI) | Unique per model |
| Update Frequency | Quarterly | Quarterly | Monthly-Quarterly | Monthly |
| Maintenance | Edit JSON file | Edit JSON file | Edit JSON file | Update scraping logic |
| Complexity | Low (~280 lines) | Low (~280 lines) | Low (~290 lines) | High (~1400 lines) |
| Special Features | Ultra-fast inference | Vision, MoE models | Audio, Vision, Embeddings | Custom parameters |
| Unique Selling Point | Speed (CS-3 chip) | Speed + variety | Speed + features | Model diversity |

## Future Enhancements

When Cerebras expands their model catalog, consider:

- **Web scraping** if model count exceeds 15-20 models
- **API-based extraction** if Cerebras provides a public models API
- **Automated pricing updates** from pricing page
- **Preview model tracking** with automated promotion to production

For now, manual JSON maintenance is the most efficient approach given the small model count.

## Data Source References

- **Official Docs**: https://inference-docs.cerebras.ai/models/overview
- **Model Details**: https://inference-docs.cerebras.ai/models/ (individual model pages)
- **Pricing Page**: https://www.cerebras.ai/pricing
- **Performance Analysis**: https://artificialanalysis.ai/providers/cerebras
- **Company Info**: https://cerebras.ai
- **API Reference**: https://inference-docs.cerebras.ai/api-reference/chat-completions
