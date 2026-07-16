# Adding a New Provider

End-to-end guide for adding a new LLM provider to Conduit. This ties together the code-side implementation with model data and pricing configuration.

## Overview

Adding a provider involves three steps:

1. **Code** — Implement the provider client and register it in the factory
2. **Database** — Define models and pricing via the SQL generator
3. **Configure** — Add the provider and credentials via Admin API or WebAdmin

## Step 1: Implement the Provider Client

### 1a. Add the ProviderType enum value

**File:** `Shared/ConduitLLM.Configuration/Enums/ProviderType.cs`

```csharp
public enum ProviderType
{
    // ... existing providers ...
    YourProvider = 14  // Next available value
}
```

### 1b. Create the client class

**Directory:** `ConduitLLM.Providers/Providers/YourProvider/`

Your client must implement `ILLMClient`. If the provider uses an OpenAI-compatible API, inherit from `OpenAICompatibleClient` — most of the work is already done.

```csharp
public class YourProviderClient : OpenAICompatibleClient
{
    public YourProviderClient(
        Provider provider,
        ProviderKeyCredential keyCredential,
        string modelId,
        ILogger<YourProviderClient> logger,
        IHttpClientFactory httpClientFactory,
        IEnumerable<string>? defaultModels = null)
        : base(provider, keyCredential, modelId, logger, httpClientFactory, defaultModels)
    {
    }

    protected override string GetApiEndpoint()
        => "https://api.yourprovider.com/v1";
}
```

For non-OpenAI-compatible providers, see existing implementations like `ReplicateClient` for reference.

### 1c. Register in the factory

**File:** `ConduitLLM.Providers/DatabaseAwareLLMClientFactory.cs`

Add a case to the `CreateClientForProvider` switch:

```csharp
case ProviderType.YourProvider:
    var yourLogger = _loggerFactory.CreateLogger<YourProviderClient>();
    client = new YourProviderClient(provider, keyCredential, modelId, yourLogger,
        _httpClientFactory, defaultModels);
    break;
```

### 1d. Build and verify

```bash
dotnet build
dotnet test --filter "FullyQualifiedName~YourProvider"
```

See [LLM Client Factory Guide](./llm-client-factory-guide.md) for detailed factory architecture, decorators, and credential handling.

## Step 2: Define Models and Pricing

### 2a. Add provider config

**File:** `scripts/db/providers/provider-config.json`

```json
"yourprovider": {
    "providerType": 14,
    "websiteUrl": "https://yourprovider.com",
    "modelCardUrl": "https://docs.yourprovider.com/models",
    "supportsAudio": false
}
```

### 2b. Create model data

**File:** `scripts/db/providers/yourprovider-models.json`

```json
{
    "model-id-from-api": {
        "name": "Model Display Name",
        "family": "Model Family",
        "series": "Model Series",
        "owner": "company-name",
        "maxInputTokens": 128000,
        "maxOutputTokens": 32768,
        "tokenizerType": "LLaMA3",
        "supportsChat": true,
        "supportsStreaming": true,
        "supportsVision": false,
        "supportsFunctionCalling": true,
        "supportsEmbeddings": false,
        "supportsAudio": false,
        "inputPricePerMillion": 0.60,
        "outputPricePerMillion": 1.20,
        "speedTokensPerSec": null,
        "notes": "Description"
    }
}
```

See [Updating Models Guide](../../scripts/db/providers/UPDATING-MODELS.md) for field-by-field instructions including tokenizer types, capability rules, and pricing conventions.

### 2c. Generate and apply SQL

```bash
cd scripts/db/providers
dotnet run generate-provider-sql.cs -- yourprovider

# Apply to database
psql -h localhost -U conduit -d conduit_db < yourprovider-models.sql
# or via Docker:
docker exec -i conduit-postgres psql -U conduit -d conduit_db < yourprovider-models.sql
```

See [Provider Models SQL Generator](../../scripts/db/providers/README.md) for full usage.

## Step 3: Configure the Provider

Via Admin API or WebAdmin:

1. **Create the provider** — Name, ProviderType, enabled status
2. **Add credentials** — API key(s), endpoint URL (if non-default)
3. **Create model mappings** — Map model aliases to your provider
4. **Test the connection** — Use the test endpoint to verify

```bash
# Example: test via Admin API
curl -X POST http://localhost:5002/api/providers/{id}/test \
  -H "X-API-Key: your-master-key"
```

## Checklist

- [ ] ProviderType enum value added
- [ ] Client class implemented (or OpenAI-compatible base used)
- [ ] Factory switch case added
- [ ] `dotnet build` passes
- [ ] Provider config added to `provider-config.json`
- [ ] Model JSON created with correct pricing (per million tokens)
- [ ] SQL generated and reviewed
- [ ] SQL applied to database
- [ ] Provider created and credentials added via Admin API/WebAdmin
- [ ] Model mappings configured
- [ ] Test endpoint returns success
- [ ] Chat completion request works end-to-end

## Related Documentation

- [LLM Client Factory Guide](./llm-client-factory-guide.md) — Factory architecture and decorators
- [Provider Architecture](../architecture/provider-system/provider-architecture.md) — Multi-instance provider design
- [Provider SQL Generator](../../scripts/db/providers/README.md) — Model data management
- [Updating Models](../../scripts/db/providers/UPDATING-MODELS.md) — JSON field reference
