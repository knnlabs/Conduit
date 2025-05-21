![ConduitLLM Logo](docs/assets/conduit.png)
[![CodeQL](https://github.com/knnlabs/Conduit/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/knnlabs/Conduit/actions/workflows/codeql-analysis.yml)
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

## Overview

ConduitLLM is a unified, modular, and extensible platform designed to simplify interaction with multiple Large Language Models (LLMs). It provides a single, consistent OpenAI-compatible REST API endpoint, acting as a gateway or "conduit" to various LLM backends such as OpenAI, Anthropic, Azure OpenAI, Google Gemini, Cohere, and others.

Built with .NET and designed for containerization (Docker), ConduitLLM streamlines the development, deployment, and management of LLM-powered applications by abstracting provider-specific complexities.

## Key Features

- **OpenAI-Compatible REST API**: Exposes a standard `/v1/chat/completions` endpoint for seamless integration with existing tools and SDKs
- **Multi-Provider Support**: Interact with various LLM providers through a single interface
- **Model Routing & Mapping**: Define custom model aliases (e.g., `my-gpt4`) and map them to specific provider models (e.g., `openai/gpt-4`)
- **Virtual API Key Management**: Create and manage Conduit-specific API keys (`condt_...`) with built-in spend tracking
- **Streaming Support**: Real-time token streaming for responsive applications
- **Web-Based User Interface**: Administrative dashboard for configuration and monitoring
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
- **ConduitLLM.WebUI**: Blazor-based admin interface for configuration and monitoring
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
  CONDUIT_MASTER_KEY: your_master_key       # Master key for authentication
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

- **WebUI Image**: The Blazor-based admin dashboard (`ConduitLLM.WebUI`)
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
      CONDUIT_MASTER_KEY: your_secure_master_key
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
      CONDUIT_MASTER_KEY: your_secure_master_key
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

## Database Configuration (Postgres & SQLite)

Conduit now supports robust, container-friendly database configuration via environment variables ONLY (no appsettings.json required).

- **Postgres:**
  - Set `DATABASE_URL` in the format:
    - `postgresql://user:password@host:port/database`
  - Example:
    - `DATABASE_URL=postgresql://postgres:yourpassword@yourhost:5432/yourdb`
- **SQLite:**
  - Set `CONDUIT_SQLITE_PATH` to the file path (default: `ConduitConfig.db`)
  - Example:
    - `CONDUIT_SQLITE_PATH=/data/ConduitConfig.db`

No other database-related environment variables are needed. The application will auto-detect which provider to use.

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

## Documentation

See the `docs/` directory for detailed documentation:

- [API Reference](docs/API-Reference.md)
- [Architecture Overview](docs/Architecture-Overview.md)
  - [Admin API Adapters](docs/Architecture/Admin-API-Adapters.md)
  - [DTO Standardization](docs/Architecture/DTO-Standardization.md)
  - [Repository Pattern](docs/Architecture/Repository-Pattern.md)
- [Budget Management](docs/Budget-Management.md)
- [Cache Configuration](docs/Cache-Configuration.md)
- [Configuration Guide](docs/Configuration-Guide.md)
- [Dashboard Features](docs/Dashboard-Features.md)
- [Environment Variables](docs/Environment-Variables.md)
- [Getting Started](docs/Getting-Started.md)
- [LLM Routing](docs/LLM-Routing.md)
- [Multimodal Vision Support](docs/Multimodal-Vision-Support.md)
- [Provider Integration](docs/Provider-Integration.md)
- [Virtual Keys](docs/Virtual-Keys.md)
- [WebUI Guide](docs/WebUI-Guide.md)
- [Admin API Migration Guide](docs/admin-api-migration-guide.md)
- [Admin API Implementation Status](docs/ADMIN-API-MIGRATION-STATUS.md)
- [Legacy Mode Deprecation Timeline](docs/LEGACY-MODE-DEPRECATION-TIMELINE.md)

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the terms specified in the `LICENSE` file.
