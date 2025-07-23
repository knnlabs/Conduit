# Admin API Migration Guide

This guide provides detailed instructions for transitioning from the legacy direct database access mode to the new Admin API architecture in Conduit LLM.

## Background

The Conduit LLM architecture has been updated to use a modern microservice approach that separates the WebUI from database access. Previously, the WebUI accessed the database directly, which created tight coupling and made it harder to scale the application.

With the latest version, Conduit LLM now uses an Admin API service that handles all database operations, and the WebUI communicates with this API instead of accessing the database directly. This change brings several benefits:

- **Improved Security**: Database credentials only stored in Admin API
- **Better Separation of Concerns**: WebUI focuses on UI only, Admin API handles all data access
- **Simplified Deployment**: WebUI can run without database access or dependencies
- **Easier Scaling**: Services can be scaled independently
- **Consistent API Surface**: All client applications use the same API with consistent validation

## Deprecation Notice

**Important**: Direct database access mode (legacy mode) is now deprecated and will be removed after October 2025. All users are encouraged to migrate to the Admin API architecture as soon as possible.

See [Legacy Mode Deprecation Timeline](LEGACY-MODE-DEPRECATION-TIMELINE.md) for the complete removal schedule.

## Changes in Default Behavior

Starting with the May 2025 release, the default behavior has changed:

- **Previous default**: Direct database access (`CONDUIT_USE_ADMIN_API=false`)
- **New default**: Admin API mode (`CONDUIT_USE_ADMIN_API=true` or not set)
- **Future**: Only Admin API mode will be supported (after October 2025)

## Migration Steps

### 1. Update to the Latest Version

First, ensure you're running the latest version of ConduitLLM which includes the Admin API components:

```bash
# For Docker Compose users
docker pull ghcr.io/knnlabs/conduit-webui:latest
docker pull ghcr.io/knnlabs/conduit-admin:latest
docker pull ghcr.io/knnlabs/conduit-http:latest

# For Docker users
docker pull ghcr.io/knnlabs/conduit:latest
```

### 2. Configure Docker Compose

If you're using Docker Compose, update your `docker-compose.yml` to include the Admin API service and configure the WebUI:

```yaml
services:
  webui:
    image: ghcr.io/knnlabs/conduit-webui:latest
    environment:
      # Admin API configuration
      CONDUIT_ADMIN_API_BASE_URL: http://admin:8080
      CONDUIT_API_TO_API_BACKEND_AUTH_KEY: your_secure_master_key
      CONDUIT_USE_ADMIN_API: "true"  # Now the default
      CONDUIT_DISABLE_DIRECT_DB_ACCESS: "true"  # Recommended
      # No longer need DATABASE_URL in the WebUI in Admin API mode

  admin:
    image: ghcr.io/knnlabs/conduit-admin:latest
    environment:
      DATABASE_URL: postgresql://user:password@postgres:5432/conduitdb
      AdminApi__MasterKey: your_secure_master_key  # Must match WebUI's key
      AdminApi__AllowedOrigins__0: http://webui:8080
      AdminApi__AllowedOrigins__1: http://localhost:5001

  # Other services...
```

### 3. For Single-Server Deployments

If you're running a single-server deployment:

1. Configure these environment variables for the WebUI:
   ```
   CONDUIT_API_TO_API_BACKEND_AUTH_KEY=<your-master-key>
   CONDUIT_ADMIN_API_BASE_URL=http://localhost:5002
   CONDUIT_USE_ADMIN_API=true
   CONDUIT_DISABLE_DIRECT_DB_ACCESS=true  # Recommended
   ```

2. Configure these environment variables for the Admin API:
   ```
   AdminApi__MasterKey=<your-master-key>  # Must match WebUI's key
   AdminApi__AllowedOrigins__0=http://localhost:5001
   DATABASE_URL=<your-database-connection-string>
   ```

3. Start all services and verify they can communicate:
   ```bash
   docker compose up -d
   ```

### 4. For Multi-Server/Kubernetes Deployments

If you're running a distributed deployment with WebUI and API on separate servers:

1. On the Admin API server, configure:
   ```
   AdminApi__MasterKey=<your-master-key>
   AdminApi__AllowedOrigins__0=https://your-webui-url.com
   DATABASE_URL=<your-database-connection-string>
   ```

2. On the WebUI server, configure:
   ```
   CONDUIT_API_TO_API_BACKEND_AUTH_KEY=<your-master-key>  # Must match Admin API's key
   CONDUIT_ADMIN_API_BASE_URL=https://your-admin-api-url.com
   CONDUIT_USE_ADMIN_API=true
   CONDUIT_DISABLE_DIRECT_DB_ACCESS=true  # Recommended
   ```

3. Deploy both services and ensure network connectivity between them.

## Reverting to Legacy Mode (If Needed)

