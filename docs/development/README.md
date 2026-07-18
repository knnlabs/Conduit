# Development Documentation

This directory contains technical guides and best practices for **developing the Conduit codebase** - adding features, implementing providers, and contributing code.

> **Looking for SDK usage guides?** See **[SDK Documentation](../api-guides/sdk/)** for guides on using Conduit SDKs in your applications.

## Contents

### Backend Development
- **[API Patterns Best Practices](./API-PATTERNS-BEST-PRACTICES.md)** - RESTful API design patterns and conventions for Next.js/SDK integration
- **[LLM Client Factory Guide](./llm-client-factory-guide.md)** - **CRITICAL** - Which factory to use when developing LLM providers
- **[Provider API Research](./provider-api-research.md)** - Comprehensive research on model discovery and metadata APIs for all providers

### SDK Development
- **[SDK Gaps](./sdk-gaps.md)** - Known SDK limitations and genuinely missing features

## Development Workflow

### Getting Started

1. **Start development environment**: Always use `./scripts/dev/start-dev.sh` (see [CLAUDE.md](../../CLAUDE.md#development-workflow---critical))
2. **Review API patterns**: Check [API Patterns Best Practices](./API-PATTERNS-BEST-PRACTICES.md) before implementing new endpoints
3. **Check SDK coverage**: Review [SDK Gaps](./sdk-gaps.md) for known limitations
4. **Follow established patterns**: Reference existing code in the codebase

### Adding New LLM Providers

1. Review [LLM Client Factory Guide](./llm-client-factory-guide.md) to understand factory architecture
2. Check [Provider API Research](./provider-api-research.md) for provider-specific API details
3. Implement your provider client in `ConduitLLM.Providers/`
4. Update `DatabaseAwareLLMClientFactory` with your new provider client
5. Add fallback models and patterns to `ProviderFallbackModels`

**Example provider implementation:**

```csharp
// ConduitLLM.Providers/YourProviderClient.cs
public class YourProviderClient : OpenAICompatibleClient
{
    public YourProviderClient(
        ProviderKeyCredential credentials,
        string modelId,
        ILogger<YourProviderClient> logger,
        IHttpClientFactory httpClientFactory,
        ProviderDefaultModels defaultModels)
        : base(credentials, modelId, logger, httpClientFactory, defaultModels)
    {
        // Provider-specific initialization
    }
}

// Update DatabaseAwareLLMClientFactory.cs
case "yourprovider":
    var logger = _loggerFactory.CreateLogger<YourProviderClient>();
    client = new YourProviderClient(credentials, modelId, logger, _httpClientFactory, defaultModels);
    break;
```

### Backend API Development

When creating new API endpoints:

1. **Follow RESTful patterns** from [API Patterns Best Practices](./API-PATTERNS-BEST-PRACTICES.md)
2. **Use established authentication** patterns (`withSDKAuth`, `validateCoreSession`)
3. **Implement proper error handling** with typed error responses
4. **Add XML documentation** to all public methods
5. **Write tests** for new functionality

### SDK Development

When adding features to Admin/Core SDKs:

- TypeScript SDKs are in `/SDKs/Node/` (Admin, Core, Common)
- Build with `npm run build` in each SDK directory
- Follow patterns from [API Patterns Best Practices](./API-PATTERNS-BEST-PRACTICES.md)
- Ensure backward compatibility for API changes
- Update [SDK Gaps](./sdk-gaps.md) if adding previously missing features

### Contributing

- Write clear, self-documenting code with XML documentation
- **Always verify builds**: Run `npm run lint` and `npm run type-check` for WebAdmin changes
- **Backend changes**: Run `dotnet build` to verify compilation
- Update relevant documentation when making changes
- Follow naming conventions from [CLAUDE.md](../../CLAUDE.md#code-style-guidelines)

## Architecture & Patterns

### Key Architecture Guides

- **[Provider Architecture](../architecture/provider-system/provider-architecture.md)** - Multi-instance provider design
- **[Repository & Data Access](../architecture/patterns/repository-and-data-access.md)** - Data access patterns
- **[DTO Guidelines](../architecture/data-transfer/dto-guidelines.md)** - Data transfer patterns

### Important Design Decisions

1. **Provider ID is canonical** - Not ProviderType or ProviderName (see [Provider Architecture](../architecture/provider-system/provider-architecture.md))
2. **Use DatabaseAwareLLMClientFactory** for production code (see [LLM Client Factory Guide](./llm-client-factory-guide.md))
3. **Admin SDK server-side only** - Never expose master keys client-side
4. **Virtual keys for Gateway API** - Use virtual keys, not master keys

## Testing

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName=ConduitLLM.Tests.TestClassName.TestMethodName"

# Run tests for specific project
dotnet test ConduitLLM.Tests/ConduitLLM.Tests.csproj
```

### Test Patterns

```csharp
// Test naming: MethodName_Condition_ExpectedResult
[Fact]
public async Task GetProvider_WithValidId_ReturnsProvider()
{
    // Arrange
    var providerId = 1;

    // Act
    var result = await _service.GetProvider(providerId);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(providerId, result.Id);
}
```

## Build Commands

### Solution Build
```bash
dotnet build                    # Build entire solution
dotnet build --configuration Release  # Production build
```

### Project-Specific Builds
```bash
dotnet build ConduitLLM.Gateway           # Gateway API
dotnet build ConduitLLM.Admin          # Admin API
dotnet build WebAdmin          # WebAdmin backend
```

### SDK Builds
```bash
cd SDKs/Node/Admin && npm run build     # Admin SDK
cd SDKs/Node/Gateway && npm run build  # Gateway SDK
cd SDKs/Node/Common && npm run build  # Common SDK
```

### WebAdmin Development
```bash
# ⚠️ WebAdmin-specific rules (see CLAUDE.md)
npm run lint         # ✅ Check ESLint errors
npm run type-check   # ✅ Verify TypeScript types
# ❌ NEVER run `npm run build` - breaks development container
```

## Code Style

### C# Backend

```csharp
// Naming conventions
public interface ILLMClient { }           // Interface: I prefix
public class OpenAIClient { }             // Class: PascalCase
private readonly ILogger _logger;         // Private field: underscore prefix
public string ModelId { get; set; }       // Property: PascalCase
public async Task<Response> GetAsync() {} // Async: Async suffix

// Braces: Allman style (opening brace on new line)
public void Method()
{
    if (condition)
    {
        // code
    }
}
```

### TypeScript SDKs

```typescript
// Naming conventions
interface VirtualKeyDto { }               // Interface: PascalCase
export class ConduitAdminClient { }       // Class: PascalCase
private readonly baseUrl: string;         // Private: camelCase
public async list(): Promise<Type[]> { }  // Method: camelCase

// Use strict type safety
// ✅ Good
const keys: VirtualKeyDto[] = await client.virtualKeys.list();

// ❌ Bad
const keys: any = await client.virtualKeys.list();
```

## Database Migrations

**⚠️ CRITICAL**: Always follow the migration guidelines in [CLAUDE.md](../../CLAUDE.md#database-migrations) and [Repository & Data Access](../architecture/patterns/repository-and-data-access.md) before creating migrations.

Quick reference:
```bash
# Create migration
dotnet ef migrations add MigrationName --project ConduitLLM.Infrastructure

# Apply migration
dotnet ef database update --project ConduitLLM.Infrastructure

# Test migration rollback
dotnet ef database update PreviousMigration --project ConduitLLM.Infrastructure
```

## Related Documentation

### Development Guides
- **[CLAUDE.md](../../CLAUDE.md)** - **PRIMARY** development workflow, Docker setup, build verification
- **[Architecture Overview](../architecture/README.md)** - System design and components
### API Documentation
- **[Gateway API Guide](../api-guides/gateway/README.md)** - Gateway API reference
- **[Admin API Guide](../api-guides/admin/README.md)** - Admin API overview
- **[Admin API Reference](../api-guides/admin/api-reference.md)** - Complete endpoint specifications

### SDK Usage (Not Development)
- **[SDK Documentation](../api-guides/sdk/)** - For using SDKs in applications
- **[Next.js Integration](../api-guides/sdk/nextjs-integration.md)** - Next.js integration guide
- **[SDK Best Practices](../api-guides/sdk/best-practices.md)** - SDK usage patterns

---

**Note**: This directory is for **developing Conduit itself**. If you're looking to **use** Conduit APIs/SDKs in your application, see [API Guides](../api-guides/) and [SDK Documentation](../api-guides/sdk/).
