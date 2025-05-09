---
sidebar_position: 1
title: Common Issues
description: Solutions for common issues when using Conduit
---

# Common Issues

This guide covers common issues you might encounter when using Conduit and provides troubleshooting steps to resolve them.

## Installation Issues

### Docker Compose Fails to Start

**Issue**: Docker Compose fails with permission errors or port conflicts.

**Solutions**:
1. Check if the ports (5000, 5001, 6379) are already in use:
   ```bash
   sudo lsof -i :5000,5001,6379
   ```
2. Ensure your user has permission to use Docker:
   ```bash
   sudo usermod -aG docker $USER
   # Log out and back in for changes to take effect
   ```
3. Check Docker Compose logs for specific errors:
   ```bash
   docker compose logs
   ```

### Database Migration Errors

**Issue**: Conduit fails to start with database migration errors.

**Solutions**:
1. Check if the database file is accessible and the location has write permissions
2. Try resetting the database (backup any important data first):
   ```bash
   rm -f /path/to/conduit.db
   docker compose up -d
   ```
3. Manually run migrations:
   ```bash
   dotnet ef database update --project ConduitLLM.Configuration
   ```

## Authentication Issues

### Invalid Master Key

**Issue**: Cannot log in to the Web UI with the master key.

**Solutions**:
1. Verify the master key in your configuration:
   ```bash
   # Check your .env file or environment variables
   cat .env | grep MASTER_KEY
   ```
2. Ensure there are no whitespace or special characters in the key
3. Reset the master key by updating the environment variable and restarting Conduit

### Virtual Key Authentication Fails

**Issue**: API requests return 401 Unauthorized when using a virtual key.

**Solutions**:
1. Verify the virtual key format (should start with `condt_`)
2. Check if the key is active in the Web UI under **Virtual Keys**
3. Ensure the key has the necessary permissions for the requested operation
4. Check for rate limit issues (429 responses)
5. Verify the Authorization header format: `Authorization: Bearer condt_your_key`

## Provider Connectivity Issues

### Provider Connection Failed

**Issue**: Errors when trying to connect to an LLM provider.

**Solutions**:
1. Verify the provider credentials in the Web UI under **Configuration > Provider Credentials**
2. Test the connection using the **Test Connection** button
3. Check if you're using the correct API key format for the provider
4. Verify network connectivity to the provider's API endpoint
5. Check if the provider is experiencing an outage on their status page

### Model Not Found

**Issue**: Model not found errors when making requests.

**Solutions**:
1. Verify that the model name exists in your model mappings
2. Check that the underlying provider model is correct
3. Ensure the provider supports the requested model
4. Check if the virtual key has permission to access the model
5. Verify the model is properly configured in the routing settings

## Performance Issues

### High Latency

**Issue**: Requests take too long to complete.

**Solutions**:
1. Enable caching to improve response times for repeated requests
2. Check network latency between Conduit and LLM providers
3. Monitor provider-specific latency in the **Provider Health** dashboard
4. Consider using faster models or providers for time-sensitive operations
5. Implement query optimization techniques like prompt compression

### Memory Usage

**Issue**: High memory usage causing performance issues.

**Solutions**:
1. If using Redis cache, check Redis memory settings
2. For in-memory cache, adjust the maximum cache size
3. Set appropriate lifecycle management for database connections
4. Check for memory leaks in logs
5. Increase container memory limits if necessary

## Routing Issues

### Unexpected Provider Selection

**Issue**: Requests are routed to unexpected providers.

**Solutions**:
1. Check your routing strategy configuration under **Configuration > Routing**
2. Review model mapping priorities
3. For Least Cost routing, verify model costs are correctly defined
4. For Round Robin routing, check the distribution pattern over multiple requests
5. Use request-level routing overrides for testing:
   ```json
   {
     "routing": {
       "provider_override": "specific_provider"
     }
   }
   ```

### Fallback Not Working

**Issue**: Requests fail without falling back to alternative providers.

**Solutions**:
1. Verify fallback is enabled in the routing configuration
2. Check fallback rules for the specific model
3. Ensure the fallback provider is properly configured
4. Review error conditions that trigger fallbacks
5. Check logs for fallback attempts and failures

## Logging and Monitoring Issues

### Missing Request Logs

**Issue**: Request logs are not visible in the dashboard.

**Solutions**:
1. Verify logging is enabled in the configuration
2. Check database permissions for log writing
3. Ensure log retention settings are appropriate
4. Try filtering logs with different parameters
5. Check for log processing errors in the application logs

### Inaccurate Cost Reporting

**Issue**: Cost reports show incorrect or missing data.

**Solutions**:
1. Verify model costs are correctly defined
2. Check token counting precision and algorithms
3. Ensure all requests are being logged correctly
4. Verify the date range for cost reports
5. Update provider-specific token counting methods if necessary

## Caching Issues

### Cache Not Working

**Issue**: Responses are not being cached or cache hits are not happening.

**Solutions**:
1. Verify caching is enabled in **Configuration > Caching**
2. Check cache configuration (provider, TTL, etc.)
3. For Redis cache, verify Redis connection
4. Check if no_cache flag is being set in requests
5. Verify cache key generation for complex requests

### Redis Connection Issues

**Issue**: Cannot connect to Redis for caching.

**Solutions**:
1. Verify Redis connection string in the configuration
2. Check if Redis server is running:
   ```bash
   docker compose exec redis redis-cli ping
   ```
3. Check for Redis authentication issues
4. Verify network connectivity to Redis server
5. Check Redis memory and performance

## Advanced Troubleshooting

### Viewing Detailed Logs

For more detailed troubleshooting, check the application logs:

```bash
# For Docker deployments
docker compose logs conduit-api --tail=100

# For direct deployments
tail -f logs/conduit.log
```

### Debugging Network Issues

To troubleshoot connectivity issues:

```bash
# Test connectivity to a provider API
curl -v https://api.openai.com/v1/models \
  -H "Authorization: Bearer your_api_key"

# Check for network issues
ping api.openai.com
traceroute api.openai.com
```

### Database Inspection

For database issues, you can inspect the SQLite database:

```bash
# Install SQLite
sudo apt-get install sqlite3

# Open the database
sqlite3 /path/to/conduit.db

# View tables
.tables

# Query specific data
SELECT * FROM VirtualKeys LIMIT 10;
```

### Checking System Resources

If experiencing performance issues:

```bash
# Check system resources
htop

# Check disk usage
df -h

# Check Docker resource usage
docker stats
```

## Getting Additional Help

If you're still experiencing issues:

1. Check the [FAQ](faq) for additional guidance
2. Search the [GitHub Issues](https://github.com/knnlabs/conduit/issues) for similar problems
3. Review recent changes in the [Release Notes](https://github.com/knnlabs/conduit/releases)
4. Open a new issue with detailed information about your problem
5. Join the community discussion forums for help from other users