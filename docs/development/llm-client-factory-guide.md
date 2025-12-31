# LLM Client Factory Usage Guide

**IMPORTANT: This guide explains how Conduit's LLM client factory works and how to add new provider support.**

**Last Updated:** 2025-11-08

---

## Architecture Overview

Conduit uses a **single factory pattern** for creating LLM clients:

### `DatabaseAwareLLMClientFactory`

**Location:** `ConduitLLM.Providers/DatabaseAwareLLMClientFactory.cs`

**Characteristics:**
- ✅ **Credentials Source:** Database (via `IProviderService`)
- ✅ **Client Type:** Real production clients
- ✅ **Used By:** Gateway API, Admin API
- ✅ **Decorators:** Context tracking, performance monitoring

**Key Methods:**
```csharp
Task<ILLMClient> GetClient(string modelName)              // Get by model alias
Task<ILLMClient> GetClientByProviderId(int providerId)    // Get by provider ID
Task<ILLMClient> GetClientByProviderType(ProviderType)    // Get by provider type
Task<ILLMClient> CreateTestClient(...)                    // For credential testing
```

---

## Current Service Registrations

### **Gateway API**
**File:** `ConduitLLM.Gateway/Program.CoreServices.cs` (Line 181)
```csharp
builder.Services.AddScoped<ILLMClientFactory, ConduitLLM.Providers.DatabaseAwareLLMClientFactory>();
```

### **Admin API**
**File:** `ConduitLLM.Admin/Program.cs` (Lines 62, 68)
```csharp
builder.Services.AddCoreServices(builder.Configuration);  // Includes factory
builder.Services.AddProviderServices();                   // Registers DatabaseAwareLLMClientFactory
```

### **WebAdmin**
WebAdmin does **NOT** use any factory directly. It uses HTTP clients to call Core/Admin APIs:
- `WebAdmin/src/lib/server/coreClient.ts`
- `WebAdmin/src/lib/server/adminClient.ts`

---

## Supported Providers

The factory currently supports **9 active providers** (as of 2025-11-08):

| Provider Type | Client Class | Use Case | Status |
|--------------|--------------|----------|---------|
| `OpenAI` | `OpenAIClient` | GPT models, embeddings | ✅ Active |
| `Groq` | `GroqClient` | Fast inference | ✅ Active |
| `Replicate` | `ReplicateClient` | Image/video generation | ✅ Active |
| `Fireworks` | `FireworksClient` | Fast LLM inference | ✅ Active |
| `OpenAICompatible` | `OpenAICompatibleGenericClient` | Generic OpenAI-compatible endpoints | ✅ Active |
| `MiniMax` | `MiniMaxClient` | Chinese LLM provider with video support | ✅ Active |
| `Cerebras` | `CerebrasClient` | High-performance inference | ✅ Active |
| `SambaNova` | `SambaNovaClient` | Ultra-fast inference | ✅ Active |
| `DeepInfra` | `DeepInfraClient` | OpenAI-compatible inference platform | ✅ Active |

**Obsolete Providers (removed):**
- `Ultravox` (Audio functionality removed)
- `ElevenLabs` (Audio functionality removed)

---

## Adding a New LLM Provider

Follow these steps to add support for a new provider:

### **Step 1: Add Provider to Enum**

**File:** `ConduitLLM.Configuration/Enums/ProviderType.cs`

```csharp
public enum ProviderType
{
    // ... existing providers ...

    /// <summary>
    /// YourProvider description
    /// </summary>
    YourProvider = 12  // Next available number
}
```

### **Step 2: Create Provider Client**

**File:** `ConduitLLM.Providers/Providers/YourProvider/YourProviderClient.cs`

```csharp
using ConduitLLM.Clients;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Providers.YourProvider
{
    public class YourProviderClient : ILLMClient
    {
        private readonly Provider _provider;
        private readonly ProviderKeyCredential _keyCredential;
        private readonly string _modelId;
        private readonly ILogger<YourProviderClient> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public YourProviderClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string modelId,
            ILogger<YourProviderClient> logger,
            IHttpClientFactory httpClientFactory,
            ProviderDefaultModels? defaultModels = null)
        {
            _provider = provider;
            _keyCredential = keyCredential;
            _modelId = modelId;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        // Implement ILLMClient interface methods
        public async Task<ChatCompletionResult> ChatCompletion(
            ChatCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            // Your implementation
        }

        // ... implement other required methods
    }
}
```

