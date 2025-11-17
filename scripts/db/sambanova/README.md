# SambaNova Models SQL Generator

This directory contains scripts for generating SQL to populate SambaNova models in the Conduit database.

## Overview

Like the Groq approach, the SambaNova implementation uses a **static JSON + generator** pattern because:

- SambaNova has a small, stable set of models (~12-15 models)
- All models use standard OpenAI-compatible parameters
- Model specifications are clearly documented
- Pricing information requires manual verification from multiple sources
- Manual maintenance of JSON is faster than building/maintaining a web scraper

## Files

- **sambanova-models.json** - Hand-maintained list of SambaNova models with specifications
- **generate-sambanova-sql.cs** - C# script that reads JSON and generates PostgreSQL SQL
- **UPDATING-MODELS.md** - Comprehensive guide for updating the JSON file (designed for LLM consumption)
- **README.md** - This file

## Usage

### Basic Usage

```bash
# Generate SQL (outputs to sambanova-models.sql)
./generate-sambanova-sql.cs

# Generate SQL with custom filename
./generate-sambanova-sql.cs my-sambanova-models.sql
```

### Executing Generated SQL

```bash
# Execute against local database
psql -h localhost -U conduit -d conduit_db < sambanova-models.sql

# Execute via Docker
docker exec -i conduit-postgres psql -U conduit -d conduit_db < sambanova-models.sql
```

## Model Data Structure

Each model in `sambanova-models.json` includes:

```json
{
  "model-id": {
    "name": "model-name",
    "family": "Model Family",
    "series": "Model Series Name",
    "owner": "company-name",
    "maxInputTokens": 128000,
    "maxOutputTokens": 128000,
    "tokenizerType": "LLaMA3|Cl100KBase|Tiktoken|Mistral",
    "supportsChat": true,
    "supportsStreaming": true,
    "supportsVision": false,
    "supportsFunctionCalling": true,
    "supportsEmbeddings": false,
    "supportsAudio": false,
    "inputPricePerMillion": 3.00,
    "outputPricePerMillion": 4.50,
    "speedTokensPerSec": 250,
    "notes": "Optional notes"
  }
}
```

## Generated SQL Structure

The script generates SQL that creates:

1. **ModelAuthors** - Model creators (Meta, DeepSeek, Qwen, OpenAI, etc.)
2. **ModelSeries** - Model series/families (Llama 3.3 Series, DeepSeek V3 Series, etc.)
3. **Models** - Individual model records with capabilities and token limits
4. **ModelIdentifiers** - Maps SambaNova model IDs to Models (ProviderType.SambaNova = 10)
5. **ModelCosts** - Pricing information per model

## Current Models

### Production Models (6)
- DeepSeek-R1-0528 (128K context, reasoning model)
- DeepSeek-V3-0324 (128K context, 250 tokens/sec)
- DeepSeek-V3.1 (128K context, latest iteration)
- DeepSeek-R1-Distill-Llama-70B (128K context, distilled reasoning)
- Meta-Llama-3.3-70B-Instruct (128K context, 70B parameters)
- Meta-Llama-3.1-8B-Instruct (16K context, fast inference)

### Preview Models (6)
- Llama-4-Maverick-17B-128E-Instruct (128K context, **multimodal with vision**)
- gpt-oss-120b (128K context, OpenAI-compatible)
- Whisper-Large-v3 (**audio transcription**)
- Qwen3-32B (8K context, 32B parameters)
- Llama-3.3-Swallow-70B-Instruct-v0.4 (16K context, Tokyo Tech variant)
- E5-Mistral-7B-Instruct (4K context, **embedding-optimized**)

## Special Capabilities

### Vision Support (1 model)
- Llama-4-Maverick-17B-128E-Instruct: Supports up to 5 images (20MB max each)

### Audio Support (1 model)
- Whisper-Large-v3: Audio transcription, 25MB max file size

