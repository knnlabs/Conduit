# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Database Migrations

**CRITICAL REQUIREMENTS** for creating migrations:

Key requirements:
- Use standard EF Core workflow: `dotnet ef migrations add` → `dotnet ef database update`
- PostgreSQL syntax only (double quotes for identifiers, `true`/`false` booleans, not `1`/`0`)
- Test basic tooling first with `dotnet ef --version`
- See `docs/architecture/patterns/repository-and-data-access.md` for EF Core best practices
- Common mistake: Using SQL Server syntax like `IsActive = 1` instead of `"IsActive" = true`

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
**WebAdmin Authentication**: The WebAdmin now uses Clerk for authentication. Human administrators authenticate through Clerk, not through password-based authentication.

**Backend Authentication Key**:
- **CONDUIT_API_TO_API_BACKEND_AUTH_KEY**: 
   - Used by WebAdmin backend to authenticate with the Core API and Admin API
   - This is for server-to-server communication between backend services
   - NOT for end-users or client applications
   - Configured on the WebAdmin service to talk to other backend services

## Development Workflow - CRITICAL
**⚠️ CANONICAL DEVELOPMENT STARTUP: Always use `./scripts/dev/start-dev.sh` for development**

### Starting Development Environment

#### Available Flags:
```bash
# Standard startup (builds containers if needed)
./scripts/dev/start-dev.sh

# Rebuild WebAdmin container (fixes Next.js issues)
./scripts/dev/start-dev.sh --webadmin

# Complete reset (removes all volumes and containers)
./scripts/dev/start-dev.sh --clean

# Force rebuild containers with no cache
./scripts/dev/start-dev.sh --build
```

#### What Each Flag Actually Does:
- **--webadmin**: Restarts WebAdmin container (container manages its own isolated .next directory)
- **--clean**: Removes all containers, volumes, node_modules, and build artifacts for fresh start
- **--build**: Rebuilds containers with `--no-cache` flag
- **--help**: Shows usage information

#### Key Features:
- ✅ Node modules exist on HOST - direct npm command access
- ✅ WebAdmin directory mounted for hot reloading
- ✅ User ID mapping prevents permission issues (uses your UID/GID)
- ✅ Development containers use node:22-alpine directly
- ✅ Isolated .next directories - run `npm run build` on host without breaking container

### Build Commands
- Build entire solution: `dotnet build`
- Run all tests: `dotnet test`
- Run specific test: `dotnet test --filter "FullyQualifiedName=ConduitLLM.Tests.TestClassName.TestMethodName"`
- Build Core API: `dotnet build ConduitLLM.Http`
- Build Admin API: `dotnet build ConduitLLM.Admin`

## Docker Development Setup

### Starting Development Environment
```bash
# Always use the development script for local development
./scripts/dev/start-dev.sh

# If switching from production docker-compose:
docker compose down --volumes --remove-orphans
./scripts/dev/start-dev.sh --clean
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
| WebAdmin Container | `node:22-alpine` with mounted source | Built Next.js app in container |
| Hot Reloading | ✅ Enabled via volume mounts | ❌ Static build |
| User Permissions | Maps to host UID/GID | Runs as container user |
| Node Modules | Shared with host | Container-only |
| Performance | Optimized for development | Optimized for production |

### How Development Environment Works

**Volume Mounting**: WebAdmin source code is mounted directly into container, allowing hot reloading.

**Permission Handling**: Container starts as root, fixes ownership to match host user (${DOCKER_USER_ID}:${DOCKER_GROUP_ID}), then switches to that user for all operations.

**Dependency Management**: Node modules are installed in mounted volumes, making them accessible from both host and container.

## Development Troubleshooting

### Common Issues and Solutions

#### Permission Denied Errors
```bash
# Symptom: npm EACCES errors, cannot write to node_modules
# Solution: Clean restart with proper permissions
./scripts/dev/start-dev.sh --clean
```

#### After Adding New Packages
```bash
# When you add packages to package.json
# Restart WebAdmin container to install new dependencies
./scripts/dev/start-dev.sh --webadmin
```

#### Container Conflicts
```bash
# Symptom: Containers already exist or port conflicts
# Solution: Stop all containers and restart
docker compose down --volumes --remove-orphans
./scripts/dev/start-dev.sh --clean
```

#### Next.js Build Issues
```bash
# Symptom: WebAdmin not updating, stale builds
# Solution: Restart WebAdmin container
./scripts/dev/start-dev.sh --webadmin
```

#### WebAdmin Not Starting
```bash
# Check container logs
docker compose -f docker-compose.yml -f docker-compose.dev.yml logs webadmin

