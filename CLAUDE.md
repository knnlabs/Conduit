# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Information
- **GitHub Repository**: knnlabs/Conduit
- **Issues URL**: https://github.com/knnlabs/Conduit/issues
- **Pull Requests URL**: https://github.com/knnlabs/Conduit/pulls

## Build Commands
- Build solution: `dotnet build`
- Run tests: `dotnet test`
- Run specific test: `dotnet test --filter "FullyQualifiedName=ConduitLLM.Tests.TestClassName.TestMethodName"`
- Start API server: `dotnet run --project ConduitLLM.Http`
- Start web UI: `dotnet run --project ConduitLLM.WebUI`
- Start both services: `docker compose up -d`

## Development Workflow
- After implementing features, always run: `dotnet build` to check for compilation errors
- Test your changes locally before committing
- When working with API changes, test with curl or a REST client
- For UI changes, verify in the browser with developer tools open
- Clean up temporary test files and scripts after completing features

## Git Branching Rules
- **NEVER push to origin/master** - The master branch is protected
- **ALWAYS push to origin/dev** - All development work goes to the dev branch
- Create feature branches from dev when working on new features
- Pull requests should target the dev branch, not master
- The dev branch will be merged to master through proper release processes

## Code Style Guidelines
- **Naming**: 
  - Interfaces prefixed with 'I' (e.g., `ILLMClient`)
  - Async methods suffixed with 'Async'
  - Private fields prefixed with underscore (`_logger`)
  - Use PascalCase for public members, camelCase for parameters
- **Formatting**:
  - 4 spaces for indentation
  - Opening braces on new line (Allman style)
  - Max line length ~100 characters
- **Error Handling**:
  - Use custom exception types inheriting from base exceptions
  - Include contextual information in exception messages
  - Use try/catch with appropriate logging
- **Testing**:
  - Test methods follow pattern: `MethodName_Condition_ExpectedResult`
  - One assertion per test is preferred
  - Use Moq for mocking dependencies

## XML Documentation Standards

- **All public APIs** should have comprehensive XML documentation.
- **Add XML comments** to the following code elements:
  - Classes and interfaces
  - Public properties and methods
  - Public enum values
  - Non-obvious public fields
  - Important private methods that implement complex logic

- **XML Tags to Use**:
  - `<summary>` - Required for all documented elements
  - `<param>` - Required for all method parameters
  - `<returns>` - Required for non-void methods
  - `<exception>` - Document exceptions thrown by methods
  - `<remarks>` - Add additional details beyond the summary
  - `<example>` - Add usage examples where helpful
  - `<see>` / `<seealso>` - Cross-reference related classes or methods

- **Documentation Quality**:
  - **Summaries** should be brief, concise descriptions (1-2 sentences)
  - **Include 'why' information** in addition to 'what' when appropriate
  - Use **complete sentences** ending with periods
  - Be **specific about parameter roles** and constraints
  - Document **side effects** such as state changes
  - Note **thread safety** considerations for multi-threaded code
  - Include **performance characteristics** for performance-critical code

- **API Documentation**:
  - Controllers and DTOs should include detailed response documentation
  - Include `<response>` tags with HTTP status codes and response descriptions
  - Document serialization attributes and their effects

- **Example Format**:

```csharp
/// <summary>
/// Authenticates a user and generates an access token.
/// </summary>
/// <param name="username">The user's login name.</param>
/// <param name="password">The user's password.</param>
/// <param name="rememberMe">Whether to extend the token validity period.</param>
/// <returns>A JWT token string if authentication is successful.</returns>
/// <exception cref="ArgumentException">Thrown when username or password is empty.</exception>
/// <exception cref="AuthenticationException">Thrown when credentials are invalid.</exception>
/// <remarks>
/// The token validity period depends on the rememberMe parameter:
/// - If true: 30-day validity
/// - If false: 24-hour validity
/// </remarks>
public async Task<string> AuthenticateAsync(string username, string password, bool rememberMe)
{
    // Method implementation
}
```

## Documentation Coverage

To check XML documentation coverage across the solution, use the provided XML documentation coverage checker:

```bash
# Run the documentation coverage check
./check-documentation.sh
```

The coverage checker will:
- Scan the solution for C# files
- Identify undocumented and partially documented types
- Generate a report with documentation coverage statistics
- Provide recommendations for documentation improvements

The tool is located in the `tools/XmlDocumentationChecker` directory.

### Coverage Priorities

Focus documentation efforts in this order:
1. **Core interfaces** - Foundation of the architecture
2. **Provider implementations** - Implementation of core interfaces for different LLM providers
3. **DTO and Model classes** - Data structures used across the application
4. **Controller classes** - Public API endpoints
5. **Service classes** - Business logic implementation
6. **Helper and utility classes** - Supporting functionality

