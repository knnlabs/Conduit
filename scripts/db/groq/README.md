# Groq Models SQL Generator

> **⚠️ DEPRECATION NOTICE**
> This script has been migrated to the consolidated provider system at `scripts/db/providers/`.
> Please use `./generate-provider-sql.cs groq` instead.
> This directory will be removed in a future release.
> See: [scripts/db/providers/README.md](../providers/README.md) for migration details.

---

This directory contains scripts for generating SQL to populate Groq models in the Conduit database.

## Overview

Unlike the Replicate extraction script which scrapes web pages, the Groq approach uses a simpler **static JSON + generator** pattern because:

- Groq has a small, stable set of production models (~5-7 models)
- All models use standard OpenAI-compatible parameters
- Model specifications are clearly documented and change infrequently
- Manual maintenance of JSON is faster than building/maintaining a web scraper

## Files

- **groq-models.json** - Hand-maintained list of Groq production models with specifications
- **generate-groq-sql.cs** - C# script that reads JSON and generates PostgreSQL SQL
- **README.md** - This file

## Usage

### Basic Usage

```bash
# Generate SQL (outputs to groq-models.sql)
./generate-groq-sql.cs

# Generate SQL with custom filename
./generate-groq-sql.cs my-groq-models.sql
```

### Executing Generated SQL

```bash
# Execute against local database
psql -h localhost -U conduit -d conduit_db < groq-models.sql

# Execute via Docker
docker exec -i conduit-postgres psql -U conduit -d conduit_db < groq-models.sql
```

## Model Data Structure

Each model in `groq-models.json` includes:

```json
{
  "model-id": {
    "name": "model-name",
    "family": "Model Family",
    "series": "Model Series Name",
    "owner": "meta|openai|groq",
    "maxInputTokens": 131072,
    "maxOutputTokens": 65536,
    "tokenizerType": "LLaMA3|Cl100KBase",
    "supportsChat": true,
    "supportsStreaming": true,
    "supportsVision": false,
    "supportsFunctionCalling": false,
    "supportsEmbeddings": false,
    "inputPricePerMillion": 0.05,
    "outputPricePerMillion": 0.08,
    "speedTokensPerSec": 560,
    "notes": "Optional notes"
  }
}
```

## Generated SQL Structure

The script generates SQL that creates:

1. **ModelAuthors** - Model creators (Meta, OpenAI, etc.)
2. **ModelSeries** - Model series/families (Llama 3.1 Series, GPT OSS Series, etc.)
3. **Models** - Individual model records with capabilities and token limits
4. **ModelIdentifiers** - Maps Groq model IDs to Models (ProviderType.Groq = 2)
5. **ModelCosts** - Pricing information per model
6. **ModelCostMappings** - Links models to their pricing

## Updating Models

When Groq releases new models or updates specifications:

1. Edit `groq-models.json` to add/update model data
2. Run `./generate-groq-sql.cs` to generate new SQL
3. Review the generated SQL
4. Execute against your database

## Provider Type

Groq uses **ProviderType = 2** in the database.

## Tokenizer Types

Common tokenizer types used by Groq models:

- **LLaMA3** (12) - Llama 3.x models
- **Cl100KBase** (1) - OpenAI GPT models
- **None** (0) - Default/unknown

See `MapTokenizerTypeToEnum()` in the script for complete mapping.

## Notes

- Script uses `ON CONFLICT DO NOTHING` to preserve manual changes
- All models are marked as `IsActive = true` by default
- Groq models use standard OpenAI-compatible chat completion parameters
- Pricing is per million tokens (input/output)
- The script is idempotent - safe to run multiple times

## Comparison with Replicate Script

| Aspect | Groq Approach | Replicate Approach |
|--------|---------------|-------------------|
| Data Source | Static JSON file | Web scraping |
| Model Count | 5-7 models | 20-50+ models per type |
| Parameters | Standard (OpenAI-compatible) | Unique per model |
| Update Frequency | Quarterly | Monthly |
| Maintenance | Edit JSON file | Update scraping logic |
| Complexity | Low (~200 lines) | High (~1400 lines) |

## Future Enhancements

If Groq significantly expands their model catalog, consider:

- Web scraping from documentation page (simpler than Replicate since all data is on one page)
- API-based extraction if Groq provides a public models API
- Automated pricing updates from Groq pricing page

For now, manual JSON maintenance is the most efficient approach.
