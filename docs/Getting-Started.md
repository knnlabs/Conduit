# Getting Started with ConduitLLM

## Overview

ConduitLLM is a comprehensive LLM management and routing system that allows you to interact with multiple LLM providers through a unified interface. It provides advanced routing capabilities, virtual key management, and a web-based configuration UI.

## Prerequisites

- .NET 9.0 SDK or later (for local development)
- SQLite or PostgreSQL for database storage
- Docker (recommended for deployment)
- API keys for any LLM providers you plan to use (OpenAI, Anthropic, Cohere, Gemini, Fireworks, OpenRouter)

## Installation

### Option 1: Docker (Recommended)

1. Pull the latest public Docker image:
   ```bash
   docker pull ghcr.io/knnlabs/conduit:latest
   ```

2. Create a directory for persistent data:
   ```bash
   mkdir -p ./data
   ```

3. Run the container (SQLite example):
   ```bash
   docker run -d \
     -p 5000:5000 \
     -v $(pwd)/data:/data \
     -e DB_PROVIDER=sqlite \
     -e CONDUIT_SQLITE_PATH=/data/conduit.db \
     -e CONDUIT_MASTER_KEY=your_secure_master_key \
     ghcr.io/knnlabs/conduit:latest
   ```

   For PostgreSQL, use:
   ```bash
   docker run -d \
     -p 5000:5000 \
     -e DB_PROVIDER=postgres \
     -e CONDUIT_POSTGRES_CONNECTION_STRING=Host=yourhost;Port=5432;Database=conduitllm;Username=youruser;Password=yourpassword \
     -e CONDUIT_MASTER_KEY=your_secure_master_key \
     ghcr.io/knnlabs/conduit:latest
   ```

### Option 2: Using Docker Compose (Local Development)

1. Clone the repository:
   ```bash
   git clone https://github.com/your-org/ConduitLLM.git
   cd ConduitLLM
   ```
2. Start the services with Docker Compose:
   ```bash
   docker compose up -d
   ```

### Option 3: Manual setup (Advanced)

1. Clone the repository:
   ```bash
   git clone https://github.com/your-org/ConduitLLM.git
   cd ConduitLLM
   ```
2. Build the solution:
   ```bash
   dotnet build
   ```
3. Run the WebUI project:
   ```bash
   cd ConduitLLM.WebUI
   dotnet run
   ```

## Docker Images: WebUI and Http Separation

As of April 2025, ConduitLLM is distributed as two separate Docker images:

- **WebUI Image**: The Blazor-based admin dashboard (`ConduitLLM.WebUI`)
- **Http Image**: The OpenAI-compatible REST API gateway (`ConduitLLM.Http`)

Each image is published independently:

- `ghcr.io/knnlabs/conduit-webui:latest` (WebUI)
- `ghcr.io/knnlabs/conduit-http:latest` (API Gateway)

### Running with Docker Compose

Create a `docker-compose.yml`:

```yaml
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

Then start both services:

```bash
docker compose up -d
```

Or run separately:

```bash
docker run -d --name conduit-webui -p 5001:8080 ghcr.io/knnlabs/conduit-webui:latest
docker run -d --name conduit-http -p 5000:8080 ghcr.io/knnlabs/conduit-http:latest
```

> **Note:** Update all deployment scripts and CI/CD workflows to use the new image tags. See `.github/workflows/docker-release.yml` for reference.

## Initial Configuration

1. Open your browser and navigate to the WebUI.
   - **Local Development (Docker Compose):** `http://localhost:5001`
   - **Docker/Deployed:** Access via the URL configured in the `CONDUIT_API_BASE_URL` environment variable (e.g., `https://conduit.yourdomain.com`), typically through an HTTPS reverse proxy, or `http://localhost:5000` if running locally.

2. Navigate to the Configuration page to set up:
   - LLM providers (API keys and endpoints)
   - Model mappings
   - Global settings including the master key

3. Set up your first provider:
   - Select a provider (e.g., OpenAI)
   - Enter your API key (obtain from the provider's website)
   - Configure any additional provider-specific settings

## Configuring Router

The router allows you to distribute requests across different model deployments:

1. Configure model deployments through the WebUI
2. Select a routing strategy (simple, random, round-robin)
3. Set up fallback configurations between models

## Next Steps

- Explore the [Architecture Overview](Architecture-Overview.md) to understand the system components
- Check the [Configuration Guide](Configuration-Guide.md) for detailed configuration options
- See the [API Reference](API-Reference.md) for available endpoints
- Learn about [Virtual Keys](Virtual-Keys.md) for managing access and budgets
