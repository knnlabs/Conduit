# Health Checks

ConduitLLM provides comprehensive health monitoring through standardized health check endpoints that monitor system components, external dependencies, and provider availability.

## Overview

Health checks in ConduitLLM:
- Monitor all critical system components
- Check external service availability
- Validate provider API connectivity
- Track performance degradation
- Support container orchestration platforms
- Enable automated recovery actions

## Health Check Endpoints

### Liveness Check

Indicates if the service is running and able to handle requests.

```http
GET /health/live
```

Response:
```json
{
  "status": "Healthy"
}
```

Use this endpoint for:
- Kubernetes liveness probes
- Basic uptime monitoring
- Service discovery registration

### Readiness Check

Indicates if the service is ready to handle traffic.

```http
GET /health/ready
```

Response:
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0456789",
  "entries": {
    "database": {
      "status": "Healthy",
      "duration": "00:00:00.0123456",
      "data": {
        "connectionString": "Host=postgres:5432",
        "activeConnections": 5,
        "maxConnections": 100
      }
    },
    "redis": {
      "status": "Healthy", 
      "duration": "00:00:00.0023456",
      "data": {
        "endpoint": "redis:6379",
        "connectedClients": 12,
        "usedMemory": "256MB"
      }
    },
    "providers": {
      "status": "Degraded",
      "duration": "00:00:00.0234567",
      "description": "Some providers are experiencing issues",
      "data": {
        "healthy": ["openai", "googlecloud", "aws"],
        "degraded": ["anthropic"],
        "unhealthy": ["cohere"]
      }
    },
    "audioServices": {
      "status": "Healthy",
      "duration": "00:00:00.0087654",
      "data": {
        "transcriptionProviders": 4,
        "ttsProviders": 5,
        "realtimeProviders": 2
      }
    }
  }
}
```

### Startup Check

Used during application startup to verify initialization.

```http
GET /health/startup
```

Checks performed:
- Database migrations completed
- Configuration loaded
- Provider credentials validated
- Cache connections established
- Background services started

## Component Health Checks

### Database Health Check

Monitors database connectivity and performance:

```csharp
public class DatabaseHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            
            return HealthCheckResult.Healthy("Database is accessible");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    }
}
```

### Redis Health Check

Validates Redis connectivity and operations:

```csharp
public class RedisHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var database = _redis.GetDatabase();
            await database.PingAsync();
            
            var info = await database.ExecuteAsync("INFO", "server");
            var data = new Dictionary<string, object>
            {
                ["connected"] = true,
                ["responseTime"] = $"{database.Ping().TotalMilliseconds}ms"
            };
            
            return HealthCheckResult.Healthy("Redis is responsive", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis connection failed", ex);
        }
    }
}
```

### Provider Health Checks

Monitors LLM provider availability:

```csharp
public class ProviderHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        var results = new List<ProviderHealthResult>();
        
        foreach (var provider in _providers)
        {
            try
            {
                var response = await provider.CheckHealthAsync(cancellationToken);
                results.Add(new ProviderHealthResult
                {
                    Provider = provider.Name,
                    Status = response.IsHealthy ? "healthy" : "unhealthy",
                    ResponseTime = response.ResponseTime,
                    Services = response.ServiceStatuses
                });
            }
            catch (Exception ex)
            {
                results.Add(new ProviderHealthResult
                {
                    Provider = provider.Name,
                    Status = "unhealthy",
                    Error = ex.Message
                });
            }
        }
        
        var unhealthyCount = results.Count(r => r.Status == "unhealthy");
        
        if (unhealthyCount == 0)
            return HealthCheckResult.Healthy("All providers healthy");
        else if (unhealthyCount < results.Count)
            return HealthCheckResult.Degraded($"{unhealthyCount} providers unhealthy");
        else
            return HealthCheckResult.Unhealthy("All providers unhealthy");
    }
}
```

### Audio Service Health Check

Specialized checks for audio services:

```csharp
public class AudioServiceHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        var checks = new Dictionary<string, object>();
        
        // Check transcription providers
        var transcriptionProviders = await _audioRouter
            .GetHealthyTranscriptionProvidersAsync();
        checks["transcriptionProviders"] = transcriptionProviders.Count;
        
        // Check TTS providers
        var ttsProviders = await _audioRouter
            .GetHealthyTtsProvidersAsync();
        checks["ttsProviders"] = ttsProviders.Count;
        
        // Check realtime providers
        var realtimeProviders = await _audioRouter
            .GetHealthyRealtimeProvidersAsync();
        checks["realtimeProviders"] = realtimeProviders.Count;
        
        // Check active sessions
        checks["activeSessions"] = _sessionManager.GetActiveSessionCount();
        
        if (transcriptionProviders.Count == 0 || ttsProviders.Count == 0)
            return HealthCheckResult.Unhealthy("No audio providers available", null, checks);
        else if (realtimeProviders.Count == 0)
            return HealthCheckResult.Degraded("Realtime audio unavailable", null, checks);
        else
            return HealthCheckResult.Healthy("Audio services operational", checks);
    }
}
```

## Configuration

### Basic Configuration

```json
{
  "HealthChecks": {
    "Enabled": true,
    "DetailedErrors": false,
    "HealthCheckInterval": 30,
    "Endpoints": {
      "Live": "/health/live",
      "Ready": "/health/ready",
      "Startup": "/health/startup"
    }
  }
}
```

### Advanced Configuration

```csharp
services.AddHealthChecks()
    // Database check with timeout
    .AddNpgSql(
        connectionString,
        name: "database",
        failureStatus: HealthStatus.Unhealthy,
        timeout: TimeSpan.FromSeconds(5))
    
    // Redis check with custom logic
    .AddRedis(
        redisConnection,
        name: "redis",
        failureStatus: HealthStatus.Degraded)
    
    // Custom provider checks
    .AddTypeActivatedCheck<ProviderHealthCheck>(
        "providers",
        failureStatus: HealthStatus.Degraded,
        args: new object[] { providerFactory })
    
    // Audio service checks
    .AddCheck<AudioServiceHealthCheck>(
        "audio-services",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "audio" })
    
    // Add health check UI
    .AddHealthChecksUI(setup =>
    {
        setup.SetEvaluationTimeInSeconds(30);
        setup.MaximumHistoryEntriesPerEndpoint(50);
    });
