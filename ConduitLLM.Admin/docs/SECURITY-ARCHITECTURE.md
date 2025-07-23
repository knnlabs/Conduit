# Admin API Security Architecture

## Overview

The Admin API has been enhanced with a comprehensive security architecture that provides multiple layers of protection while maintaining compatibility with existing API clients.

## Security Features

### 1. Unified Security Middleware

A single `SecurityMiddleware` handles all security checks in one pass:
- API key authentication
- IP filtering (environment and database-based)
- Rate limiting
- Failed authentication protection

### 2. API Key Authentication

- Primary header: `X-API-Key` (configurable)
- Alternative headers: `X-Master-Key` (backward compatibility)
- Uses `CONDUIT_API_TO_API_BACKEND_AUTH_KEY` environment variable
- Failed authentication attempts are tracked

### 3. IP Filtering

**Environment-based filtering:**
- Whitelist/blacklist with CIDR support
- Private IP detection (RFC 1918)
- Configurable modes: permissive (blacklist) or restrictive (whitelist)

**Database-based filtering:**
- Integrates with existing IP filter service
- Managed through Admin API endpoints

### 4. Rate Limiting

- Sliding window algorithm
- Configurable per-IP limits
- Excludes health and Swagger endpoints
- Returns appropriate headers (Retry-After, X-RateLimit-Limit)

### 5. Failed Authentication Protection

- Automatic IP banning after threshold
- Configurable ban duration
- Shared tracking with WebUI via Redis

### 6. Security Headers

- X-Content-Type-Options: nosniff
- X-XSS-Protection: 1; mode=block
- Strict-Transport-Security (HTTPS only)
- Removes server identification headers

## Configuration

### Environment Variables

```bash
# IP Filtering
CONDUIT_ADMIN_IP_FILTERING_ENABLED=true
CONDUIT_ADMIN_IP_FILTER_MODE=permissive
CONDUIT_ADMIN_IP_FILTER_ALLOW_PRIVATE=true
CONDUIT_ADMIN_IP_FILTER_WHITELIST=192.168.1.0/24,10.0.0.0/8
CONDUIT_ADMIN_IP_FILTER_BLACKLIST=192.168.1.100

# Rate Limiting
CONDUIT_ADMIN_RATE_LIMITING_ENABLED=true
CONDUIT_ADMIN_RATE_LIMIT_MAX_REQUESTS=100
CONDUIT_ADMIN_RATE_LIMIT_WINDOW_SECONDS=60
CONDUIT_ADMIN_RATE_LIMIT_EXCLUDED_PATHS=/health,/swagger

# Failed Authentication Protection
CONDUIT_ADMIN_MAX_FAILED_AUTH_ATTEMPTS=5
CONDUIT_ADMIN_AUTH_BAN_DURATION_MINUTES=30

# Distributed Tracking (shared with WebUI)
CONDUIT_SECURITY_USE_DISTRIBUTED_TRACKING=true

# Security Headers
CONDUIT_ADMIN_SECURITY_HEADERS_HSTS_ENABLED=true
CONDUIT_ADMIN_SECURITY_HEADERS_HSTS_MAX_AGE=31536000
```

## Shared Security Tracking

The Admin API shares security tracking data with WebUI through Redis:

### Redis Key Structure

```
rate_limit:admin-api:{ip} - Rate limiting counters
failed_login:{ip} - Failed authentication attempts
ban:{ip} - Banned IPs
```

### Data Format

```json
{
  "bannedUntil": "2024-01-20T10:30:00Z",
  "failedAttempts": 5,
  "source": "admin-api",
  "reason": "Exceeded max failed authentication attempts"
}
```

## Integration with WebUI Security Dashboard

The WebUI Security Dashboard can display:
- Combined banned IPs from both services
- Failed authentication attempts across services
- Rate limiting statistics per service
- Source identification for security events

## Migration from Old Architecture

### Before (Multiple Middleware)
```csharp
app.UseMiddleware<AdminAuthenticationMiddleware>();
app.UseMiddleware<AdminRequestTrackingMiddleware>();
```

### After (Unified Security)
```csharp
app.UseAdminSecurityHeaders();
app.UseAdminSecurity();
app.UseMiddleware<AdminRequestTrackingMiddleware>();
```

## Backward Compatibility

- Existing API clients continue to work without changes
- `X-API-Key` header is still supported
- `X-Master-Key` header supported for legacy clients
- Same authentication flow, enhanced with security features

## Performance Considerations

- Single middleware pass for all security checks
- Efficient Redis caching for distributed tracking
- Minimal overhead for authenticated requests
- Excluded paths bypass unnecessary checks

## Security Best Practices

1. **Enable IP Filtering**: Restrict access to known IP ranges
2. **Set Rate Limits**: Prevent API abuse
3. **Monitor Failed Attempts**: Review security dashboard regularly
4. **Use HTTPS**: Enable HSTS headers for secure transport
5. **Rotate API Keys**: Change `CONDUIT_API_TO_API_BACKEND_AUTH_KEY` periodically

## Troubleshooting

### Common Issues

1. **403 Forbidden**: Check IP filtering rules
2. **429 Too Many Requests**: Rate limit exceeded
3. **401 Unauthorized**: Invalid or missing API key
4. **Banned IP**: Check failed authentication attempts

### Debug Mode

Enable debug logging to see security decisions:
```bash
ASPNETCORE_ENVIRONMENT=Development
Logging__LogLevel__ConduitLLM.Admin.Services.SecurityService=Debug
```