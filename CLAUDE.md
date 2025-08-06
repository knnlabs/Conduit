# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**Last Reviewed**: 2025-07-29 (Enhanced development workflow)

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

### Starting Development Environment - UPDATED WORKFLOW

#### Common Usage Patterns:
```bash
# First time setup or after package.json changes
./scripts/start-dev.sh

# After adding/removing npm packages
./scripts/start-dev.sh --webui

# Complete reset (removes everything)
./scripts/start-dev.sh --clean
```

#### What Each Flag Does:
- **--webui**: Forces rebuild of the WebUI container
- **--fix**: Fixes permissions and restarts containers
- **--clean**: Removes all containers/volumes and starts fresh
- **--build**: Rebuilds container images, retains volumes.

#### Key Improvements:
- ✅ Node modules exist on HOST - Claude Code works perfectly
- ✅ Run npm/build/lint commands directly on host
- ✅ Fast startup with --fast flag (seconds not minutes)
- ✅ No more anonymous volumes blocking access
- ✅ Shared dependencies between host and container

### Alternative Build Commands
- Build solution: `dotnet build`
- Run tests: `dotnet test`
- Run specific test: `dotnet test --filter "FullyQualifiedName=ConduitLLM.Tests.TestClassName.TestMethodName"`
- Start API server only: `dotnet run --project ConduitLLM.Http`
- Start web UI only: `dotnet run --project ConduitLLM.WebUI`

### ⚠️ Production Testing Only
```bash
# Only use for production-like testing, NOT for development
docker compose up -d
```

**Note**: Using `docker compose up -d` will create permission conflicts with development. If you accidentally use it, run `docker compose down --volumes --remove-orphans` before using `./scripts/start-dev.sh`.

## Migration Guide: From `docker compose` to `start-dev.sh`

### 🚨 IMPORTANT: Read This If You've Been Using `docker compose up -d`

If you've been using `docker compose up -d` for development, you **MUST** migrate to `./scripts/start-dev.sh` to avoid permission issues and get proper hot reloading.

#### Step 1: Clean Your Current Setup
```bash
# Stop all containers and remove problematic volumes
docker compose down --volumes --remove-orphans

# Verify containers are stopped
docker ps -a --filter "name=conduit"
```

#### Step 2: Switch to Development Script
```bash
# Start development environment with the new script
./scripts/start-dev.sh

# If you encounter any issues:
./scripts/start-dev.sh --clean
```

#### Step 3: Verify Everything Works
```bash
# Check that all services are running
docker ps

# Test WebUI at http://localhost:3000
# Test API Swagger at http://localhost:5000/swagger
# Test Admin API at http://localhost:5002/swagger
```

### Key Differences: Old vs New Workflow

| Old Workflow | New Workflow | Benefit |
|-------------|-------------|---------|
| `docker compose up -d` | `./scripts/start-dev.sh` | Permission conflict detection |
| Manual permission fixes | Automatic user ID mapping | No more EACCES errors |
| Production-like containers | Development containers | Hot reloading works |
| Manual cleanup when broken | `--clean` flag available | Easy recovery |
| No validation | Comprehensive health checks | Catch issues early |

### What Changed and Why

**Volume Ownership**: The new script ensures Docker volumes are owned by your user (1000:1000) instead of root, preventing permission denied errors when npm tries to install dependencies.

**Container Strategy**: 
- **Old**: Used production Dockerfile with built WebUI 
- **New**: Uses `node:20-alpine` with source code mounted for hot reloading

**User Mapping**: The development containers now run as your host user, so files created in containers have correct ownership on the host.

## Development Troubleshooting

### Common Issues and Solutions

#### Permission Denied Errors
```bash
# Symptom: npm EACCES errors, cannot write to node_modules
# Solution: Use the fix flag
./scripts/start-dev.sh --fix
```

#### After Adding New Packages
```bash
# When you add packages to package.json
./scripts/start-dev.sh --rebuild
```

#### Daily Development
```bash
# For fast startup when dependencies haven't changed
./scripts/start-dev.sh --fast
```

#### Container Conflicts
```bash
# Symptom: "Found production containers that conflict with development setup"
# Solution: Stop production containers first
docker compose down --volumes --remove-orphans
./scripts/start-dev.sh
```

#### Volume Permission Issues
```bash
# Symptom: "Volume permission mismatch detected"
# Solution 1: Fix permissions without removing volumes (RECOMMENDED)
./scripts/start-dev.sh --fix-perms

# Solution 2: Use the clean flag to remove and recreate volumes
./scripts/start-dev.sh --clean
```

#### WebUI Not Starting
```bash
# Check container logs
docker logs conduit-webui-1

# Common causes:
# 1. Permission issues (use --clean)
# 2. Port conflicts (check if port 3000 is in use)
# 3. Dependency installation failed (check logs for npm errors)
```

#### File Watching Not Working
```bash
# Symptom: Changes not triggering hot reload
# Check if container can see file changes:
docker exec conduit-webui-1 ls -la /app/ConduitLLM.WebUI/

# Solution: Restart development environment
./scripts/start-dev.sh --clean
```

### Development Services
After successful startup, these services are available:
- 🌐 **WebUI**: http://localhost:3000 (Next.js with hot reloading)
- 📚 **Core API Swagger**: http://localhost:5000/swagger
- 🔧 **Admin API Swagger**: http://localhost:5002/swagger
- 🐰 **RabbitMQ Management**: http://localhost:15672 (conduit/conduitpass)