**Warning**: Legacy mode is deprecated and will be removed after October 2025. Use this only as a temporary measure while resolving migration issues.

If you need to temporarily revert to the legacy direct database access mode:

1. Set `CONDUIT_USE_ADMIN_API=false` in your WebUI environment.
2. Add the database connection variables back to the WebUI:
   ```
   DATABASE_URL=<your-database-connection-string>
   ```
3. Remove `CONDUIT_DISABLE_DIRECT_DB_ACCESS=true` if it's set.
4. Restart the WebUI service.
5. You should see a deprecation warning banner in the UI indicating you're using legacy mode.

## Verification Steps

After migrating to the Admin API architecture, verify that everything is working correctly:

### 1. Check for Deprecation Warnings

- If you see deprecation warnings in the WebUI, you may still be using legacy mode
- A clean migration should show no deprecation warnings

### 2. Test Critical Features

Verify that all key functionality works properly:

1. **Authentication**:
   - Login with master key
   - Session persistence works correctly

2. **Virtual Keys**:
   - Create a new virtual key
   - List existing keys
   - Update key settings
   - Delete a key
   - Test key validation

3. **IP Filtering** (if enabled):
   - View IP filter settings
   - Add/remove IP filters
   - Test IP validation

4. **Analytics and Reporting**:
   - View request logs
   - Check cost dashboard
   - Generate detailed reports

5. **Configuration**:
   - Update model provider mappings
   - Configure global settings
   - Test provider credentials

### 3. Check API Service Health

- Verify the Admin API is running without errors in logs
- Monitor resource usage during normal operation
- Test Admin API health endpoint: `GET /health`

## Troubleshooting

### Connection Issues

If the WebUI cannot connect to the Admin API:

1. **Check Configuration**:
   - Verify that `CONDUIT_ADMIN_API_BASE_URL` is set correctly
   - Ensure the URL includes the protocol (http/https) and correct port
   - Try accessing the Admin API health endpoint directly to verify it's responding

2. **Network Troubleshooting**:
   - Check firewall rules and network policies
   - Verify connectivity between WebUI and Admin API containers/servers
   - Check for SSL/TLS certificate issues if using HTTPS

3. **Service Health**:
   - Check Admin API service logs for errors
   - Verify the Admin API container is running and healthy
   - Check resource usage (CPU, memory) on the Admin API server

### Authentication Issues

If you get authentication errors:

1. **Check Key Configuration**:
   - Verify that `CONDUIT_API_TO_API_BACKEND_AUTH_KEY` on WebUI matches `AdminApi__MasterKey` on Admin API
   - Ensure there are no extra spaces or special characters in the keys
   - Check if the key is being properly set in the environment

2. **CORS Configuration**:
   - Make sure your WebUI origin is listed in `AdminApi__AllowedOrigins`
   - Check browser console for CORS-related errors
   - Verify that the AllowedOrigins format is correct (includes protocol, host, and port)

3. **Token Issues**:
   - Clear browser cookies and cache
   - Try a private/incognito window
   - Check if tokens are being properly generated and validated

### Database Issues

If the Admin API has database problems:

1. **Connection String**:
   - Verify the `DATABASE_URL` is correctly formatted
   - Check database credentials
   - Ensure the database server is accessible from the Admin API

2. **Migration Errors**:
   - Check for database migration errors in Admin API logs
   - Verify database permissions (Admin API needs read/write access)

3. **Schema Issues**:
   - If upgrading from an older version, check for schema compatibility issues
   - Consider running the migration scripts manually if needed

## Admin API Architecture Benefits

By completing the migration to the Admin API architecture, you'll enjoy these key benefits:

1. **Enhanced Security**:
   - Database credentials isolated to Admin API only
   - Reduced attack surface in WebUI
   - More granular access control

2. **Better Performance**:
   - Optimized API queries
   - Better connection pooling
   - Reduced resource usage in WebUI

3. **Simplified Operations**:
   - Easier scaling of components
   - Simplified deployment
   - Cleaner architecture

4. **Future-Ready**:
   - Compatible with all future Conduit versions
   - Ready for upcoming features

## Support Resources

If you need help with the migration:

1. **Documentation**:
   - Read the [Admin API Implementation Status](ADMIN-API-MIGRATION-STATUS.md)
   - Review the [Legacy Mode Deprecation Timeline](LEGACY-MODE-DEPRECATION-TIMELINE.md)

2. **Community Support**:
   - Join our [Discord community](https://discord.gg/conduitllm)
   - Ask questions on our [GitHub Discussions](https://github.com/knnlabs/Conduit/discussions)

3. **Issue Reporting**:
   - Report migration issues on [GitHub Issues](https://github.com/knnlabs/Conduit/issues)
   - Include detailed environment information and logs

We're committed to making this migration as smooth as possible for all users. Don't hesitate to reach out if you need assistance!