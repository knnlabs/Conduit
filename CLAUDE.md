# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**Last Reviewed**: 2025-01-02

## Repository Information
- **GitHub Repository**: knnlabs/Conduit
- **Issues URL**: https://github.com/knnlabs/Conduit/issues
- **Pull Requests URL**: https://github.com/knnlabs/Conduit/pulls

## CRITICAL SECURITY: Authentication
**WebUI Authentication**: The WebUI now uses Clerk for authentication. Human administrators authenticate through Clerk, not through password-based authentication.

**Backend Authentication Key**:
- **CONDUIT_API_TO_API_BACKEND_AUTH_KEY**: 
   - Used by WebUI backend to authenticate with the Core API and Admin API
   - This is for server-to-server communication between backend services
   - NOT for end-users or client applications
   - Configured on the WebUI service to talk to other backend services

## Build Commands
- Build solution: `dotnet build`
- Run tests: `dotnet test`
- Run specific test: `dotnet test --filter "FullyQualifiedName=ConduitLLM.Tests.TestClassName.TestMethodName"`
- Start API server: `dotnet run --project ConduitLLM.Http`
- Start web UI: `dotnet run --project ConduitLLM.WebUI`
- Start both services: `docker compose up -d`

## Database Migrations - CRITICAL
**⚠️ ALWAYS READ [Database Migration Guide](docs/claude/database-migration-guide.md) BEFORE MAKING DATABASE CHANGES**
- We use PostgreSQL ONLY - no SQL Server syntax allowed
- Always run `./scripts/migrations/validate-postgresql-syntax.sh` after creating migrations
- Common mistake: Using `IsActive = 1` instead of `"IsActive" = true`

## Build Verification - CRITICAL
**ALWAYS VERIFY BUILDS BEFORE COMPLETING WORK:**

### Project-Specific Build Commands
- **WebUI**: `cd ConduitLLM.WebUI && npm run build`
- **Core API**: `dotnet build ConduitLLM.Http`
- **Admin Client**: `cd SDKs/Node/Admin && npm run build`
- **Core Client**: `cd SDKs/Node/Core && npm run build`
- **Full Solution**: `dotnet build`

### Incremental Development Rules
1. **NEVER make more than 3-5 file changes without building**
2. **ALWAYS run the appropriate build command after ANY TypeScript/React changes**
3. **Fix ALL ESLint errors immediately - do not accumulate technical debt**
4. **Never commit code that doesn't build cleanly**

### TypeScript/React Specific Rules
- When replacing `any` types, test immediately with `npm run build`
- Check existing error handling patterns before creating new ones
- Use small, incremental changes (1-3 files at a time)
- Follow established import patterns in the codebase
- Validate type changes before moving to next files

### ESLint Error Prevention
- Configure stricter ESLint rules at the start of work
- Run `npm run lint` frequently during development
- Address warnings immediately, don't let them accumulate
- Use proper TypeScript patterns from existing codebase

### WebUI ESLint Strict Rules - CRITICAL
The WebUI uses very strict ESLint rules that will cause build failures:

1. **Type Safety Rules**:
   - `@typescript-eslint/no-unsafe-assignment`: Cannot assign `any` or `unknown` types without explicit casting
   - `@typescript-eslint/no-unsafe-argument`: Cannot pass `any` or `unknown` typed values as arguments
   - `@typescript-eslint/no-unsafe-member-access`: Cannot access properties on `any` typed values
   - `@typescript-eslint/no-unsafe-return`: Cannot return `any` typed values

2. **Console Logging**:
   - Only `console.warn` and `console.error` are allowed
   - `console.log` will cause build failures
   - Use `console.warn` for development debugging

3. **Nullish Coalescing**:
   - Use `??` instead of `||` for default values
   - ESLint rule: `@typescript-eslint/prefer-nullish-coalescing`

4. **Type Casting Pattern**:
   ```typescript
   // BAD - will fail ESLint
   const data = event.data as MetricsData;
   
   // GOOD - proper type narrowing
   const data = event.data as unknown as MetricsData;
   // OR better - type guard
   if (isMetricsData(event.data)) {
     // event.data is now typed
   }
   ```

5. **Always Run Build Before Committing**:
   - `cd ConduitLLM.WebUI && npm run build`
   - Fix ALL ESLint errors immediately
   - The build will fail with any ESLint errors

## Development Workflow
- After implementing features, always run: `dotnet build` to check for compilation errors
- **For WebUI changes**: ALWAYS run `cd ConduitLLM.WebUI && npm run build` 
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
- **[R2 Health Check](docs/claude/r2-health-check.md)** - Cloudflare R2 health monitoring and connectivity checks

## Key Points from Detailed Docs

### Media Storage
- Development uses in-memory storage by default
- Production requires S3-compatible storage (AWS S3, Cloudflare R2, MinIO)
- **WARNING**: Media files are not cleaned up when virtual keys are deleted (see `docs/TODO-Media-Lifecycle-Management.md`)

### Cloudflare R2 Specific Configuration
- **Automatic Detection**: The system automatically detects R2 based on the service URL
- **Optimized Settings**: When R2 is detected, multipart uploads use 10MB chunks (vs 5MB default)
- **CORS Configuration**: R2 may require manual CORS setup in the Cloudflare dashboard
- **Public Access**: Enable public access in R2 dashboard for CDN functionality
- **Benefits**: R2 offers free egress bandwidth, making it cost-effective for media delivery

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