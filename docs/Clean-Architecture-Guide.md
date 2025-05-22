# Clean Architecture Developer Guide

This guide explains the clean architecture design implemented in Conduit and provides guidelines for developers working with the codebase.

## Core Principles

The Conduit platform follows these core clean architecture principles:

1. **Separation of Concerns**: Each layer has a specific responsibility
2. **Dependency Inversion**: Dependencies point inward, with abstractions protecting the core domain
3. **Explicit Dependencies**: Dependencies are injected rather than instantiated within components
4. **Domain-Centric Design**: Domain entities and business rules are central

## Architecture Layers

### 1. Domain Layer (Core)

The domain layer contains business entities, interfaces, and core business logic:

- **Location**: `ConduitLLM.Core` and parts of `ConduitLLM.Configuration`
- **Components**: Entities, value objects, domain events, core interfaces
- **Key Interfaces**: `ILLMClient`, `ILLMRouter`, `IModelCapabilityDetector`
- **Responsibilities**: Define the domain model and business rules

### 2. Application Layer

The application layer orchestrates the flow of data between the domain and infrastructure layers:

- **Location**: `ConduitLLM.Admin` and some services in `ConduitLLM.Configuration`
- **Components**: Services, DTOs, mappers, validators
- **Key Services**: `AdminVirtualKeyService`, `AdminRouterService`
- **Responsibilities**: Coordinate application activities, implement use cases

### 3. Infrastructure Layer

The infrastructure layer provides implementations of interfaces defined in the domain layer:

- **Location**: `ConduitLLM.Providers`, parts of `ConduitLLM.Configuration`
- **Components**: Database access, API clients, third-party integrations
- **Key Services**: LLM provider clients, repositories
- **Responsibilities**: Provide implementation details for persistence and external services

### 4. Presentation Layer

The presentation layer handles user interaction and API endpoints:

- **Location**: `ConduitLLM.Http`, `ConduitLLM.WebUI`
- **Components**: Controllers, Blazor components, API endpoints, middleware
- **Key Components**: API controllers, Blazor pages, middleware
- **Responsibilities**: Accept user input, format responses, handle HTTP concerns

## Communication Flow

1. **WebUI to Admin API**:
   ```
   WebUI Component → AdminApiClient → HTTP → Admin API Controller → Service → Repository → Database
   ```

2. **Client Application to LLM API**:
   ```
   Client App → HTTP API → LLM Router → Provider Client → External LLM Provider
   ```

## Key Design Patterns

### Repository Pattern

The Repository pattern provides a collection-like interface for accessing domain objects:

```csharp
public interface IVirtualKeyRepository
{
    Task<VirtualKey?> GetByIdAsync(int id);
    Task<VirtualKey?> GetByKeyAsync(string key);
    Task<IEnumerable<VirtualKey>> GetAllAsync();
    Task<VirtualKey?> CreateAsync(VirtualKey virtualKey);
    Task<bool> UpdateAsync(VirtualKey virtualKey);
    Task<bool> DeleteAsync(int id);
}
```

### Dependency Injection

All components follow the dependency injection pattern:

```csharp
public class AdminVirtualKeyService : IAdminVirtualKeyService
{
    private readonly IVirtualKeyRepository _virtualKeyRepository;
    private readonly ILogger<AdminVirtualKeyService> _logger;

    public AdminVirtualKeyService(
        IVirtualKeyRepository virtualKeyRepository,
        ILogger<AdminVirtualKeyService> logger)
    {
        _virtualKeyRepository = virtualKeyRepository;
        _logger = logger;
    }
    
    // Implementation
}
```

### DTO Conversion

The application converts between domain entities and DTOs for API communication:

```csharp
// Converting from Entity to DTO
public static VirtualKeyDto ToDto(this VirtualKey entity)
{
    return new VirtualKeyDto
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        // ... other properties
    };
}

// Converting from DTO to Entity
public static VirtualKey ToEntity(this VirtualKeyDto dto)
{
    return new VirtualKey
    {
        Id = dto.Id,
        Name = dto.Name,
        Description = dto.Description,
        // ... other properties
    };
}
```

## Best Practices

When working with the Conduit codebase, follow these best practices:

1. **Domain-First Development**:
   - Start by defining the domain model and interfaces
   - Implement service interfaces before infrastructure details
   - Use domain language consistently

2. **Interface Segregation**:
   - Keep interfaces focused and specific to their use cases
   - Avoid "god interfaces" with too many methods
   - Group related methods in cohesive interfaces

3. **Standardized DTOs**:
   - Use DTOs for all data transfer between layers
   - Define DTOs in the `ConduitLLM.Configuration.DTOs` namespace
   - Use consistent naming conventions (`EntityNameDto`)

4. **API Integration**:
   - Always use AdminApiClient to communicate with the Admin API
   - Implement proper error handling for all API calls
   - Use consistent HTTP status codes for API responses

5. **Testing**:
   - Test each layer independently
   - Mock external dependencies in unit tests
   - Use integration tests for API communication

## Implementation Examples

### Implementing a New Feature

When adding a new feature to the Conduit platform:

1. **Define Domain Model**: Start by defining entities and interfaces in the Core project
2. **Create DTOs**: Create DTOs for API communication in the Configuration project
3. **Implement Repository**: Create a repository interface and implementation
4. **Implement Service**: Create a service that uses the repository
5. **Implement Controller**: Create an API controller in the Admin API project
6. **Implement UI Component**: Create a Blazor component in the WebUI project
7. **Update AdminApiClient**: Add methods to the AdminApiClient to communicate with the new endpoint

### Adding a New LLM Provider

When adding a new LLM provider to the platform:

1. **Implement Client**: Create a new client class in the Providers project
2. **Register Factory**: Update the LLMClientFactory to support the new provider
3. **Add Settings**: Add provider-specific settings to the configuration
4. **Update Admin API**: Add endpoints for managing the provider's credentials
5. **Update WebUI**: Add UI components for configuring the provider

## Conclusion

Following the clean architecture principles helps keep the Conduit platform maintainable, testable, and scalable. By adhering to these guidelines, you'll ensure that your contributions align with the platform's architectural goals.

For more specific guidance, refer to:

- [Architecture Overview](./Architecture-Overview.md)
- [Admin API Integration](./Architecture/Admin-API-Integration.md)
- [Repository Pattern Implementation](./Architecture/Repository-Pattern-Implementation.md)
- [Clean Architecture Diagram](./Architecture/Clean-Architecture-Diagram.md)