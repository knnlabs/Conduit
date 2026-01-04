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
- `./scripts/dev/dev-workflow.ps1 build-webadmin` (production testing only)

**Why?** The development container uses an isolated `.next` directory. Running npm build on the host corrupts the container's build state.

### ✅ SAFE WEBADMIN VERIFICATION
Use these instead:
- `npm run lint` - Check ESLint errors
- `npm run type-check` - Verify TypeScript types
- Hot reloading automatically validates code changes

### ❌ FORBIDDEN DEVELOPMENT COMMANDS
- `docker compose up` for development (always use `./scripts/dev/start-dev.ps1`)

**If you run forbidden commands, you will:**
1. Break the development environment
2. Force a restart with `--clean` (5+ minutes)
3. Waste time and ignore explicit instructions

---

# Quick Start Guide

## Starting Development Services

**⚠️ CANONICAL DEVELOPMENT STARTUP:**
```powershell
./scripts/dev/start-dev.ps1
```

### Available Flags
```powershell
./scripts/dev/start-dev.ps1              # Standard startup
./scripts/dev/start-dev.ps1 -WebAdmin    # Rebuild WebAdmin container
./scripts/dev/start-dev.ps1 -Clean       # Complete reset (removes all volumes)
./scripts/dev/start-dev.ps1 -Build       # Force rebuild (uses cache where possible)
./scripts/dev/start-dev.ps1 -Rebuild     # Full rebuild with --no-cache (nuclear option)
./scripts/dev/start-dev.ps1 -Logs -LogService webadmin  # Show container logs
```

**Flag Details:**
- `-WebAdmin`: Restarts WebAdmin container (fixes Next.js issues)
- `-Clean`: Removes containers, volumes, node_modules, build artifacts
- `-Build`: Rebuilds containers (uses cache where possible)
- `-Rebuild`: Full rebuild with `--no-cache` flag (nuclear option)
- `-Logs [-LogService <name>]`: Show container logs for specific service

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

| Aspect | Development (`start-dev.ps1`) | Production (`docker compose up`) |
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

### dev-workflow.ps1
```powershell
# Build Commands
./scripts/dev/dev-workflow.ps1 build-webadmin       # Build WebAdmin application
./scripts/dev/dev-workflow.ps1 build-sdks           # Build all SDK packages
./scripts/dev/dev-workflow.ps1 build-sdk <name>     # Build specific SDK (common|admin|core)

# Lint/Type Commands
./scripts/dev/dev-workflow.ps1 lint-webadmin        # Run ESLint on WebAdmin
./scripts/dev/dev-workflow.ps1 lint-fix-webadmin    # Run ESLint with --fix
./scripts/dev/dev-workflow.ps1 type-check-webadmin  # Run TypeScript type checking

# NPM Commands
./scripts/dev/dev-workflow.ps1 npm-install-webadmin # Install WebAdmin dependencies
./scripts/dev/dev-workflow.ps1 npm-install-sdks     # Install all SDK dependencies

# Container Commands
./scripts/dev/dev-workflow.ps1 shell                # Open bash shell in container
./scripts/dev/dev-workflow.ps1 logs                 # Show WebAdmin container logs
./scripts/dev/dev-workflow.ps1 restart-webadmin     # Restart WebAdmin container
./scripts/dev/dev-workflow.ps1 status               # Show container status
./scripts/dev/dev-workflow.ps1 exec <cmd>           # Execute command in container

# Local Development
./scripts/dev/dev-workflow.ps1 install-local        # Install all dependencies locally
./scripts/dev/dev-workflow.ps1 build-local          # Build all TypeScript projects locally
./scripts/dev/dev-workflow.ps1 clean                # Clean node_modules and build artifacts
```

### Other Helper Scripts
- `scripts/dev/fix-webadmin-errors.ps1` - Automated TypeScript/ESLint fixes
  - `-LintOnly` - Run linting and fixing only (skip build)
  - `-BuildOnly` - Run build only (skip linting)
  - `-CheckOnly` - Check environment and permissions only
- `scripts/dev/fix-sdk-errors.ps1` - SDK TypeScript compilation fixes
- `scripts/dev/create-webadmin-key.ps1` - Create virtual keys for testing
- `scripts/dev/setup-r2-dev.ps1` - Setup Cloudflare R2 development environment
- `scripts/test/validate-eslint.sh` - Validate ESLint configuration
- `scripts/test/validate-eslint-strict.sh` - Strict ESLint validation (CI/CD)
- `scripts/migrations/validate-migrations.sh` - Validate EF Core migrations

## Troubleshooting

### Permission Denied Errors
```powershell
# Symptom: npm EACCES errors, cannot write to node_modules
./scripts/dev/start-dev.ps1 -Clean
```

