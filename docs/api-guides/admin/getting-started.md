# Admin API - Getting Started

This guide will help you get started with the Conduit Admin API.

## Authentication

The Admin API uses Master Key authentication to secure all administrative endpoints. This ensures that only authorized administrators can access sensitive configuration and management functions.

### Master Key Configuration

The Master Key is configured through environment variables:

```bash
# Primary method (recommended)
export CONDUIT_API_TO_API_BACKEND_AUTH_KEY="your-secure-master-key"

# Legacy method (for backward compatibility)
export AdminApi__MasterKey="your-secure-master-key"
```

### Generating a Secure Master Key

Use a strong, randomly generated key (minimum 32 characters):

```bash
openssl rand -base64 32
```

### Supported Authentication Methods

#### 1. HTTP Header (Recommended)

```http
X-API-Key: your-master-key-here
```

#### 2. Bearer Token

```http
Authorization: Bearer your-master-key-here
```

### Example Requests

**Using cURL:**
```bash
curl -H "X-API-Key: your-master-key" \
     https://api.example.com/api/virtualkeys
```

**Using JavaScript/TypeScript:**
```typescript
const response = await fetch('https://api.example.com/api/virtualkeys', {
  headers: {
    'X-API-Key': process.env.ADMIN_API_KEY
  }
});
```

## Quick Start with TypeScript SDK

### Installation

```bash
npm install @knn_labs/conduit-admin-client
```

### Basic Setup

```typescript
import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';

const adminClient = new ConduitAdminClient({
  masterKey: process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY!,
  adminApiUrl: 'http://localhost:5002'
});
```

### Environment Variables

Create a `.env` file:

```env
CONDUIT_ADMIN_API_URL=http://localhost:5002
CONDUIT_API_TO_API_BACKEND_AUTH_KEY=your_master_key_here
NODE_ENV=development
```

## First Steps

### 1. Test Connection

Verify your authentication setup:

```bash
# Should fail without authentication
curl -I https://api.example.com/api/virtualkeys
# Expected: 401 Unauthorized

# Should succeed with valid authentication
curl -I -H "X-API-Key: your-master-key" https://api.example.com/api/virtualkeys
# Expected: 200 OK
```

Using the SDK:

```typescript
try {
  const info = await adminClient.system.getInfo();
  console.log('Connected successfully:', info.version);
} catch (error) {
  console.error('Connection failed:', error);
}
```

### 2. List Virtual Keys

```typescript
const keys = await adminClient.virtualKeys.list();
console.log(`Found ${keys.length} virtual keys`);

keys.forEach(key => {
  console.log(`${key.keyName}: $${key.currentSpend}/${key.maxBudget}`);
});
```

### 3. Create Your First Virtual Key

```typescript
const newKey = await adminClient.virtualKeys.create({
  keyName: 'My First Key',
  maxBudget: 100.00,
  budgetDuration: 'Monthly',
  allowedModels: 'gpt-4*,claude-*',
  rateLimitRpm: 60,
  rateLimitRpd: 10000
});

console.log('Created key:', newKey.virtualKey);
console.log('Key ID:', newKey.keyInfo.id);
```

### 4. Add a Provider

```typescript
// Create provider
const provider = await adminClient.providers.create({
  name: 'Production OpenAI',
  providerType: 'OpenAI',
  isEnabled: true,
  priority: 100
});

// Add credentials
await adminClient.providers.addCredential(provider.id, {
  apiKey: process.env.OPENAI_API_KEY!,
  isEnabled: true
});

// Test connection
const result = await adminClient.providers.testConnection(provider.id);
if (result.success) {
  console.log('Provider connected successfully');
} else {
  console.error('Connection failed:', result.message);
}
```

### 5. Create Model Mapping

```typescript
const mapping = await adminClient.modelMappings.create({
  modelAlias: 'gpt-4',
  providerId: provider.id,
  providerModelId: 'gpt-4-0613',
  isEnabled: true,
  priority: 100
});

console.log('Model mapping created');
```

## Protected and Public Endpoints

### Endpoints Requiring Authentication

All Admin API endpoints require Master Key authentication **except**:

### Anonymous Access Endpoints

These endpoints do NOT require authentication:

- `GET /health` - Basic health status
- `GET /health/live` - Liveness probe for Kubernetes
- `GET /health/ready` - Readiness probe with detailed checks
- `GET /api/ipfilter/check/{ipAddress}` - IP filter check (performance optimization)

## Authentication Flow

1. Client includes Master Key in the request (header or query parameter)
2. `MasterKeyAuthenticationHandler` validates the key
3. `MasterKeyAuthorizationHandler` enforces the `MasterKeyPolicy`
4. Request proceeds if authentication succeeds
5. Error returned if authentication fails

## Security Best Practices

### 1. Key Generation

