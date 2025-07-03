# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**Last Reviewed**: 2025-01-02

## Repository Information
- **GitHub Repository**: knnlabs/Conduit
- **Issues URL**: https://github.com/knnlabs/Conduit/issues
- **Pull Requests URL**: https://github.com/knnlabs/Conduit/pulls

## CRITICAL SECURITY: Authentication Keys
**NEVER CONFUSE THESE TWO KEYS - THEY SERVE COMPLETELY DIFFERENT PURPOSES:**

1. **CONDUIT_MASTER_KEY**: 
   - Used by API clients to authenticate with the Core API
   - Provides access to LLM functionality (chat, completions, embeddings)
   - This is what end-users/applications use to consume AI services
   - Configured on the Core API (ConduitLLM.Http)

2. **CONDUIT_WEBUI_AUTH_KEY**:
   - Used to authenticate administrators to the WebUI dashboard
   - Provides access to admin functions (virtual key management, provider configuration)
   - This is for system administrators only
   - Configured on the WebUI service (ConduitLLM.WebUI)

**SECURITY RULE**: These keys must NEVER be the same value and serve completely different authentication boundaries. The MASTER_KEY is for API consumers, the WEBUI_AUTH_KEY is for administrators.

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

## Detailed Documentation

For comprehensive documentation on specific topics, see:

- **[XML Documentation Standards](docs/claude/xml-documentation-standards.md)** - Comprehensive XML documentation requirements and examples
- **[Media Storage Configuration](docs/claude/media-storage-configuration.md)** - S3/CDN setup, Docker SignalR configuration, lifecycle management
- **[Event-Driven Architecture](docs/claude/event-driven-architecture.md)** - MassTransit events, domain events, troubleshooting
- **[SignalR Configuration](docs/claude/signalr-configuration.md)** - Real-time updates, Redis backplane, multi-instance setup
- **[RabbitMQ High-Throughput](docs/claude/rabbitmq-high-throughput.md)** - Production scaling, 1000+ tasks/minute configuration
- **[Provider Models](docs/claude/provider-models.md)** - Supported models by provider (MiniMax, OpenAI, Replicate)

## Key Points from Detailed Docs

### Media Storage
- Development uses in-memory storage by default
- Production requires S3-compatible storage (AWS S3, Cloudflare R2, MinIO)
- **WARNING**: Media files are not cleaned up when virtual keys are deleted (see `docs/TODO-Media-Lifecycle-Management.md`)

### Event-Driven Architecture
- Uses MassTransit for event processing
- Supports in-memory (dev) or RabbitMQ (production) transport
- Events ensure cache consistency and eliminate race conditions
- Virtual Key events are partitioned by key ID for ordered processing

### Real-Time Updates
- SignalR provides real-time navigation state updates
- Supports Redis backplane for horizontal scaling
- Falls back to polling if WebSocket connection fails
- Three hubs: navigation-state, video-generation, image-generation

### High-Throughput Configuration
- RabbitMQ supports 1,000+ async tasks per minute
- Optimized settings: 25 prefetch, 30 partitions, 50 concurrent messages
- HTTP client connection pooling: 50 connections per server
- Circuit breakers and rate limiting prevent overload