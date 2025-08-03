# ConduitLLM.Core

## Overview

**ConduitLLM.Core** is the foundational library of the ConduitLLM system, providing a unified abstraction layer for interacting with multiple Large Language Model (LLM) providers. It offers sophisticated routing, context management, and orchestration capabilities for enterprise-scale LLM applications.

## Architecture

The ConduitLLM system follows a modular architecture:

- **ConduitLLM.Core**: Central orchestration, interfaces, models, routing, and provider abstraction
- **ConduitLLM.Providers**: Provider-specific implementations for OpenAI, Anthropic, Google, and other LLM services
- **ConduitLLM.Configuration**: Centralized configuration management and validation
- **ConduitLLM.Http**: RESTful API layer for external integrations
- **ConduitLLM.Security**: Authentication, authorization, and security utilities
- **ConduitLLM.Admin**: Administrative interface and management tools

## Core Capabilities

### Multi-Provider Support
- **Unified API**: Single interface for OpenAI, Anthropic, Google, Azure, and custom providers
- **Provider-specific optimizations**: Leverage unique features of each LLM service
- **Fallback mechanisms**: Automatic failover between providers for reliability

### Advanced Routing
- **Intelligent load balancing**: Round-robin, least-used, random, and custom strategies
- **Model aliasing**: Abstract model names from provider-specific identifiers
- **Priority routing**: Route requests based on cost, latency, or quality requirements

### Context Management
- **Token optimization**: Automatic context window management and message trimming
- **Conversation history**: Intelligent conversation state management
- **Cost optimization**: Dynamic context sizing based on model pricing

### Enterprise Features
- **Health monitoring**: Built-in health checks and provider status monitoring
- **Metrics collection**: Prometheus-compatible metrics for observability
- **Circuit breakers**: Resilience patterns with Polly integration
- **Caching**: Redis-based caching for embeddings and responses
- **Message queuing**: MassTransit integration for async processing

## Key Components

### Core Classes
- **Conduit**: Primary orchestration class for all LLM operations
- **ILLMClientFactory**: Factory pattern for provider client instantiation
- **ILLMRouter**: Routing strategy implementations
- **IContextManager**: Context window and conversation management

### Data Models
- **ChatCompletionRequest/Response**: Standardized chat interfaces
- **EmbeddingRequest/Response**: Text embedding operations
- **ImageGenerationRequest/Response**: DALL-E, Stable Diffusion, and other image models
- **Streaming support**: Real-time response streaming for all operations

### Infrastructure
- **HealthChecks**: Provider health monitoring and status reporting
- **Caching**: Multi-level caching for performance optimization
- **Metrics**: Comprehensive telemetry and monitoring
- **Validation**: Request/response validation and sanitization

## Key Features

### Provider Support
- **OpenAI**: GPT-4, GPT-3.5-turbo, DALL-E, Whisper, and embeddings
- **Anthropic**: Claude 3.x series (Haiku, Sonnet, Opus)
- **Google**: Gemini Pro and Vision models
- **Azure OpenAI**: Enterprise-grade OpenAI models
- **Custom Providers**: Extensible interface for proprietary models

### Routing Strategies
- **Simple**: Direct routing to specified models
- **RoundRobin**: Distribute load evenly across available models
- **LeastUsed**: Route to the least recently used provider
- **Random**: Random selection with optional weighting
- **Passthrough**: Bypass routing for direct model access

### Enterprise Integrations
- **AWS S3**: Model artifact storage and retrieval
- **Redis**: Distributed caching and session management
- **Prometheus**: Metrics collection and monitoring
- **MassTransit**: Message queuing and async processing
- **HealthChecks**: Comprehensive health monitoring system

## Core Components

### Primary Classes
- **Conduit**: Main orchestrator for chat completions, streaming, embeddings, and image generation
- **ILLMClient**: Unified interface for all LLM provider interactions
- **ILLMClientFactory**: Factory for creating provider-specific clients
- **ILLMRouter**: Intelligent routing and load balancing
- **IContextManager**: Context window and conversation management

