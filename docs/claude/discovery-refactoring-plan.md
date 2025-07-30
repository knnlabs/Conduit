# Discovery Provider Refactoring Plan

**Created**: 2025-07-30  
**Status**: Planning  
**Goal**: Refactor provider-specific discovery code to follow the driver model architecture

## Executive Summary

Currently, provider-specific discovery code is scattered across the codebase in violation of our driver model architecture. This plan outlines the refactoring to consolidate all provider-specific code within their respective client implementations using a "sister class" pattern.

## Current Problems

1. **Architecture Violation**: Provider-specific code exists outside of LLM clients
2. **DI Complexity**: Each provider requires multiple service registrations
3. **Lifecycle Mismatch**: Discovery providers are scoped services, clients are per-request
4. **Code Duplication**: Similar discovery patterns repeated across providers
5. **Maintenance Burden**: Adding new providers requires changes in multiple places

## Proposed Solution: Sister Class Pattern

### Architecture

```csharp
// All provider code stays together
namespace ConduitLLM.Clients.Groq
{
    // Request handling
    public class GroqClient : ILLMClient { }
    
    // Model discovery
    public static class GroqModels
    {
        public static async Task<List<ModelInfo>> DiscoverAsync(HttpClient http, string apiKey) { }
    }
}
```

### Benefits

- **Clean Separation**: Discovery logic separate from request handling
- **No Lifecycle Issues**: Static methods instead of DI services
- **Provider Encapsulation**: All provider code in one namespace
- **Simple Testing**: Test discovery independently
- **Easy Maintenance**: Add providers without touching core

## Migration Plan

### Phase 1: Create Sister Classes

For each provider that supports discovery:

1. **Groq** → `GroqModels.DiscoverAsync()`
2. **Anthropic** → `AnthropicModels.DiscoverAsync()`
3. **OpenRouter** → `OpenRouterModels.DiscoverAsync()`
4. **OpenAI** → `OpenAIModels.DiscoverAsync()`
5. **Cerebras** → `CerebrasModels.DiscoverAsync()`

Providers with special cases:
- **Azure OpenAI**: Return empty list with TODO (deployments != models)
- **Ollama**: Simple discovery of locally pulled models
- **Bedrock**: Region-aware discovery

### Phase 2: Update ProviderDiscoveryService

Replace dynamic provider lookup with explicit switch:

```csharp
public async Task<List<DiscoveredModel>> DiscoverProviderModelsAsync(string providerName, string? apiKey)
{
    return providerName.ToLowerInvariant() switch
    {
        "groq" => await GroqModels.DiscoverAsync(_httpClient, apiKey),
        "anthropic" => await AnthropicModels.DiscoverAsync(_httpClient, apiKey),
        "openai" => await OpenAIModels.DiscoverAsync(_httpClient, apiKey),
        // ... etc
        _ => new List<DiscoveredModel>()
    };
}
```

### Phase 3: Remove Old Infrastructure

1. Delete all `*DiscoveryProvider.cs` files
2. Remove DI registrations from:
   - `/ConduitLLM.Http/Program.cs`
   - `/ConduitLLM.Admin/Extensions/ServiceCollectionExtensions.cs`
3. Remove `IModelDiscoveryProvider` interface
4. Clean up unused usings and references

### Phase 4: Remove Fallback Models

1. Delete hardcoded model patterns from `ProviderDiscoveryService`
2. Remove fallback logic from discovery
3. Trust provider APIs or return empty (no stale data)

## Implementation Order

1. **Start with Groq** (proof of concept)
   - Create `GroqModels` class
   - Test API compatibility
   - Remove `GroqDiscoveryProvider` (if exists)

2. **Bulk Migration**
   - Anthropic, OpenRouter, Cerebras
   - Follow established pattern
   - Ensure identical API responses

3. **Special Cases**
   - Handle OpenRouter prefix ("openrouter/")
   - Azure OpenAI TODO
   - Region-aware providers

4. **Cleanup**
   - Remove all old code
   - Simplify DI configuration
   - Update documentation

## Testing Strategy

### Unit Tests
- Test each `*Models.DiscoverAsync()` method independently
- Mock HTTP responses
- Verify model transformation logic

### Integration Tests
- Compare responses before/after refactoring
- Ensure byte-for-byte compatibility
- Test with real provider APIs (if keys available)

### End-to-End Tests
1. Start Docker containers
2. Access WebUI bulk import
3. Verify all providers show models
4. Confirm no user-visible changes

## Success Criteria

- ✅ No changes to API contracts
- ✅ No SDK regeneration needed
- ✅ WebUI works identically
- ✅ All providers discoverable
- ✅ Cleaner, more maintainable code
- ✅ Faster startup (less DI complexity)

## Rollback Plan

Since this is a "rip the band-aid off" approach:
1. Full implementation before deployment
2. Comprehensive testing in dev environment
3. If issues found, don't deploy
4. No partial rollout - all or nothing

## Provider-Specific Notes

### OpenRouter
- Models from other providers prefixed with "openrouter/"
- Example: "anthropic/claude-3-opus" → "openrouter/anthropic/claude-3-opus"

### Ollama
- Discovery returns locally available models only
- No transformation needed

### Azure OpenAI
- Return empty list for now
- TODO: Handle deployment discovery differently

### Bedrock
- Return whatever AWS provides
- May vary by region

## Code Locations

### Files to Create
- `/ConduitLLM.Clients/Groq/GroqModels.cs`
- `/ConduitLLM.Clients/Anthropic/AnthropicModels.cs`
- `/ConduitLLM.Clients/OpenRouter/OpenRouterModels.cs`
- etc.

### Files to Modify
- `/ConduitLLM.Core/Services/ProviderDiscoveryService.cs`

### Files to Delete
- `/ConduitLLM.Core/Services/AnthropicDiscoveryProvider.cs`
- `/ConduitLLM.Core/Services/OpenRouterDiscoveryProvider.cs`
- `/ConduitLLM.Core/Services/CerebrasDiscoveryProvider.cs`
- `/ConduitLLM.Core/Services/BaseModelDiscoveryProvider.cs`
- `/ConduitLLM.Core/Interfaces/IModelDiscoveryProvider.cs`

### DI Registration to Remove
- All `services.AddScoped<*DiscoveryProvider>` registrations
- All `services.AddScoped<IModelDiscoveryProvider>` registrations

## Timeline

This is a single-phase delivery:
1. Implement all changes
2. Test thoroughly
3. Deploy once complete

No gradual rollout, no feature flags, no compatibility mode.

## Questions Resolved

1. **Caching**: Keep existing 24-hour cache
2. **Fallbacks**: Remove entirely (no stale data)
3. **API Keys**: Pass explicitly to discovery methods
4. **HttpClient**: Use shared instance from DI

## Next Steps

1. Begin implementation with Groq
2. Validate approach works as expected
3. Proceed with bulk migration
4. Complete testing
5. Deploy when ready