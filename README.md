![ConduitLLM Logo](docs/assets/conduit.png)
[![CodeQL](https://github.com/knnlabs/Conduit/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/knnlabs/Conduit/actions/workflows/codeql-analysis.yml)
[![Build & Test](https://github.com/knnlabs/Conduit/actions/workflows/build-and-release.yml/badge.svg)](https://github.com/knnlabs/Conduit/actions/workflows/build-and-release.yml)
[![OpenAI Compatible](https://img.shields.io/badge/OpenAI-Compatible-brightgreen.svg)](https://platform.openai.com/docs/api-reference)
[![Built with .NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![Docker Ready](https://img.shields.io/badge/Docker-Ready-2496ED)](https://www.docker.com/)

> A unified API gateway for multiple LLM providers with OpenAI-compatible endpoints

## Why ConduitLLM?

Are you juggling multiple LLM provider APIs in your applications? ConduitLLM solves this problem by providing:

- **Single Integration Point**: Write your code once, switch LLM providers anytime
- **Vendor Independence**: Avoid lock-in to any single LLM provider
- **Simplified API Management**: Centralized key management and usage tracking
- **Cost Optimization**: Route requests to the most cost-effective or performant models
- **🚀 Enterprise Scale**: Built to handle **10,000+ concurrent sessions** with a lean, efficient tech stack ([see scaling architecture](docs/Scaling-Architecture.md))

## Overview

ConduitLLM is a unified, modular, and extensible platform designed to simplify interaction with multiple Large Language Models (LLMs). It provides a single, consistent OpenAI-compatible REST API endpoint, acting as a gateway or "conduit" to various LLM backends such as OpenAI, Anthropic, Azure OpenAI, Google Gemini, Cohere, and others.

Built with .NET and designed for containerization (Docker), ConduitLLM streamlines the development, deployment, and management of LLM-powered applications by abstracting provider-specific complexities.

## 🚧 Project Status

> **Note**: ConduitLLM is actively under development. We recommend using the provided Client SDKs for the most stable integration experience.

### ✅ **Production Ready Features**
- **Text Generation**: Fully tested with OpenAI, Anthropic, and MiniMax providers
- **Image Generation**: Complete implementation with manual testing across multiple providers  
- **Video Generation**: Feature complete with provider integration
- **Client SDKs**: Stable APIs for Node.js and other platforms

### ⚠️ **In Development**
- **Audio Support**: Not feature complete - expect significant changes in upcoming releases
- **Core & Admin APIs**: May evolve without backward compatibility - use Client SDKs instead

### 💡 **Recommended Integration**
```bash
# Use Client SDKs for stable integration
npm install @knn_labs/conduit-core-client
npm install @knn_labs/conduit-admin-client
```

## Key Features

- **OpenAI-Compatible REST API**: Exposes a standard `/v1/chat/completions` endpoint for seamless integration with existing tools and SDKs
- **Multi-Provider Support**: Interact with various LLM providers through a single interface
- **Model Routing & Mapping**: Define custom model aliases (e.g., `my-gpt4`) and map them to specific provider models (e.g., `openai/gpt-4`)
- **Virtual API Key Management**: Create and manage Conduit-specific API keys (`condt_...`) with built-in spend tracking
- **Streaming Support**: Real-time token streaming for responsive applications
- **Audio API Support**: Audio capabilities including transcription (STT), text-to-speech (TTS), and real-time audio streaming ⚠️ *In Development*
- **Web-Based User Interface**: Administrative dashboard for configuration and monitoring
- **Enterprise Security Features**: IP filtering, rate limiting, failed login protection, and security headers
- **Security Dashboard**: Real-time monitoring of security events and access attempts
- **Centralized Configuration**: Flexible configuration via database, environment variables, or JSON files
- **Extensible Architecture**: Easily add support for new LLM providers

![ConduitLLM Dashboard](docs/assets/dashboard-home.png)

## 📦 Versioning

Conduit uses Semantic Versioning (MAJOR.MINOR.PATCH):

- **Docker Tags**: Images are tagged with semantic versions (e.g., `v1.0.0`), branch names, and the `latest` tag.
- **Version Checking**: The WebUI displays the current version and can check for updates automatically.
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
    WebUI["ConduitLLM.WebUI(Admin Dashboard)"]
    AdminAPI["ConduitLLM.Admin(Admin API)"]
    Http["ConduitLLM.Http(API Gateway)"]
    Core["ConduitLLM.Core(Orchestration)"]
    Providers["ConduitLLM.Providers(Provider Logic)"]
    Config["ConduitLLM.Configuration(Entities & DTOs)"]
    LLM["LLM Backends(OpenAI, Anthropic, etc.)"]
    
    WebUI -->|Admin API Client| AdminAPI
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

- **ConduitLLM.Http**: OpenAI-compatible REST API gateway handling authentication and request forwarding
- **ConduitLLM.WebUI**: Next.js-based admin interface for configuration and monitoring
- **ConduitLLM.Core**: Central orchestration logic, interfaces, and routing strategies
- **ConduitLLM.Providers**: Provider-specific implementations for different LLM services
- **ConduitLLM.Configuration**: Configuration management across various sources
- **ConduitLLM.Admin**: Administrative API for configuration management

### Admin API Client

The Admin API client provides a way for the WebUI to interact with the Admin API service without direct project references. This breaks the circular dependency between the projects and improves the architecture.

To configure the Admin API client in your deployment:

```yaml
# Docker Compose environment variables
environment:
  CONDUIT_ADMIN_API_URL: http://admin:8080  # URL to the Admin API
  CONDUIT_API_TO_API_BACKEND_AUTH_KEY: your_backend_key  # Backend service authentication
  CONDUIT_USE_ADMIN_API: "true"             # Enable Admin API client (vs direct DB access)
  CONDUIT_DISABLE_DIRECT_DB_ACCESS: "true"  # Completely disable legacy mode
```

> **Important**: Direct database access mode (`CONDUIT_USE_ADMIN_API=false`) is deprecated and will be removed after October 2025. See [Migration Guide](docs/admin-api-migration-guide.md) for details.

The WebUI includes a built-in health check indicator that monitors the connection to the Admin API:

- A green checkmark indicates the Admin API is healthy
- A red warning icon indicates connection issues
- Click the icon to view detailed status and troubleshooting options

Key features:
- **Decoupled Architecture**: WebUI and Admin projects are fully decoupled
- **Flexible Deployment**: Services can be deployed separately in distributed environments
- **Clean API Contracts**: API contracts explicitly defined through interfaces and DTOs
- **Configuration Control**: Toggle between direct DB access and API access with a simple flag

### Docker Images: Component Separation

As of May 2025, ConduitLLM is distributed as three separate Docker images:

- **WebUI Image**: The Next.js-based admin dashboard (`ConduitLLM.WebUI`)
- **Admin API Image**: The administrative API service (`ConduitLLM.Admin`) 
- **Http Image**: The OpenAI-compatible REST API gateway (`ConduitLLM.Http`)

Each service is built, tagged, and published as an independent container:

- `ghcr.io/knnlabs/conduit-webui:latest` (WebUI)
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
docker-compose.yml

services:
  webui:
    image: ghcr.io/knnlabs/conduit-webui:latest
    ports:
      - "5001:8080"
    environment:
      CONDUIT_ADMIN_API_BASE_URL: http://admin:8080
      CONDUIT_API_TO_API_BACKEND_AUTH_KEY: your_secure_backend_key
      CONDUIT_USE_ADMIN_API: "true"
      CONDUIT_DISABLE_DIRECT_DB_ACCESS: "true"  # Completely disable legacy mode
    depends_on:
      - admin

  admin:
    image: ghcr.io/knnlabs/conduit-admin:latest
    ports:
      - "5002:8080"
    environment:
      DATABASE_URL: postgresql://conduit:conduitpass@postgres:5432/conduitdb
      CONDUIT_API_TO_API_BACKEND_AUTH_KEY: your_secure_backend_key
    depends_on:
      - postgres

  http:
    image: ghcr.io/knnlabs/conduit-http:latest
    ports:
      - "5000:8080"
    environment:
      DATABASE_URL: postgresql://conduit:conduitpass@postgres:5432/conduitdb
    depends_on:
      - postgres

  postgres:
    image: postgres:16
    environment:
      POSTGRES_USER: conduit
      POSTGRES_PASSWORD: conduitpass
      POSTGRES_DB: conduitdb
    volumes:
      - pgdata:/var/lib/postgresql/data

volumes:
  pgdata:
```

> **Note:** All CI/CD workflows and deployment scripts should be updated to reference the new image tags. See `.github/workflows/docker-release.yml` for examples.

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

- .NET 9.0 SDK
- (Optional) Docker Desktop for containerized deployment

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/knnlabs/Conduit.git
   cd Conduit/ConduitLLM.WebUI
   ```

2. **Configure LLM Providers**
   - Add your provider API keys via:
     - Environment variables (see `docs/Environment-Variables.md`)
     - Edit `appsettings.json`
     - Use the WebUI after startup

3. **Start the Services**
   ```bash
   docker compose up -d
   ```

4. **Access ConduitLLM**
   - **Local API**: `http://localhost:5000`
   - **Local WebUI**: `http://localhost:5001`
   - **Local API Docs**: `http://localhost:5000/swagger` (Development Mode)
   
   *Note: When running locally via `start.sh`, these are the default ports. When deployed using Docker or other methods, access is typically via an HTTPS reverse proxy. Configure the `CONDUIT_API_BASE_URL` environment variable to the public-facing URL (e.g., `https://conduit.yourdomain.com`) for correct link generation.*

### Docker Installation

```bash
docker pull ghcr.io/knnlabs/conduit:latest
```

Or use with Docker Compose:

```bash
docker compose up -d
```

*Note: The default Docker configuration assumes ConduitLLM runs behind a reverse proxy that handles HTTPS termination. The container exposes HTTP ports only.*

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
# Core API Authentication (uses Virtual Keys)
# Virtual keys are created via Admin API and used for LLM access
# Format: condt_your-virtual-key-here

# Admin API Authentication
CONDUIT_API_TO_API_BACKEND_AUTH_KEY=your-secure-backend-key  # For backend service authentication

# WebUI Authentication (separate from Admin API)
CONDUIT_ADMIN_LOGIN_PASSWORD=your-admin-password  # For human admin login to WebUI

# Legacy format (still supported)
AdminApi__MasterKey=your-secure-master-key
```

**CRITICAL SECURITY:** The `CONDUIT_API_TO_API_BACKEND_AUTH_KEY` and `CONDUIT_ADMIN_LOGIN_PASSWORD` serve different purposes:
- **CONDUIT_API_TO_API_BACKEND_AUTH_KEY**: Used for backend service-to-service authentication
- **CONDUIT_ADMIN_LOGIN_PASSWORD**: Used by human administrators to log into the WebUI dashboard
- **Virtual Keys**: Used by Core API for client LLM access (created via Admin API)

#### Next.js WebUI Configuration
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

#### Security Configuration (WebUI)
```bash
# WebUI Authentication
CONDUIT_ADMIN_LOGIN_PASSWORD=your-admin-password  # Separate password for human admin login

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

#### WebUI Configuration Notes

**Security Architecture:**
- All API calls are made server-side through Next.js API routes
- No API keys or sensitive URLs are exposed to the browser
- WebUI authenticates administrators separately from API consumers
- SignalR connections use server-side authentication

**Required Configuration:**
1. **Server-side API URLs** - Configure `CONDUIT_ADMIN_API_BASE_URL` and `CONDUIT_API_BASE_URL` for internal communication
2. **Separate WebUI authentication** - Set `CONDUIT_ADMIN_LOGIN_PASSWORD` distinct from `CONDUIT_API_TO_API_BACKEND_AUTH_KEY`
3. **Session security** - Use a strong `SESSION_SECRET` for production deployments

For a complete migration guide from old to new environment variables, see [Environment Variable Migration Guide](docs/MIGRATION_ENV_VARS.md).

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

```python
# Python example
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

### Using the Terminal UI (TUI)

Conduit includes a Terminal User Interface (TUI) for debugging and testing without a web browser. The TUI provides all the functionality of the WebUI through a text-based interface.

#### Quick Start

```bash
# Linux/macOS
./conduit-tui --master-key YOUR_MASTER_KEY

# Windows (PowerShell)
.\conduit-tui.ps1 --master-key YOUR_MASTER_KEY

# Windows (Command Prompt)
conduit-tui.bat --master-key YOUR_MASTER_KEY
```

#### TUI Features

- **Full WebUI Functionality**: Chat, provider management, model mappings, virtual keys, image/video generation
- **Real-time Updates**: SignalR integration for live status updates
- **Cross-platform**: Works on Linux, macOS, and Windows
- **Keyboard Navigation**: Fast access with F1-F9 shortcuts

#### Command Line Options

```bash
conduit-tui --master-key <KEY> [OPTIONS]

Options:
  -k, --master-key <KEY>     Master API key for authentication (required)
  -c, --core-api <URL>       Core API URL (default: http://localhost:5000)
  -a, --admin-api <URL>      Admin API URL (default: http://localhost:5002)
  -h, --help                 Show help information
  --build                    Force rebuild before running
```

#### Examples

```bash
# Use with default API URLs
./conduit-tui --master-key YOUR_MASTER_KEY

# Specify custom API URLs
./conduit-tui -k YOUR_MASTER_KEY -c https://api.example.com:5000 -a https://api.example.com:5002

# Force rebuild and run
./conduit-tui --build --master-key YOUR_MASTER_KEY
```

See [TUI Documentation](ConduitLLM.TUI/README.md) for detailed usage instructions.

## Documentation

See the `docs/` directory for detailed documentation:

### Core Documentation
- [API Reference](docs/API-Reference.md)
- [Architecture Overview](docs/Architecture-Overview.md)
  - [Admin API Adapters](docs/Architecture/Admin-API-Adapters.md)
  - [DTO Standardization](docs/Architecture/DTO-Standardization.md)
  - [Repository Pattern](docs/Architecture/Repository-Pattern.md)
- [🚀 Scaling Architecture](docs/Scaling-Architecture.md) - **10,000+ concurrent sessions architecture and roadmap**
- [Getting Started](docs/Getting-Started.md)
- [Current Status](docs/Current-Status.md)

### Development Guides
- [SDK Migration Guide](docs/development/sdk-migration-guide.md)
- [API Patterns & Best Practices](docs/development/API-PATTERNS-BEST-PRACTICES.md)
- [SDK Migration Complete](docs/development/SDK-MIGRATION-COMPLETE.md)
- [Next.js 15 Migration](docs/development/nextjs15-migration.md)
- [SDK Feature Gaps](docs/development/sdk-gaps.md)

### Deployment & Configuration
- [Deployment Configuration](docs/deployment/DEPLOYMENT-CONFIGURATION.md)
- [Docker Optimization](docs/deployment/docker-optimization.md)
- [Configuration Guide](docs/Configuration-Guide.md)
- [Environment Variables](docs/Environment-Variables.md)
- [Cache Configuration](docs/Cache-Configuration.md)

### API Reference
- [API Reference](docs/api-reference/API-REFERENCE.md)
- [Admin API Migration Guide](docs/admin-api-migration-guide.md)

### Examples & Integration
- [Integration Examples](docs/examples/INTEGRATION-EXAMPLES.md)
- [OpenAI Compatible Example](docs/examples/openai-compatible-example.md)

### Troubleshooting
- [Troubleshooting Guide](docs/troubleshooting/TROUBLESHOOTING-GUIDE.md)

### Feature Documentation
- [Audio API Guide](docs/Audio-API-Guide.md)
- [Audio Architecture](docs/Audio-Architecture.md)
- [Real-time Architecture](docs/Realtime-Architecture.md)
- [Audio Implementation Status](docs/Audio-Implementation-Status.md)
- [Budget Management](docs/Budget-Management.md)
- [Dashboard Features](docs/Dashboard-Features.md)
- [LLM Routing](docs/LLM-Routing.md)
- [Multimodal Vision Support](docs/Multimodal-Vision-Support.md)
- [Provider Integration](docs/Provider-Integration.md)
- [Virtual Keys](docs/Virtual-Keys.md)
- [WebUI Guide](docs/WebUI-Guide.md)

### Project Documentation
- [SDK Integration Epic](docs/epics/sdk-integration.md)
- [Archived Documentation](docs/archive/webui-migration/)

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the terms specified in the `LICENSE` file.