## Media Storage Configuration

Conduit supports storing generated images and videos using either in-memory storage (for development) or S3-compatible storage (for production).

### Development (In-Memory Storage)
By default, Conduit uses in-memory storage for development. Generated media files are stored in memory and served directly by the API.

### Production (S3-Compatible Storage)
For production deployments, configure S3-compatible storage (AWS S3, Cloudflare R2, MinIO, etc.):

```bash
# Storage provider configuration
export CONDUITLLM__STORAGE__PROVIDER=S3

# S3 configuration
export CONDUITLLM__STORAGE__S3__SERVICEURL=https://your-s3-endpoint.com  # Optional for AWS S3
export CONDUITLLM__STORAGE__S3__ACCESSKEY=your-access-key
export CONDUITLLM__STORAGE__S3__SECRETKEY=your-secret-key
export CONDUITLLM__STORAGE__S3__BUCKETNAME=conduit-media
export CONDUITLLM__STORAGE__S3__REGION=auto  # Or specific region like us-east-1
export CONDUITLLM__STORAGE__S3__PUBLICBASEURL=https://cdn.yourdomain.com  # Optional CDN URL
```

### Cloudflare R2 Example
```bash
export CONDUITLLM__STORAGE__PROVIDER=S3
export CONDUITLLM__STORAGE__S3__SERVICEURL=https://<account-id>.r2.cloudflarestorage.com
export CONDUITLLM__STORAGE__S3__ACCESSKEY=<r2-access-key>
export CONDUITLLM__STORAGE__S3__SECRETKEY=<r2-secret-key>
export CONDUITLLM__STORAGE__S3__BUCKETNAME=conduit-media
export CONDUITLLM__STORAGE__S3__REGION=auto
```

### Important: Media Lifecycle Management

**WARNING**: Generated media files (images/videos) are currently not cleaned up when virtual keys are deleted. This is a known limitation that will lead to:
- Ever-growing storage costs
- Orphaned media files in your CDN/S3 bucket
- No ability to track storage usage per virtual key

**Temporary Workarounds**:
1. Use S3 lifecycle policies to auto-delete old files
2. Manually clean up orphaned media periodically
3. Monitor your storage usage and costs

See `docs/TODO-Media-Lifecycle-Management.md` for the planned implementation to address this.

### Image Generation API
The image generation endpoint is available at:
```
POST /v1/images/generations
```

This endpoint follows OpenAI's image generation API format and supports providers like OpenAI (DALL-E), MiniMax, and Replicate.

#### Supported Image Generation Models
- **OpenAI**: `dall-e-2`, `dall-e-3`
- **MiniMax**: `minimax-image` (maps to `image-01`)
- **Replicate**: Various models via model name

### MiniMax Models
MiniMax provides both chat and image generation capabilities:

#### Chat Models with Vision
- **Models**: 
  - `minimax-chat` (maps to `abab6.5-chat`) - Latest model
  - `abab6.5s-chat` - Smaller variant
  - `abab5.5-chat` - Previous generation
- **Features**: Text generation with image understanding
- **Context**: 245K tokens
- **Max Output**: 8K tokens
- **Supports**: Function calling, JSON mode, streaming

#### Image Generation
- **Model**: `minimax-image` (maps to `image-01`)
- **Aspect Ratios**: 1:1, 16:9, 9:16, 4:3, 3:4, and more
- **Features**: Prompt optimization, high-quality generation

#### Video Generation (Model Defined)
- **Model**: `video-01`
- **Resolutions**: 720x480, 1280x720, 1920x1080, 720x1280, 1080x1920
- **Max Duration**: 6 seconds

## Event-Driven Architecture

Conduit uses a comprehensive event-driven architecture to ensure data consistency, eliminate race conditions, and optimize performance across the Core API and Admin API services.

### Overview

The event-driven system is built on **MassTransit** with domain events for critical operations:
- **Virtual Key management** - CRUD operations and spend tracking
- **Provider Credential management** - Provider changes and capability refresh
- **Model Capability discovery** - Automated discovery and caching

### Architecture Benefits

- **Eliminates Race Conditions**: Ordered event processing per entity prevents concurrent update conflicts
- **Cache Consistency**: Event-driven cache invalidation ensures immediate data consistency across services
- **Performance Optimization**: Eliminates N+1 query patterns and redundant external API calls
- **Service Decoupling**: Admin API and Core API coordinate through events instead of direct calls
- **Horizontal Scaling**: Partitioned event processing supports multiple service instances

### Domain Events

#### Virtual Key Events

**VirtualKeyUpdated**
- **Trigger**: When virtual key properties are modified in Admin API
- **Consumers**: Cache invalidation in Core API
- **Partition Key**: Virtual Key ID (ensures ordered processing)

