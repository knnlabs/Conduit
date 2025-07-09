# Security Documentation: Client-Side SDK Authentication

## Overview

This document describes the security considerations and implementation details for the client-side SDK authentication in ConduitLLM WebUI.

## Authentication Flow

### 1. Admin Login
- Admin authenticates using `CONDUIT_WEBUI_AUTH_KEY` environment variable
- Upon successful authentication, a WebUI-specific virtual key is created/retrieved
- Virtual key is stored in encrypted session storage (when available)
- Session cookie is set with `httpOnly`, `secure`, and `sameSite` flags

### 2. Virtual Key Management
- Each WebUI instance gets its own virtual key with name "WebUI Admin Access"
- Virtual key is created automatically on first login if it doesn't exist
- Virtual key provides access to the Core SDK for making API calls
- Master key (`CONDUIT_MASTER_KEY`) is never exposed to the client

### 3. Session Management
- Sessions expire after 24 hours (or 30 days with "Remember Me")
- Automatic session refresh occurs 1 hour before expiry
- Session refresh is checked every 5 minutes
- Failed refresh triggers automatic logout

## Security Features

### 1. Rate Limiting
- Authentication attempts: 10 per 15 minutes
- Virtual key requests: 5 per hour
- General API requests: 1000 per 15 minutes
- Rate limit headers included in responses

### 2. Security Headers
All responses include:
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `X-XSS-Protection: 1; mode=block`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy: camera=(), microphone=(), geolocation=()`
- Content Security Policy (CSP) in production

### 3. Session Security
- Session cookies are:
  - `httpOnly`: Not accessible via JavaScript
  - `secure`: HTTPS only in production
  - `sameSite: strict`: CSRF protection
- Session data includes expiration time
- Expired sessions are automatically cleaned up

### 4. Key Separation
**CRITICAL**: Two distinct authentication keys:
- `CONDUIT_WEBUI_AUTH_KEY`: Admin dashboard access
- `CONDUIT_MASTER_KEY`: API client authentication
- These must NEVER be the same value

## Implementation Details

### Middleware Protection
- All routes except public paths require authentication
- Expired sessions return 401 and clear cookies
- Invalid sessions trigger redirect to login

### Virtual Key Storage
- Virtual key stored in auth store (Zustand)
- Encrypted when browser supports Web Crypto API
- Fallback to sessionStorage with warning
- Cleared on logout

### SDK Integration
- Core SDK uses virtual key for authentication
- Admin SDK uses master key (server-side only)
- SDK providers wrap components with authentication context

## Security Best Practices

1. **Environment Variables**
   - Store `CONDUIT_WEBUI_AUTH_KEY` securely
   - Never commit keys to version control
   - Use different keys for each environment

2. **Network Security**
   - Always use HTTPS in production
   - Configure proper CORS settings
   - Monitor rate limit violations

3. **Session Management**
   - Implement proper logout functionality
   - Clear all sensitive data on logout
   - Monitor for suspicious session activity

4. **Virtual Key Management**
   - Regularly rotate virtual keys
   - Monitor virtual key usage
   - Implement key expiration if needed

## Potential Vulnerabilities & Mitigations

1. **XSS Attacks**
   - Mitigation: CSP headers, httpOnly cookies
   - Virtual key in memory/storage is encrypted

2. **CSRF Attacks**
   - Mitigation: sameSite cookies, custom headers
   - State-changing operations require authentication

3. **Session Hijacking**
   - Mitigation: Secure cookies, session expiration
   - Regular session refresh validates authenticity

4. **Brute Force**
   - Mitigation: Rate limiting on auth endpoints
   - Account lockout after repeated failures (future enhancement)

## Future Enhancements

1. **Multi-Factor Authentication (MFA)**
   - Add TOTP/WebAuthn support
   - Require MFA for sensitive operations

2. **Session Anomaly Detection**
   - Detect unusual login patterns
   - Alert on concurrent sessions

3. **Audit Logging**
   - Log all authentication events
   - Track virtual key usage

4. **Key Rotation**
   - Automatic virtual key rotation
   - Graceful key transition period