### Supporting Infrastructure
- **Interfaces/**: Contracts for extensibility and dependency injection
- **Models/**: Comprehensive request/response DTOs
- **Exceptions/**: Rich error handling with specific exception types
- **Configuration/**: Environment-based configuration management
- **HealthChecks/**: Provider health monitoring and diagnostics
- **Caching/**: Multi-tier caching strategies
- **Validation/**: Input validation and sanitization
- **Utilities/**: Common helpers and extensions

## Configuration & Environment

### Database Configuration
The `DbConnectionHelper` provides zero-configuration database connectivity:

**PostgreSQL:**
```bash
DATABASE_URL=postgresql://user:password@host:5432/conduit_db
```

**SQLite:**
```bash
CONDUIT_SQLITE_PATH=/data/conduit_config.db
```

**Auto-detection**: Automatically detects provider type and configures Entity Framework Core

### Environment Variables
```bash
# Core Configuration
CONDUIT_ENVIRONMENT=Development|Staging|Production
CONDUIT_LOG_LEVEL=Debug|Information|Warning|Error

# Provider Configuration
OPENAI_API_KEY=sk-...
ANTHROPIC_API_KEY=sk-ant-...
GOOGLE_API_KEY=...

# Optional Features
REDIS_CONNECTION_STRING=localhost:6379
PROMETHEUS_ENABLED=true
HEALTH_CHECK_INTERVAL=30
```

## Usage Patterns

### Basic Setup (Dependency Injection)
```csharp
// Program.cs or Startup.cs
services.AddConduitLLM(configuration);

// Usage
public class MyService
{
    private readonly IConduit _conduit;
    
    public MyService(IConduit conduit)
    {
        _conduit = conduit;
    }
    
    public async Task<string> GenerateResponse(string prompt)
    {
        var request = new ChatCompletionRequest
        {
            Model = "router:roundrobin:gpt-4",
            Messages = new[]
            {
                new ChatMessage { Role = "user", Content = prompt }
            }
        };
        
        var response = await _conduit.CreateChatCompletionAsync(request);
        return response.Choices.First().Message.Content;
    }
}
```

### Advanced Configuration
```csharp
// Custom routing strategy
services.Configure<ConduitOptions>(options =>
{
    options.DefaultStrategy = "leastused";
    options.EnableContextManagement = true;
    options.MaxTokens = 4000;
});

// Provider-specific settings
services.Configure<OpenAIOptions>(configuration.GetSection("OpenAI"));
services.Configure<AnthropicOptions>(configuration.GetSection("Anthropic"));
```

### Direct Usage (Advanced)
```csharp
var factory = new LLMClientFactory(configuration);
var router = new RoundRobinRouter(factory);
var conduit = new Conduit(factory, logger, router);

// Streaming example
await foreach (var chunk in conduit.StreamChatCompletionAsync(request))
{
    Console.Write(chunk.Content);
}
```

## Routing & Model Management

### Routing Syntax
```
router:[strategy]:[model]
router:roundrobin:gpt-4
router:leastused:claude-3-sonnet
router:random
```

### Model Aliases
Configure provider-specific model mappings in `appsettings.json`:

```json
{
  "ConduitLLM": {
    "ModelMappings": {
      "gpt-4": "openai:gpt-4-turbo-preview",
      "claude-3": "anthropic:claude-3-sonnet-20240229",
      "gemini": "google:gemini-pro"
    }
  }
}
```

### Advanced Routing
- **Weighted routing**: Assign weights to providers for cost optimization
- **Fallback chains**: Define fallback sequences for reliability
- **Region-aware routing**: Route based on geographic latency
- **Quota management**: Automatic provider switching based on usage limits

## Configuration Management

### Application Configuration
```json
{
  "ConduitLLM": {
    "Providers": {
      "OpenAI": {
        "ApiKey": "${OPENAI_API_KEY}",
        "BaseUrl": "https://api.openai.com/v1",
        "Models": ["gpt-4", "gpt-3.5-turbo", "text-embedding-ada-002"]
      },
      "Anthropic": {
        "ApiKey": "${ANTHROPIC_API_KEY}",
        "BaseUrl": "https://api.anthropic.com",
        "Models": ["claude-3-sonnet", "claude-3-opus"]
      }
    },
    "Routing": {
      "DefaultStrategy": "roundrobin",
      "EnableFallback": true,
      "MaxRetries": 3
    },
    "ContextManagement": {
      "Enabled": true,
      "MaxTokens": 4000,
      "TrimStrategy": "oldest"
    }
  }
}
```

### Environment-based Configuration
All configuration supports environment variable substitution and validation.

## Dependencies

### Core Framework
- **.NET 9.0** - Latest LTS framework with performance optimizations
- **C# 12.0** - Modern language features and performance improvements

### Microsoft Extensions
- **Microsoft.Extensions.Options** (9.0.7) - Configuration management
- **Microsoft.Extensions.Logging.Abstractions** (9.0.7) - Structured logging
- **Microsoft.Extensions.Diagnostics.HealthChecks** (9.0.7) - Health monitoring

### Enterprise Libraries
- **AWSSDK.S3** (4.0.5) - AWS S3 integration for model artifacts
- **Polly** (8.6.2) - Resilience patterns and circuit breakers
- **prometheus-net** (8.2.1) - Metrics collection and monitoring
- **MassTransit** (8.5.1) - Message queuing and async processing
- **MassTransit.Redis** (8.5.1) - Redis transport for MassTransit
- **TiktokenSharp** (1.1.7) - Token counting and optimization

### Project Dependencies
- **ConduitLLM.Configuration** - Shared configuration models and validation
- **ConduitLLM.Providers** - Provider-specific implementations (indirect)

## Extensibility Guide

### Adding New LLM Providers

1. **Create Provider Implementation**
   ```csharp
   public class CustomLLMClient : ILLMClient
   {
       public async Task<ChatCompletionResponse> CreateChatCompletionAsync(
           ChatCompletionRequest request, 
           string? apiKey = null, 
           CancellationToken cancellationToken = default)
       {
           // Implementation for your LLM provider
       }
       
       // Implement other required methods...
   }
   ```

2. **Register in Factory**
   ```csharp
   public class CustomClientFactory : ILLMClientFactory
   {
       public ILLMClient GetClient(string modelAlias)
       {
           if (modelAlias.StartsWith("custom:"))
               return new CustomLLMClient();
           // ... other providers
       }
   }
   ```

3. **Add Configuration**
   ```json
   {
     "CustomProvider": {
       "ApiKey": "${CUSTOM_API_KEY}",
       "BaseUrl": "https://api.custom-llm.com",
       "Models": ["custom-model-1", "custom-model-2"]
     }
   }
   ```

### Custom Routing Strategies

1. **Implement ILLMRouter**
   ```csharp
   public class PriorityRouter : ILLMRouter
   {
       public Task<ILLMClient> GetClientAsync(
           string model, 
           CancellationToken cancellationToken = default)
       {
           // Custom routing logic based on priority, cost, or other factors
       }
   }
   ```

2. **Register Custom Router**
   ```csharp
   services.AddSingleton<ILLMRouter, PriorityRouter>();
   ```

## Development & Testing

### Development Setup
```bash
# Clone repository
git clone [repository-url]
cd Conduit

# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run tests
dotnet test

# Run specific test project
dotnet test ConduitLLM.Tests
dotnet test ConduitLLM.Http.Tests
dotnet test ConduitLLM.Admin.Tests
```

### Testing Strategy
- **Unit Tests**: Core logic validation in `ConduitLLM.Tests`
- **Integration Tests**: API testing in `ConduitLLM.Http.Tests`
- **Admin Tests**: Administrative interface testing in `ConduitLLM.Admin.Tests`
- **Load Testing**: Performance and scalability validation
- **Provider Tests**: Individual provider integration validation

### Contributing
1. Fork the repository
2. Create feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open Pull Request

### Performance Benchmarks
- **Throughput**: >1000 requests/second (depends on provider limits)
- **Latency**: <100ms overhead (excluding provider latency)
- **Memory**: <50MB baseline memory usage
- **Scalability**: Horizontal scaling with Redis clustering

## License & Support

### License
See the root of the repository for license information.

### Support
- **Documentation**: [Full documentation](https://docs.conduit-llm.com)
- **Issues**: [GitHub Issues](https://github.com/[org]/conduit-llm/issues)
- **Discussions**: [GitHub Discussions](https://github.com/[org]/conduit-llm/discussions)
- **Security**: Report security issues to security@conduit-llm.com

### Roadmap
- **Q2 2025**: Enhanced multi-modal support (video, audio)
- **Q3 2025**: Advanced caching strategies with semantic search
- **Q4 2025**: Federated learning integration
- **Q1 2026**: Edge deployment optimizations
