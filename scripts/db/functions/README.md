# Function Configurations SQL Generator

This directory contains scripts for generating SQL to populate Function Configurations for search and retrieval providers in the Conduit database.

## Overview

This implementation uses a **static JSON + generator** pattern because:

- Function configurations are relatively stable once defined
- Each provider has well-documented API parameters
- Manual curation ensures accurate parameter schemas
- Easier to maintain and review than web scraping
- Allows for custom descriptions and categorization

## Files

- **function-configurations.json** - Hand-maintained function configurations with complete parameter schemas
- **generate-functions-sql.cs** - C# script that reads JSON and generates PostgreSQL SQL
- **functions-seed.sql** - Generated SQL output (gitignored, regenerate as needed)
- **README.md** - This file

## Usage

### Basic Usage

```bash
# Generate SQL (outputs to functions-seed.sql)
./generate-functions-sql.cs

# Generate SQL with custom filename
./generate-functions-sql.cs my-functions-seed.sql
```

### Executing Generated SQL

```bash
# Execute against local database
psql -h localhost -U conduit -d conduit_db < functions-seed.sql

# Execute via Docker
docker exec -i conduit-postgres psql -U conduit -d conduit_db < functions-seed.sql
```

## Supported Providers

### Exa.ai (ProviderType = 1) - 3 Configurations

1. **Exa Neural Search** (Purpose: Search)
   - Neural semantic search powered by embeddings
   - Best for conceptual similarity and discovery
   - Supports content extraction (text, highlights, summary)

2. **Exa Keyword Search** (Purpose: Search)
   - Traditional keyword-based search
   - Best for exact term matching and technical queries
   - Supports category filtering and domain restrictions

3. **Exa Content Retrieval** (Purpose: ContentRetrieval)
   - Extract content from specific URLs
   - Supports highlights, summaries, and subpage crawling
   - Async execution for large content extraction

### Tavily (ProviderType = 4) - 4 Configurations

1. **Tavily General Search** (Purpose: Search)
   - RAG-optimized search for general queries
   - Clean, structured results for AI consumption
   - Basic and advanced search depth options

2. **Tavily News Search** (Purpose: Search)
   - Real-time news search with temporal filtering
   - Optimized for current events and breaking news
   - Supports recency filters (days parameter)

3. **Tavily Finance Search** (Purpose: Search)
   - Financial data and market information search
   - Prioritizes authoritative financial sources
   - Topic-specific optimization for finance queries

4. **Tavily Answer Generation** (Purpose: Answer)
   - LLM-powered answer generation from search results
   - Includes citations and source grounding
   - Advanced search depth for comprehensive answers

### Perplexity AI (ProviderType = 2) - 3 Configurations

1. **Perplexity Sonar Search** (Purpose: Answer)
   - Fast online search with LLM-generated answers
   - Automatic citations and source attribution
   - Up to 4K tokens output

2. **Perplexity Sonar Pro Search** (Purpose: Answer)
   - Advanced search with deeper reasoning
   - Enhanced citation quality and image support
   - Up to 8K tokens output

3. **Perplexity Sonar Reasoning** (Purpose: Answer)
   - Complex reasoning over search results
   - Chain-of-thought processing for analytical queries
   - Best for multi-step problems

## Configuration Data Structure

Each configuration in `function-configurations.json` includes:

```json
{
  "providerType": "Exa",
  "providerTypeId": 1,
  "configurationName": "Exa Neural Search",
  "purpose": "Search",
  "purposeId": 1,
  "description": "Neural semantic search powered by embeddings...",
  "defaultExecutionMode": "Synchronous",
  "defaultExecutionModeId": 1,
  "baseUrl": null,
  "isEnabled": true,
  "timeoutSeconds": 30,
  "maxRetries": 3,
  "providerSettings": {
    "defaultSearchType": "neural",
    "defaultNumResults": 10
  },
  "parameterSchema": {
    "required": ["query"],
    "optional": {
      "numResults": {
        "type": "number",
        "min": 1,
        "max": 100,
        "default": 10,
        "description": "Number of search results to return"
      }
    }
  }
}
```

## Enums Reference

### ProviderType
```csharp
Exa = 1,
Perplexity = 2,
CustomRAG = 3,
Tavily = 4,
Custom = 99
```

### Purpose
```csharp
Search = 1,         // Web/content search
Answer = 2,         // LLM-powered answers
ContentRetrieval = 3, // URL content extraction
RAG = 4            // Retrieval-augmented generation
```

### ExecutionMode
```csharp
Synchronous = 1,   // Wait for result
Asynchronous = 2   // Background execution
```

## Generated SQL Structure

The script generates SQL that:

1. **Checks for existing configurations** by ConfigurationName + ProviderType
2. **Inserts new configurations** if not found
3. **Updates existing configurations** with latest settings
4. **Preserves custom modifications** while updating schemas

All operations are wrapped in a transaction for atomicity.

## Parameter Schema Design

Parameter schemas follow a UI-focused structure:

- **required**: Array of required parameter names
- **optional**: Object with parameter definitions including:
  - `type`: Data type (string, number, boolean, array, object)
  - `min/max`: Validation constraints
  - `default`: Default value
  - `description`: User-facing description
  - `enum`: Allowed values for enums
  - `format`: Special formats (date, url, etc.)

