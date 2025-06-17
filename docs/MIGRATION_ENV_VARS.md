# Environment Variable Migration Guide

This guide helps you migrate from the old environment variable structure to the new simplified configuration in Conduit.

## Overview of Changes

We've simplified the environment variable configuration to make first-time setup easier while maintaining backward compatibility. The main changes are:

1. **Redis Configuration**: Simplified from 4 variables to 1 (plus optional instance name)
2. **Master Key**: Standardized across all services
3. **Auto-enable Cache**: Cache automatically enables when Redis is configured

## Redis Configuration Changes

### Old Configuration
```bash
# Required 4 separate variables:
CONDUIT_REDIS_CONNECTION_STRING="localhost:6379"
CONDUIT_CACHE_ENABLED="true"
CONDUIT_CACHE_TYPE="Redis"
CONDUIT_REDIS_INSTANCE_NAME="conduit:"  # Optional
```

### New Configuration
```bash
# Only 1 required variable:
REDIS_URL="redis://localhost:6379"
CONDUIT_REDIS_INSTANCE_NAME="conduit:"  # Optional, defaults to "conduitllm-cache"
```

The cache is automatically enabled when `REDIS_URL` is provided, and the cache type is automatically set to "Redis".

### Redis URL Formats Supported

- Basic: `redis://hostname:port`
- With auth: `redis://username:password@hostname:port`
- With password only: `redis://:password@hostname:port`
- Custom port: `redis://hostname:1234`
- SSL/TLS: `rediss://hostname:port` (note the double 's')

## Master Key Configuration Changes

### Old Configuration
```bash
# Admin service used a different variable:
AdminApi__MasterKey="your-master-key"

# Other services might have used different patterns
```

### New Configuration
```bash
# All services now use the same variable:
CONDUIT_MASTER_KEY="your-master-key"
```

## Migration Steps

### 1. Update Docker Compose Files

If you're using Docker Compose, update your `docker-compose.yml`:

```yaml
# Old configuration
services:
  api:
    environment:
      CONDUIT_REDIS_CONNECTION_STRING: "redis:6379"
      CONDUIT_CACHE_ENABLED: "true"
      CONDUIT_CACHE_TYPE: "Redis"
      
  admin:
    environment:
      AdminApi__MasterKey: "your-key"

# New configuration
services:
  api:
    environment:
      REDIS_URL: "redis://redis:6379"
      
  admin:
    environment:
      CONDUIT_MASTER_KEY: "your-key"
```

### 2. Update Environment Files

If you're using `.env` files:

```bash
# Old .env
CONDUIT_REDIS_CONNECTION_STRING=localhost:6379
CONDUIT_CACHE_ENABLED=true
CONDUIT_CACHE_TYPE=Redis
AdminApi__MasterKey=your-master-key

# New .env
REDIS_URL=redis://localhost:6379
CONDUIT_MASTER_KEY=your-master-key
```

### 3. Update System Environment Variables

For system-level environment variables:

```bash
# Remove old variables
unset CONDUIT_REDIS_CONNECTION_STRING
unset CONDUIT_CACHE_ENABLED
unset CONDUIT_CACHE_TYPE

# Set new variables
export REDIS_URL="redis://localhost:6379"
export CONDUIT_MASTER_KEY="your-master-key"
```

### 4. Update CI/CD Pipelines

Update your CI/CD configuration files to use the new variables:

```yaml
# GitHub Actions example
env:
  REDIS_URL: redis://localhost:6379
  CONDUIT_MASTER_KEY: ${{ secrets.MASTER_KEY }}
```

## Backward Compatibility

**Important**: The old environment variables continue to work. The system checks for new variables first, then falls back to the old ones if not found.

Priority order:
1. For Redis: `REDIS_URL` → `CONDUIT_REDIS_CONNECTION_STRING`
2. For Master Key: `CONDUIT_MASTER_KEY` → `AdminApi__MasterKey`

This means you can migrate gradually without breaking existing deployments.

## Special Considerations

### Auto-enable Cache Behavior

When `REDIS_URL` is provided:
- `IsEnabled` automatically becomes `true`
- `CacheType` automatically becomes `"Redis"`

You can still explicitly disable cache if needed:
```bash
REDIS_URL=redis://localhost:6379
CONDUIT_CACHE_ENABLED=false  # Explicitly disable despite Redis being configured
```

### Redis Authentication

If your Redis instance requires authentication:

```bash
# With username and password
REDIS_URL="redis://myuser:mypassword@redis-server:6379"

# With password only (no username)
REDIS_URL="redis://:mypassword@redis-server:6379"
```

### SSL/TLS Connections

For Redis instances with SSL/TLS:

```bash
# Use 'rediss' protocol (note the double 's')
REDIS_URL="rediss://secure-redis:6380"
```

## Troubleshooting

### Cache Not Enabling

If cache isn't automatically enabling:
1. Check that `REDIS_URL` is properly formatted
2. Check application logs for Redis connection errors
3. Verify Redis is accessible from your application

### Authentication Issues

If you're getting authentication errors with the Admin API:
1. Ensure `CONDUIT_MASTER_KEY` is set in the Admin service environment
2. Check that you're using the correct header: `X-API-Key: your-master-key`

### Connection String Format Errors

If you see errors parsing the Redis URL:
1. Ensure the URL follows the format: `redis://[username]:[password]@hostname:port`
2. Special characters in passwords must be URL-encoded
3. Port is required (default is 6379)

## Future Deprecation

While backward compatibility is currently maintained, we recommend migrating to the new variables as the old ones may be deprecated in a future major version:

- `CONDUIT_REDIS_CONNECTION_STRING` → Use `REDIS_URL` instead
- `CONDUIT_CACHE_ENABLED` → No longer needed (auto-enabled with Redis)
- `CONDUIT_CACHE_TYPE` → No longer needed (auto-detected)
- `AdminApi__MasterKey` → Use `CONDUIT_MASTER_KEY` instead

## Questions or Issues?

If you encounter any issues during migration, please:
1. Check the application logs for specific error messages
2. Verify your Redis URL format is correct
3. Open an issue on GitHub with details about your configuration