# ConduitLLM.Http

## Overview

`ConduitLLM.Http` is the HTTP API backend for the Conduit solution (`Conduit.sln`). It exposes a unified, OpenAI-compatible REST API for interacting with multiple Large Language Model (LLM) providers such as OpenAI, Anthropic, Azure OpenAI, Gemini, Cohere, and others. It acts as the main programmatic interface for client applications and the ConduitLLM WebUI frontend.

## Role in the Conduit Solution

- **Conduit.sln**: The overall solution file tying together all Conduit sub-projects.
- **ConduitLLM.Http**: This project. Provides the HTTP API for LLM access, model routing, API key/virtual key management, and provider abstraction.
- **ConduitLLM.WebUI**: The web-based frontend for interactive LLM usage, configuration, and administration. Communicates with this API.
- **ConduitLLM.Core**: Shared logic, models, and interfaces for LLM operations, used by both Http and WebUI.
- **ConduitLLM.Configuration**: Handles configuration persistence (database, environment, files) and settings management.
- **ConduitLLM.Providers**: Implements provider-specific logic for different LLM backends.
- **ConduitLLM.Tests**: Unit and integration tests for all components.

## Key Features

- **OpenAI-compatible REST API** (`/v1/chat/completions`)
- **Multiple LLM Provider Support** (OpenAI, Anthropic, Azure, Gemini, Cohere, etc.)
- **Model Routing**: Map model names to provider/model pairs
- **API Key & Virtual Key Management**: Supports both provider keys and Conduit-managed virtual keys
- **Streaming and non-streaming responses**
- **Swagger/OpenAPI documentation** (enabled in development)
- **Database-backed and file-based configuration**

## Configuration

### Environment Variables

- `HttpApiHttpPort` (default: 5000): HTTP port for the API
- `HttpApiHttpsPort` (default: 5003): HTTPS port for the API
- `ASPNETCORE_URLS`: Used internally by `start.sh` to set the actual listen URLs
- Provider API keys can be set via environment variables or `appsettings.json` (see below)

### appsettings.json

- `ConnectionStrings:DefaultConnection`: Path to the configuration database (default: `../configuration.db`)
- `Conduit:ModelProviderMapping`: Maps logical model names to provider/model pairs
- `Conduit:ProviderCredentials`: Stores API keys and endpoints for each provider

Example snippet:
```json
"Conduit": {
  "ModelProviderMapping": {
    "gpt-4-proxy-example": "openai/gpt-4",
    "claude-3-opus-proxy-example": "anthropic/claude-3-opus-20240229"
  },
  "ProviderCredentials": {
    "OpenAI": {
      "ApiKey": "YOUR_OPENAI_API_KEY_OR_USE_ENV_VAR"
    },
    "Anthropic": {
      "ApiKey": "YOUR_ANTHROPIC_API_KEY_OR_USE_ENV_VAR"
    }
    // ... other providers ...
  }
}
```

### Docker & Port Management

- Designed for containerization. Ports are set via environment variables for flexible deployment.
- The `start.sh` script configures ports and launches the API with correct settings.

## Persistent Database Storage (Docker)

To ensure your SQLite database persists across container restarts, set the `CONDUIT_SQLITE_PATH` environment variable and mount a persistent volume:

```yaml
environment:
  - DB_PROVIDER=sqlite
  - CONDUIT_SQLITE_PATH=/data/conduit.db
volumes:
  - ./my-data:/data
```

This will store the database at `/data/conduit.db` inside the container, mapped to your host directory `./my-data`.

### Best Practices
- Always use a persistent volume for `/data` in production or testing containers.
- Make sure the volume is writable by the app user (check Docker UID/GID).
- Use `CONDUIT_SQLITE_PATH` for clarity and to avoid accidental data loss.

### Troubleshooting
- **File not created or missing**: Check that the volume is mounted and the path is correct.
- **Permission denied**: Ensure the directory and file are writable by the container user.
- **Database is read-only**: The file or directory may be mounted as read-only or lack write permissions.
- **App uses wrong database file**: Double-check environment variable spelling and container/service environment.

For more help, see the Database Status page in the WebUI.

## Running the API

1. **With .NET CLI:**
   ```bash
   export HttpApiHttpPort=5000
   export HttpApiHttpsPort=5003
   dotnet run --project ConduitLLM.Http
   ```
2. **With Docker:**
   ```bash
   docker build -t conduitllm-http .
   docker run -e HttpApiHttpPort=5000 -e HttpApiHttpsPort=5003 -p 5000:5000 -p 5003:5003 conduitllm-http
   ```
3. **With start.sh:**
   ```bash
   ./start.sh
   ```

## API Usage

- Main endpoint: `POST /v1/chat/completions` (OpenAI-compatible)
- Supports both standard and streaming responses
- Requires API key (provider or virtual key) in the `Authorization: Bearer ...` header
- See Swagger UI at `/swagger` (in development mode)

## Virtual Keys

- Virtual keys (`condt_...`) are managed by Conduit and can be used instead of provider keys.
- Spend tracking and access control are supported for virtual keys.

## Refreshing Configuration

- POST `/admin/refresh-configuration` endpoint reloads provider credentials and model mappings from the database.

## Development Notes

- Uses ASP.NET Core Minimal APIs
- Configuration is loaded from both `appsettings.json` and the configuration database
- Provider credentials and model mappings can be managed via the WebUI or directly in the database

## Troubleshooting

- Use `stop.sh` to terminate all ConduitLLM processes (covers all ports and process names)
- Logs are output to the console by default (configurable in `appsettings.json`)

## License

See the root of the repository for license information.
