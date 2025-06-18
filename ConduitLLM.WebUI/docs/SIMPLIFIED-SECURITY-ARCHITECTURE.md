# Simplified Security Architecture

## Overview

The WebUI security system has been refactored to provide a cleaner, more maintainable architecture with reduced complexity while maintaining all security features.

## Key Changes

### 1. Unified Security Service

The security functionality has been consolidated into a single `SecurityService` that handles:
- IP filtering (environment and database based)
- Rate limiting
- Failed login protection
- Security dashboard data

### 2. Simplified Configuration

Security configuration now uses a single `SecurityOptions` class with nested options:

```csharp
public class SecurityOptions
{
    public IpFilteringOptions IpFiltering { get; set; }
    public RateLimitingOptions RateLimiting { get; set; }
    public FailedLoginOptions FailedLogin { get; set; }
    public SecurityHeadersOptions Headers { get; set; }
    public bool UseDistributedTracking { get; set; }
}
```

### 3. Unified Middleware

A single `SecurityMiddleware` replaces multiple middleware components:
- Replaces `IpFilterMiddleware`
- Replaces `RateLimitingMiddleware`
- Integrates failed login checking

The `SecurityHeadersMiddleware` remains separate as it serves a different purpose.

### 4. Reduced Environment Variables

The number of security-related environment variables has been reduced while maintaining flexibility:

#### IP Filtering
- `CONDUIT_IP_FILTERING_ENABLED` - Enable/disable IP filtering
- `CONDUIT_IP_FILTER_MODE` - Filter mode (permissive/restrictive)
- `CONDUIT_IP_FILTER_ALLOW_PRIVATE` - Allow private/intranet IPs
- `CONDUIT_IP_FILTER_WHITELIST` - Comma-separated whitelist
- `CONDUIT_IP_FILTER_BLACKLIST` - Comma-separated blacklist

#### Rate Limiting
- `CONDUIT_RATE_LIMITING_ENABLED` - Enable/disable rate limiting
- `CONDUIT_RATE_LIMIT_MAX_REQUESTS` - Max requests per window
- `CONDUIT_RATE_LIMIT_WINDOW_SECONDS` - Time window in seconds
- `CONDUIT_RATE_LIMIT_EXCLUDED_PATHS` - Comma-separated excluded paths

#### Failed Login Protection
- `CONDUIT_MAX_FAILED_ATTEMPTS` - Max failed login attempts
- `CONDUIT_IP_BAN_DURATION_MINUTES` - Ban duration in minutes

#### Security Headers
- `CONDUIT_SECURITY_HEADERS_X_FRAME_OPTIONS` - X-Frame-Options value
- `CONDUIT_SECURITY_HEADERS_CSP` - Content-Security-Policy
- `CONDUIT_SECURITY_HEADERS_HSTS_MAX_AGE` - HSTS max age

### 5. Simplified Service Registration

In `Program.cs`:

```csharp
// Configure security options from environment variables
builder.Services.ConfigureSecurityOptions(builder.Configuration);

// Register unified security service
builder.Services.AddSingleton<ISecurityService, SecurityService>();

// In the pipeline
app.UseSecurityHeaders();
app.UseAuthentication();
app.UseAuthorization();
app.UseSecurity(); // Unified security middleware
```

## Benefits

1. **Reduced Complexity**: Fewer services and middleware components to manage
2. **Better Performance**: Single middleware check instead of multiple
3. **Easier Testing**: One service to mock instead of many
4. **Clearer Configuration**: Centralized options pattern
5. **Maintainability**: Less code duplication and clearer responsibilities

## Migration Notes

When applying this pattern to other projects:

1. Start with the `SecurityOptions` class and configure from environment
2. Implement the `ISecurityService` with methods for each security check
3. Create the unified `SecurityMiddleware` 
4. Keep specialized middleware (like security headers) separate
5. Update dependency injection and middleware pipeline

## Security Features Maintained

All original security features are preserved:
- IP address filtering with CIDR support
- Private/intranet IP detection
- Rate limiting with sliding window
- Failed login protection with automatic banning
- Comprehensive security headers
- Redis-based distributed tracking (when available)
- Security dashboard for monitoring