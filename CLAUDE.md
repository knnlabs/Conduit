# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**Last Reviewed**: 2025-08-07 (Corrected to match actual codebase implementation)

## Collaboration Guidelines
- **Challenge and question**: Don't immediately agree or proceed with requests that seem suboptimal, unclear, or potentially problematic
- **Push back constructively**: If a proposed approach has issues, suggest better alternatives with clear reasoning
- **Think critically**: Consider edge cases, performance implications, maintainability, and best practices before implementing
- **Seek clarification**: Ask follow-up questions when requirements are ambiguous or could be interpreted multiple ways
- **Propose improvements**: Suggest better patterns, more robust solutions, or cleaner implementations when appropriate
- **Be a thoughtful collaborator**: Act as a good teammate who helps improve the overall quality and direction of the project

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

## Development Workflow - CRITICAL
**⚠️ CANONICAL DEVELOPMENT STARTUP: Always use `./scripts/start-dev.sh` for development**

### Starting Development Environment

#### Available Flags:
```bash
# Standard startup (builds containers if needed)
./scripts/start-dev.sh

# Rebuild WebUI container (fixes Next.js issues)
./scripts/start-dev.sh --webui

# Complete reset (removes all volumes and containers)
./scripts/start-dev.sh --clean

# Force rebuild containers with no cache
./scripts/start-dev.sh --build
```

#### What Each Flag Actually Does:
- **--webui**: Restarts WebUI container, cleans .next build artifacts
- **--clean**: Removes all containers, volumes, node_modules, and build artifacts for fresh start
- **--build**: Rebuilds containers with `--no-cache` flag
- **--help**: Shows usage information

#### Key Features:
- ✅ Node modules exist on HOST - direct npm command access
- ✅ WebUI directory mounted for hot reloading
- ✅ User ID mapping prevents permission issues (uses your UID/GID)
- ✅ Development containers use node:22-alpine directly

### Build Commands
- Build entire solution: `dotnet build`
- Run all tests: `dotnet test`
- Run specific test: `dotnet test --filter "FullyQualifiedName=ConduitLLM.Tests.TestClassName.TestMethodName"`
- Build Core API: `dotnet build ConduitLLM.Http`
- Build Admin API: `dotnet build ConduitLLM.Admin`

### ⚠️ Production Testing Only
```bash
# Only use for production-like testing, NOT for development
docker compose up -d
```

**Note**: Using `docker compose up -d` will create permission conflicts with development. If you accidentally use it, run `docker compose down --volumes --remove-orphans` before using `./scripts/start-dev.sh`.

## Docker Development Setup

### Starting Development Environment
```bash
# Always use the development script for local development
./scripts/start-dev.sh

# If switching from production docker-compose:
docker compose down --volumes --remove-orphans
./scripts/start-dev.sh --clean
```

### Verifying Services
```bash
# Check running containers
docker ps

# View logs
docker compose -f docker-compose.yml -f docker-compose.dev.yml logs -f [service]
```

### Development vs Production

| Aspect | Development (`start-dev.sh`) | Production (`docker compose up`) |
|--------|------------------------------|----------------------------------|
| WebUI Container | `node:22-alpine` with mounted source | Built Next.js app in container |
| Hot Reloading | ✅ Enabled via volume mounts | ❌ Static build |
| User Permissions | Maps to host UID/GID | Runs as container user |
| Node Modules | Shared with host | Container-only |
| Performance | Optimized for development | Optimized for production |

### How Development Environment Works

**Volume Mounting**: WebUI source code is mounted directly into container, allowing hot reloading.

**Permission Handling**: Container starts as root, fixes ownership to match host user (${DOCKER_USER_ID}:${DOCKER_GROUP_ID}), then switches to that user for all operations.

**Dependency Management**: Node modules are installed in mounted volumes, making them accessible from both host and container.

## Development Troubleshooting

### Common Issues and Solutions

#### Permission Denied Errors
```bash
# Symptom: npm EACCES errors, cannot write to node_modules
# Solution: Clean restart with proper permissions
./scripts/start-dev.sh --clean
```

#### After Adding New Packages
```bash
# When you add packages to package.json
# Restart WebUI container to install new dependencies
./scripts/start-dev.sh --webui
```

#### Container Conflicts
```bash
# Symptom: Containers already exist or port conflicts
# Solution: Stop all containers and restart
docker compose down --volumes --remove-orphans
./scripts/start-dev.sh --clean
```

#### Next.js Build Issues
```bash
# Symptom: WebUI not updating, stale builds
# Solution: Restart WebUI container
./scripts/start-dev.sh --webui
```

#### WebUI Not Starting
```bash
# Check container logs
docker compose -f docker-compose.yml -f docker-compose.dev.yml logs webui

# Common causes:
# 1. Port 3000 already in use
# 2. Missing environment variables in .env
# 3. Node modules corruption (use --clean)
```