**VirtualKeyDeleted**
- **Trigger**: When virtual key is deleted in Admin API  
- **Consumers**: Cache cleanup in Core API
- **Partition Key**: Virtual Key ID

**SpendUpdateRequested**
- **Trigger**: When spend needs to be updated for a virtual key
- **Consumers**: Ordered spend processing in Core API
- **Partition Key**: Virtual Key ID (prevents race conditions)

**SpendUpdated**
- **Trigger**: After spend is successfully updated
- **Consumers**: Cache invalidation in Core API
- **Partition Key**: Virtual Key ID

#### Provider Credential Events

**ProviderCredentialUpdated**
- **Trigger**: When provider credentials are modified in Admin API
- **Consumers**: Capability refresh in Core API
- **Partition Key**: Provider ID

**ProviderCredentialDeleted**
- **Trigger**: When provider credentials are deleted in Admin API
- **Consumers**: Cache cleanup in Core API
- **Partition Key**: Provider ID

#### Model Capability Events

**ModelCapabilitiesDiscovered**
- **Trigger**: When model capabilities are discovered for a provider
- **Consumers**: Cache updates, eliminates redundant discovery calls
- **Partition Key**: Provider ID

### Event Processing

#### Message Transport

- **Development/Single-instance**: In-memory transport via MassTransit
- **Production/Multi-instance**: Can be upgraded to RabbitMQ, Azure Service Bus, or Amazon SQS
- **Redis Usage**: Used for caching and data protection, not message transport

#### Reliability Features

- **Retry Policy**: 3 retries with incremental backoff (1s, 2s, 3s)
- **Dead Letter Queue**: Failed messages with delayed redelivery (5min, 15min, 30min intervals)
- **Graceful Degradation**: Services function normally when event bus is unavailable
- **Error Isolation**: Event failures don't break main business operations

#### Ordered Processing

Events are partitioned by entity ID to ensure:
- **Virtual Key events** process in order per virtual key
- **Provider events** process in order per provider
- **Spend updates** eliminate race conditions between individual and batch operations

### Implementation Details

#### Event Publishers

**AdminVirtualKeyService** (`ConduitLLM.Admin`)
```csharp
// Publishes VirtualKeyUpdated when properties change
await _publishEndpoint.Publish(new VirtualKeyUpdated
{
    KeyId = key.Id,
    KeyHash = key.KeyHash,
    ChangedProperties = changedProperties.ToArray(),
    CorrelationId = Guid.NewGuid().ToString()
});
```

**AdminProviderCredentialService** (`ConduitLLM.Admin`)
```csharp
// Publishes ProviderCredentialUpdated when credentials change
await _publishEndpoint.Publish(new ProviderCredentialUpdated
{
    ProviderId = existingCredential.Id,
    ProviderName = existingCredential.ProviderName,
    IsEnabled = existingCredential.IsEnabled,
    ChangedProperties = changedProperties.ToArray()
});
```

**ProviderDiscoveryService** (`ConduitLLM.Core`)
```csharp
// Publishes ModelCapabilitiesDiscovered after discovery
await _publishEndpoint.Publish(new ModelCapabilitiesDiscovered
{
    ProviderId = providerId,
    ProviderName = providerName,
    ModelCapabilities = modelCapabilities,
    DiscoveredAt = DateTime.UtcNow
});
```

#### Event Consumers

**VirtualKeyCacheInvalidationHandler** (`ConduitLLM.Http`)
- Handles `VirtualKeyUpdated`, `VirtualKeyDeleted`, `SpendUpdated`
- Invalidates Redis cache entries for affected virtual keys
- Ensures cache consistency across service instances

**SpendUpdateProcessor** (`ConduitLLM.Http`)
- Handles `SpendUpdateRequested` events
- Processes spend updates in order per virtual key
- Publishes `SpendUpdated` events after successful processing

**ProviderCredentialEventHandler** (`ConduitLLM.Http`)
- Handles `ProviderCredentialUpdated`, `ProviderCredentialDeleted`
- Triggers automatic model capability refresh
- Cleans up cached provider data

### Event Flow Examples

#### Virtual Key Update Flow
1. **Admin API**: User updates virtual key via REST API
2. **AdminVirtualKeyService**: Updates database and publishes `VirtualKeyUpdated`
3. **Core API**: `VirtualKeyCacheInvalidationHandler` receives event
4. **Redis Cache**: Key invalidated immediately across all instances
5. **Next Request**: Fresh data loaded from database

#### Provider Credential Change Flow
1. **Admin API**: User updates provider credentials
2. **AdminProviderCredentialService**: Updates database and publishes `ProviderCredentialUpdated`
3. **Core API**: `ProviderCredentialEventHandler` receives event
4. **Discovery Service**: Automatically refreshes model capabilities for provider
5. **Model Capabilities**: New `ModelCapabilitiesDiscovered` event published
6. **Cache**: Provider capability cache updated