```

## Kubernetes Integration

### Deployment Configuration

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: conduit-api
spec:
  template:
    spec:
      containers:
      - name: api
        image: conduit:latest
        livenessProbe:
          httpGet:
            path: /health/live
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 3
        
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 20
          periodSeconds: 10
          timeoutSeconds: 10
          failureThreshold: 3
        
        startupProbe:
          httpGet:
            path: /health/startup
            port: 8080
          initialDelaySeconds: 0
          periodSeconds: 5
          timeoutSeconds: 10
          failureThreshold: 30
```

## Health Check UI

ConduitLLM includes an optional health check dashboard:

```csharp
// Enable in Startup.cs
app.UseHealthChecksUI(config =>
{
    config.UIPath = "/health-ui";
    config.ApiPath = "/health-api";
});
```

Access at: `http://your-domain/health-ui`

Features:
- Real-time health status
- Historical health data
- Webhook notifications
- Custom styling options

## Monitoring Integration

### Prometheus Metrics

Health check results are exported as Prometheus metrics:

```prometheus
# Health check status (0=unhealthy, 1=healthy, 2=degraded)
conduit_health_check_status{check="database"} 1
conduit_health_check_status{check="redis"} 1
conduit_health_check_status{check="providers"} 2

# Health check duration
conduit_health_check_duration_seconds{check="database"} 0.012
conduit_health_check_duration_seconds{check="providers"} 0.234

# Provider-specific health
conduit_provider_health_status{provider="openai"} 1
conduit_provider_health_status{provider="anthropic"} 0
```

### Custom Health Metrics

```csharp
public class CustomHealthCheck : IHealthCheck
{
    private readonly IMetricsCollector _metrics;
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Perform check
            var result = await PerformHealthCheckAsync();
            
            // Record metrics
            _metrics.RecordHealthCheck(
                checkName: context.Registration.Name,
                status: result.Status,
                duration: stopwatch.Elapsed);
            
            return result;
        }
        catch (Exception ex)
        {
            _metrics.RecordHealthCheckFailure(context.Registration.Name);
            return HealthCheckResult.Unhealthy("Check failed", ex);
        }
    }
}
```

## Health Check Strategies

### Cascading Health Checks

Configure dependencies between health checks:

```csharp
services.AddHealthChecks()
    .AddCheck("database", () => 
    {
        // Primary check
        return CheckDatabase();
    })
    .AddCheck("cache", () =>
    {
        // Only check if database is healthy
        if (!IsDatabaseHealthy())
            return HealthCheckResult.Degraded("Skipped due to database issues");
        
        return CheckCache();
    });
```

### Cached Health Checks

Prevent overwhelming services with health checks:

```csharp
public class CachedHealthCheck : IHealthCheck
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(30);
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(
            $"health_{context.Registration.Name}",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheDuration;
                return await PerformActualHealthCheckAsync();
            });
    }
}
```

## Troubleshooting

### Common Issues

1. **Health Check Timeouts**
   - Increase timeout values
   - Add circuit breakers
   - Implement caching

2. **False Positives**
   - Adjust failure thresholds
   - Implement retry logic
   - Use degraded status appropriately

3. **Performance Impact**
   - Cache health check results
   - Reduce check frequency
   - Use parallel checks carefully

### Debugging Health Checks

Enable detailed logging:

```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.Extensions.Diagnostics.HealthChecks": "Debug",
      "ConduitLLM.HealthChecks": "Debug"
    }
  }
}
```

## Best Practices

1. **Appropriate Timeouts**: Set realistic timeouts for each check
2. **Failure Thresholds**: Use multiple failures before marking unhealthy
3. **Graceful Degradation**: Use "Degraded" status for partial failures
4. **Resource Limits**: Prevent health checks from consuming excessive resources
5. **Security**: Don't expose sensitive data in health check responses
6. **Monitoring**: Alert on health check failures

## Next Steps

- [Metrics Monitoring](metrics-monitoring.md) - Prometheus metrics setup
- [Production Deployment](production-deployment.md) - Deploy with health checks
- [Troubleshooting Guide](../troubleshooting/common-issues.md) - Common health check issues