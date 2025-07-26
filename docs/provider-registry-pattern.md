# Provider Registry Pattern

## Overview

The Provider Registry pattern provides a centralized, single source of truth for all provider metadata in the ConduitLLM system. This pattern eliminates duplication, improves maintainability, and ensures consistency across the codebase.

## Problem Statement

Previously, provider capabilities and metadata were duplicated across multiple locations:
- Each provider's `IProviderMetadata` implementation
- `ProviderTypesController`'s hardcoded switch statement
- Various service classes with provider-specific logic

This duplication led to:
- Maintenance burden when adding or modifying providers
- Risk of inconsistencies between different parts of the system
- Difficulty in discovering provider capabilities at runtime

## Solution Architecture

### Core Components

1. **`IProviderMetadata` Interface**
   - Defines the contract for provider-specific metadata
   - Includes capabilities, authentication requirements, and configuration hints
   - Located in `ConduitLLM.Core/Interfaces/IProviderMetadata.cs`

2. **`BaseProviderMetadata` Abstract Class**
   - Provides common implementation for provider metadata
   - Includes validation logic and helper methods
   - Located in `ConduitLLM.Core/Providers/BaseProviderMetadata.cs`

3. **`IProviderMetadataRegistry` Interface**
   - Defines the registry contract for managing provider metadata
   - Provides methods for querying and discovering providers
   - Located in `ConduitLLM.Core/Interfaces/IProviderMetadataRegistry.cs`

4. **`ProviderMetadataRegistry` Implementation**
   - Automatically discovers and registers all provider metadata implementations
   - Provides caching and efficient lookup
   - Located in `ConduitLLM.Core/Services/ProviderMetadataRegistry.cs`

5. **Provider-Specific Metadata Classes**
   - One class per provider type (e.g., `OpenAIProviderMetadata`)
   - Located in `ConduitLLM.Core/Providers/Metadata/`

### Key Features

#### 1. Automatic Discovery
The registry automatically discovers all `IProviderMetadata` implementations at startup using reflection:
```csharp
var providerMetadataTypes = AppDomain.CurrentDomain.GetAssemblies()
    .SelectMany(a => a.GetTypes())
    .Where(t => t.IsClass && !t.IsAbstract && 
                typeof(IProviderMetadata).IsAssignableFrom(t))
    .ToList();
```

#### 2. Type-Safe Provider Lookup
Uses the strongly-typed `ProviderType` enum for lookups:
```csharp
var metadata = registry.GetMetadata(ProviderType.OpenAI);
```

#### 3. Feature-Based Discovery
Find providers by their capabilities:
```csharp
var imageProviders = registry.GetProvidersByFeature(f => f.ImageGeneration);
```

#### 4. Configuration Validation
Each provider can validate its configuration:
```csharp
var result = metadata.ValidateConfiguration(configDict);
if (!result.IsValid)
{
    // Handle validation errors
}
```

## Usage Examples

### Dependency Injection Registration
```csharp
// In Program.cs or Startup.cs
services.AddSingleton<IProviderMetadataRegistry, ProviderMetadataRegistry>();
```

### Controller Usage
```csharp
[ApiController]
public class ProviderTypesController : ControllerBase
{
    private readonly IProviderMetadataRegistry _registry;

    public ProviderTypesController(IProviderMetadataRegistry registry)
    {
        _registry = registry;
    }

    [HttpGet("{providerType}/capabilities")]
    public IActionResult GetCapabilities(ProviderType providerType)
    {
        if (_registry.TryGetMetadata(providerType, out var metadata))
        {
            return Ok(metadata.Capabilities);
        }
        return NotFound();
    }
}
```

### Service Usage
```csharp
public class ProviderConfigurationService
{
    private readonly IProviderMetadataRegistry _registry;

    public ValidationResult ValidateProviderConfig(
        ProviderType providerType, 
        Dictionary<string, object> config)
    {
        var metadata = _registry.GetMetadata(providerType);
        return metadata.ValidateConfiguration(config);
    }
}
```

## Adding a New Provider

To add a new provider to the system:

1. **Add the provider to the `ProviderType` enum**:
   ```csharp
   public enum ProviderType
   {
       // ... existing providers ...
       NewProvider = 22
   }
   ```

2. **Create a metadata class**:
   ```csharp
   public class NewProviderMetadata : BaseProviderMetadata
   {
       public override ProviderType ProviderType => ProviderType.NewProvider;
       public override string DisplayName => "New Provider";
       public override string DefaultBaseUrl => "https://api.newprovider.com/v1";

       public NewProviderMetadata()
       {
           // Configure capabilities
           Capabilities.Features.Streaming = true;
           // ... other capabilities ...

           // Configure authentication
           AuthRequirements.RequiresApiKey = true;
           // ... other auth requirements ...
       }
   }
   ```

3. **Build the solution** - the registry will automatically discover the new provider

## API Endpoints

The following endpoints are available for provider metadata:

- `GET /api/admin/providertypes` - List all provider types
- `GET /api/admin/providertypes/{providerType}/capabilities` - Get provider capabilities
- `GET /api/admin/providertypes/{providerType}/auth-requirements` - Get authentication requirements
- `GET /api/admin/providertypes/{providerType}/configuration-hints` - Get configuration hints
- `GET /api/admin/providertypes/by-feature/{feature}` - Find providers by feature
- `GET /api/admin/providertypes/diagnostics` - Get registry diagnostics

## Testing

The pattern includes comprehensive unit tests:

1. **Registry Tests** (`ProviderRegistryTests.cs`)
   - Tests automatic discovery
   - Tests metadata retrieval
   - Tests feature-based filtering
   - Tests diagnostics

2. **Base Metadata Tests** (`BaseProviderMetadataTests.cs`)
   - Tests validation logic
   - Tests default values
   - Tests helper methods

3. **Controller Tests** (`ProviderTypesControllerTests.cs`)
   - Tests all API endpoints
   - Tests error handling
   - Tests registry integration

## Benefits

1. **Single Source of Truth**: All provider metadata is defined in one place
2. **Type Safety**: Uses strongly-typed enums instead of magic strings
3. **Discoverability**: Providers can be discovered by their capabilities
4. **Extensibility**: New providers can be added without modifying existing code
5. **Testability**: Clean separation of concerns enables easy testing
6. **Performance**: Metadata is cached at startup for fast lookups

## Future Enhancements

1. **Source Generators**: Create a source generator to automatically generate provider metadata registration code at compile time
2. **Plugin Support**: Allow providers to be loaded from external assemblies
3. **Hot Reload**: Support adding/updating providers without restart
4. **Configuration UI**: Generate provider configuration UI based on metadata

## Migration Guide

To migrate existing code to use the Provider Registry:

1. Replace hardcoded provider checks with registry lookups
2. Use `IProviderMetadataRegistry` instead of switch statements
3. Update tests to mock the registry interface
4. Remove duplicate provider metadata definitions

## Conclusion

The Provider Registry pattern provides a robust, maintainable solution for managing provider metadata in ConduitLLM. It eliminates duplication, improves type safety, and makes the system more extensible and testable.