### After Adding New Packages
```powershell
./scripts/dev/start-dev.ps1 -WebAdmin
```

### Container Conflicts
```powershell
# Symptom: Containers already exist or port conflicts
docker compose down --volumes --remove-orphans
./scripts/dev/start-dev.ps1 -Clean
```

### Next.js Build Issues / Stale Builds
```powershell
./scripts/dev/start-dev.ps1 -WebAdmin
```

### WebAdmin Not Starting
```powershell
# Check logs
docker compose -f docker-compose.yml -f docker-compose.dev.yml logs webadmin

# Common causes:
# 1. Port 3000 already in use
# 2. Missing environment variables in .env
# 3. Node modules corruption (use -Clean)
```

### Hot Reload Not Working
```powershell
# Verify file mounting
docker compose -f docker-compose.yml -f docker-compose.dev.yml exec webadmin ls -la /app/WebAdmin/

# Clean host build artifacts (container has isolated .next)
Remove-Item -Recurse -Force WebAdmin/.next
./scripts/dev/start-dev.ps1 -WebAdmin
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
cd SDKs/Node/Gateway && npm run build
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
    SambaNova = 10,     // Ultra-fast inference
    DeepInfra = 11      // OpenAI-compatible LLM inference
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

### Health Monitoring Key
**CONDUIT_HEALTH_MONITORING_KEY**:
- Used by external monitoring services (BetterStack, Pingdom, etc.) to access health endpoints
- Passed via `X-Conduit-Health-Key` header
- Private network requests (10.x, 172.16-31.x, 192.168.x, 127.x) don't require this key
- External requests without valid key receive `404 Not Found`
- See `docs/operations/monitoring/health-checks.md` for configuration details

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
- See `docs/operations/deployment/media-cleanup-configuration.md`

### Cloudflare R2 Specifics
- Automatic detection based on service URL
- 10MB multipart upload chunks (vs 5MB default)
- May require manual CORS setup in dashboard
- Enable public access in R2 dashboard for CDN
- Free egress bandwidth

## Event-Driven Architecture

- **MassTransit** for event processing with RabbitMQ
- Supports in-memory (dev) or RabbitMQ (production)
- Events ensure cache consistency and eliminate race conditions
- Virtual Key events partitioned by key ID for ordered processing

**See:** `docs/architecture/media-generation/async-media-generation.md`

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

## Quick Start by Role
| Role | Start Here |
|------|------------|
| New to Conduit | [Main Documentation Hub](docs/README.md) |
| API User | [API Guides Overview](docs/api-guides/README.md) |
| Administrator | [Operations Guide](docs/operations/README.md) |
| Developer/Contributor | [Development Guide](docs/development/README.md) |

## Core Architecture
- **[Architecture Overview](docs/architecture/README.md)** - Complete system design index
- **[Provider System](docs/architecture/provider-system/provider-architecture.md)** - Multi-instance provider support
- **[Model & Cost Mapping](docs/architecture/provider-system/model-and-cost-mapping.md)** - Cost tracking details
- **[Scaling Architecture](docs/architecture/infrastructure/scaling-architecture.md)** - 10,000+ concurrent sessions
- **[Cache Usage Patterns](docs/architecture/infrastructure/cache-usage.md)** - IMemoryCache vs IDistributedCache
- **[Repository & Data Access](docs/architecture/patterns/repository-and-data-access.md)** - EF Core patterns
- **[Background Services](docs/architecture/patterns/background-services-and-workers.md)** - Worker patterns

## API Documentation

### Gateway API (OpenAI-Compatible)
- **[Getting Started](docs/api-guides/gateway/getting-started.md)** - Authentication, quick start
- **[API Reference](docs/api-guides/gateway/api-reference.md)** - Complete endpoint documentation
- **[Streaming with Tools](docs/api-guides/streaming-with-tools.md)** - Function calling with streaming

### Admin API (Management)
- **[Getting Started](docs/api-guides/admin/getting-started.md)** - Auth setup, first steps
- **[TypeScript SDK](docs/api-guides/admin/typescript-sdk.md)** - Complete SDK guide
- **[API Reference](docs/api-guides/admin/api-reference.md)** - Endpoint documentation

### Feature Guides
- **[Function Calling](docs/api-guides/features/function-calling.md)** - LLM tool execution
- **[Multimodal Vision](docs/api-guides/features/multimodal-vision.md)** - Image analysis
- **[LLM Routing](docs/api-guides/features/llm-routing.md)** - Load balancing, failover
- **[Webhooks](docs/api-guides/features/webhooks.md)** - Event notifications

### SDK Integration
- **[SDK Overview](docs/api-guides/sdk/README.md)** - Client library capabilities
- **[Best Practices](docs/api-guides/sdk/best-practices.md)** - Security, performance
- **[Next.js Integration](docs/api-guides/sdk/nextjs-integration.md)** - WebAdmin patterns
- **[Troubleshooting](docs/api-guides/sdk/troubleshooting.md)** - Common issues

## Real-Time Communication
- **[SignalR Overview](docs/api-guides/signalr/README.md)** - Real-time features
- **[Getting Started](docs/api-guides/signalr/getting-started.md)** - Setup and usage
- **[Hub Reference](docs/api-guides/signalr/hub-reference.md)** - Available hubs and events
- **[Client Examples](docs/api-guides/signalr/client-examples.md)** - Integration examples
- **[Streaming & WebSockets](docs/architecture/real-time/streaming-and-websockets.md)** - Architecture details
- **[Webhook Delivery](docs/architecture/real-time/webhook-delivery.md)** - Distributed delivery

## Media Generation
- **[Async Media Generation](docs/architecture/media-generation/async-media-generation.md)** - Event-driven image/video
- **[Progress & Notifications](docs/architecture/media-generation/progress-and-notifications.md)** - Real-time progress
- **[Media Cleanup](docs/operations/deployment/media-cleanup-configuration.md)** - S3/R2 cleanup (CRITICAL)

## Development Guides
- **[Development Overview](docs/development/README.md)** - Contributing to Conduit
- **[API Patterns](docs/development/API-PATTERNS-BEST-PRACTICES.md)** - RESTful design patterns
- **[LLM Client Factory](docs/development/llm-client-factory-guide.md)** - Provider client creation
- **[WebAdmin Development](docs/development/webui/README.md)** - WebAdmin contribution guide
- **[DTO Guidelines](docs/architecture/data-transfer/dto-guidelines.md)** - Data transfer patterns

## Operations & Deployment
- **[Operations Hub](docs/operations/README.md)** - Production operations index
- **[Deployment Configuration](docs/operations/deployment/DEPLOYMENT-CONFIGURATION.md)** - Production guide
- **[Docker Optimization](docs/operations/deployment/docker-optimization.md)** - Container optimization
- **[CI/CD Maintenance](docs/operations/deployment/ci-cd-maintenance-guide.md)** - Pipeline maintenance

### Infrastructure Scaling
- **[PostgreSQL Scaling](docs/operations/infrastructure/postgresql-scaling.md)** - Database optimization
- **[RabbitMQ Scaling](docs/operations/infrastructure/rabbitmq-scaling.md)** - 1,000+ tasks/min
- **[Redis Resilience](docs/operations/infrastructure/redis-resilience.md)** - High availability
- **[HTTP Connection Pooling](docs/operations/infrastructure/http-connection-pooling.md)** - Connection optimization

### Monitoring & Observability
- **[Monitoring Setup](docs/operations/monitoring/setup-guide.md)** - Prometheus/Grafana
- **[Health Checks](docs/operations/monitoring/health-checks.md)** - Health monitoring
- **[Cost Tracking](docs/operations/monitoring/cost-tracking.md)** - Cost observability
- **[Performance Metrics](docs/operations/monitoring/performance-metrics.md)** - Token/sec, latency

### Runbooks (Incident Response)
- **[Runbook Index](docs/operations/runbooks/README.md)** - Alert catalog with severity
- **[High Error Rate](docs/operations/runbooks/high-error-rate.md)** - Error spike response
- **[High Response Time](docs/operations/runbooks/high-response-time.md)** - Latency incidents
- **[DB Connection Pool](docs/operations/runbooks/db-connection-pool.md)** - Pool exhaustion

### Security
- **[Security Guidelines](docs/operations/security/Security-Guidelines.md)** - API key security, auth patterns
- **[Pre-commit Hooks](docs/operations/security/Security-Pre-commit-Hooks.md)** - Secret detection

## Provider Configuration
- **[Provider Architecture](docs/architecture/provider-system/provider-architecture.md)** - Multi-instance design
- **[Error Tracking](docs/architecture/provider-system/error-tracking.md)** - Provider error registry
- **[Usage Mappings](docs/operations/providers/usage-mappings.md)** - Usage tracking config
- **[Compatibility Report](docs/operations/providers/compatibility-report.md)** - Provider compatibility

## Model Pricing
- **[Pricing Overview](docs/model-pricing/README.md)** - Import process, configuration
- **[Quick Reference](docs/model-pricing/pricing-quick-reference.md)** - Common model lookup
- **[OpenAI Pricing](docs/model-pricing/openai-pricing.md)** - GPT-4, DALL-E, Whisper
- **[Anthropic Pricing](docs/model-pricing/anthropic-pricing.md)** - Claude models

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