# Common causes:
# 1. Port 3000 already in use
# 2. Missing environment variables in .env
# 3. Node modules corruption (use --clean)
```

#### Hot Reload Not Working
```bash
# Verify file mounting
docker compose -f docker-compose.yml -f docker-compose.dev.yml exec webadmin ls -la /app/WebAdmin/

# Restart with clean build artifacts (cleans host .next only)
rm -rf WebAdmin/.next
./scripts/dev/start-dev.sh --webadmin
# Note: Container has its own isolated .next directory
```

### Development Services
After successful startup, these services are available:
- 🌐 **WebAdmin**: http://localhost:3000 (Next.js with hot reloading)
- 📚 **Core API Swagger**: http://localhost:5000/swagger
- 🔧 **Admin API Swagger**: http://localhost:5002/swagger
- 🐰 **RabbitMQ Management**: http://localhost:15672 (conduit/conduitpass)

### Development Helper Commands (dev-workflow.sh)
```bash
# Show WebAdmin logs in real-time
./scripts/dev/dev-workflow.sh logs

# Open shell in WebAdmin container
./scripts/dev/dev-workflow.sh shell

# Build WebAdmin in container (builds in container's isolated .next)
./scripts/dev/dev-workflow.sh build-webadmin

# Run ESLint with --fix
./scripts/dev/dev-workflow.sh lint-fix-webadmin

# Build SDKs
./scripts/dev/dev-workflow.sh build-sdks