### **Step 3: Update DatabaseAwareLLMClientFactory**

**File:** `ConduitLLM.Providers/DatabaseAwareLLMClientFactory.cs`

Add your provider to the switch statement in the `CreateClientForProvider` method (around line 248):

```csharp
using ConduitLLM.Providers.YourProvider;  // Add at top of file

// In CreateClientForProvider method:
switch (provider.ProviderType)
{
    // ... existing cases ...

    case ProviderType.YourProvider:
        var yourProviderLogger = _loggerFactory.CreateLogger<YourProviderClient>();
        client = new YourProviderClient(provider, keyCredential, modelId, yourProviderLogger,
            _httpClientFactory, defaultModels);
        break;

    default:
        throw new ConfigurationException($"Unsupported provider type: {provider.ProviderType}");
}
```

### **Step 4: Test Your Provider**

1. **Add provider credentials** via Admin UI or database seed
2. **Create model mapping** to your provider
3. **Test with Admin API** using the test endpoint
4. **Verify in Swagger** at http://localhost:5002/swagger

---

## Provider Credentials

### ✅ **Best Practices:**
- Always store credentials in the database via Admin UI
- Use `IProviderService` to access provider and credential data
- Support multiple API keys per provider for load balancing
- Never commit credentials to source control

### ❌ **Anti-Patterns:**
- Never use `appsettings.json` for production credentials
- Never hardcode API keys in client code
- Never bypass the database credential system

---

## Client Decorators

The factory automatically applies decorators to all clients:

### **1. ContextAwareLLMClient** (Always Applied)
- Adds provider ID and key ID context
- Enables error tracking by specific provider instance
- Required for proper monitoring and debugging

### **2. PerformanceTrackingLLMClient** (Optional)
- Tracks request latency and throughput
- Enabled when `IPerformanceMetricsService` is available
- Provides real-time performance metrics

**Application Order:**
```
Base Client → ContextAwareLLMClient → PerformanceTrackingLLMClient
```

---

## Architecture Notes

### **Why Database-Aware?**
The factory is called "DatabaseAware" because it:
1. Loads provider configurations from the database
2. Retrieves API keys from database-stored credentials
3. Supports dynamic provider configuration without deployments
4. Enables multi-tenant provider key management

### **Provider vs ProviderType**
- **`Provider`** (entity): A specific instance of a provider (e.g., "Production OpenAI")
- **`ProviderType`** (enum): The category of provider (e.g., `OpenAI`)
- Multiple `Provider` records can share the same `ProviderType`

### **Model Resolution Flow**
```
Model Alias → ModelProviderMapping → Provider.Id → ProviderType → Client
```

---

## Common Development Tasks

### **Testing a New Provider Client**
```csharp
// Use the CreateTestClient method
var testClient = await _factory.CreateTestClient(
    providerType: ProviderType.YourProvider,
    providerName: "Test Provider",
    credentials: new Dictionary<string, string>
    {
        { "ApiKey", "test-key" }
    },
    modelId: "your-model-id"
);
```

### **Getting a Client by Model Alias**
```csharp
// Most common pattern in Gateway API
var client = await _factory.GetClient("gpt-4");
```

### **Getting a Client by Provider ID**
```csharp
// Useful for provider-specific operations
var client = await _factory.GetClientByProviderId(providerId: 1);
```

---

## Troubleshooting

### **"Unsupported provider type" Exception**
- Verify you added the enum value to `ProviderType.cs`
- Ensure the switch case in `DatabaseAwareLLMClientFactory` includes your provider
- Check that the provider record in the database has the correct `ProviderType` value

### **"No credentials found" Exception**
- Verify provider has at least one enabled `ProviderKeyCredential`
- Check that the credential's `IsEnabled` flag is true
- Ensure the provider's `IsActive` flag is true

### **Client Not Using Correct API Key**
- Factory selects the first enabled key where `IsPrimary = true`
- If no primary key exists, uses the first enabled key
- Verify key priority in the database

---

## Related Documentation

- **Provider Architecture:** `/docs/architecture/provider-multi-instance.md`
- **Model Cost Mapping:** `/docs/architecture/model-cost-mapping.md`
- **Provider Models:** `/docs/claude/provider-models.md`
- **Database Migration Guide:** `/docs/claude/database-migration-guide.md`

---

*This guide reflects the actual implementation as of 2025-11-08. The architecture uses a single factory pattern for simplicity and maintainability.*
