# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Table of Contents
1. [⚠️ CRITICAL: Safety First](#️-critical-safety-first)
2. [Quick Start Guide](#quick-start-guide)
3. [Development Environment](#development-environment)
4. [Build & Verification](#build--verification)
5. [Code Quality Standards](#code-quality-standards)
6. [Architecture Essentials](#architecture-essentials)
7. [Documentation Index](#documentation-index)
8. [Repository & Collaboration](#repository--collaboration)

---

# ⚠️ CRITICAL: Safety First

## Commands That WILL Break Development

### ❌ FORBIDDEN WEBADMIN COMMANDS
**These commands break the development container and force a 5+ minute restart:**
- `npm run build` (anywhere in WebAdmin directory)
- `cd WebAdmin && npm run build`
- `./scripts/dev/dev-workflow.sh build-webadmin` (production testing only)

**Why?** The development container uses an isolated `.next` directory. Running npm build on the host corrupts the container's build state.

### ✅ SAFE WEBADMIN VERIFICATION
Use these instead:
- `npm run lint` - Check ESLint errors
- `npm run type-check` - Verify TypeScript types
- Hot reloading automatically validates code changes

### ❌ FORBIDDEN DEVELOPMENT COMMANDS
- `docker compose up` for development (always use `./scripts/dev/start-dev.sh`)

**If you run forbidden commands, you will:**
1. Break the development environment
2. Force a restart with `--clean` (5+ minutes)
3. Waste time and ignore explicit instructions

---

# Quick Start Guide

## Starting Development Services

**⚠️ CANONICAL DEVELOPMENT STARTUP:**
```bash
./scripts/dev/start-dev.sh
```

### Available Flags
```bash
./scripts/dev/start-dev.sh              # Standard startup
./scripts/dev/start-dev.sh --webadmin   # Rebuild WebAdmin container
./scripts/dev/start-dev.sh --clean      # Complete reset (removes all volumes)
./scripts/dev/start-dev.sh --build      # Force rebuild with --no-cache
./scripts/dev/start-dev.sh --help       # Show usage
```

**Flag Details:**
- `--webadmin`: Restarts WebAdmin container (fixes Next.js issues)
- `--clean`: Removes containers, volumes, node_modules, build artifacts
- `--build`: Rebuilds containers with `--no-cache` flag

## Available Services
After startup, these services are available:
- 🌐 **WebAdmin**: http://localhost:3000 (Next.js with hot reloading)
- 📚 **Gateway API Swagger**: http://localhost:5000/swagger
- 🔧 **Admin API Swagger**: http://localhost:5002/swagger
- 🐰 **RabbitMQ Management**: http://localhost:15672 (conduit/conduitpass)

## Quick Verification
```bash
docker ps                              # Check running containers
docker compose -f docker-compose.yml -f docker-compose.dev.yml logs -f [service]
```

---

# Development Environment

## How Development Works

### Key Features
- ✅ Node modules exist on HOST - direct npm command access
- ✅ WebAdmin directory mounted for hot reloading
- ✅ User ID mapping prevents permission issues (uses your UID/GID)
- ✅ Development containers use node:22-alpine directly
- ✅ Isolated .next directories - container manages its own build state

### Development vs Production

| Aspect | Development (`start-dev.sh`) | Production (`docker compose up`) |
|--------|------------------------------|----------------------------------|
| WebAdmin Container | `node:22-alpine` with mounted source | Built Next.js app in container |
| Hot Reloading | ✅ Enabled via volume mounts | ❌ Static build |
| User Permissions | Maps to host UID/GID | Runs as container user |
| Node Modules | Shared with host | Container-only |

### Technical Implementation

**Volume Mounting:**
- WebAdmin source: `./WebAdmin:/app/WebAdmin`
- SDKs: `./SDKs:/app/SDKs`
- Node modules accessible from both host and container

**Permission Handling:**
1. Container starts as root
2. Installs su-exec (Alpine Linux user switching)
3. Fixes ownership to `${DOCKER_USER_ID}:${DOCKER_GROUP_ID}`
4. Switches to mapped user for all operations

**Environment Variables:**
```bash
export DOCKER_USER_ID=$(id -u)
export DOCKER_GROUP_ID=$(id -g)
```

## Helper Commands

### dev-workflow.sh
```bash
./scripts/dev/dev-workflow.sh logs                 # WebAdmin logs (real-time)
./scripts/dev/dev-workflow.sh shell                # Open shell in container
./scripts/dev/dev-workflow.sh lint-fix-webadmin    # ESLint with --fix
./scripts/dev/dev-workflow.sh build-sdks           # Build SDKs
./scripts/dev/dev-workflow.sh exec [command]       # Execute custom command
```

### Other Helper Scripts
- `scripts/dev/fix-webadmin-errors.sh` - Automated TypeScript/ESLint fixes
- `scripts/dev/fix-sdk-errors.sh` - SDK TypeScript compilation fixes
- `scripts/dev/create-webadmin-key.sh` - Create virtual keys for testing
- `scripts/test/validate-eslint.sh` - Validate ESLint configuration

## Troubleshooting

### Permission Denied Errors
```bash
# Symptom: npm EACCES errors, cannot write to node_modules
./scripts/dev/start-dev.sh --clean
```

### After Adding New Packages
```bash
./scripts/dev/start-dev.sh --webadmin
```

### Container Conflicts
```bash
# Symptom: Containers already exist or port conflicts
docker compose down --volumes --remove-orphans
./scripts/dev/start-dev.sh --clean
```

### Next.js Build Issues / Stale Builds
```bash
./scripts/dev/start-dev.sh --webadmin
```

### WebAdmin Not Starting
```bash
# Check logs
docker compose -f docker-compose.yml -f docker-compose.dev.yml logs webadmin

# Common causes:
# 1. Port 3000 already in use
# 2. Missing environment variables in .env
# 3. Node modules corruption (use --clean)
```

### Hot Reload Not Working
```bash
# Verify file mounting
docker compose -f docker-compose.yml -f docker-compose.dev.yml exec webadmin ls -la /app/WebAdmin/

# Clean host build artifacts (container has isolated .next)
rm -rf WebAdmin/.next
./scripts/dev/start-dev.sh --webadmin
```

---

# Build & Verification

## ⚠️ CRITICAL: Always Verify Builds

### WebAdmin Verification (SAFE)
**Never use `npm run build` - it breaks the container!**

```bash
cd WebAdmin
npm run lint         # Check ESLint errors
npm run type-check   # Verify TypeScript types
```

### Backend & SDK Build Commands
```bash
# Full solution
dotnet build

# All tests
dotnet test

# Specific test
dotnet test --filter "FullyQualifiedName=ConduitLLM.Tests.TestClassName.TestMethodName"

# Individual projects
dotnet build ConduitLLM.Gateway    # Gateway API
dotnet build ConduitLLM.Admin   # Admin API

# SDKs
cd SDKs/Node/Admin && npm run build
cd SDKs/Node/Core && npm run build
cd SDKs/Node/Common && npm run build
```

## Incremental Development Rules

1. **NEVER make more than 3-5 file changes without verifying**
2. **ALWAYS verify after ANY changes:**
   - WebAdmin: `npm run lint` and `npm run type-check` ONLY
   - Backend/SDKs: Use build commands above
3. **Fix ALL errors immediately** - do not accumulate technical debt
4. **Never commit code that doesn't verify cleanly**

## Development Workflow

1. Make changes to code
2. Verify immediately:
   - **WebAdmin**: `npm run lint` && `npm run type-check`
   - **Backend**: `dotnet build`
   - **SDKs**: `npm run build` in SDK directory
3. Test changes:
   - API changes: Test with Swagger UI or curl
   - UI changes: Verify in browser with dev tools open
4. Clean up temporary files/scripts
5. Commit only verified code

---

# Code Quality Standards

## WebAdmin ESLint Strict Rules

The WebAdmin uses **very strict ESLint rules** that cause build failures:

### 1. Type Safety Rules
- `@typescript-eslint/no-unsafe-assignment` - Cannot assign `any`/`unknown` without explicit casting
- `@typescript-eslint/no-unsafe-argument` - Cannot pass `any`/`unknown` as arguments
- `@typescript-eslint/no-unsafe-member-access` - Cannot access properties on `any` types
- `@typescript-eslint/no-unsafe-return` - Cannot return `any` types

### 2. Console Logging
- ✅ Only `console.warn` and `console.error` allowed
- ❌ `console.log` causes build failures
- Use `console.warn` for development debugging

### 3. Nullish Coalescing
- Use `??` instead of `||` for default values
- Rule: `@typescript-eslint/prefer-nullish-coalescing`

### 4. Type Casting Pattern
```typescript
// ❌ BAD - will fail ESLint
const data = event.data as MetricsData;

// ✅ GOOD - proper type narrowing
const data = event.data as unknown as MetricsData;

// ✅ BETTER - type guard
if (isMetricsData(event.data)) {
  // event.data is now typed
}
```

## TypeScript/React Best Practices

- Replace `any` types incrementally, verify immediately
- Check existing error handling patterns before creating new ones
- Use small changes (1-3 files at a time)
- Follow established import patterns
- Run `npm run lint` frequently during development
- Address warnings immediately

## C# Code Style Guidelines

### Naming Conventions
- Interfaces: Prefix with 'I' (e.g., `ILLMClient`)
- Async methods: Suffix with 'Async'
- Private fields: Prefix with underscore (`_logger`)
- Public members: PascalCase
- Parameters: camelCase

### Formatting
- Indentation: 4 spaces
- Braces: Opening braces on new line (Allman style)
- Max line length: ~100 characters

### Error Handling
- Use custom exception types inheriting from base exceptions
- Include contextual information in exception messages
- Use try/catch with appropriate logging

### Testing
- Test method pattern: `MethodName_Condition_ExpectedResult`
- One assertion per test (preferred)
- Use Moq for mocking dependencies

---

# Architecture Essentials

## Database Migrations

**⚠️ CRITICAL: PostgreSQL Syntax ONLY**

### Requirements
- Use standard EF Core workflow: `dotnet ef migrations add` → `dotnet ef database update`
- PostgreSQL syntax only (double quotes for identifiers, `true`/`false` booleans)
- Test tooling first: `dotnet ef --version`
- See `docs/architecture/patterns/repository-and-data-access.md` for best practices

### Common Mistakes
- ❌ SQL Server syntax: `IsActive = 1`
- ✅ PostgreSQL syntax: `"IsActive" = true`
- ❌ Single quotes for identifiers
- ✅ Double quotes for identifiers

## Provider Architecture

**⚠️ IMPORTANT**: The codebase supports multiple providers of the same type (e.g., multiple OpenAI configurations). **Provider ID is the canonical identifier, not ProviderType.**

### Key Concepts
- **Provider ID**: Canonical identifier for Provider records (use for lookups, relationships, identification)
- **ProviderType**: Categorizes by API type (OpenAI, Anthropic, etc.) - multiple providers can share the same type
- **Provider Name**: User-facing display name (can change, don't use for identification)
- **ProviderKeyCredential**: Individual API keys per provider (supports load balancing/failover)

### Architecture
- **Provider Entity**: Instance (e.g., "Production OpenAI", "Dev Azure OpenAI")
- **ProviderKeyCredential Entity**: API keys with ProviderAccountGroup for account separation
- **ModelProviderMapping**: Links model aliases to Provider.Id (NOT ProviderType!)
- **ModelCost**: Flexible cost configs via ModelCostMapping

### Available Provider Types
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
    ElevenLabs = 8,     // Audio provider
    Cerebras = 9,       // High-performance inference
    SambaNova = 10      // Ultra-fast inference
}
```

**Documentation:**
- `docs/architecture/provider-system/provider-architecture.md` - Detailed architecture
- `docs/architecture/provider-system/model-and-cost-mapping.md` - Cost configuration
- `docs/architecture/provider-system/provider-system-analysis.md` - System analysis

## Security & Authentication

### WebAdmin Authentication
- **Human admins**: Authenticate through Clerk (not password-based)
- **Backend service-to-service**: Uses `CONDUIT_API_TO_API_BACKEND_AUTH_KEY`

### Backend Authentication Key
**CONDUIT_API_TO_API_BACKEND_AUTH_KEY**:
- Used by WebAdmin backend to authenticate with Gateway API and Admin API
- Server-to-server communication only
- NOT for end-users or client applications
- Configured on WebAdmin service

## WebAdmin API Architecture

**The WebAdmin has only 3 API routes** - relies on client-side SDK usage with ephemeral keys:
- `/api/health` - Health check endpoint
- `/api/auth/ephemeral-key` - Generate virtual keys for Gateway API access
- `/api/auth/ephemeral-master-key` - Generate master keys for Admin API access

### When Writing New API Routes
- ✅ Use `getServerAdminClient()` for Admin API access
- ✅ Use `await getServerCoreClient()` for Gateway API access (async!)
- ✅ Wrap all SDK calls in `try/catch` with `handleSDKError(error)`
- ✅ Return `NextResponse.json(data)` for responses
- ✅ Validate request input before passing to SDK
- ❌ **Never** create SDK clients directly with `new ConduitAdminClient()`
- ❌ **Never** expose master keys to clients

**Full guide:** `docs/development/API-PATTERNS-BEST-PRACTICES.md`

## Media Storage & Cleanup

**⚠️ CRITICAL**: Proper cleanup configuration required to prevent unbounded storage costs

### Configuration
- Development: S3-compatible storage (configure in .env)
- Production: AWS S3 or Cloudflare R2
- **MUST** configure storage provider in Admin API for automatic cleanup
- See `docs/CRITICAL-Media-Cleanup-Configuration.md`

### Cloudflare R2 Specifics
- Automatic detection based on service URL
- 10MB multipart upload chunks (vs 5MB default)
- May require manual CORS setup in dashboard
- Enable public access in R2 dashboard for CDN
- Free egress bandwidth

## Event-Driven Architecture

- **Publish/consume through the Conduit-owned `IEventBus` / `IEventHandler<T>` abstraction**
  (`ConduitLLM.Configuration.Messaging`), NOT MassTransit types directly. Inject `IEventBus`
  to publish; implement `IEventHandler<TEvent>` (handler context is `IEventContext`) to consume.
- The abstraction currently runs over **MassTransit** (in-memory for dev, RabbitMQ for
  production); it is being migrated to **Wolverine on the PostgreSQL transport + outbox**
  behind a config flag, with no domain changes (epic #909).
- Events ensure cache consistency and eliminate race conditions; spend ordering is
  RabbitMQ-native (single-active-consumer), webhook retry is deferred delivery.
- The 4 tuned endpoints are described as data in `ConduitEndpointPolicies` (retry,
  circuit-breaker, rate-limit, ordering) so both backends translate the same descriptors.

**See:** `docs/architecture/messaging-migration/` (ADR-001, inventory, phase plans) and
`docs/architecture/media-generation/async-media-generation.md`

## Real-Time Updates

- **SignalR** provides real-time updates via WebSockets
- Redis backplane for horizontal scaling
- Falls back to polling if WebSocket fails
- Hubs: navigation-state, video-generation, image-generation

**See:** `docs/architecture/real-time/streaming-and-websockets.md`

## High-Throughput Configuration

- RabbitMQ: 1,000+ async tasks per minute
- Settings: 25 prefetch, 30 partitions, 50 concurrent messages
- HTTP client pooling: 50 connections per server
- Circuit breakers and rate limiting

**See:** `docs/operations/infrastructure/rabbitmq-scaling.md`

---

# Documentation Index

## Core Development Guides
- **[API Patterns & Best Practices](docs/development/API-PATTERNS-BEST-PRACTICES.md)** - WebAdmin API patterns, SDK usage, error handling
- **[LLM Client Factory Guide](docs/development/llm-client-factory-guide.md)** - Provider client creation patterns
- **[Development Documentation](docs/development/README.md)** - Development guides index

## Architecture Documentation
- **[Architecture Overview](docs/architecture/README.md)** - Complete architecture index
- **[Provider System](docs/architecture/provider-system/provider-architecture.md)** - Provider design, multi-instance support
- **[Model & Cost Mapping](docs/architecture/provider-system/model-and-cost-mapping.md)** - Cost tracking details
- **[Streaming & WebSockets](docs/architecture/real-time/streaming-and-websockets.md)** - Real-time communication, SSE
- **[Webhook Delivery](docs/architecture/real-time/webhook-delivery.md)** - Distributed delivery, circuit breakers
- **[Async Media Generation](docs/architecture/media-generation/async-media-generation.md)** - Event-driven image/video
- **[Background Services](docs/architecture/patterns/background-services-and-workers.md)** - Worker patterns, distributed locking
- **[Repository & Data Access](docs/architecture/patterns/repository-and-data-access.md)** - EF Core best practices
- **[DTO Guidelines](docs/architecture/data-transfer/dto-guidelines.md)** - Data transfer patterns
- **[Scaling Architecture](docs/architecture/infrastructure/scaling-architecture.md)** - 10,000+ concurrent sessions

## Operations & Deployment
- **[Operations Documentation](docs/operations/README.md)** - Operations index
- **[SignalR Configuration](docs/operations/signalr/configuration.md)** - Real-time updates, Redis backplane
- **[RabbitMQ Scaling](docs/operations/infrastructure/rabbitmq-scaling.md)** - 1,000+ tasks/min configuration
- **[Redis Resilience](docs/operations/infrastructure/redis-resilience.md)** - Configuration and failover
- **[PostgreSQL Scaling](docs/operations/infrastructure/postgresql-scaling.md)** - Database scaling
- **[HTTP Connection Pooling](docs/operations/infrastructure/http-connection-pooling.md)** - Connection optimization
- **[Provider Health Monitoring](docs/operations/providers/health-monitoring.md)** - Provider status tracking
- **[Provider Usage Mappings](docs/operations/providers/usage-mappings.md)** - Usage tracking config
- **[Deployment Configuration](docs/operations/deployment/DEPLOYMENT-CONFIGURATION.md)** - Production guide
- **[Docker Optimization](docs/operations/deployment/docker-optimization.md)** - Container optimization

## Media & Storage
- **[Media Cleanup Configuration](docs/CRITICAL-Media-Cleanup-Configuration.md)** - ⚠️ CRITICAL - S3/R2 cleanup requirements

## API Integration Guides
- **[API Guides Index](docs/api-guides/README.md)** - API integration documentation
- **[Gateway API Getting Started](docs/api-guides/core/getting-started.md)** - Gateway API usage
- **[Admin API Getting Started](docs/api-guides/admin/getting-started.md)** - Admin API usage
- **[SignalR Getting Started](docs/api-guides/signalr/getting-started.md)** - Real-time integration
- **[SDK Best Practices](docs/api-guides/sdk/best-practices.md)** - SDK usage patterns
- **[Next.js Integration](docs/api-guides/sdk/nextjs-integration.md)** - WebAdmin SDK integration

---

# Repository & Collaboration

## Git Branching Rules

- **Protected branch**: `master` - Never push directly
- **Development branch**: `dev` - All development work goes here
- **Feature branches**: Create from `dev`
- **Pull requests**: Target the `dev` branch
- **Release process**: `dev` merged to `master` through controlled releases

### Current Branch Status
- Main branch: `master`
- Active development: `dev`

## Collaboration Guidelines

- **Challenge and question**: Don't immediately agree with suboptimal/unclear requests
- **Push back constructively**: Suggest better alternatives with clear reasoning
- **Think critically**: Consider edge cases, performance, maintainability, best practices
- **Seek clarification**: Ask follow-up questions when requirements are ambiguous
- **Propose improvements**: Suggest better patterns, more robust solutions
- **Be a thoughtful collaborator**: Act as a good teammate improving project quality

## Repository Information

- **GitHub Repository**: knnlabs/Conduit
- **Issues URL**: https://github.com/knnlabs/Conduit/issues
- **Pull Requests URL**: https://github.com/knnlabs/Conduit/pulls