### Advanced Development Commands
```bash
# Show WebUI logs in real-time
./scripts/dev-workflow.sh logs

# Open shell in WebUI container
./scripts/dev-workflow.sh shell

# Build WebUI manually
./scripts/dev-workflow.sh build-webui

# Fix ESLint errors
./scripts/dev-workflow.sh lint-fix-webui
```

### Technical Implementation Notes

#### Volume Ownership Solution
The development script solves Docker volume permission issues using this approach:

1. **Container starts as root** - Allows initial setup and ownership changes
2. **Install su-exec** - Lightweight tool for secure user switching (`apk add su-exec`)
3. **Fix volume ownership** - Set volumes to host user ID: `chown -R ${DOCKER_USER_ID}:${DOCKER_GROUP_ID}`
4. **Switch to host user** - All development operations run as mapped user: `su-exec ${DOCKER_USER_ID}:${DOCKER_GROUP_ID}`

#### Why su-exec vs alternatives?
- **su-exec**: Lightweight, Alpine-friendly, designed for containers
- **gosu**: Heavier alternative, more dependencies
- **USER directive**: Cannot fix existing volume ownership

#### Volume Mapping Strategy
```yaml
# docker-compose.dev.yml uses named volumes to cache dependencies
volumes:
  - webui_node_modules:/app/ConduitLLM.WebUI/node_modules
  - admin_sdk_node_modules:/app/SDKs/Node/Admin/node_modules
  # This avoids slow dependency installation on every container restart
```

#### User ID Environment Variables
```bash
# start-dev.sh automatically detects and exports:
export DOCKER_USER_ID=$(id -u)    # Usually 1000
export DOCKER_GROUP_ID=$(id -g)   # Usually 1000
# These are used in docker-compose.dev.yml for user mapping
```

## Database Migrations - CRITICAL
**⚠️ ALWAYS READ [Database Migration Guide](docs/claude/database-migration-guide.md) BEFORE MAKING DATABASE CHANGES**
- We use PostgreSQL ONLY - no SQL Server syntax allowed
- Always run `./scripts/migrations/validate-postgresql-syntax.sh` after creating migrations
- Common mistake: Using `IsActive = 1` instead of `"IsActive" = true`

## Build Verification - CRITICAL
**ALWAYS VERIFY BUILDS BEFORE COMPLETING WORK:**

### Project-Specific Build Commands
- **WebUI**: `./scripts/start-web.sh --webui`
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
- When replacing `any` types, test immediately with `./scripts/fix-web-errors.sh` or `./scripts/fix-sdk-errors.sh`
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
- **For WebUI changes**: Run `cd ConduitLLM.WebUI && npm run build` directly on host
- **For ESLint**: Run `cd ConduitLLM.WebUI && npm run lint` directly on host
- **For TypeScript checks**: Run `cd ConduitLLM.WebUI && npm run type-check` directly on host
- Test your changes locally before committing
- When working with API changes, test with curl or a REST client
- For UI changes, verify in the browser with developer tools open
- Clean up temporary test files and scripts after completing features

### WebUI Development - NEW SIMPLIFIED WORKFLOW
You can now run npm commands DIRECTLY on the host filesystem:
- ✅ `cd ConduitLLM.WebUI && npm run lint` - Works directly!
- ✅ `cd SDKs/Node/Admin && npm run build` - Works directly!

The development environment now shares node_modules between host and container.

⚠️ **CRITICAL WARNING: WebUI Build Commands**
- **NEVER run `npm run build` in the ConduitLLM.WebUI directory during development**
- The WebUI container runs Next.js dev server which hot-reloads automatically
- Running `npm run build` will conflict with the dev server and break the container
- If you accidentally break the WebUI: `./scripts/start-dev.sh --restart-webui`
- Only run build to verify TypeScript compilation: `cd ConduitLLM.WebUI && npx tsc --noEmit`

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
- **ProviderType enum**: Used for categorization, stored as integers in database (OpenAI=1, Anthropic=2, etc.)
- **Backward compatibility**: Read-only `ProviderName` properties exist for compatibility but are marked `[Obsolete]`
- **Audio Migration (Issue #654)**: AudioCost and AudioUsageLog entities now use ProviderType enum for categorization

### Documentation:
- See `/docs/architecture/provider-multi-instance.md` for detailed provider architecture
- See `/docs/architecture/model-cost-mapping.md` for cost configuration details

### Available Provider Types:
```csharp
public enum ProviderType
{
    OpenAI = 1,
    Anthropic = 2,
    AzureOpenAI = 3,
    Gemini = 4,
    VertexAI = 5,
    Cohere = 6,
    Mistral = 7,
    Groq = 8,
    Ollama = 9,
    Replicate = 10,
    Fireworks = 11,
    Bedrock = 12,
    HuggingFace = 13,
    SageMaker = 14,
    OpenRouter = 15,
    OpenAICompatible = 16,
    MiniMax = 17,
    Ultravox = 18,
    ElevenLabs = 19,
    GoogleCloud = 20,
    Cerebras = 21
}
```

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
- Production requires S3-compatible storage (AWS S3, Cloudflare R2)
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