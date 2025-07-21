# Security Considerations

## Overview

The Conduit WebUI implements multiple security layers to protect administrative operations while enabling efficient client-side API access.

## Key Security Principles

### 1. Authentication Key Separation
**CRITICAL**: Two distinct authentication keys serve different purposes:

| Key | Purpose | Visibility | Used By |
|-----|---------|------------|---------|
| `CONDUIT_WEBUI_AUTH_KEY` | Admin login | Server-only | Login endpoint |
| `CONDUIT_MASTER_KEY` | Admin API access | Server-only | Admin SDK |
| Virtual Key | Core API access | Client-side | Core SDK |

**Security Rule**: Never use the same value for `CONDUIT_WEBUI_AUTH_KEY` and `CONDUIT_MASTER_KEY`.

### 2. Virtual Key Exposure

#### Understanding the Risk
- Virtual keys are visible in browser DevTools
- Can be extracted from JavaScript memory
- Transmitted in API request headers

#### Why This is Acceptable for Admin Tools
1. **Limited Scope**: Only administrators have access
2. **Controlled Environment**: Internal tool, not public
3. **Rate Limited**: Prevents abuse if compromised
4. **Revocable**: Can be deleted and recreated
5. **Auditable**: All usage is logged

#### NOT Suitable For
- Public-facing applications
- Multi-tenant environments
- Applications where users shouldn't see keys
- High-security environments requiring key isolation

## Security Layers

### 1. Network Security

#### HTTPS Enforcement
```typescript
// middleware.ts enforces secure cookies in production
secure: process.env.NODE_ENV === 'production'
```

#### Security Headers
```typescript
// Applied to all responses
{
  'X-Content-Type-Options': 'nosniff',
  'X-Frame-Options': 'DENY',
  'X-XSS-Protection': '1; mode=block',
  'Referrer-Policy': 'strict-origin-when-cross-origin',
  'Content-Security-Policy': "default-src 'self'; ..."
}
```

### 2. Session Security

#### Cookie Configuration
- `httpOnly`: Prevents XSS access
- `secure`: HTTPS only in production
- `sameSite: strict`: CSRF protection
- 24-hour expiration with auto-refresh

#### Session Storage
```typescript
// Encrypted when Web Crypto API available
if (EncryptionService.isAvailable()) {
  const encrypted = await EncryptionService.encrypt(data);
  storage.setItem(key, encrypted);
}
```

### 3. Rate Limiting

#### Endpoint Protection
| Endpoint | Limit | Window |
|----------|-------|--------|
| Login | 10 attempts | 15 minutes |
| Virtual Key | 5 requests | 1 hour |
| API Operations | 1000 requests | 15 minutes |

#### Virtual Key Limits
```typescript
{
  requestsPerMinute: 100,
  requestsPerHour: 2000,
  tokensPerMinute: 100000,
  tokensPerHour: 2000000
}
```

### 4. Input Validation

#### Authentication
```typescript
// Master key validation
function validateMasterKeyFormat(key: string) {
  if (key.length < 4) return 'Too short';
  if (key.length > 100) return 'Too long';
  if (/^[a-z]+$/.test(key)) return 'Too simple';
  // Additional checks...
}
```

#### API Requests
- All inputs validated by SDK
- Type safety enforced by TypeScript
- Server-side validation as final check

## Best Practices

### 1. Environment Configuration

#### Development
```bash
# .env.local (git ignored)
CONDUIT_WEBUI_AUTH_KEY=dev-auth-key-change-me
CONDUIT_MASTER_KEY=dev-master-key-change-me
```

#### Production
```bash
# Use strong, unique keys
CONDUIT_WEBUI_AUTH_KEY=$(openssl rand -hex 32)
CONDUIT_MASTER_KEY=$(openssl rand -hex 32)

# Never commit production keys
# Use environment variables or secrets management
```

### 2. Key Management

#### Regular Rotation
1. Rotate `CONDUIT_WEBUI_AUTH_KEY` monthly
2. Update all admin users after rotation
3. Monitor for unauthorized access attempts

#### Virtual Key Monitoring
```typescript
// Check key usage regularly
const keys = await adminClient.virtualKeys.list();
const webuiKey = keys.find(k => k.name === 'WebUI Admin Access');
console.log('Usage:', webuiKey.usage);
```

### 3. Access Control

#### Principle of Least Privilege
- Virtual keys only access necessary operations
- No admin operations via virtual keys
- Separate keys for different environments

#### IP Restrictions (Optional)
```typescript
// For additional security, restrict by IP
const allowedIPs = ['10.0.0.0/8', '192.168.0.0/16'];
if (!isIPAllowed(request.ip, allowedIPs)) {
  return new Response('Forbidden', { status: 403 });
}
```

### 4. Monitoring and Auditing

#### Log Authentication Events
```typescript
// Log all login attempts
console.log('Login attempt:', {
  timestamp: new Date(),
  ip: request.headers.get('x-forwarded-for'),
  success: isValid
});
```

#### Monitor Anomalies
- Sudden spike in API usage
- Login attempts from new IPs
- Multiple failed authentications
- Unusual request patterns

## Security Checklist

### Deployment
- [ ] HTTPS enabled in production
- [ ] Strong, unique authentication keys
- [ ] Environment variables properly set
- [ ] Security headers configured
- [ ] Rate limiting active

### Monitoring
- [ ] Authentication logs reviewed
- [ ] Virtual key usage monitored
- [ ] Error rates tracked
- [ ] Performance metrics collected

### Maintenance
- [ ] Keys rotated regularly
- [ ] Dependencies updated
- [ ] Security patches applied
- [ ] Access logs reviewed

## Incident Response

### If Virtual Key is Compromised
1. Immediately delete the key via Admin UI
2. Force all users to re-authenticate
3. Review API logs for unauthorized usage
4. Rotate all authentication keys
5. Investigate how compromise occurred

### If Admin Key is Compromised
1. Change `CONDUIT_WEBUI_AUTH_KEY` immediately
2. Restart WebUI service
3. Notify all administrators
4. Review authentication logs
5. Consider additional security measures

## Future Security Enhancements

### 1. Multi-Factor Authentication
```typescript
// Potential 2FA implementation
interface LoginRequest {
  authKey: string;
  totpCode?: string;
}
```

### 2. OAuth/SAML Integration
- Integrate with corporate SSO
- Eliminate password management
- Centralized access control

### 3. Audit Trail
- Comprehensive action logging
- Tamper-proof audit records
- Compliance reporting

### 4. Advanced Threat Detection
- Machine learning for anomaly detection
- Real-time alerting
- Automated response to threats

## Security Resources

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [Next.js Security](https://nextjs.org/docs/advanced-features/security-headers)
- [React Security Best Practices](https://react.dev/learn/security)
- [Conduit Security Documentation](../SECURITY-AUTH-SDK.md)