#### Hot Reload Not Working
```bash
# Verify file mounting
docker compose -f docker-compose.yml -f docker-compose.dev.yml exec webui ls -la /app/ConduitLLM.WebUI/

# Restart with clean build artifacts
rm -rf ConduitLLM.WebUI/.next
./scripts/start-dev.sh --webui
```

### Development Services
After successful startup, these services are available:
- 🌐 **WebUI**: http://localhost:3000 (Next.js with hot reloading)
- 📚 **Core API Swagger**: http://localhost:5000/swagger
- 🔧 **Admin API Swagger**: http://localhost:5002/swagger
- 🐰 **RabbitMQ Management**: http://localhost:15672 (conduit/conduitpass)

### Development Helper Commands (dev-workflow.sh)
```bash
# Show WebUI logs in real-time
./scripts/dev-workflow.sh logs

# Open shell in WebUI container
./scripts/dev-workflow.sh shell

# Build WebUI in container
./scripts/dev-workflow.sh build-webui

# Run ESLint with --fix
./scripts/dev-workflow.sh lint-fix-webui

# Build SDKs
./scripts/dev-workflow.sh build-sdks

# Execute any command in WebUI container
./scripts/dev-workflow.sh exec [command]
```

### Technical Implementation Notes

#### How Permission Handling Works
The development environment uses user ID mapping to prevent permission issues:

1. **Container starts as root** - Allows initial setup and ownership changes
2. **Installs su-exec** - Lightweight tool for user switching in Alpine Linux
3. **Fixes volume ownership** - Changes ownership to match host user (${DOCKER_USER_ID}:${DOCKER_GROUP_ID})
4. **Switches to host user** - All operations run as the mapped user

#### Volume Mounting Strategy
- WebUI source is mounted directly: `./ConduitLLM.WebUI:/app/ConduitLLM.WebUI`
- SDKs are mounted for development: `./SDKs:/app/SDKs`
- Node modules are accessible from both host and container
- No anonymous volumes that would block host access

#### Environment Variables Set by start-dev.sh
```bash
export DOCKER_USER_ID=$(id -u)    # Your user ID
export DOCKER_GROUP_ID=$(id -g)   # Your group ID
```

## Database Migrations - CRITICAL
**⚠️ ALWAYS READ [Database Migration Guide](docs/claude/database-migration-guide.md) BEFORE MAKING DATABASE CHANGES**
- We use PostgreSQL ONLY - no SQL Server syntax allowed
- Always run `./scripts/migrations/validate-postgresql-syntax.sh` after creating migrations
- Common mistake: Using `IsActive = 1` instead of `"IsActive" = true`

## Build Verification - CRITICAL
**ALWAYS VERIFY BUILDS BEFORE COMPLETING WORK:**

### Project-Specific Build Commands
- **WebUI (in container)**: `./scripts/dev-workflow.sh build-webui`
- **WebUI (on host)**: `cd ConduitLLM.WebUI && npm run build`
- **Core API**: `dotnet build ConduitLLM.Http`
- **Admin API**: `dotnet build ConduitLLM.Admin`
- **Admin SDK**: `cd SDKs/Node/Admin && npm run build`
- **Core SDK**: `cd SDKs/Node/Core && npm run build`
- **Common SDK**: `cd SDKs/Node/Common && npm run build`
- **Full Solution**: `dotnet build`

### Incremental Development Rules
1. **NEVER make more than 3-5 file changes without building**
2. **ALWAYS run the appropriate build command after ANY TypeScript/React changes**
3. **Fix ALL ESLint errors immediately - do not accumulate technical debt**
4. **Never commit code that doesn't build cleanly**

### TypeScript/React Specific Rules
- When replacing `any` types, test immediately with `./scripts/fix-webui-errors.sh` or `./scripts/fix-sdk-errors.sh`
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
- **For WebUI changes**: Run `cd ConduitLLM.WebUI && npm run lint` to check for ESLint errors
- **For TypeScript checks**: Run `cd ConduitLLM.WebUI && npm run type-check` to verify types
- **For production build verification**: Run `cd ConduitLLM.WebUI && npm run build`
- Test your changes locally before committing
- When working with API changes, test with Swagger UI or curl
- For UI changes, verify in the browser with developer tools open
- Clean up temporary test files and scripts after completing features

### Available Helper Scripts
- **fix-webui-errors.sh**: Automated fixes for common WebUI TypeScript/ESLint errors
- **fix-sdk-errors.sh**: Fixes SDK TypeScript compilation issues
- **validate-eslint.sh**: Validates ESLint configuration
- **dev-workflow.sh**: Helper commands for development tasks (see above)
- **create-webui-key.sh**: Creates virtual keys for WebUI testing
- **test-webui-connection.sh**: Tests WebUI connectivity

