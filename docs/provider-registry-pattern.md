# Provider Registry Pattern

## Overview

The Provider Registry pattern provides a centralized, single source of truth for all provider metadata in the ConduitLLM system. This pattern eliminates duplication, improves maintainability, and ensures consistency across the codebase.

## Problem Statement

Previously, provider capabilities and metadata were duplicated across multiple locations:
- Each provider's `IProviderMetadata` implementation
- Various service classes with provider-specific logic
- Hardcoded switch statements and capabilities spread throughout the codebase

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

## Integration with Admin API

Provider metadata is integrated into the existing provider management endpoints:

- Provider creation uses metadata for validation and configuration hints
- Provider listing includes capabilities from the registry
- Provider configuration UI is generated based on metadata requirements
- The registry ensures consistent provider behavior across all API endpoints

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

3. **Integration Tests**
   - Tests registry integration with existing Admin API controllers
   - Tests provider creation with metadata validation
   - Tests error handling and edge cases

## Benefits

1. **Single Source of Truth**: All provider metadata is defined in one place
2. **Type Safety**: Uses strongly-typed enums instead of magic strings
3. **Discoverability**: Provider can be discovered by their capabilities
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