# Execute any command in WebAdmin container
./scripts/dev/dev-workflow.sh exec [command]
```

### Technical Implementation Notes

#### How Permission Handling Works
The development environment uses user ID mapping to prevent permission issues:

1. **Container starts as root** - Allows initial setup and ownership changes
2. **Installs su-exec** - Lightweight tool for user switching in Alpine Linux
3. **Fixes volume ownership** - Changes ownership to match host user (${DOCKER_USER_ID}:${DOCKER_GROUP_ID})
4. **Switches to host user** - All operations run as the mapped user

#### Volume Mounting Strategy
- WebAdmin source is mounted directly: `./WebAdmin:/app/WebAdmin`
- SDKs are mounted for development: `./SDKs:/app/SDKs`
- Node modules are accessible from both host and container
- No anonymous volumes that would block host access

#### Environment Variables Set by start-dev.sh
```bash
export DOCKER_USER_ID=$(id -u)    # Your user ID
export DOCKER_GROUP_ID=$(id -g)   # Your group ID
```

## Database Migrations - CRITICAL
**⚠️ POSTGRESQL SYNTAX ONLY - NO SQL SERVER PATTERNS**
- We use PostgreSQL ONLY - no SQL Server syntax allowed
- Common mistake: Using `IsActive = 1` instead of `"IsActive" = true`
- See `docs/architecture/patterns/repository-and-data-access.md` for EF Core patterns
- Use double quotes for identifiers, boolean literals for booleans

## Build Verification - CRITICAL
**ALWAYS VERIFY BUILDS BEFORE COMPLETING WORK:**

### ⚠️ WEBADMIN EXCEPTION - NEVER RUN NPM BUILD ⚠️
**FORBIDDEN FOR WEBADMIN DEVELOPMENT:**
- ❌ `npm run build` - **WILL BREAK THE DEVELOPMENT CONTAINER**
- ❌ `cd WebAdmin && npm run build` - **WILL BREAK THE DEVELOPMENT CONTAINER**
- ❌ `./scripts/dev/dev-workflow.sh build-webadmin` - **ONLY FOR PRODUCTION TESTING**

**WEBADMIN VERIFICATION COMMANDS (SAFE FOR DEVELOPMENT):**
- ✅ `npm run lint` - Check ESLint errors
- ✅ `npm run type-check` - Verify TypeScript types
- ✅ Hot reloading in development container automatically validates code

### Project-Specific Build Commands
- **WebAdmin**: **USE LINT AND TYPE-CHECK ONLY** (see above)
- **Core API**: `dotnet build ConduitLLM.Http`
- **Admin API**: `dotnet build ConduitLLM.Admin`
- **Admin SDK**: `cd SDKs/Node/Admin && npm run build`
- **Core SDK**: `cd SDKs/Node/Core && npm run build`
- **Common SDK**: `cd SDKs/Node/Common && npm run build`
- **Full Solution**: `dotnet build`

### Incremental Development Rules
1. **NEVER make more than 3-5 file changes without verifying**
2. **ALWAYS run the appropriate verification command after ANY TypeScript/React changes**
   - **WebAdmin**: `npm run lint` and `npm run type-check` ONLY
   - **Backend/SDKs**: Use build commands listed above
3. **Fix ALL ESLint errors immediately - do not accumulate technical debt**
4. **Never commit code that doesn't verify cleanly**

### TypeScript/React Specific Rules
- When replacing `any` types, test immediately with `./scripts/dev/fix-webadmin-errors.sh` or `./scripts/dev/fix-sdk-errors.sh`
- Check existing error handling patterns before creating new ones
- Use small, incremental changes (1-3 files at a time)
- Follow established import patterns in the codebase
- Validate type changes before moving to next files

### ESLint Error Prevention
- Configure stricter ESLint rules at the start of work
- Run `npm run lint` frequently during development
- Address warnings immediately, don't let them accumulate
- Use proper TypeScript patterns from existing codebase

### WebAdmin ESLint Strict Rules - CRITICAL
The WebAdmin uses very strict ESLint rules that will cause build failures:

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

5. **Always Verify Before Committing**:
   - **WebAdmin**: `npm run lint` and `npm run type-check` (NEVER `npm run build`)
   - Fix ALL ESLint errors immediately
   - The verification will fail with any ESLint errors

## Development Workflow
- After implementing features, always run: `dotnet build` to check for compilation errors
- **For WebAdmin changes**: Run `npm run lint` to check for ESLint errors
- **For TypeScript checks**: Run `npm run type-check` to verify types
- **❌ NEVER run `npm run build` for WebAdmin - it breaks the development container**
- Test your changes locally before committing
- When working with API changes, test with Swagger UI or curl
- For UI changes, verify in the browser with developer tools open
- Clean up temporary test files and scripts after completing features

### Available Helper Scripts
- **dev/fix-webadmin-errors.sh**: Automated fixes for common WebAdmin TypeScript/ESLint errors
- **dev/fix-sdk-errors.sh**: Fixes SDK TypeScript compilation issues
- **test/validate-eslint.sh**: Validates ESLint configuration
- **dev/dev-workflow.sh**: Helper commands for development tasks (see above)
- **dev/create-webadmin-key.sh**: Creates virtual keys for WebAdmin testing

### WebAdmin Development
You can run npm commands DIRECTLY on the host filesystem:
- ✅ `npm run lint` - Run ESLint
- ✅ `npm run type-check` - Check TypeScript types  
- ✅ `cd SDKs/Node/Admin && npm run build` - Build SDKs
- ❌ **NEVER run `npm run build` for WebAdmin - breaks development container**

The development environment shares node_modules between host and container.

💡 **Development Notes**:
- The WebAdmin container runs Next.js dev server with hot-reloading
- Changes to source files are immediately reflected
- **DO NOT run production builds during development**
- To check types: `npm run type-check` (equivalent to `npx tsc --noEmit`)

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
- See `docs/architecture/provider-system/provider-architecture.md` for detailed provider architecture
- See `docs/architecture/provider-system/model-and-cost-mapping.md` for cost configuration details
- See `docs/architecture/provider-system/provider-system-analysis.md` for system analysis

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

### Core Development Guides
- **[API Patterns & Best Practices](docs/development/API-PATTERNS-BEST-PRACTICES.md)** - WebAdmin API patterns, SDK usage, error handling
- **[LLM Client Factory Guide](docs/development/llm-client-factory-guide.md)** - Provider client creation patterns
- **[Development Documentation](docs/development/README.md)** - Development guides index

### Architecture Documentation
- **[Architecture Overview](docs/architecture/README.md)** - Complete architecture documentation index
- **[Provider System Architecture](docs/architecture/provider-system/provider-architecture.md)** - Provider design and multi-instance support
- **[Model and Cost Mapping](docs/architecture/provider-system/model-and-cost-mapping.md)** - Cost tracking and model mapping details
- **[Provider System Analysis](docs/architecture/provider-system/provider-system-analysis.md)** - Architectural analysis and improvements
- **[Streaming and WebSockets](docs/architecture/real-time/streaming-and-websockets.md)** - Real-time communication, SSE, WebSocket streaming
- **[Webhook Delivery](docs/architecture/real-time/webhook-delivery.md)** - Distributed webhook delivery with circuit breakers
- **[Async Media Generation](docs/architecture/media-generation/async-media-generation.md)** - Event-driven image/video generation
- **[Background Services and Workers](docs/architecture/patterns/background-services-and-workers.md)** - Worker patterns, distributed locking, eventual consistency
- **[Repository and Data Access](docs/architecture/patterns/repository-and-data-access.md)** - Data access patterns, EF Core best practices
- **[DTO Guidelines](docs/architecture/data-transfer/dto-guidelines.md)** - Data transfer object patterns
- **[Scaling Architecture](docs/architecture/infrastructure/scaling-architecture.md)** - Scaling to 10,000+ concurrent sessions

### Operations and Deployment
- **[Operations Documentation](docs/operations/README.md)** - Operations documentation index
- **[SignalR Configuration](docs/operations/signalr/configuration.md)** - Real-time updates, Redis backplane
- **[RabbitMQ Scaling](docs/operations/infrastructure/rabbitmq-scaling.md)** - High-throughput configuration (1,000+ tasks/min)
- **[Redis Resilience](docs/operations/infrastructure/redis-resilience.md)** - Redis configuration and failover
- **[PostgreSQL Scaling](docs/operations/infrastructure/postgresql-scaling.md)** - Database scaling strategies
- **[HTTP Connection Pooling](docs/operations/infrastructure/http-connection-pooling.md)** - Connection pool optimization
- **[Provider Health Monitoring](docs/operations/providers/health-monitoring.md)** - Provider status tracking
- **[Provider Usage Mappings](docs/operations/providers/usage-mappings.md)** - Usage tracking configuration
- **[Deployment Configuration](docs/operations/deployment/DEPLOYMENT-CONFIGURATION.md)** - Production deployment guide
- **[Docker Optimization](docs/operations/deployment/docker-optimization.md)** - Container optimization strategies

### Media and Storage
- **[Media Cleanup Configuration](docs/CRITICAL-Media-Cleanup-Configuration.md)** - **⚠️ CRITICAL** - S3/R2 cleanup requirements to prevent unbounded storage costs

### API Integration Guides
- **[API Guides Index](docs/api-guides/README.md)** - API integration documentation
- **[Core API Getting Started](docs/api-guides/core/getting-started.md)** - Core API usage
- **[Admin API Getting Started](docs/api-guides/admin/getting-started.md)** - Admin API usage
- **[SignalR Getting Started](docs/api-guides/signalr/getting-started.md)** - Real-time updates integration
- **[SDK Best Practices](docs/api-guides/sdk/best-practices.md)** - SDK usage patterns
- **[Next.js Integration](docs/api-guides/sdk/nextjs-integration.md)** - WebAdmin SDK integration

## Key Points from Detailed Docs

### WebAdmin API Architecture
**The WebAdmin has only 3 API routes** - it relies on client-side SDK usage with ephemeral keys:
- `/api/health` - Health check endpoint
- `/api/auth/ephemeral-key` - Generate virtual keys for Core API access
- `/api/auth/ephemeral-master-key` - Generate master keys for Admin API access

**When writing new API routes:**
- ✅ Use `getServerAdminClient()` for Admin API access
- ✅ Use `await getServerCoreClient()` for Core API access (note: async!)
- ✅ Wrap all SDK calls in `try/catch` with `handleSDKError(error)`
- ✅ Return `NextResponse.json(data)` for responses
- ✅ Validate request input before passing to SDK
- ❌ **Never** create SDK clients directly with `new ConduitAdminClient()`
- ❌ **Never** expose master keys to clients

**Full guide:** See [API Patterns & Best Practices](docs/development/API-PATTERNS-BEST-PRACTICES.md)

### Media Storage and Cleanup (CRITICAL)
- Development defaults to S3-compatible storage (configure in .env)
- Production requires S3-compatible storage (AWS S3, Cloudflare R2)
- **⚠️ CRITICAL**: Media files require proper cleanup configuration to prevent unbounded storage costs
- **MUST** configure storage provider in Admin API for automatic cleanup when virtual keys are deleted
- See `docs/CRITICAL-Media-Cleanup-Configuration.md` for required configuration

#### Cloudflare R2 Specific Configuration
- **Automatic Detection**: System automatically detects R2 based on service URL
- **Optimized Settings**: R2 uses 10MB multipart upload chunks (vs 5MB default)
- **CORS Configuration**: May require manual CORS setup in Cloudflare dashboard
- **Public Access**: Enable public access in R2 dashboard for CDN functionality
- **Benefits**: R2 offers free egress bandwidth, making it cost-effective for media delivery

### Event-Driven Architecture
- Uses MassTransit for event processing with RabbitMQ
- Supports in-memory (dev) or RabbitMQ (production) transport
- Events ensure cache consistency and eliminate race conditions
- Virtual Key events are partitioned by key ID for ordered processing
- See `docs/operations/infrastructure/rabbitmq-scaling.md` for production configuration
- See `docs/architecture/media-generation/async-media-generation.md` for async task patterns

### Real-Time Updates
- SignalR provides real-time updates via WebSockets
- Supports Redis backplane for horizontal scaling across multiple instances
- Falls back to polling if WebSocket connection fails
- Hubs: navigation-state, video-generation, image-generation
- See `docs/operations/signalr/configuration.md` for setup
- See `docs/architecture/real-time/streaming-and-websockets.md` for architecture

### High-Throughput Configuration
- RabbitMQ supports 1,000+ async tasks per minute
- Optimized settings: 25 prefetch, 30 partitions, 50 concurrent messages
- HTTP client connection pooling: 50 connections per server
- Circuit breakers and rate limiting prevent overload
- See `docs/operations/infrastructure/rabbitmq-scaling.md` for complete configuration
- See `docs/architecture/infrastructure/scaling-architecture.md` for scaling to 10,000+ concurrent sessions

# CRITICAL SAFETY SECTION - READ FIRST
## Commands That WILL Break Development and Waste Time

### ❌ FORBIDDEN WEBADMIN COMMANDS
These commands will break the development container and force a 5+ minute restart:
- `npm run build` (anywhere in WebAdmin directory)
- `cd WebAdmin && npm run build`
- `./scripts/dev/dev-workflow.sh build-webadmin` (production only)

### ✅ SAFE WEBADMIN COMMANDS  
Use these instead for WebAdmin verification:
- `npm run lint`
- `npm run type-check`

### ❌ FORBIDDEN DEVELOPMENT COMMANDS
- `docker compose up` for development (use `./scripts/dev/start-dev.sh`)

**If you run any forbidden command, you will:**
1. Break the development environment
2. Force the human to restart with `--clean`
3. Waste 5+ minutes of their time
4. Ignore explicit instructions