Always use strong, randomly generated keys:

```bash
# Generate a secure key
openssl rand -base64 32
```

### 2. Key Storage

- ❌ Never commit keys to source control
- ✅ Use environment variables
- ✅ Use secure key management services (AWS Secrets Manager, Azure Key Vault, etc.)
- ✅ Rotate keys regularly

### 3. Transport Security

- ✅ Always use HTTPS in production
- ✅ Consider IP allowlisting for additional security
- ✅ Use network isolation in container environments

### 4. Monitoring

- ✅ Monitor failed authentication attempts
- ✅ Set up alerts for suspicious activity
- ✅ Log all administrative operations
- ✅ Review access logs regularly

## Common Workflows

### Virtual Key Lifecycle

```typescript
// 1. Create virtual key
const created = await adminClient.virtualKeys.create({
  keyName: 'Demo Application',
  maxBudget: 100.00,
  budgetDuration: 'Monthly',
  allowedModels: 'gpt-4*,claude-*'
});

// 2. Validate the key works
const validation = await adminClient.virtualKeys.validate(created.virtualKey);
console.log('Key valid:', validation.isValid);

// 3. Monitor usage
const key = await adminClient.virtualKeys.get(created.keyInfo.id);
console.log(`Spend: $${key.currentSpend} / $${key.maxBudget}`);

// 4. Update if needed
if (key.currentSpend > key.maxBudget * 0.8) {
  await adminClient.virtualKeys.update(key.id, {
    maxBudget: key.maxBudget * 1.5
  });
}

// 5. Get usage logs
const logs = await adminClient.logs.query({
  virtualKeyId: key.id,
  startDate: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000),
  endDate: new Date()
});
```

### Provider Setup

```typescript
// 1. Create provider
const provider = await adminClient.providers.create({
  name: 'Production OpenAI',
  providerType: 'OpenAI',
  isEnabled: true
});

// 2. Add API key
await adminClient.providers.addCredential(provider.id, {
  apiKey: process.env.OPENAI_API_KEY!,
  isEnabled: true
});

// 3. Test connection
const testResult = await adminClient.providers.testConnection(provider.id);

// 4. Create model mappings
await adminClient.modelMappings.create({
  modelAlias: 'gpt-4',
  providerId: provider.id,
  providerModelId: 'gpt-4-0613',
  isEnabled: true,
  priority: 100
});

// 5. Monitor health
const health = await adminClient.providers.getHealthStatus(provider.id);
console.log('Provider health:', health.isHealthy ? 'healthy' : 'unhealthy');
```

## Error Responses

### Missing or Invalid Authentication

```json
{
  "error": "Unauthorized",
  "message": "Invalid or missing API key",
  "statusCode": 401
}
```

### Insufficient Permissions

```json
{
  "error": "Forbidden",
  "message": "Insufficient permissions for this operation",
  "statusCode": 403
}
```

## Troubleshooting

### Common Issues

#### 401 Unauthorized

**Symptoms:** All requests return 401 status

**Solutions:**
- Verify Master Key is correctly configured in environment
- Check header name is exactly `X-API-Key` (case-sensitive)
- Ensure no extra spaces in the key value
- Confirm key matches exactly between client and server

#### Key Not Working After Deployment

**Symptoms:** Key works locally but fails in production

**Solutions:**
- Verify environment variable is set: `echo $CONDUIT_API_TO_API_BACKEND_AUTH_KEY`
- Check container/pod environment variables
- Restart the Admin API service after configuration changes
- Review deployment logs for configuration errors

#### Intermittent Authentication Failures

**Symptoms:** Some requests succeed, others fail

**Solutions:**
- Check for multiple Admin API instances with different configurations
- Verify load balancer is passing headers correctly
- Check for request timeouts or network issues
- Review Redis connection if using distributed caching

## Development vs Production

### Development Setup

```bash
# .env.development
CONDUIT_ADMIN_API_URL=http://localhost:5002
CONDUIT_API_TO_API_BACKEND_AUTH_KEY=dev_master_key_here
NODE_ENV=development
```

### Production Setup

```bash
# Environment variables (use secrets management)
CONDUIT_ADMIN_API_URL=https://admin-api.yourdomain.com
CONDUIT_API_TO_API_BACKEND_AUTH_KEY=<from-secrets-manager>
NODE_ENV=production
```

## Next Steps

- **[TypeScript SDK Guide](./typescript-sdk.md)** - Complete SDK documentation with examples
- **[API Reference](./api-reference.md)** - Detailed endpoint documentation
- **[Gateway API](../core/)** - Learn about the user-facing LLM API
- **[Architecture Docs](../../architecture/)** - Understand system design

## Support

For questions or issues:
- **GitHub Issues**: https://github.com/knnlabs/Conduit/issues
- **Documentation**: https://docs.conduit.ai