### WebUI Development
You can run npm commands DIRECTLY on the host filesystem:
- ✅ `cd ConduitLLM.WebUI && npm run lint` - Run ESLint
- ✅ `cd ConduitLLM.WebUI && npm run type-check` - Check TypeScript types
- ✅ `cd SDKs/Node/Admin && npm run build` - Build SDKs

The development environment shares node_modules between host and container.

💡 **Development Notes**:
- The WebUI container runs Next.js dev server with hot-reloading
- Changes to source files are immediately reflected
- To verify production build: `cd ConduitLLM.WebUI && npm run build`
- To check types without building: `cd ConduitLLM.WebUI && npx tsc --noEmit`

## Git Branching Rules
- **Protected branch**: `master` - Never push directly to master
- **Development branch**: `dev` - All development work should be pushed here
- **Feature branches**: Create from `dev` for new features
- **Pull requests**: Should target the `dev` branch
- **Release process**: `dev` is merged to `master` through controlled releases

### Current Branch Status
- Main branch: `master`
- Active development: `dev`

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

## Provider Architecture - CRITICAL
**⚠️ IMPORTANT**: The codebase supports multiple providers of the same type (e.g., multiple OpenAI configurations). Provider ID is the canonical identifier, not ProviderType.

### Key Concepts:
- **Provider ID**: The canonical identifier for Provider records. Use this for lookups, relationships, and identification.
- **ProviderType**: Categorizes providers by their API type (OpenAI, Anthropic, etc.). Multiple providers can share the same ProviderType.
- **Provider Name**: User-facing display name. Can be changed and should not be used for identification.
- **ProviderKeyCredential**: Individual API keys for a provider. Supports multiple keys per provider for load balancing and failover.

### Architecture:
- **Provider Entity**: Represents a provider instance (e.g., "Production OpenAI", "Dev Azure OpenAI")
- **ProviderKeyCredential Entity**: Individual API keys with ProviderAccountGroup for external account separation
- **ModelProviderMapping**: Links model aliases to Provider.Id (NOT ProviderType!)
- **ModelCost**: Flexible cost configurations that can apply to multiple models via ModelCostMapping

### Migration Notes:
- **ProviderType enum**: Used for categorization, stored as integers in database
- **Provider instances**: Multiple providers of the same type can exist (e.g., multiple OpenAI configs)
- **Backward compatibility**: Some legacy properties may be marked `[Obsolete]`

### Documentation:
- See `/docs/architecture/provider-multi-instance.md` for detailed provider architecture
- See `/docs/architecture/model-cost-mapping.md` for cost configuration details
- See `/docs/architecture/provider-system-analysis.md` for system analysis

### Available Provider Types:
```csharp
public enum ProviderType
{
    OpenAI = 1,
    Groq = 2,
    Replicate = 3,
    Fireworks = 4,
    OpenAICompatible = 5,
    MiniMax = 6,
    Ultravox = 7,
    ElevenLabs = 8,  // Audio provider
    Cerebras = 9,     // High-performance inference
    SambaNova = 10    // Ultra-fast inference
}
```

## Detailed Documentation

For comprehensive documentation on specific topics, see:

- **[Database Migration Guide](docs/claude/database-migration-guide.md)** - PostgreSQL migration requirements and validation
- **[XML Documentation Standards](docs/claude/xml-documentation-standards.md)** - Comprehensive XML documentation requirements
- **[Media Storage Configuration](docs/claude/media-storage-configuration.md)** - S3/CDN setup, Docker SignalR configuration
- **[Event-Driven Architecture](docs/claude/event-driven-architecture.md)** - MassTransit events, domain events
- **[SignalR Configuration](docs/claude/signalr-configuration.md)** - Real-time updates, Redis backplane
- **[RabbitMQ High-Throughput](docs/claude/rabbitmq-high-throughput.md)** - Production scaling configuration
- **[Provider Models](docs/claude/provider-models.md)** - Supported models by provider
- **[R2 Health Check](docs/claude/r2-health-check.md)** - Cloudflare R2 health monitoring
- **[SDK Generation Workflow](docs/claude/sdk-generation-workflow.md)** - SDK generation from OpenAPI specs
- **[Batch Cache Invalidation](docs/claude/batch-cache-invalidation.md)** - Cache invalidation batching
- **[Workflow Concurrency Strategy](docs/claude/workflow-concurrency-strategy.md)** - Concurrent execution patterns

## Key Points from Detailed Docs

### Media Storage
- Development defaults to S3-compatible storage (configure in .env)
- Production requires S3-compatible storage (AWS S3, Cloudflare R2)
- **WARNING**: Media files are not automatically cleaned up when virtual keys are deleted

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