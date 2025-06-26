# Getting Started with ConduitLLM

## Overview

ConduitLLM is a comprehensive LLM management and routing system that allows you to interact with multiple LLM providers through a unified interface. It provides advanced routing capabilities, virtual key management, and a web-based configuration UI.

## Prerequisites

- .NET 9.0 SDK or later (for local development)
- PostgreSQL for database storage
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

3. Run the container with PostgreSQL:
   ```bash
   docker run -d \
     -p 5000:5000 \
     -e DATABASE_URL=postgresql://youruser:yourpassword@yourhost:5432/conduitllm \
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
3. Run the Admin API, HTTP API, and WebUI projects:
   ```bash
   # Run Admin API
   cd ConduitLLM.Admin
   dotnet run &
   
   # Run API Gateway
   cd ../ConduitLLM.Http
   dotnet run &
   
   # Run WebUI
   cd ../ConduitLLM.WebUI
   dotnet run
   ```

Note: For WebUI to communicate with the Admin API, set the environment variables:
```bash
export CONDUIT_ADMIN_API_BASE_URL=http://localhost:5002
export CONDUIT_USE_ADMIN_API=true
export CONDUIT_MASTER_KEY=your_secure_master_key
```

## Architecture Overview: Admin API

Starting in May 2025, ConduitLLM uses a three-tier architecture:

1. **Admin API (`ConduitLLM.Admin`)**: Central configuration and management service
   - Handles all database operations
   - Manages virtual keys, model configurations, and provider settings
   - Provides REST API for administrative operations
   - Runs on port 5002 by default

2. **HTTP API Gateway (`ConduitLLM.Http`)**: OpenAI-compatible API service
   - Handles LLM requests from clients
   - Routes requests to appropriate providers
   - Validates virtual keys via Admin API
   - Runs on port 5000 by default

3. **WebUI (`ConduitLLM.WebUI`)**: Blazor-based administration dashboard
   - Provides visual interface for configuration
   - Communicates with Admin API for all operations
   - Runs on port 5001 by default

### Admin API Mode

The WebUI can operate in two modes:
- **Legacy Mode** (deprecated): Direct database access
- **Admin API Mode** (recommended): All operations go through Admin API

To enable Admin API mode, set these environment variables:
```bash
CONDUIT_USE_ADMIN_API=true
CONDUIT_ADMIN_API_BASE_URL=http://localhost:5002
CONDUIT_MASTER_KEY=your_secure_master_key
```

## Docker Images: Component Separation

As of May 2025, ConduitLLM is distributed as three separate Docker images:

- **WebUI Image**: The Blazor-based admin dashboard (`ConduitLLM.WebUI`)
- **Admin API Image**: The administrative API service (`ConduitLLM.Admin`) 
- **HTTP Image**: The OpenAI-compatible REST API gateway (`ConduitLLM.Http`)

Each image is published independently:

- `ghcr.io/knnlabs/conduit-webui:latest` (WebUI)
- `ghcr.io/knnlabs/conduit-admin:latest` (Admin API)
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
      CONDUIT_ADMIN_API_BASE_URL: http://admin:8080
      CONDUIT_MASTER_KEY: your_secure_master_key
      CONDUIT_USE_ADMIN_API: "true"
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

Then start all services:

```bash
docker compose up -d
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
