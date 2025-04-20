# Configuration Guide

This document provides detailed information on configuring ConduitLLM for your environment and requirements.

## Configuration Options

ConduitLLM can be configured through:

1. **Environment Variables**: System-wide configuration
2. **Configuration Files**: Application-specific settings
3. **WebUI**: Interactive configuration through the web interface
4. **API**: Programmatic configuration through REST endpoints

## Database Configuration

ConduitLLM uses a database to store configuration, provider credentials, and usage data.

### SQLite (Default)

The simplest configuration uses SQLite:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=configuration.db"
  }
}
```

### SQLite Database Path (Persistent Storage)

To store your SQLite database on a persistent volume (e.g., in Docker), set the `CONDUIT_SQLITE_PATH` environment variable:

```bash
export CONDUIT_SQLITE_PATH=/data/conduit.db
```

If set, this will override the default location and any value in `DB_CONNECTION_STRING` for SQLite. This is recommended for Docker and production deployments.

#### Docker Example
```yaml
environment:
  - DB_PROVIDER=sqlite
  - CONDUIT_SQLITE_PATH=/data/conduit.db
volumes:
  - ./my-data:/data
```

#### Best Practices
- Always mount a persistent volume for `/data` in Docker to avoid data loss.
- Ensure the directory is writable by the container user.
- Use `CONDUIT_SQLITE_PATH` for clarity and portability.

### SQL Server

For production environments, SQL Server is recommended:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=myserver;Database=ConduitLLM;User Id=myuser;Password=mypassword;TrustServerCertificate=True"
  }
}
```

## Troubleshooting Database Path Issues

- **Database file not found or not created**: Ensure the volume is mounted and the path in `CONDUIT_SQLITE_PATH` is correct.
- **Permission denied**: Make sure the directory and file are writable by the user running the app (check Docker UID/GID).
- **Database is read-only**: The file or directory may be mounted as read-only or lack write permissions.
- **App uses wrong database file**: Double-check environment variable spelling and container/service environment.
- **Switching between SQLite and PostgreSQL**: Ensure you set `DB_PROVIDER` and the correct connection string/variable for your provider.

For more diagnostics, use the Database Status page in the WebUI.

## Provider Configuration

### Adding a Provider

Providers can be configured through the WebUI or API. Each provider requires:

- **Name**: Provider identifier (e.g., "OpenAI", "Anthropic")
- **API Key**: Authentication key for the provider
- **Endpoint**: Base URL for API requests (optional, uses default if not specified)

#### Supported Providers

| Provider | Default Endpoint | API Key URL |
|----------|-----------------|-------------|
| OpenAI | https://api.openai.com/v1 | https://platform.openai.com/api-keys |
| Anthropic | https://api.anthropic.com | https://console.anthropic.com/keys |
| Cohere | https://api.cohere.ai | https://dashboard.cohere.com/api-keys |
| Gemini | https://generativelanguage.googleapis.com | https://makersuite.google.com/app/apikey |
| Fireworks | https://api.fireworks.ai/inference | https://app.fireworks.ai/users/settings/api-keys |
| OpenRouter | https://openrouter.ai/api | https://openrouter.ai/keys |

### Model Mappings

Model mappings allow you to use generic model names that map to provider-specific models:

1. Create a generic model name (e.g., "gpt-4-equivalent")
2. Map it to provider-specific models:
   - OpenAI: "gpt-4"
   - Anthropic: "claude-2"
   - Cohere: "command-r-plus"

This allows clients to request a capability rather than a specific model.

## Router Configuration

The router distributes requests across model deployments based on a selected strategy.

### Routing Strategies

1. **Simple**: Uses the first available deployment for a requested model
2. **Random**: Randomly selects from available deployments
3. **Round-Robin**: Cycles through available deployments in sequence

### Model Deployments

Each deployment represents a model available for routing:

```json
{
  "deployments": [
    {
      "model": "gpt-4-equivalent",
      "provider": "openai-provider-id",
      "weight": 1.0,
      "isActive": true
    },
    {
      "model": "gpt-4-equivalent",
      "provider": "anthropic-provider-id",
      "weight": 0.5,
      "isActive": true
    }
  ]
}
```

### Fallback Configuration

Fallbacks define what models to try if the requested model fails:

```json
{
  "fallbacks": [
    {
      "primaryModel": "gpt-4-equivalent",
      "fallbackModels": ["gpt-3.5-equivalent", "command-equivalent"]
    }
  ]
}
```

## Virtual Key Management

Virtual keys provide a way to control access and track usage.

### Creating Virtual Keys

Each virtual key can have:

- **Name**: Descriptive name for the key
- **Budget**: Maximum amount that can be spent
- **Expiration**: Date when the key becomes invalid
- **Active Status**: Whether the key is currently usable

### Budget Management

Budgets help control spending:

1. **Budget Amount**: Maximum spending allowed
2. **Budget Period**: Daily or monthly reset period
3. **Notifications**: Alerts when approaching limits

### Master Key

The master key is used for administrative operations:

1. Initial setup via environment variable:
   ```
   CONDUITLLM_MASTER_KEY=your-secure-master-key
   ```

2. Used for authentication with the `X-Master-Key` header

## Global Settings

Configure system-wide settings:

### Cache Configuration

Control how responses are cached:

```json
{
  "CacheConfiguration": {
    "EnableCache": true,
    "CacheExpiration": 3600,
    "MaxCacheEntries": 1000
  }
}
```

See [Cache-Configuration.md](Cache-Configuration.md) for more details.

### Logging

Configure logging levels and destinations:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

## Environment Variables

Key environment variables for configuration:

| Variable | Description | Default |
|----------|-------------|---------|
| `CONDUITLLM_MASTER_KEY` | Master key for administrative access | None (required) |
| `CONDUITLLM_CONNECTION_STRING` | Database connection string | `Data Source=configuration.db` |
| `CONDUITLLM_CACHE_ENABLED` | Enable response caching | `true` |
| `CONDUITLLM_PORT` | Port for the HTTP server | `5000` |
| `CONDUITLLM_LOG_LEVEL` | Logging level | `Information` |
| `CONDUIT_SQLITE_PATH` | SQLite database path | None |

See [Environment-Variables.md](Environment-Variables.md) for a complete list.

## Configuration Files

### appsettings.json

Main configuration file for application settings:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=configuration.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "CacheConfiguration": {
    "EnableCache": true,
    "CacheExpiration": 3600,
    "MaxCacheEntries": 1000
  },
  "AllowedHosts": "*",
  "ServerPort": 5000
}
```

### appsettings.Development.json

Override settings for development:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

## WebUI Configuration

The WebUI provides an interactive way to configure all aspects of the system:

1. **Providers Page**: Manage LLM provider configurations
2. **Model Mappings**: Configure generic-to-specific model mappings
3. **Router Settings**: Configure routing strategy and fallbacks
4. **Virtual Keys**: Manage API access and budgets
5. **Global Settings**: Configure system-wide settings

## Configuration Best Practices

1. **Security**:
   - Use environment variables for sensitive information
   - Rotate the master key periodically
   - Limit access to the WebUI

2. **Performance**:
   - Enable caching for frequently used prompts
   - Configure appropriate router weights for load balancing
   - Use SQL Server for high-traffic production environments

3. **Cost Management**:
   - Set appropriate budgets for virtual keys
   - Configure fallbacks from expensive to cheaper models
   - Monitor usage through the WebUI dashboard

4. **Reliability**:
   - Configure multiple providers for the same capability
   - Set up fallbacks for critical models
   - Monitor provider health through the WebUI dashboard
