# WebUI Security Features

This document describes the security features available in the Conduit WebUI application.

## Overview

The WebUI includes comprehensive security features to protect against unauthorized access:

1. **IP Address Filtering** - Whitelist/blacklist specific IPs or subnets
2. **Failed Login Protection** - Automatic IP banning after repeated failed attempts
3. **Private/Intranet IP Detection** - Automatic handling of private network addresses
4. **Environment Variable Configuration** - Configure security settings via environment variables

## IP Address Filtering

The IP filtering middleware can restrict access based on IP addresses and CIDR subnets.

### Features

- **Whitelist Mode**: Only allow specific IPs/subnets
- **Blacklist Mode**: Block specific IPs/subnets
- **CIDR Support**: Support for subnet notation (e.g., 192.168.1.0/24)
- **Private IP Detection**: Automatically detect and optionally allow private IPs
- **Endpoint Exclusions**: Exclude certain endpoints from filtering

### Environment Variables

```bash
# Enable/disable IP filtering
CONDUIT_IP_FILTERING_ENABLED=true

# Filter mode: "permissive" (blacklist) or "restrictive" (whitelist)
CONDUIT_IP_FILTER_MODE=permissive

# Default action when no rules match
CONDUIT_IP_FILTER_DEFAULT_ALLOW=true

# Bypass filtering for admin UI paths
CONDUIT_IP_FILTER_BYPASS_ADMIN_UI=true

# Automatically allow private/intranet IPs
CONDUIT_IP_FILTER_ALLOW_PRIVATE=true

# Comma-separated whitelist (IPs or CIDR)
CONDUIT_IP_FILTER_WHITELIST=192.168.1.0/24,10.0.0.0/8

# Comma-separated blacklist (IPs or CIDR)
CONDUIT_IP_FILTER_BLACKLIST=203.0.113.0/24
```

### Private IP Ranges

The following IP ranges are automatically detected as private/intranet:

- **IPv4 Private**: 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16
- **IPv4 Loopback**: 127.0.0.0/8
- **IPv4 Link-Local**: 169.254.0.0/16
- **IPv6 Private**: fc00::/7, fd00::/8
- **IPv6 Loopback**: ::1/128
- **IPv6 Link-Local**: fe80::/10

## Failed Login Protection

Protects against brute-force attacks by tracking failed login attempts.

### Features

- **Automatic IP Banning**: Ban IPs after configurable failed attempts
- **Configurable Ban Duration**: Set how long IPs remain banned
- **Memory-Based Tracking**: Fast, in-memory tracking of attempts
- **Sliding Expiration**: Failed attempts expire after the ban duration

### Environment Variables

```bash
# Maximum failed login attempts before banning
CONDUIT_MAX_FAILED_ATTEMPTS=5

# IP ban duration in minutes
CONDUIT_IP_BAN_DURATION_MINUTES=30
```

## WebUI Authentication

The WebUI requires authentication using either:

1. **WebUI Auth Key** (recommended): Separate key for WebUI access
2. **Master Key** (fallback): System master key for backward compatibility

### Environment Variables

```bash
# WebUI-specific authentication key (recommended)
CONDUIT_WEBUI_AUTH_KEY=your-secure-webui-key

# Master key (fallback if WebUI key not set)
CONDUIT_MASTER_KEY=your-master-key
```

## Security Configuration Priority

Security settings are applied in the following priority order:

1. **Environment Variables**: Highest priority, immediate effect
2. **Admin API Settings**: Retrieved from database, merged with env settings
3. **Default Settings**: Permissive defaults to avoid lockouts

## Integration with Existing Systems

The security features integrate seamlessly with:

- **Admin API**: IP filter settings can be managed via Admin API
- **Health Checks**: Health endpoints can be excluded from filtering
- **Blazor SignalR**: WebSocket connections respect IP filtering
- **Static Assets**: CSS/JS/images can be excluded from filtering

## Security Best Practices

1. **Use HTTPS**: Always use HTTPS in production
2. **Strong Keys**: Use long, random authentication keys
3. **Restrict Private Access**: Consider disabling `CONDUIT_IP_FILTER_ALLOW_PRIVATE` in production
4. **Monitor Failed Logins**: Check logs for repeated failed login attempts
5. **Regular Updates**: Keep IP filter rules updated
6. **Least Privilege**: Use restrictive mode with explicit whitelists when possible

## Example Configurations

### Development Environment

```yaml
environment:
  CONDUIT_IP_FILTERING_ENABLED: "false"  # Disable for development
  CONDUIT_WEBUI_AUTH_KEY: "dev-key"
```

### Production - Private Network

```yaml
environment:
  CONDUIT_IP_FILTERING_ENABLED: "true"
  CONDUIT_IP_FILTER_MODE: "permissive"
  CONDUIT_IP_FILTER_ALLOW_PRIVATE: "true"
  CONDUIT_IP_FILTER_BLACKLIST: "203.0.113.0/24"  # Block specific external IPs
  CONDUIT_WEBUI_AUTH_KEY: "${WEBUI_AUTH_KEY}"  # From secrets
  CONDUIT_MAX_FAILED_ATTEMPTS: "3"
  CONDUIT_IP_BAN_DURATION_MINUTES: "60"
```

### Production - Public Internet

```yaml
environment:
  CONDUIT_IP_FILTERING_ENABLED: "true"
  CONDUIT_IP_FILTER_MODE: "restrictive"
  CONDUIT_IP_FILTER_ALLOW_PRIVATE: "false"
  CONDUIT_IP_FILTER_WHITELIST: "203.0.113.0/24,198.51.100.0/24"  # Only allow specific IPs
  CONDUIT_WEBUI_AUTH_KEY: "${WEBUI_AUTH_KEY}"  # From secrets
  CONDUIT_MAX_FAILED_ATTEMPTS: "3"
  CONDUIT_IP_BAN_DURATION_MINUTES: "120"
```

## Troubleshooting

### Locked Out

If you're locked out due to IP filtering:

1. Set `CONDUIT_IP_FILTERING_ENABLED=false` temporarily
2. Or add your IP to `CONDUIT_IP_FILTER_WHITELIST`
3. Restart the application

### Failed Login Bans

If banned due to failed logins:

1. Wait for the ban duration to expire
2. Or restart the application (clears in-memory bans)
3. Check `CONDUIT_MAX_FAILED_ATTEMPTS` and `CONDUIT_IP_BAN_DURATION_MINUTES`

### Debug Logging

Enable debug logging to see IP filtering decisions:

```yaml
Logging:
  LogLevel:
    ConduitLLM.WebUI.Middleware.IpFilterMiddleware: Debug
    ConduitLLM.WebUI.Services: Debug
```