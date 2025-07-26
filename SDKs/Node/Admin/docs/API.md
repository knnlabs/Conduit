# Conduit Admin Client API Documentation

## Table of Contents

- [Client Initialization](#client-initialization)
- [Virtual Keys](#virtual-keys)
- [Providers](#providers)
- [Model Mappings](#model-mappings)
- [Settings](#settings)
- [IP Filters](#ip-filters)
- [Model Costs](#model-costs)
- [Analytics](#analytics)
- [System Management](#system-management)
- [Error Handling](#error-handling)

## Client Initialization

### Basic Initialization

```typescript
import { ConduitAdminClient } from '@conduit/admin-client';

const client = new ConduitAdminClient({
  masterKey: 'your-master-key',
  adminApiUrl: 'http://localhost:5002',
  conduitApiUrl: 'http://localhost:5001', // optional
});
```

### Advanced Configuration

```typescript
const client = new ConduitAdminClient({
  masterKey: process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY!,
  adminApiUrl: process.env.CONDUIT_ADMIN_API_URL!,
  options: {
    timeout: 30000, // 30 seconds
    retries: 3,
    logger: customLogger,
    cache: customCacheProvider,
    headers: {
      'X-Custom-Header': 'value',
    },
  },
});
```

### From Environment Variables

```typescript
const client = ConduitAdminClient.fromEnvironment();
// Expects: CONDUIT_API_TO_API_BACKEND_AUTH_KEY, CONDUIT_ADMIN_API_URL
```

## Virtual Keys

### Create a Virtual Key

```typescript
const { virtualKey, keyInfo } = await client.virtualKeys.create({
  keyName: 'Production API',
  allowedModels: 'gpt-4,gpt-3.5-turbo',
  maxBudget: 100,
  budgetDuration: 'Monthly',
  expiresAt: '2025-12-31T23:59:59Z',
  metadata: JSON.stringify({ team: 'engineering' }),
  rateLimitRpm: 60,
  rateLimitRpd: 1000,
});
```

### List Virtual Keys

```typescript
const keys = await client.virtualKeys.list({
  pageSize: 50,
  isEnabled: true,
  budgetDuration: 'Monthly',
  sortBy: { field: 'createdAt', direction: 'desc' },
});
```

### Update a Virtual Key

```typescript
await client.virtualKeys.update(keyId, {
  maxBudget: 200,
  isEnabled: false,
  metadata: JSON.stringify({ team: 'engineering', updated: true }),
});
```

### Validate a Key

```typescript
const validation = await client.virtualKeys.validate('ck_abc123...');
if (validation.isValid) {
  console.log(`Budget remaining: $${validation.budgetRemaining}`);
}
```

### Check Budget

```typescript
const budgetCheck = await client.virtualKeys.checkBudget(keyId, 10);
console.log(`Can afford $10 request: ${budgetCheck.hasAvailableBudget}`);
```

## Providers

### Create Provider Credentials

```typescript
const provider = await client.providers.create({
  providerName: 'openai',
  apiKey: 'sk-...',
  organizationId: 'org-...',
  isEnabled: true,
});
```

### Test Provider Connection

```typescript
const testResult = await client.providers.testConnection({
  providerName: 'openai',
  apiKey: 'sk-...',
});

if (testResult.success) {
  console.log(`Available models: ${testResult.modelsAvailable?.join(', ')}`);
}
```

### Configure Health Monitoring

```typescript
await client.providers.updateHealthConfiguration('openai', {
  isEnabled: true,
  checkIntervalSeconds: 60,
  timeoutSeconds: 10,
  unhealthyThreshold: 3,
  healthyThreshold: 2,
});
```

### Get Provider Health Status

```typescript
const healthSummary = await client.providers.getHealthStatus();
console.log(`Healthy providers: ${healthSummary.healthyProviders}/${healthSummary.totalProviders}`);
```

## Model Mappings

### Create Model Mapping

```typescript
const mapping = await client.modelMappings.create({
  modelId: 'gpt-4',
  providerId: 'openai',
  providerModelId: 'gpt-4',
  isEnabled: true,
  priority: 100,
});
```

### List Mappings for a Model

```typescript
const mappings = await client.modelMappings.getByModel('gpt-4');
// Returns all provider mappings for gpt-4
```

### Reorder Mapping Priorities

```typescript
// Reorder mappings by ID (highest priority first)
await client.modelMappings.reorderMappings('gpt-4', [mappingId1, mappingId2, mappingId3]);
```

## Settings

### Global Settings

```typescript
// Get all settings
const settings = await client.settings.getGlobalSettings();

// Get specific setting
const value = await client.settings.getSetting('RATE_LIMIT_WINDOW');

// Set a setting
await client.settings.setSetting('RATE_LIMIT_WINDOW', '60', {
  description: 'Rate limit window in seconds',
  dataType: 'number',
  category: 'RateLimiting',
});
```

### Audio Configuration

```typescript
// Configure audio provider
await client.settings.createAudioConfiguration({
  provider: 'openai',
  isEnabled: true,
  defaultVoice: 'alloy',
  defaultModel: 'tts-1',
  maxDuration: 300,
  allowedVoices: ['alloy', 'echo', 'fable'],
});
```

### Router Configuration

```typescript
await client.settings.updateRouterConfiguration({
  routingStrategy: 'least-cost',
  fallbackEnabled: true,
  maxRetries: 3,
  loadBalancingEnabled: true,
  circuitBreakerEnabled: true,
  circuitBreakerThreshold: 5,
});
```

## IP Filters

### Create IP Filter Rules

```typescript
// Allow office network
await client.ipFilters.createAllowFilter(
  'Office Network',
  '192.168.1.0/24',
  'Main office IP range'
);

// Block suspicious IP
await client.ipFilters.createDenyFilter(
  'Blocked Range',
  '10.0.0.0/8',
  'Suspicious activity detected'
);
```

### Check IP Address

```typescript
const ipCheck = await client.ipFilters.checkIp('192.168.1.100');
if (!ipCheck.isAllowed) {
  console.log(`Blocked by: ${ipCheck.matchedFilter}`);
}
```

### Update IP Filter Settings

```typescript
await client.ipFilters.updateSettings({
  isEnabled: true,
  defaultAllow: false,
  filterMode: 'restrictive',
  bypassForAdminUi: true,
});
```

## Model Costs

### Set Model Costs

```typescript
await client.modelCosts.create({
  modelId: 'gpt-4',
  inputTokenCost: 0.03,  // per 1000 tokens
  outputTokenCost: 0.06, // per 1000 tokens
  currency: 'USD',
  isActive: true,
});
```

### Calculate Cost

```typescript
const cost = await client.modelCosts.calculateCost('gpt-4', 1000, 500);
console.log(`Estimated cost: $${cost.totalCost.toFixed(4)}`);
```

### Update Multiple Model Costs

```typescript
await client.modelCosts.updateCosts(
  ['gpt-4', 'gpt-3.5-turbo'],
  0.002,  // input cost
  0.004   // output cost
);
```

## Analytics

### Cost Summary

```typescript
const dateRange = {
  startDate: '2024-01-01T00:00:00Z',
  endDate: '2024-01-31T23:59:59Z',
};

const summary = await client.analytics.getCostSummary(dateRange);
console.log(`Total cost: $${summary.totalCost}`);
console.log(`Total requests: ${summary.costByModel.reduce((sum, m) => sum + m.requestCount, 0)}`);
```

### Cost Trends

```typescript
const trend = await client.analytics.getCostByPeriod(dateRange, 'day');
console.log(`Cost trend: ${trend.trend} (${trend.trendPercentage}%)`);
```

### Request Logs

```typescript
const logs = await client.analytics.getRequestLogs({
  startDate: '2024-01-01T00:00:00Z',
  endDate: '2024-01-31T23:59:59Z',
  status: 'error',
  minCost: 1.0,
  pageSize: 100,
});

logs.items.forEach(log => {
  console.log(`[${log.timestamp}] ${log.model}: ${log.errorMessage}`);
});
```

### Quick Helpers

```typescript
// Get today's costs
const todayCosts = await client.analytics.getTodayCosts();

// Get current month's costs
const monthCosts = await client.analytics.getMonthCosts();
```

## System Management

### System Information

```typescript
const info = await client.system.getSystemInfo();
console.log(`Version: ${info.version}`);
console.log(`Uptime: ${info.uptime} seconds`);
console.log(`Database: ${info.database.provider}`);
```

### Health Check

```typescript
const health = await client.system.getHealth();
if (health.status !== 'healthy') {
  Object.entries(health.checks).forEach(([check, result]) => {
    if (result.status !== 'healthy') {
      console.error(`${check}: ${result.error}`);
    }
  });
}
```

### Backup Management

```typescript
// Create backup
const backup = await client.system.createBackup({
  description: 'Pre-upgrade backup',
  includeKeys: true,
  includeProviders: true,
  includeSettings: true,
  encryptionPassword: 'secure-password',
});

// List backups
const backups = await client.system.listBackups();

// Restore backup
const result = await client.system.restoreBackup({
  backupId: backup.id,
  decryptionPassword: 'secure-password',
  overwriteExisting: true,
});
```

### Notifications

```typescript
// Get unread notifications
const notifications = await client.system.getNotifications(true);

// Mark as read
await client.system.markNotificationRead(notificationId);

// Create custom notification
await client.system.createNotification({
  type: 'warning',
  title: 'High Usage Alert',
  message: 'API usage is approaching monthly limit',
  metadata: { threshold: 0.9 },
});
```

## Error Handling

The client provides typed error classes for different scenarios:

```typescript
import { 
  ValidationError,
  AuthenticationError,
  NotFoundError,
  RateLimitError,
  ServerError,
  NotImplementedError
} from '@conduit/admin-client';

try {
  await client.virtualKeys.create({ keyName: '' });
} catch (error) {
  if (error instanceof ValidationError) {
    console.error('Validation failed:', error.details);
  } else if (error instanceof AuthenticationError) {
    console.error('Check your master key');
  } else if (error instanceof RateLimitError) {
    console.error(`Rate limited. Retry after ${error.retryAfter} seconds`);
  } else if (error instanceof NotImplementedError) {
    console.error('This feature requires Admin API implementation');
  }
}
```

### Error Properties

All errors extend `ConduitError` and include:

- `message`: Human-readable error message
- `statusCode`: HTTP status code (if applicable)
- `details`: Additional error details
- `endpoint`: API endpoint that failed
- `method`: HTTP method used

## Best Practices

1. **Use Environment Variables**: Store sensitive configuration in environment variables
2. **Implement Caching**: Use the cache option for read-heavy operations
3. **Handle Errors**: Always wrap API calls in try-catch blocks
4. **Pagination**: Use pagination for large datasets
5. **Rate Limiting**: Implement client-side rate limiting for bulk operations
6. **Logging**: Use the logger option for debugging and monitoring