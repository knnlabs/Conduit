---
sidebar_position: 3
title: Configuration Guide
description: Detailed information about configuring Conduit
---

# Configuration Guide

This guide covers the essential configuration options for Conduit to help you set up the system according to your needs.

## Core Configuration

### System Settings

Conduit's core settings can be configured through environment variables or the Web UI. The most important settings include:

| Setting | Description | Default | Environment Variable |
|---------|-------------|---------|---------------------|
| Master Key | Authentication key for administrative access | (Required) | `CONDUIT_MASTER_KEY` |
| Host | Interface to bind the HTTP server | `localhost` | `CONDUIT_HOST` |
| Port | Port to run the HTTP server | `5000` | `CONDUIT_PORT` |
| Database Path | Location of the SQLite database | `./data/conduit.db` | `CONDUIT_DATABASE_PATH` |
| Log Level | Verbosity of logging | `Information` | `CONDUIT_LOG_LEVEL` |

For a complete list of available environment variables, see the [Environment Variables](../guides/environment-variables) guide.

## Provider Configuration

### Adding Provider Credentials

Provider credentials can be added through the Web UI under **Configuration > Provider Credentials**:

1. Click **Add Provider Credential**
2. Select the provider type
3. Enter the required credentials:
   - API Key (all providers)
   - Base URL (some providers)
   - Organization ID (some providers)
4. Click **Save**

### Model Mappings

Model mappings define which provider models are available and how they're exposed through Conduit. To configure model mappings:

1. Navigate to **Configuration > Model Mappings**
2. Click **Add Model Mapping**
3. Configure the following:
   - **Virtual Model Name**: The name clients will use (e.g., `my-gpt4`)
   - **Provider Model**: The actual provider model (e.g., `gpt-4`)
   - **Provider**: The provider to use
   - **Priority**: Used for routing when multiple providers can serve the same model
4. Click **Save**

## Routing Configuration

Conduit offers flexible routing options to direct requests to appropriate providers.

### Routing Strategies

Navigate to **Configuration > Routing** to set up your routing strategy:

| Strategy | Description |
|----------|-------------|
| Simple | Uses the first available mapping for a requested model |
| Priority | Uses the mapping with the highest priority |
| Least Cost | Routes to the provider with the lowest cost |
| Round Robin | Distributes requests evenly across providers |
| Random | Randomly selects among available providers |
| Least Used | Favors providers with fewer recent requests |
| Least Latency | Routes to the provider with the lowest recent latency |

### Fallback Configuration

Configure fallback options for when a primary provider is unavailable:

1. Navigate to **Configuration > Routing > Fallbacks**
2. Add fallback rules specifying:
   - Primary model/provider
   - Fallback model/provider
   - Conditions for fallback (timeout, error codes, etc.)

## Virtual Keys

Virtual keys control access to Conduit. To configure them:

1. Navigate to **Virtual Keys**
2. Click **Create New Key**
3. Configure:
   - **Name**: Descriptive name
   - **Description**: Purpose of the key
   - **Permissions**: What actions are allowed
   - **Rate Limits**: Requests per minute/hour/day
   - **Model Access**: Which models this key can access
   - **Budget**: Optional spending limits

See the [Virtual Keys](../features/virtual-keys) guide for more detailed information.

## Caching

Caching can significantly improve performance and reduce costs. To configure caching:

1. Navigate to **Configuration > Caching**
2. Enable or disable the cache
3. Configure cache options:
   - **Provider**: Redis or In-Memory
   - **TTL**: Time-to-live for cached responses
   - **Max Size**: For in-memory cache
   - **Connection String**: For Redis cache

For detailed caching configuration, see the [Cache Configuration](../guides/cache-configuration) guide.

## Advanced Configuration

### Custom Headers

Configure custom headers to be included in responses:

1. Navigate to **Configuration > Advanced**
2. Add headers in the **Custom Headers** section

### Webhook Notifications

Set up webhooks to notify external systems about events:

1. Navigate to **Configuration > Notifications**
2. Add webhook endpoints for different event types

## Configuration Files

While the Web UI is the recommended way to configure Conduit, you can also modify configuration files directly:

- **Database**: SQLite database at the configured location
- **Model Mappings**: `appsettings.json` or database

## Next Steps

- Learn about [Environment Variables](../guides/environment-variables) for deployment
- Understand [Model Routing](../features/model-routing) in depth
- Explore [Budget Management](../guides/budget-management) for cost control