![ConduitLLM Logo](docs/assets/conduit.png)
[![CodeQL](https://github.com/knnlabs/Conduit/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/knnlabs/Conduit/actions/workflows/codeql-analysis.yml)
[![Build & Test](https://github.com/knnlabs/Conduit/actions/workflows/ci.yml/badge.svg)](https://github.com/knnlabs/Conduit/actions/workflows/ci.yml)
[![OpenAI Compatible](https://img.shields.io/badge/OpenAI-Compatible-brightgreen.svg)](https://platform.openai.com/docs/api-reference)
[![Built with .NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Docker Ready](https://img.shields.io/badge/Docker-Ready-2496ED)](https://www.docker.com/)

> A unified API gateway for multiple LLM providers with OpenAI-compatible endpoints

## Why ConduitLLM?

Are you juggling multiple LLM provider APIs in your applications? ConduitLLM solves this problem by providing:

- **Single Integration Point**: Write your code once, switch LLM providers anytime
- **Vendor Independence**: Avoid lock-in to any single LLM provider
- **Simplified API Management**: Centralized key management and usage tracking
- **Cost Optimization**: Route requests to the most cost-effective or performant models
- **🚀 Enterprise Scale**: Built to handle **10,000+ concurrent sessions** with a lean, efficient tech stack ([see scaling architecture](docs/architecture/infrastructure/scaling-architecture.md))

## Overview

ConduitLLM is a unified, modular, and extensible platform designed to simplify interaction with multiple Large Language Models (LLMs). It provides a single, consistent OpenAI-compatible REST API endpoint, acting as a gateway or "conduit" to various LLM backends such as OpenAI, Groq, Replicate, Fireworks, MiniMax, Cerebras, SambaNova, DeepInfra, Cloudflare Workers AI, and any OpenAI-compatible API.

Built with .NET and designed for containerization (Docker), ConduitLLM streamlines the development, deployment, and management of LLM-powered applications by abstracting provider-specific complexities.

## 🚧 Project Status

> **Note**: ConduitLLM is actively under development. We recommend using the provided SDKs for the most stable integration experience.

### ✅ **Production Ready Features**
- **Text Generation**: Fully tested with OpenAI, Anthropic, and MiniMax providers
- **Image Generation**: Complete implementation with manual testing across multiple providers  
- **Video Generation**: Feature complete with provider integration
- **SDKs**: Stable APIs for Node.js and other platforms

### ⚠️ **In Development**
- **Core & Admin APIs**: May evolve without backward compatibility - use SDKs instead

### 💡 **Recommended Integration**
```bash
# Use SDKs for stable integration
npm install @knn_labs/conduit-gateway-client
npm install @knn_labs/conduit-admin-client
```

## Key Features

- **OpenAI-Compatible REST API**:
  - ✅ **100% OpenAI compatible** - drop-in replacement for OpenAI API clients
  - ✅ **Extended with Conduit features** - optional enhanced events for reasoning, tool execution, and metrics
  - ✅ **Works with standard clients** - OpenAI SDKs and tools work without any modifications
  - 📚 For enhanced features, use Conduit SDKs to access real-time tool execution, reasoning events, and performance metrics
- **Multi-Provider Support**: Interact with various LLM providers through a single interface
- **Model Routing & Mapping**: Define custom model aliases (e.g., `my-gpt4`) and map them to specific provider models (e.g., `openai/gpt-4`)
- **Virtual API Key Management**: Create and manage Conduit-specific API keys (`condt_...`) with built-in spend tracking
- **Streaming Support**: Real-time token streaming with optional enhanced events (reasoning, tool execution progress, metrics)
- **Web-Based User Interface**: Administrative dashboard for configuration and monitoring
- **Enterprise Security Features**: IP filtering, rate limiting, failed login protection, and security headers
- **Security Dashboard**: Real-time monitoring of security events and access attempts
- **Function Calling & Tool Execution**: Server-side function execution with configuration management, cost tracking, and agentic mode support
- **Media Generation Webhooks**: Per-request callback notifications with retry logic, circuit breakers, and delivery tracking for async image/video generation
- **Observability & Monitoring**: Built-in Prometheus metrics, pre-built Grafana dashboards, multi-layered health checks, and optional OpenTelemetry tracing
- **Centralized Configuration**: Flexible configuration via database, environment variables, or JSON files
- **Extensible Architecture**: Easily add support for new LLM providers

![ConduitLLM Dashboard](docs/assets/dashboard-home.png)

## 📦 Versioning

Conduit uses Semantic Versioning (MAJOR.MINOR.PATCH):

- **Docker Tags**: Images are tagged with semantic versions (e.g., `v1.0.0`), branch names, and the `latest` tag.
- **Version Checking**: The WebAdmin displays the current version and can check for updates automatically.
- **Configuration**: Version checking can be controlled via environment variables:
  ```
  CONDUIT_VERSION_CHECK_ENABLED=true
  CONDUIT_VERSION_CHECK_INTERVAL_HOURS=24
  ```

For detailed information on Conduit's versioning approach, see [Versioning Guide](docs/Versioning.md).

## 🏗️ Architecture

ConduitLLM follows a modular architecture with distinct components handling specific responsibilities:

```mermaid
flowchart LR
    WebAdmin["WebAdmin(Admin Dashboard)"]
    AdminAPI["ConduitLLM.Admin(Admin API)"]
    Http["ConduitLLM.Gateway(API Gateway)"]
    Core["ConduitLLM.Core(Orchestration)"]
    Providers["ConduitLLM.Providers(Provider Logic)"]
    Config["ConduitLLM.Configuration(Entities & DTOs)"]
    LLM["LLM Backends(OpenAI, Anthropic, etc.)"]
    
    WebAdmin -->|Admin API Client| AdminAPI
    AdminAPI --> Config
    
    Client["Client App"] --> Http
    Http --> Core
    Core --> Providers
    Providers --> LLM
    
    Http --> Config
    Core --> Config
    Providers --> Config
```

### Components

- **ConduitLLM.Gateway**: OpenAI-compatible REST API gateway handling authentication and request forwarding
- **WebAdmin**: Next.js-based admin interface for configuration and monitoring
- **ConduitLLM.Core**: Central orchestration logic, interfaces, and routing strategies
- **ConduitLLM.Providers**: Provider-specific implementations for different LLM services
- **ConduitLLM.Configuration**: Configuration management across various sources
- **ConduitLLM.Admin**: Administrative API for configuration management

### Admin API Client

The Admin API client provides a way for the WebAdmin to interact with the Admin API service without direct project references. This breaks the circular dependency between the projects and improves the architecture.

To configure the Admin API client in your deployment:

```yaml
# Docker Compose environment variables
environment:
  CONDUIT_ADMIN_API_URL: http://admin:8080  # URL to the Admin API
  CONDUIT_API_TO_API_BACKEND_AUTH_KEY: your_backend_key  # Backend service authentication
  CONDUIT_USE_ADMIN_API: "true"             # Enable Admin API client (vs direct DB access)
  CONDUIT_DISABLE_DIRECT_DB_ACCESS: "true"  # Completely disable legacy mode
```

> **Important**: Direct database access mode (`CONDUIT_USE_ADMIN_API=false`) is deprecated and will be removed after October 2025.

The WebAdmin includes a built-in health check indicator that monitors the connection to the Admin API:

- A green checkmark indicates the Admin API is healthy
- A red warning icon indicates connection issues
- Click the icon to view detailed status and troubleshooting options

Key features:
- **Decoupled Architecture**: WebAdmin and Admin projects are fully decoupled
- **Flexible Deployment**: Services can be deployed separately in distributed environments
- **Clean API Contracts**: API contracts explicitly defined through interfaces and DTOs
- **Configuration Control**: Toggle between direct DB access and API access with a simple flag

### Docker Images: Component Separation

As of May 2025, ConduitLLM is distributed as three separate Docker images:

- **WebAdmin Image**: The Next.js-based admin dashboard (`WebAdmin`)
- **Admin API Image**: The administrative API service (`ConduitLLM.Admin`) 
- **Http Image**: The OpenAI-compatible REST API gateway (`ConduitLLM.Gateway`)

Each service is built, tagged, and published as an independent container:

- `ghcr.io/knnlabs/conduit-webadmin:latest` (WebAdmin)
- `ghcr.io/knnlabs/conduit-admin:latest` (Admin API)
- `ghcr.io/knnlabs/conduit-http:latest` (API Gateway)

#### Why this architecture?
- **Separation of concerns**: Each component can be scaled, deployed, and maintained independently
- **Improved security**: You can isolate components and apply different security policies
- **Simpler deployments**: Compose, Kubernetes, and cloud-native deployments are easier to manage
- **Enhanced reliability**: Components can be updated independently without affecting others

#### How to use the new images

With Docker Compose:

```yaml
# docker-compose.yml

services:
  webadmin:
    image: ghcr.io/knnlabs/conduit-webadmin:latest
    ports:
      - "3000:3000"
    environment:
      CONDUIT_ADMIN_API_BASE_URL: http://admin:8080
      CONDUIT_API_BASE_URL: http://api:8080
      CONDUIT_API_TO_API_BACKEND_AUTH_KEY: your_secure_backend_key
      CONDUIT_API_EXTERNAL_URL: http://localhost:5000
      CONDUIT_ADMIN_API_EXTERNAL_URL: http://localhost:5002
      # Clerk authentication (required for production)
      NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY: your_clerk_publishable_key
      CLERK_SECRET_KEY: your_clerk_secret_key
    depends_on:
      - admin
      - api

  admin:
    image: ghcr.io/knnlabs/conduit-admin:latest
    ports:
      - "5002:8080"
    environment:
      DATABASE_URL: postgresql://conduit:conduitpass@postgres:5432/conduitdb
      CONDUIT_API_TO_API_BACKEND_AUTH_KEY: your_secure_backend_key
      REDIS_URL: redis://redis:6379
      CONDUITLLM__RABBITMQ__HOST: rabbitmq
      CONDUITLLM__RABBITMQ__PORT: 5672
      CONDUITLLM__RABBITMQ__USERNAME: conduit
      CONDUITLLM__RABBITMQ__PASSWORD: conduitpass
    depends_on:
      - postgres
      - redis
      - rabbitmq

  api:
    image: ghcr.io/knnlabs/conduit-http:latest
    ports:
      - "5000:8080"
    environment:
      DATABASE_URL: postgresql://conduit:conduitpass@postgres:5432/conduitdb
      REDIS_URL: redis://redis:6379
      CONDUITLLM__RABBITMQ__HOST: rabbitmq
      CONDUITLLM__RABBITMQ__PORT: 5672
      CONDUITLLM__RABBITMQ__USERNAME: conduit
      CONDUITLLM__RABBITMQ__PASSWORD: conduitpass
    depends_on:
      - postgres
      - redis
      - rabbitmq

  postgres:
    image: postgres:16
    environment:
      POSTGRES_USER: conduit
      POSTGRES_PASSWORD: conduitpass
      POSTGRES_DB: conduitdb
    volumes:
      - pgdata:/var/lib/postgresql/data

  redis:
    image: redis:alpine
    ports:
      - "6379:6379"

  rabbitmq:
    image: rabbitmq:3-management
    ports:
      - "5672:5672"
      - "15672:15672"

volumes:
  pgdata:
```

> **Note:** All CI/CD workflows and deployment scripts should be updated to reference the new image tags. See `.github/workflows/release.yml` for examples.

## Database Configuration (PostgreSQL Only)

Conduit requires PostgreSQL as its database. Configure it via the `DATABASE_URL` environment variable.

- **PostgreSQL (Required):**
  - Set `DATABASE_URL` in the format:
    - `postgresql://user:password@host:port/database`
  - Examples:
    - `DATABASE_URL=postgresql://postgres:yourpassword@yourhost:5432/yourdb`
    - `DATABASE_URL=postgres://conduit:conduitpass@localhost:5432/conduitdb`

The application will fail to start if PostgreSQL is not available. Connection retry logic with exponential backoff is implemented to handle temporary connection issues.

For more details, see the per-service README files.

## Quick Start

### Prerequisites

- .NET 10.0 SDK
- (Optional) Docker Desktop for containerized deployment

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/knnlabs/Conduit.git
   cd Conduit
   ```

2. **Configure LLM Providers**
   - Add your provider API keys via:
     - Environment variables (see [Documentation](docs/README.md))
     - Edit `appsettings.json`
     - Use the WebAdmin after startup

3. **Start the Services**
   ```bash
   docker compose up -d
   ```

4. **Access ConduitLLM**
   - **Local API**: `http://localhost:5000`
   - **Local WebAdmin**: `http://localhost:3000`
   - **Local API Docs**: `http://localhost:5000/swagger` (Development Mode)
   
   *Note: When running locally via `./scripts/dev/start-dev.ps1`, these are the default ports. When deployed using Docker or other methods, access is typically via an HTTPS reverse proxy. Configure the `CONDUIT_API_BASE_URL` environment variable to the public-facing URL (e.g., `https://conduit.yourdomain.com`) for correct link generation.*

### Docker Installation

Pull the individual service images:

```bash
docker pull ghcr.io/knnlabs/conduit-webadmin:latest
docker pull ghcr.io/knnlabs/conduit-admin:latest
docker pull ghcr.io/knnlabs/conduit-http:latest
```

Or use with Docker Compose:

```bash
docker compose up -d
```

*Note: The default Docker configuration assumes ConduitLLM runs behind a reverse proxy that handles HTTPS termination. The containers expose HTTP ports only.*

### Environment Variables

ConduitLLM uses simplified environment variables for easier configuration:

#### Redis Cache Configuration
```bash
# New simplified format (recommended)
REDIS_URL=redis://localhost:6379
CONDUIT_REDIS_INSTANCE_NAME=conduit:  # Optional, defaults to "conduitllm-cache"

# Legacy format (still supported)
CONDUIT_REDIS_CONNECTION_STRING=localhost:6379
CONDUIT_CACHE_ENABLED=true
CONDUIT_CACHE_TYPE=Redis
```

When `REDIS_URL` is provided, cache is automatically enabled with type "Redis".

#### Authentication Configuration
```bash
# Gateway API Authentication (uses Virtual Keys)
# Virtual keys are created via Admin API and used for LLM access
# Format: condt_your-virtual-key-here

# Admin API Authentication
CONDUIT_API_TO_API_BACKEND_AUTH_KEY=your-secure-backend-key  # For backend service authentication

# Legacy format (still supported)
AdminApi__MasterKey=your-secure-master-key
```

**CRITICAL SECURITY:** Authentication in Conduit:
- **CONDUIT_API_TO_API_BACKEND_AUTH_KEY**: Used for backend service-to-service authentication between WebAdmin backend and API services
- **WebAdmin Authentication**: Human administrators authenticate exclusively via Clerk (OAuth/SAML)
  - Users must have `siteadmin: true` in their Clerk public metadata to access the WebAdmin
  - Configure with `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY` and `CLERK_SECRET_KEY`
- **Virtual Keys**: Used by Gateway API for client LLM access (created via Admin API)

#### Next.js WebAdmin Configuration
```bash
# Server-side URLs (for API routes only - never exposed to browser)
CONDUIT_ADMIN_API_BASE_URL=http://localhost:5002
CONDUIT_API_BASE_URL=http://localhost:5000

# Session management
SESSION_SECRET=your-session-secret-key-change-in-production

# SignalR real-time updates
NEXT_PUBLIC_SIGNALR_AUTO_RECONNECT=true
NEXT_PUBLIC_SIGNALR_RECONNECT_INTERVAL=5000

# Feature flags
NEXT_PUBLIC_ENABLE_REAL_TIME_UPDATES=true
NEXT_PUBLIC_ENABLE_ANALYTICS=true
NEXT_PUBLIC_ENABLE_DEBUG_MODE=false
```

#### Security Configuration (WebAdmin)
```bash
# IP Filtering
CONDUIT_IP_FILTERING_ENABLED=true
CONDUIT_IP_FILTER_MODE=permissive          # or "restrictive"
CONDUIT_IP_FILTER_ALLOW_PRIVATE=true       # Auto-allow private IPs
CONDUIT_IP_FILTER_WHITELIST=192.168.1.0/24,10.0.0.0/8
CONDUIT_IP_FILTER_BLACKLIST=203.0.113.0/24

# Rate Limiting
CONDUIT_RATE_LIMITING_ENABLED=true
CONDUIT_RATE_LIMIT_MAX_REQUESTS=100
CONDUIT_RATE_LIMIT_WINDOW_SECONDS=60

# Failed Login Protection
CONDUIT_MAX_FAILED_ATTEMPTS=5
CONDUIT_IP_BAN_DURATION_MINUTES=30
```

#### WebAdmin Configuration Notes

**Security Architecture:**
- All API calls are made server-side through Next.js API routes
- No API keys or sensitive URLs are exposed to the browser
- WebAdmin authenticates administrators separately from API consumers
- SignalR connections use server-side authentication

**Required Configuration:**
1. **Server-side API URLs** - Configure `CONDUIT_ADMIN_API_BASE_URL` and `CONDUIT_API_BASE_URL` for internal communication
2. **Clerk Authentication** - Configure Clerk publishable and secret keys for WebAdmin authentication
3. **Session security** - Use a strong `SESSION_SECRET` for production deployments

For more configuration details, see the [Documentation](docs/README.md).

## Usage

### Using the API

```bash
# Example: Chat completion request
curl http://localhost:5000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer condt_yourvirtualkey" \
  -d '{
    "model": "my-gpt4",
    "messages": [{"role": "user", "content": "Hello, world!"}]
  }'
```

### Using with OpenAI SDKs

Conduit is **100% compatible with standard OpenAI SDKs** - simply point them to your Conduit instance:

```python
# Python example with OpenAI SDK (fully compatible)
from openai import OpenAI

client = OpenAI(
    api_key="condt_yourvirtualkey",
    # Use http://localhost:5000/v1 for local testing,
    # or your configured CONDUIT_API_BASE_URL for deployed instances
    base_url="http://localhost:5000/v1"
)

response = client.chat.completions.create(
    model="my-gpt4",
    messages=[{"role": "user", "content": "Hello, world!"}]
)
```

#### Enhanced Features with Conduit SDKs

For access to Conduit-specific features like real-time tool execution progress, reasoning events, and performance metrics, use the official Conduit SDKs:

```typescript
// Node.js/TypeScript example with Conduit SDK
import { ConduitCoreClient } from '@knn_labs/conduit-gateway-client';
import {
  isChatCompletionChunk,
  isToolExecutingEvent,
  isFinalMetrics
} from '@knn_labs/conduit-gateway-client';

const client = new ConduitCoreClient({
  apiKey: 'condt_yourvirtualkey',
  baseURL: 'http://localhost:5000'
});

const stream = await client.chat.create({
  model: 'gpt-4',
  messages: [{ role: 'user', content: 'What is the weather?' }],
  stream: true,
  function_configuration_ids: ['weather-functions']
});

for await (const event of stream) {
  if (isChatCompletionChunk(event)) {
    // Standard OpenAI content
    const content = event.choices[0]?.delta?.content;
  }
  else if (isToolExecutingEvent(event)) {
    // Conduit extension: real-time tool execution
    console.log(`Executing ${event.function_name}...`);
  }
  else if (isFinalMetrics(event)) {
    // Conduit extension: performance metrics
    console.log(`Tokens: ${event.total_tokens}, Speed: ${event.tokens_per_second}`);
  }
}
```

**Key Differences:**
- **OpenAI SDKs**: ✅ Full compatibility, ignores Conduit extensions
- **Conduit SDKs**: ✅ Full compatibility + enhanced events (reasoning, tool execution, metrics)

See [Streaming with Tools Guide](docs/api-guides/streaming-with-tools.md) for complete documentation.


## Documentation

See the **[Documentation Hub](docs/README.md)** for the complete, organized documentation index. Key sections include:

| Area | Description |
|------|-------------|
| **[API Guides](docs/api-guides/)** | Gateway API, Admin API, SDK integration, function calling |
| **[Architecture](docs/architecture/)** | System design, provider system, real-time features |
| **[Development](docs/development/)** | Contributing, API patterns, LLM client factory |
| **[Operations](docs/operations/)** | Monitoring, scaling, runbooks, security |
| **[Model Pricing](docs/model-pricing/)** | Cost information across providers |

### Redis Circuit Breaker (Resilience)
ConduitLLM includes automatic circuit breaker protection for Redis operations:
- **Automatic failure detection**: Opens circuit after 5 consecutive failures or 50% failure rate
- **Service protection**: Returns 503 Service Unavailable when Redis is down (30-second recovery period)
- **Health monitoring**: Circuit state exposed via `/health` endpoint under `redis_circuit_breaker`
- **Manual control**: Emergency trip/reset available via environment variable `REDIS_CIRCUIT_BREAKER_ENABLE_MANUAL_CONTROL=true`

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the terms specified in the `LICENSE` file.