This structure supports:
- Automatic UI generation
- Client-side validation
- API documentation generation
- Type-safe SDK usage

## Execution Modes

### Synchronous (Default)
- Most search functions use synchronous execution
- Waits for results before returning
- Typical timeout: 30 seconds

### Asynchronous
- Content retrieval with subpage crawling
- Long-running operations (60+ seconds)
- Background processing with status polling

## Provider Settings vs Parameter Schema

### ProviderSettings (JSONB)
- **Backend configuration** for default behavior
- Examples: `defaultSearchType`, `defaultNumResults`
- Not exposed to end-users
- Used by backend to set defaults

### ParameterSchema (JSONB)
- **User-facing parameters** for each function call
- Defines what users can customize per request
- Used for UI generation and validation
- Documented in API specs

## Updating Configurations

When providers release new capabilities or change APIs:

1. **Edit function-configurations.json** to add/update configurations
2. **Run ./generate-functions-sql.cs** to generate new SQL
3. **Review the generated SQL** for correctness
4. **Execute against your database**

The script is idempotent - safe to run multiple times.

## Provider-Specific Notes

### Exa.ai
- Supports both neural (semantic) and keyword search
- Content extraction adds to search cost
- Category filtering available for specific content types
- Live crawl option for fresh content

### Tavily
- Optimized for RAG applications
- Topic-specific search (general, news, finance)
- Search depth affects cost (basic vs advanced)
- Answer generation uses advanced search automatically

### Perplexity
- All functions use Purpose: Answer (2)
- Uses OpenAI-compatible chat completion format
- Sonar models automatically include citations
- Reasoning model best for complex analytical queries
- Search domain and recency filters available

## Cost Considerations

### Exa.ai
- Hybrid pricing: search type + results + content extraction
- Neural search more expensive than keyword
- Content extraction (text, highlights, summary) adds cost

### Tavily
- Per-result pricing with search depth multiplier
- Basic: 1 credit per search
- Advanced: 2 credits per search
- Answer generation adds additional credits

### Perplexity
- Per-token pricing (like LLM models)
- Input tokens: ~$0.20-1.00 per million
- Output tokens: ~$0.20-1.00 per million
- Sonar models cheaper than Sonar Pro/Reasoning

## Common Use Cases

### Research & Discovery
- **Exa Neural Search**: Find conceptually related content
- **Tavily General Search**: Broad research with structured results

### Current Events
- **Tavily News Search**: Real-time news with temporal filters
- **Perplexity Sonar Search**: Fast answers with citations

### Financial Analysis
- **Tavily Finance Search**: Market data and financial sources
- **Perplexity Sonar Pro**: Comprehensive financial analysis

### Content Extraction
- **Exa Content Retrieval**: Deep content analysis from URLs
- **Tavily Answer Generation**: Synthesized answers with citations

### Complex Reasoning
- **Perplexity Sonar Reasoning**: Multi-step analytical queries
- **Tavily Answer Generation (Advanced)**: Comprehensive research synthesis

## Database Schema

The FunctionConfigurations table structure:

```sql
CREATE TABLE "FunctionConfigurations" (
  "Id" SERIAL PRIMARY KEY,
  "ProviderType" INTEGER NOT NULL,
  "ConfigurationName" VARCHAR(200) NOT NULL,
  "Purpose" INTEGER NOT NULL,
  "DefaultExecutionMode" INTEGER NOT NULL,
  "IsEnabled" BOOLEAN NOT NULL DEFAULT true,
  "BaseUrl" VARCHAR(500),
  "TimeoutSeconds" INTEGER,
  "MaxRetries" INTEGER DEFAULT 3,
  "ProviderSettings" JSONB,
  "ParameterSchema" JSONB,
  "Description" VARCHAR(1000),
  "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
  "UpdatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
  UNIQUE("ConfigurationName", "ProviderType")
);
```

## Notes

- Script uses upsert logic to preserve manual changes
- All configurations are enabled by default (`IsEnabled = true`)
- Timeouts range from 30-60 seconds based on operation complexity
- Max retries set to 3 for all configurations
- JSONB fields properly escaped for PostgreSQL
- Transaction-wrapped for atomic execution
- Idempotent - safe to run multiple times

## Future Enhancements

Potential additions as provider capabilities evolve:

- **Additional providers**: SerpAPI, Bing Search API, Google Custom Search
- **More purposes**: ImageSearch, VideoSearch, DataRetrieval
- **Advanced parameters**: Caching strategies, rate limit configs
- **Cost tracking**: Link to ModelCosts for unified pricing
- **Webhooks**: Async result delivery configuration
- **Batch operations**: Multi-query batch configurations

## Data Source References

### Exa.ai
- **Official Docs**: https://docs.exa.ai/
- **API Reference**: https://docs.exa.ai/reference/search
- **Pricing**: https://exa.ai/pricing

### Tavily
- **Official Docs**: https://docs.tavily.com/
- **API Reference**: https://docs.tavily.com/docs/tavily-api/rest_api
- **Pricing**: https://tavily.com/pricing

### Perplexity AI
- **Official Docs**: https://docs.perplexity.ai/
- **API Reference**: https://docs.perplexity.ai/reference/post_chat_completions
- **Pricing**: https://docs.perplexity.ai/docs/pricing
- **Models**: https://docs.perplexity.ai/docs/model-cards
