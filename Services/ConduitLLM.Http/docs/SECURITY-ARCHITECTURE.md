# Core API Security Architecture

## Overview

The Core API has been enhanced with a comprehensive security architecture that provides multiple layers of protection including Virtual Key authentication, IP-based brute force protection, rate limiting, and security headers.

## Key Security Features

### 1. Virtual Key Authentication (URGENT FIX)

Previously, the Core API endpoints were **completely unprotected**. The new architecture enforces Virtual Key authentication on all LLM endpoints.

**Middleware**: `VirtualKeyAuthenticationMiddleware`
- Validates Virtual Keys from multiple headers (Authorization, api-key, X-API-Key, X-Virtual-Key)
- Checks if key is enabled and not expired
- Validates budget limits and model access
- Populates authentication context for downstream use

**Supported Headers**:
```
Authorization: Bearer condt_xxxx
api-key: condt_xxxx
X-API-Key: condt_xxxx
X-Virtual-Key: condt_xxxx (legacy)
```

### 2. IP-Based Brute Force Protection

**Key Innovation**: Tracks failed authentication attempts **per IP across ALL Virtual Keys** to prevent attackers from cycling through multiple keys.

**Features**:
- Bans IP after 10 failed attempts (configurable)
- Ban duration: 30 minutes (configurable)
- Shared tracking with Admin API and WebUI via Redis
- Automatic clearing on successful authentication

**Protection Against**:
- Single-source brute force attacks ✅
- Key enumeration attacks ✅
- Distributed attacks (requires Cloudflare/CDN) ⚠️

### 3. Rate Limiting

**Two-Layer Approach**:

1. **IP-Based Rate Limiting**
   - Default: 1000 requests/minute per IP
   - Prevents abuse from single sources
   - Applied before Virtual Key validation

2. **Virtual Key Rate Limiting**
   - Enforces per-key RPM/RPD limits from database
   - Tracks usage with sliding windows
   - Returns appropriate headers (X-RateLimit-Limit, X-RateLimit-Remaining)

### 4. IP Filtering

**Database-Driven Rules**:
- Whitelist/Blacklist support with CIDR notation
- Cached for performance (5-minute TTL)
- Private IP auto-allow option

**Environment-Based Rules**:
- Quick deployment filtering via environment variables
- Supports comma-separated IP/CIDR lists

### 5. Security Headers

**API-Optimized Headers**:
- `X-Content-Type-Options: nosniff` - Prevent MIME sniffing
- `Strict-Transport-Security` - HTTPS enforcement
- Removes server identification headers
- Custom headers support

## Configuration

### Environment Variables

```bash
# IP Filtering
CONDUIT_CORE_IP_FILTERING_ENABLED=true
CONDUIT_CORE_IP_FILTER_MODE=permissive
CONDUIT_CORE_IP_FILTER_ALLOW_PRIVATE=true
CONDUIT_CORE_IP_FILTER_WHITELIST=192.168.1.0/24,10.0.0.0/8
CONDUIT_CORE_IP_FILTER_BLACKLIST=192.168.1.100

# IP-Based Rate Limiting
CONDUIT_CORE_RATE_LIMITING_ENABLED=true
CONDUIT_CORE_RATE_LIMIT_MAX_REQUESTS=1000
CONDUIT_CORE_RATE_LIMIT_WINDOW_SECONDS=60

# Failed Authentication Protection
CONDUIT_CORE_MAX_FAILED_AUTH_ATTEMPTS=10
CONDUIT_CORE_AUTH_BAN_DURATION_MINUTES=30
CONDUIT_CORE_TRACK_FAILED_AUTH_ACROSS_KEYS=true

# Virtual Key Enforcement
CONDUIT_CORE_ENFORCE_VKEY_RATE_LIMITS=true
CONDUIT_CORE_ENFORCE_VKEY_BUDGETS=true
CONDUIT_CORE_ENFORCE_VKEY_MODELS=true

# Distributed Tracking (shared with Admin/WebUI)
CONDUIT_SECURITY_USE_DISTRIBUTED_TRACKING=true
```

## Shared Security Tracking

The Core API shares security data with Admin API and WebUI through Redis:

### Redis Key Structure
```
rate_limit:core-api:{ip}      - IP rate limiting
failed_login:{ip}             - Failed auth attempts (cross-service)
ban:{ip}                      - Banned IPs (cross-service)
vkey_rate:rpm:{keyId}         - Virtual Key RPM tracking
vkey_rate:rpd:{keyId}         - Virtual Key RPD tracking
```

### Shared Ban List
When an IP is banned by any service (Core, Admin, WebUI), all services respect the ban.

## Request Flow

1. **Security Headers** - Added to all responses
2. **Virtual Key Authentication** - Extract and validate key
3. **Security Checks** - IP bans, rate limits, IP filters
4. **Rate Limiter** - Apply Virtual Key specific limits
5. **Request Processing** - Handle the API request

## Migration Impact

### Breaking Changes
- **All endpoints now require authentication** (previously open)
- Virtual Key header is mandatory for LLM endpoints

### Backward Compatibility
- Supports multiple header formats
- Legacy X-Virtual-Key header still works
- Existing Virtual Keys continue to function

## Security Best Practices

### For Single-Source Attacks
1. **Enable IP-based rate limiting** - Prevents high-volume attacks
2. **Configure failed auth limits** - 10-20 attempts recommended
3. **Monitor ban list** - Check Redis for banned IPs
4. **Use IP filtering** - Restrict to known IP ranges when possible

### For Distributed Attacks
1. **Use Cloudflare/CDN** - Application-level protection has limits
2. **Enable Cloudflare features**:
   - Rate limiting rules
   - Bot fight mode
   - Geographic restrictions
   - DDoS protection
3. **Monitor usage patterns** - Identify anomalies early

## Monitoring and Troubleshooting

### Common HTTP Status Codes
- `401 Unauthorized` - Missing/invalid Virtual Key
- `403 Forbidden` - IP banned or filtered
- `429 Too Many Requests` - Rate limit exceeded

### Debug Logging
```bash
# Enable debug logging
Logging__LogLevel__ConduitLLM.Http.Services.SecurityService=Debug
Logging__LogLevel__ConduitLLM.Http.Middleware=Debug
```

### Check Security Status
```bash
# View banned IPs
redis-cli KEYS "ban:*"

# View failed login attempts
redis-cli KEYS "failed_login:*"

# Check rate limit status
redis-cli GET "rate_limit:core-api:192.168.1.100"

# Virtual Key rate limits
redis-cli KEYS "vkey_rate:*"
```

## Performance Considerations

- Security checks add ~1-2ms latency
- Redis caching minimizes database queries
- IP filter rules cached for 5 minutes
- Virtual Key validation cached for 60 seconds

## Future Enhancements

1. **Anomaly Detection** - ML-based pattern recognition
2. **Geo-blocking** - Country-level restrictions
3. **Advanced Rate Limiting** - Tiered limits by key type
4. **Security Analytics** - Detailed attack dashboards