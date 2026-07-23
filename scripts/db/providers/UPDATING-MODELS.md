# Updating Provider Models JSON

This document provides step-by-step instructions for updating provider model JSON files (`{provider}-models.json`) when providers release new models or update specifications. These instructions are designed to be followed by either humans or LLMs (like Claude Code).

## When to Update

Update a provider's JSON file when:
- The provider announces new models
- Model specifications change (context windows, pricing, capabilities)
- Models are deprecated or moved between production/preview status
- New capabilities are added to existing models

## Provider-Specific Data Sources

Before updating, check `provider-config.json` for provider metadata (ProviderType, URLs), then consult provider-specific documentation:

### Cerebras
1. **Model List**: https://inference-docs.cerebras.ai/models/overview
2. **Pricing**: https://www.cerebras.ai/pricing
3. **Performance**: https://artificialanalysis.ai/providers/cerebras
4. **Notes**: Small model catalog (4 production models), fast updates

### Groq
1. **Model List**: https://console.groq.com/docs/models
2. **Pricing**: https://groq.com/pricing
3. **Performance**: https://artificialanalysis.ai/providers/groq
4. **Notes**: Moderate catalog (12 models), ultra-fast LPU inference, vision support (3 models), function calling (5 models)

### SambaNova
1. **Model List**: https://docs.sambanova.ai/docs/en/models/sambacloud-models
2. **Pricing**: https://cloud.sambanova.ai/plans/pricing (dynamically loaded - wait for full render)
3. **Performance**: https://artificialanalysis.ai/providers/sambanova
4. **Notes**: Larger catalog (12+ models), includes audio/vision/embeddings

### General Sources (All Providers)

1. **Model List and Specifications**
   - Check provider's official documentation for model names, context windows, and capabilities
   - Look for production vs preview/experimental sections

2. **Pricing Information**
   - Find provider's pricing page (per million tokens)
   - If pricing page doesn't load, use web search: "[Provider] [model-name] pricing per million tokens"
   - Check provider blog announcements for new model pricing

3. **API Documentation**
   - Verify OpenAI API compatibility
   - Check function calling/tool use support
   - Confirm streaming support

4. **Model Cards and Announcements**
   - Search: "[Model Name] [Provider] announcement"
   - Contains: Parameter counts, tokenizer details, special features

5. **Artificial Analysis**
   - URL: https://artificialanalysis.ai/providers/[provider-name]
   - Contains: Performance benchmarks, pricing comparisons, speed metrics

## Step-by-Step Update Process

### Step 1: Fetch Current Model List

Visit the provider's official model documentation (see Provider-Specific Data Sources above).

For each model listed, record:
- **Model ID**: The exact identifier used in API calls (e.g., `Meta-Llama-3.3-70B-Instruct`)
- **Display Name**: Human-readable name (often same as Model ID)
- **Context Window**: Maximum input tokens (e.g., `128000`, `16000`)
- **Special Capabilities**: Vision support, audio support, multimodal features

**Example Production Models Section:**
```
Production Models:
- DeepSeek-R1-0528 (128k context)
- Meta-Llama-3.3-70B-Instruct (128k context)
- Meta-Llama-3.1-8B-Instruct (16k context)
```

**Example Preview Models Section:**
```
Preview Models:
- Llama-4-Maverick-17B-128E-Instruct (128k, supports up to 5 images)
- Whisper-Large-v3 (audio model, 25MB max file size)
```

### Step 2: Fetch Pricing Data

Visit the provider's pricing page (see Provider-Specific Data Sources above) and wait for the pricing table to fully load.

For each model, record:
- **Input Price**: Cost per million input tokens (USD)
- **Output Price**: Cost per million output tokens (USD)

If the pricing page doesn't load or pricing is missing:
1. Use web search: "[Provider] [model-name] pricing per million tokens"
2. Check provider blog announcements
3. Look for pricing in API documentation