#### Spend Update Flow
1. **Core API**: Request incurs cost for virtual key
2. **CachedApiVirtualKeyService**: Publishes `SpendUpdateRequested`
3. **SpendUpdateProcessor**: Processes spend update in order
4. **Database**: Spend amount updated atomically
5. **Cache**: `SpendUpdated` event triggers cache invalidation

### Configuration

#### RabbitMQ Configuration

For production deployments with multiple service instances, configure RabbitMQ:

```bash
# Core API and Admin API RabbitMQ Configuration
export CONDUITLLM__RABBITMQ__HOST=rabbitmq              # RabbitMQ host (use actual hostname in production)
export CONDUITLLM__RABBITMQ__PORT=5672                  # AMQP port
export CONDUITLLM__RABBITMQ__USERNAME=conduit           # RabbitMQ username
export CONDUITLLM__RABBITMQ__PASSWORD=<secure-password> # RabbitMQ password
export CONDUITLLM__RABBITMQ__VHOST=/                    # Virtual host
export CONDUITLLM__RABBITMQ__PREFETCHCOUNT=10           # Consumer prefetch count
export CONDUITLLM__RABBITMQ__PARTITIONCOUNT=10          # Number of partitions for ordered processing
```

#### MassTransit Registration

The event bus automatically detects RabbitMQ configuration and switches from in-memory to RabbitMQ transport:

**Core API** (`ConduitLLM.Http/Program.cs`)
- Configures consumers for cache invalidation, spend processing, and provider events
- Uses partitioned queues for ordered event processing per entity
- Supports both in-memory (single instance) and RabbitMQ (multi-instance) transports

**Admin API** (`ConduitLLM.Admin/Program.cs`)
- Publisher-only configuration (no consumers)
- Simplified RabbitMQ setup for event publishing
- Supports both in-memory and RabbitMQ transports

#### Service Registration

Services automatically receive `IPublishEndpoint` when MassTransit is configured:

```csharp
// Optional dependency - gracefully degrades when not available
public AdminVirtualKeyService(
    IVirtualKeyRepository virtualKeyRepository,
    IVirtualKeySpendHistoryRepository spendHistoryRepository,
    IVirtualKeyCache? cache,
    IPublishEndpoint? publishEndpoint, // Optional
    ILogger<AdminVirtualKeyService> logger)
```

### Troubleshooting

#### Event Processing Issues

**Check MassTransit Registration**
```bash
# Look for these log messages on startup
[Conduit] Event bus configured with in-memory transport (single-instance mode)
[ConduitLLM.Admin] Event bus configured with in-memory transport (single-instance mode)
```

**Verify Event Publishing**
```bash
# Check logs for event publishing
Published VirtualKeyUpdated event for key {KeyId} with changes: {ChangedProperties}
Published ProviderCredentialUpdated event for provider {ProviderName}
```

**Confirm Event Consumption**
```bash
# Check logs for event processing
Cache invalidated for Virtual Key: {KeyHash}
Spend update processed for key {KeyId}: {Amount}
```

#### Performance Monitoring

- **Cache Hit Rates**: Monitor Redis cache statistics
- **Event Processing Time**: Track event handler execution time
- **Database Query Reduction**: Compare before/after query counts
- **External API Calls**: Monitor provider discovery call frequency

### Migration Notes

#### From Direct Database Access
- **Before**: Admin API directly invalidated Core API caches
- **After**: Admin API publishes events, Core API handles cache invalidation
- **Benefit**: Eliminates tight coupling between services

#### From Polling-Based Updates
- **Before**: NavigationStateService polled APIs every 30 seconds
- **After**: Event-driven updates provide real-time data consistency
- **Benefit**: Reduces unnecessary API calls and database queries

### Multi-Instance Deployment with RabbitMQ

As of the latest update, Conduit now supports RabbitMQ for horizontal scaling:

1. **Automatic Transport Detection**: The system automatically uses RabbitMQ when configured via environment variables
2. **Partitioned Processing**: Events are routed to partition queues based on entity IDs for ordered processing
3. **Durable Messaging**: All queues and messages are durable by default
4. **Health Monitoring**: RabbitMQ connectivity is included in health checks

To enable RabbitMQ, simply set the environment variables documented above. The system will:
- Switch from in-memory to RabbitMQ transport automatically
- Create necessary exchanges and queues on startup
- Route events based on partition keys for ordered processing
- Handle connection failures with automatic recovery

### Future Enhancements

#### Additional Events
- **UserActivity events** for audit logging
- **PerformanceMetrics events** for monitoring
- **SecurityAlert events** for threat detection