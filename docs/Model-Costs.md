# Model Cost Configuration

This document provides information about the model cost configuration in Conduit, including default costs for popular frontier models from Anthropic and OpenAI.

## Overview

Model costs in Conduit are used to:
- Calculate usage costs for virtual keys
- Enforce budget limits
- Generate cost reports and dashboards
- Track spending across different models and providers

## Default Model Costs

Conduit includes default costs for popular frontier models from Anthropic and OpenAI. These costs are based on the official pricing from each provider as of May 2025.

### Anthropic Models

| Model | Input Cost (per 1K tokens) | Output Cost (per 1K tokens) |
|-------|----------------------------|----------------------------|
| `anthropic/claude-3-opus-20240229` | $15.00 | $75.00 |
| `anthropic/claude-3-sonnet-20240229` | $3.00 | $15.00 |
| `anthropic/claude-3-haiku-20240307` | $0.25 | $1.25 |
| `anthropic/claude-3*` (wildcard) | $3.00 | $15.00 |
| `anthropic/claude-2.1` | $8.00 | $24.00 |
| `anthropic/claude-2.0` | $8.00 | $24.00 |
| `anthropic/claude-instant-1.2` | $0.80 | $2.40 |

### OpenAI Models

| Model | Input Cost (per 1K tokens) | Output Cost (per 1K tokens) |
|-------|----------------------------|----------------------------|
| `openai/gpt-4o` | $5.00 | $15.00 |
| `openai/gpt-4o-mini` | $0.50 | $1.50 |
| `openai/gpt-4-turbo` | $10.00 | $30.00 |
| `openai/gpt-4-1106-preview` | $10.00 | $30.00 |
| `openai/gpt-4-0125-preview` | $10.00 | $30.00 |
| `openai/gpt-4-vision-preview` | $10.00 | $30.00 |
| `openai/gpt-4-32k` | $60.00 | $120.00 |
| `openai/gpt-4` | $30.00 | $60.00 |
| `openai/gpt-4*` (wildcard) | $10.00 | $30.00 |
| `openai/gpt-3.5-turbo` | $0.50 | $1.50 |
| `openai/gpt-3.5-turbo-16k` | $1.00 | $2.00 |
| `openai/gpt-3.5*` (wildcard) | $0.50 | $1.50 |

### Embedding Models

| Model | Embedding Cost (per 1K tokens) |
|-------|------------------------------|
| `openai/text-embedding-3-small` | $0.02 |
| `openai/text-embedding-3-large` | $0.13 |
| `openai/text-embedding-ada-002` | $0.10 |

### Image Generation Models

| Model | Cost per Image |
|-------|---------------|
| `openai/dall-e-3` | $0.04 |
| `openai/dall-e-2` | $0.02 |

## Wildcard Pattern Matching

Conduit supports wildcard pattern matching for model costs, allowing you to:
- Define costs for specific models (e.g., `openai/gpt-4o`)
- Define costs for families of models (e.g., `openai/gpt-4*`)
- Apply costs by pattern (e.g., `*-embedding*`)

When determining the cost for a model, Conduit:
1. First looks for an exact match by model name
2. If no exact match is found, searches for a match using wildcard patterns
3. Uses the longest prefix match when multiple patterns could apply

## Managing Model Costs

You can manage model costs in the Conduit Admin UI at `/model-costs`:
- View all configured model costs
- Add new model costs
- Edit existing model costs
- Delete model costs

## Reloading Default Costs

If you need to reload the default model costs for Anthropic and OpenAI, you can use the included script:

```bash
# Make the script executable (if needed)
chmod +x add-frontier-model-costs.sh

# Run the script
./add-frontier-model-costs.sh
```

The script will:
1. Delete any existing model costs for Anthropic and OpenAI models
2. Insert the default costs based on current provider pricing
3. Display a summary of the added costs

## Custom Cost Scripts

If you need to add custom model costs, you can:

1. Create a SQL script with your cost definitions
2. Use the PostgreSQL `psql` command to execute it:

```bash
docker compose exec postgres psql -U conduit -d conduitdb -f /path/to/your/script.sql
```

## Pricing Sources

The default costs are based on the official pricing pages from each provider:
- [Anthropic Pricing](https://www.anthropic.com/pricing)
- [OpenAI Pricing](https://openai.com/pricing)

Always check the official pricing pages for the most up-to-date information, as these values may change.