**Known Pricing Patterns:**
- Llama 8B models: ~$0.10 input / $0.20 output
- Llama 70B models: ~$0.60 input / $1.20 output
- Llama 405B models: ~$5.00 input / $10.00 output
- DeepSeek models: Often competitive with or cheaper than equivalent Llama models

### Step 3: Determine Model Families and Series

Group models into logical families and series:

**Model Family**: The broad category (e.g., "Llama", "DeepSeek", "Qwen")
**Model Series**: Specific version line (e.g., "Llama 3.3 Series", "DeepSeek V3 Series")

**Extraction Rules:**
- Look for version numbers in model names (3.1, 3.3, V3, V3.1, R1)
- Group models with same base name and version
- Keep series names consistent with other models in the same family

**Examples:**
```
Model: Meta-Llama-3.3-70B-Instruct
  Family: Llama
  Series: Llama 3.3 Series
  Owner: meta

Model: DeepSeek-V3.1
  Family: DeepSeek
  Series: DeepSeek V3 Series
  Owner: deepseek

Model: Qwen3-32B
  Family: Qwen
  Series: Qwen 3 Series
  Owner: qwen
```

### Step 4: Determine Tokenizer Types

Use these rules to determine tokenizer type:

| Model Family | Tokenizer Type | Enum Name |
|--------------|----------------|-----------|
| Llama 3.x, Llama 4.x | LLaMA3 | LLaMA3 |
| Llama 2.x | LLaMA2 | LLaMA2 |
| DeepSeek | LLaMA3 | LLaMA3 |
| Qwen/Qwen2/Qwen3 | Tiktoken | Tiktoken |
| GPT-based models | Cl100KBase | Cl100KBase |
| Mistral | Mistral | Mistral |
| Whisper (audio) | Tiktoken | Tiktoken |

If unsure, search: "[Model Name] tokenizer" or "[Model Family] tokenizer"

### Step 5: Determine Capabilities

For each model, determine directional modalities and operation capabilities:

#### inputModalities / outputModalities
- Allowed values: `text`, `image`, `audio`, `video`, `file`
- Input and output are independent: `["text", "video"] -> ["text"]` means video understanding, not video generation
- Omit the arrays when the provider does not publish enough information
- Use an empty array only when the provider explicitly says no modalities are supported
- Set `capabilitySource` to `Curated`, `ProviderApi`, or `Manual`, as appropriate

#### supportsChat
- **true** for: All instruct/chat models, conversational models
- **false** for: Base models, embedding models, pure audio transcription
- Default: **true** (most provider models are chat-optimized)

#### supportsStreaming
- **true** for: All text generation models (most providers support streaming)
- **false** for: Embedding models, audio transcription (batch only)
- Default: **true**

#### supportsVision
- Legacy compatibility alias for `inputModalities` containing `image`
- New metadata and UI logic must use the directional modality array

#### supportsFunctionCalling
- **true** for: Models with OpenAI API compatibility and 70B+ parameters
- **true** for: Models explicitly mentioning tool use or function calling
- **false** for: Smaller models (< 8B), audio models, embedding models
- Research: Check API docs or model cards for "function calling" or "tool use"
- Default assumption: **true** for 70B+ instruct models, **false** for others

#### supportsEmbeddings
- **true** only for: Models explicitly designed for embeddings (e.g., E5-Mistral-7B-Instruct)
- **false** for: All chat/instruct models
- Default: **false**

#### audio operations
- `supportsSpeechToText`: audio input used for transcription
- `supportsTextToSpeech`: audio output synthesized from text
- General audio understanding is represented by `inputModalities` containing `audio`

### Step 6: Extract Parameter Counts

Look for parameter counts in model names or documentation:

**Extraction Rules:**
- Model names with "8B" → 8 billion parameters
- Model names with "70B" → 70 billion parameters
- Model names with "405B" → 405 billion parameters
- Model names with "32B" → 32 billion parameters
- For compound models (e.g., "17B-128E"), use the active parameter count (17B)