### Embedding Support (1 model)
- E5-Mistral-7B-Instruct: Optimized for embeddings

## Updating Models

When SambaNova releases new models or updates specifications:

1. **Read UPDATING-MODELS.md** - Comprehensive guide for updating the JSON
2. **Edit sambanova-models.json** to add/update model data
3. **Run ./generate-sambanova-sql.cs** to generate new SQL
4. **Review the generated SQL**
5. **Execute against your database**

The UPDATING-MODELS.md guide is specifically written to be followed by LLMs (like Claude Code) and includes:
- Data source URLs for model specs and pricing
- Step-by-step decision rules for each field
- Examples for every model family
- Common pitfalls to avoid
- Validation checklist

## Provider Type

SambaNova uses **ProviderType = 10** in the database.

## Tokenizer Types

Common tokenizer types used by SambaNova models:

- **LLaMA3** (12) - Llama 3.x and DeepSeek models
- **Cl100KBase** (1) - OpenAI GPT models
- **Tiktoken** (23) - Qwen and Whisper models
- **Mistral** (13) - Mistral-based models
- **None** (0) - Default/unknown

See `MapTokenizerTypeToEnum()` in the script for complete mapping.

## Pricing Notes

SambaNova pricing is competitive and emphasizes speed:

- **DeepSeek models**: $3-7 per million tokens (reasoning-focused)
- **Llama models**: $0.10-1.20 per million tokens (various sizes)
- **Preview models**: $0.20-0.80 per million tokens (experimental)
- **Audio models**: Typically priced per audio minute (not included in token pricing)

Pricing should be verified from:
- https://cloud.sambanova.ai/plans/pricing (primary source, requires page load)
- https://artificialanalysis.ai/providers/sambanova (benchmarks and comparisons)
- SambaNova blog announcements for new models

## Performance

SambaNova is known for **ultra-fast inference** powered by custom AI chips (RDU):

- DeepSeek-V3-0324: Up to 250 tokens/second
- Generally 10x faster than GPU-based inference
- Optimized for low-latency, high-throughput workloads

## Notes

- Script uses `ON CONFLICT DO NOTHING` to preserve manual changes
- All models are marked as `IsActive = true` by default
- SambaNova models use standard OpenAI-compatible chat completion parameters
- Pricing is per million tokens (input/output)
- The script is idempotent - safe to run multiple times
- Preview models are experimental and may have limitations

## Comparison with Other Providers

| Aspect | SambaNova Approach | Groq Approach | Replicate Approach |
|--------|-------------------|---------------|-------------------|
| Data Source | Static JSON file | Static JSON file | Web scraping |
| Model Count | 12-15 models | 5-7 models | 20-50+ models per type |
| Parameters | Standard (OpenAI) | Standard (OpenAI) | Unique per model |
| Update Frequency | Monthly-Quarterly | Quarterly | Monthly |
| Maintenance | Edit JSON file | Edit JSON file | Update scraping logic |
| Complexity | Low (~300 lines) | Low (~200 lines) | High (~1400 lines) |
| Special Features | Audio, Vision, Embeddings | Vision, MoE models | Custom parameters |

## Future Enhancements

If SambaNova significantly expands their model catalog, consider:

- Web scraping from documentation page (all data is on one page)
- API-based extraction if SambaNova provides a public models API
- Automated pricing updates from pricing page (would need to handle dynamic loading)
- Integration with SambaNova's community forum for early announcements

For now, manual JSON maintenance with the UPDATING-MODELS.md guide is the most efficient approach.

## Data Source References

- **Official Docs**: https://docs.sambanova.ai/docs/en/models/sambacloud-models
- **Pricing Page**: https://cloud.sambanova.ai/plans/pricing
- **Performance Analysis**: https://artificialanalysis.ai/providers/sambanova
- **Community**: https://community.sambanova.ai/
- **Blog**: https://sambanova.ai/blog/
