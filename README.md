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

## 🏗️ Architecture

ConduitLLM follows a modular architecture with distinct components handling specific responsibilities:

```mermaid
flowchart LR
    Client["WebUI / Client App"]
    Http["ConduitLLM.Http(API Gateway)"]
    Core["ConduitLLM.Core(Orchestration)"]
    Providers["ConduitLLM.Providers(Provider Logic)"]
    Config["ConduitLLM.Configuration(Settings)"]
    LLM["LLM Backends(OpenAI, Anthropic, etc.)"]
    
    Client --> Http
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

### Docker Images: WebUI and Http Separation

As of April 2025, ConduitLLM is split into two separate Docker images:

- **WebUI Image**: Contains the Blazor-based administrative dashboard (`ConduitLLM.WebUI`).
- **Http Image**: Contains the OpenAI-compatible REST API gateway (`ConduitLLM.Http`).

Each service is built, tagged, and published as an independent container:

- `ghcr.io/knnlabs/conduit-webui:latest` (WebUI)
- `ghcr.io/knnlabs/conduit-http:latest` (API Gateway)

#### Why this change?
- **Separation of concerns**: Web UI and API gateway can be scaled, deployed, and maintained independently.
- **Improved security**: You can isolate the WebUI from the API gateway if desired.
- **Simpler deployments**: Compose, Kubernetes, and cloud-native deployments are easier to manage.

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
      # ... WebUI environment variables

  http:
    image: ghcr.io/knnlabs/conduit-http:latest
    ports:
      - "5000:8080"
    environment:
      # ... API Gateway environment variables
```

Or run separately:

```bash
docker run -d --name conduit-webui -p 5001:8080 ghcr.io/knnlabs/conduit-webui:latest
docker run -d --name conduit-http -p 5000:8080 ghcr.io/knnlabs/conduit-http:latest
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
   ./start.sh
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

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the terms specified in the `LICENSE` file.