If parameter count is not in the name:
1. Search "[Model Name] parameters"
2. Check model announcement or model card
3. Leave as `null` if unknown

### Step 7: Determine Speed Metrics

Speed is measured in tokens per second (tokens/sec):

**Sources:**
1. Provider announcements (many providers highlight speed)
2. Artificial Analysis benchmarks: https://artificialanalysis.ai/providers/[provider-name]
3. Model documentation

**If speed data is unavailable:**
- Set to `null` (do not guess)
- Note: Actual inference speed varies by model and provider infrastructure

### Step 8: Write Notes Field

The notes field should include:
- Preview status if applicable: "Preview: [description]"
- Special capabilities: multimodal, audio, vision details
- Context window size if notable (e.g., "128K context")
- Parameter architecture if relevant (e.g., "MoE with N experts")
- Performance claims if verified (e.g., "Optimized for speed")
- Limitations (e.g., "Max 5 images, 20MB each")

**Examples:**
```
"Fast inference, 128K context window"
"Preview: Multimodal model with vision support (up to 5 images, 20MB max each)"
"Audio transcription model, 25MB max file size"
"128K context, optimized for extended conversations"
```

## JSON Structure

Each model entry follows this structure:

```json
{
  "model-id-exactly-as-used-in-api": {
    "name": "Display name (usually same as model ID)",
    "family": "Model Family",
    "series": "Model Series Name",
    "owner": "company-name-lowercase",
    "maxInputTokens": 128000,
    "maxOutputTokens": 32768,
    "tokenizerType": "LLaMA3|Cl100KBase|Tiktoken|Mistral",
    "supportsChat": true,
    "supportsStreaming": true,
    "inputModalities": ["text"],
    "outputModalities": ["text"],
    "capabilitySource": "Curated",
    "supportsFunctionCalling": true,
    "supportsEmbeddings": false,
    "supportsImageGeneration": false,
    "supportsVideoGeneration": false,
    "supportsSpeechToText": false,
    "supportsTextToSpeech": false,
    "supportsRerank": false,
    "inputPricePerMillion": 0.60,
    "outputPricePerMillion": 1.20,
    "speedTokensPerSec": 300,
    "notes": "Description with special features"
  }
}
```

### Field Notes:

- **model-id**: Must match exactly what SambaNova API expects (case-sensitive)
- **name**: Display name for UI (usually same as model ID)
- **family**: Broad category (Llama, DeepSeek, Qwen, etc.)
- **series**: Specific version line (include version number)
- **owner**: Company/org name in lowercase (meta, deepseek, qwen, openai)
- **maxInputTokens**: Usually same as context window
- **maxOutputTokens**: Often smaller than input, check docs (default: use same as input)
- **tokenizerType**: See Step 4 table
- **speedTokensPerSec**: Use `null` if unknown
- **notes**: Include "Preview:" prefix for preview models

## Validation Checklist

Before saving the updated JSON, verify:

- [ ] All model IDs match exactly with provider's API (case-sensitive)
- [ ] Pricing is in USD per million tokens (not per 1K tokens)
- [ ] Context windows are in token count (not "128k" string)
- [ ] Tokenizer types match the enum values in `MapTokenizerTypeToEnum()`
- [ ] All boolean fields use `true`/`false` (not 1/0)
- [ ] `speedTokensPerSec` is `null` (not "null" string) if unknown
- [ ] Preview models have "Preview:" in notes field
- [ ] Input and output modalities use only `text`, `image`, `audio`, `video`, and `file`
- [ ] Video input is not confused with `supportsVideoGeneration`
- [ ] Unknown modality data is omitted rather than stored as an empty array
- [ ] `capabilitySource` reflects whether metadata is curated, provider-supplied, or manual
- [ ] Model families and series are consistent across similar models
- [ ] Owner names are lowercase

## Testing the Update

After updating the JSON:

1. Run the SQL generator:
```bash
cd /home/nbn/cim/Conduit/scripts/db/providers
./generate-provider-sql.cs <provider>

# Examples:
./generate-provider-sql.cs cerebras
./generate-provider-sql.cs sambanova
```

2. Verify the output:
- Check that SQL generates without errors
- Verify model count matches JSON
- Spot-check a few models for correct data

3. Review generated SQL:
```bash
head -n 50 <provider>-models.sql
grep "Model:" <provider>-models.sql | wc -l
```

## Common Pitfalls

1. **Case Sensitivity**: Model IDs are case-sensitive. `Meta-Llama-3.3-70B-Instruct` ≠ `meta-llama-3.3-70b-instruct`

2. **Pricing Units**: Always per **million** tokens, not 1K tokens. If you see "$0.60/1M" that's `0.60`, not `600.0`

3. **Context Window vs Max Output**: Some models have different input/output limits. Check documentation carefully.

4. **Tokenizer Types**: Must match the enum in `generate-provider-sql.cs`. Check the `MapTokenizerTypeToEnum()` function for valid values.

5. **Preview vs Production**: Keep preview models clearly marked in notes and verify their status hasn't changed to production.

6. **Null vs "null"**: Use `null` (no quotes) for missing numeric values, not the string `"null"`

7. **Boolean Values**: Use `true`/`false`, not `1`/`0` or `"true"`/`"false"`

## Example Update Process

**Scenario**: Provider announces "Llama-3.4-70B-Instruct" in production.

1. **Check model docs**: Find it listed in production with 128K context
2. **Check pricing page**: See $0.65 input / $1.30 output per million tokens
3. **Determine family**: Llama family, Llama 3.4 Series, owner: meta
4. **Tokenizer**: Llama 3.x uses LLaMA3
5. **Capabilities**: 70B instruct model → text input/output, chat: true, streaming: true, function calling: true
6. **Parameters**: "70B" in name → 70 billion
7. **Speed**: Check Artificial Analysis → 250 tokens/sec
8. **Notes**: "128K context, optimized for instruction following"

```json
"Meta-Llama-3.4-70B-Instruct": {
  "name": "Meta-Llama-3.4-70B-Instruct",
  "family": "Llama",
  "series": "Llama 3.4 Series",
  "owner": "meta",
  "maxInputTokens": 128000,
  "maxOutputTokens": 128000,
  "tokenizerType": "LLaMA3",
  "supportsChat": true,
  "supportsStreaming": true,
  "inputModalities": ["text"],
  "outputModalities": ["text"],
  "capabilitySource": "Curated",
  "supportsFunctionCalling": true,
  "supportsEmbeddings": false,
  "supportsImageGeneration": false,
  "supportsVideoGeneration": false,
  "supportsSpeechToText": false,
  "supportsTextToSpeech": false,
  "supportsRerank": false,
  "inputPricePerMillion": 0.65,
  "outputPricePerMillion": 1.30,
  "speedTokensPerSec": 250,
  "notes": "128K context, optimized for instruction following"
}
```

## Getting Help

If you encounter issues:

1. **Check existing entries**: Look at similar models in the JSON for patterns
2. **Review tokenizer mapping**: Check `generate-provider-sql.cs` for valid tokenizer enum values
3. **Search model documentation**: "[Model Name] [Provider] specifications"
4. **Check other providers**: See other `*-models.json` files in this directory for similar patterns
5. **Verify with humans**: When in doubt about capabilities, research thoroughly or ask

## Automation Potential

This process could be partially automated by:
- Scraping provider docs pages for model lists and context windows
- Polling pricing API endpoints (if public)
- Using LLMs to classify capabilities based on model names and descriptions

However, manual verification is still recommended for:
- Pricing accuracy (financial impact)
- Function calling support (technical capability that varies)
- New model families (tokenizer types may differ)
- Provider-specific quirks and limitations
