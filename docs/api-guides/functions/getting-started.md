# Functions API - Getting Started

This guide shows you how to integrate and use the Functions API for executing external AI functions like Exa.ai search.

## Table of Contents
1. [Quick Start](#quick-start)
2. [Setup and Configuration](#setup-and-configuration)
3. [Executing Functions](#executing-functions)
4. [Monitoring Executions](#monitoring-executions)
5. [Cost Management](#cost-management)
6. [Advanced Usage](#advanced-usage)

## Quick Start

### 1. Install SDK

```bash
npm install @knn_labs/conduit-admin-client
npm install @knn_labs/conduit-gateway-client
```

### 2. Configure Function Provider (Admin)

```typescript
import { FetchConduitAdminClient, FunctionProviderType, FunctionPurpose } from '@knn_labs/conduit-admin-client';

const adminClient = new FetchConduitAdminClient({
  baseUrl: 'https://admin.conduit.ai',
  masterKey: process.env.CONDUIT_MASTER_KEY
});

// Create function configuration
const config = await adminClient.functionConfigurations.create({
  configurationName: 'Exa Production Search',
  providerType: FunctionProviderType.Exa,
  purpose: FunctionPurpose.Search,
  description: 'Exa neural search for production use',
  timeoutSeconds: 30,
  isEnabled: true
});

// Add API credential
const credential = await adminClient.functionCredentials.create({
  functionConfigurationId: config.id,
  credentialName: 'Exa Production Key',
  apiKey: process.env.EXA_API_KEY,
  isPrimary: true,
  isEnabled: true
});
```

### 3. Execute Function (Gateway API)

```typescript
import { FetchConduitCoreClient } from '@knn_labs/conduit-gateway-client';

const coreClient = new FetchConduitCoreClient({
  baseUrl: 'https://core.conduit.ai',
  apiKey: process.env.CONDUIT_VIRTUAL_KEY
});

const result = await coreClient.functions.execute({
  functionConfigurationId: config.id,
  parameters: {
    query: 'latest AI developments',
    numResults: 10,
    searchType: 'neural'
  }
});

console.log('Execution ID:', result.executionId);
console.log('State:', result.state);
console.log('Results:', result.result);
console.log('Cost:', result.actualCost);
```

## Setup and Configuration

### Creating a Function Configuration

Function configurations represent instances of function providers (e.g., "Exa Production" vs "Exa Development").

```typescript
const config = await adminClient.functionConfigurations.create({
  configurationName: 'Exa Production Search',
  providerType: FunctionProviderType.Exa,
  purpose: FunctionPurpose.Search,
  description: 'Production Exa search with neural mode',
  defaultExecutionMode: FunctionExecutionMode.Synchronous,
  timeoutSeconds: 30,
  isEnabled: true,
  metadata: JSON.stringify({
    environment: 'production',
    region: 'us-east-1'
  })
});
```

### Managing Credentials

Add multiple API keys for load balancing and failover:

```typescript
// Primary credential
const primary = await adminClient.functionCredentials.create({
  functionConfigurationId: config.id,
  credentialName: 'Exa Primary Key',
  apiKey: process.env.EXA_API_KEY_1,
  credentialGroup: 1,
  isPrimary: true,
  isEnabled: true
});

// Backup credential
const backup = await adminClient.functionCredentials.create({
  functionConfigurationId: config.id,
  credentialName: 'Exa Backup Key',
  apiKey: process.env.EXA_API_KEY_2,
  credentialGroup: 1,
  isPrimary: false,
  isEnabled: true
});

// Test credential before use
const testResult = await adminClient.functionCredentials.testCredential({
  credentialId: primary.id
});

console.log('Credential test:', testResult.success ? 'PASSED' : 'FAILED');
```

### Configuring Costs

Define pricing models for accurate cost tracking:

#### Example 1: Flat Rate Pricing

```typescript
const flatRateCost = await adminClient.functionCosts.create({
  costName: 'Simple Flat Rate',
  providerType: FunctionProviderType.Exa,
  pricingModel: FunctionPricingModel.FlatRate,
  pricingConfiguration: JSON.stringify({
    pricingModel: 1,
    costPerExecution: 0.001
  }),
  isActive: true,
  priority: 0,
  effectiveDate: new Date().toISOString()
});
```

#### Example 2: Exa Hybrid Pricing (Recommended)

```typescript
const exaCost = await adminClient.functionCosts.create({
  costName: 'Exa Standard Pricing 2024',
  providerType: FunctionProviderType.Exa,
  purpose: FunctionPurpose.Search,
  pricingModel: FunctionPricingModel.Hybrid,
  pricingConfiguration: JSON.stringify({
    pricingModel: 6,
    neuralSearchCosts: {
      tier1: {
        minResults: 1,
        maxResults: 25,
        costPerResult: 0.002
      },
      tier2: {
        minResults: 26,
        costPerResult: 0.001
      }
    },
    keywordSearchCosts: {
      tier1: {
        minResults: 1,
        maxResults: 25,
        costPerResult: 0.0005
      },
      tier2: {
        minResults: 26,
        costPerResult: 0.0002
      }
    },
    contentExtractionCosts: {
      text: 0.001,
      highlights: 0.002,
      summary: 0.003
    }
  }),
  baseCost: 0.0,
  isActive: true,
  priority: 10,
  effectiveDate: new Date().toISOString(),
  description: 'Exa official pricing as of 2024'
});
```

## Executing Functions

### Basic Execution

```typescript
const result = await coreClient.functions.execute({
  functionConfigurationId: 1,
  parameters: {
    query: 'best practices for TypeScript',
    numResults: 10
  }
});

if (result.state === 'Completed') {
  console.log('Results:', result.result);
  console.log('Cost: $' + result.actualCost.toFixed(6));
} else if (result.state === 'Failed') {
  console.error('Execution failed:', result.errorMessage);
}
```

### Exa-Specific Parameters

```typescript
// Neural search with content extraction
const neuralResult = await coreClient.functions.execute({
  functionConfigurationId: exaConfigId,
  parameters: {
    query: 'artificial intelligence breakthroughs 2024',
    numResults: 30,
    searchType: 'neural',
    useAutoprompt: true,
    contents: {
      text: true,
      highlights: true,
      summary: true
    },
    startPublishedDate: '2024-01-01T00:00:00.000Z'
  }
});

// Keyword search (cheaper)
const keywordResult = await coreClient.functions.execute({
  functionConfigurationId: exaConfigId,
  parameters: {
    query: 'site:github.com typescript',
    numResults: 10,
    searchType: 'keyword'
  }
});
```

### With Metadata and Idempotency

```typescript
const result = await coreClient.functions.execute({
  functionConfigurationId: 1,
  parameters: { query: 'AI news' },
  metadata: {
    userId: 'user_123',
    sessionId: 'session_456',
    purpose: 'research'
  },
  idempotencyKey: 'search_2024-01-15_user123_001'
});

// Same idempotency key returns cached result
const cachedResult = await coreClient.functions.execute({
  functionConfigurationId: 1,
  parameters: { query: 'AI news' },
  idempotencyKey: 'search_2024-01-15_user123_001'
});
```

### Async Execution (Future)

```typescript
// Start async execution
const execution = await coreClient.functions.execute({
  functionConfigurationId: 1,
  parameters: { query: 'long running query' },
  executionMode: FunctionExecutionMode.Asynchronous
});

console.log('Execution started:', execution.executionId);

// Poll for completion
let status = execution;
while (status.state === 'Pending' || status.state === 'Running') {
  await new Promise(resolve => setTimeout(resolve, 1000));
  status = await coreClient.functions.getExecution(execution.executionId);
}

console.log('Final result:', status.result);
```

## Monitoring Executions

### Get Execution Details

```typescript
const execution = await coreClient.functions.getExecution(executionId);

console.log('State:', execution.state);
console.log('Duration:', execution.duration, 'ms');
console.log('Estimated cost:', execution.estimatedCost);
console.log('Actual cost:', execution.actualCost);
console.log('Started at:', execution.startedAt);
console.log('Completed at:', execution.completedAt);
```

### Admin Monitoring

```typescript
// Get executions for a virtual key
const keyExecutions = await adminClient.functionExecutions.getByVirtualKey(
  virtualKeyId,
  { page: 1, pageSize: 100 }
);

// Get failed executions
const failed = await adminClient.functionExecutions.getByState(
  ExecutionState.Failed,
  { pageSize: 50 }
);

// Get executions for a specific configuration
const configExecutions = await adminClient.functionExecutions.getByConfiguration(
  configId,
  { page: 1, pageSize: 100 }
);

// Get stuck executions
const stuck = await adminClient.functionExecutions.getExpiredLeases();

// Get executions ready for retry
const retry = await adminClient.functionExecutions.getReadyForRetry();
```

### Cleanup Old Executions

```typescript
// Delete executions older than 30 days
const cleanup = await adminClient.functionExecutions.cleanup(30);
console.log(`Deleted ${cleanup.deletedCount} old executions`);

// More aggressive cleanup (7 days)
const aggressive = await adminClient.functionExecutions.cleanup(7);
```

## Cost Management

### Viewing Costs

```typescript
// List all costs
const costs = await adminClient.functionCosts.list({ pageSize: 100 });

// Get cost for specific configuration
const configCost = await adminClient.functionCosts.getByConfiguration(configId);

console.log('Current pricing:', configCost.pricingConfiguration);
console.log('Priority:', configCost.priority);
console.log('Effective:', configCost.effectiveDate);
console.log('Expires:', configCost.expiryDate);
```

### Updating Costs

```typescript
// Update pricing when provider changes rates
const updated = await adminClient.functionCosts.update(costId, {
  id: costId,
  pricingConfiguration: JSON.stringify({
    pricingModel: 6,
    neuralSearchCosts: {
      tier1: { minResults: 1, maxResults: 25, costPerResult: 0.0025 }, // New rate
      tier2: { minResults: 26, costPerResult: 0.00125 } // New rate
    },
    // ... rest of config
  }),
  effectiveDate: '2024-02-01T00:00:00.000Z', // Future effective date
  description: 'Updated pricing effective Feb 2024'
});
```

### Cost Analysis

```typescript
// Get all executions with costs
const executions = await adminClient.functionExecutions.getByConfiguration(
  configId,
  { pageSize: 1000 }
);

// Calculate total spend
const totalSpend = executions.items.reduce((sum, exec) =>
  sum + (exec.actualCost ?? 0), 0
);

// Calculate average cost per execution
const avgCost = totalSpend / executions.items.length;

// Find expensive executions
const expensive = executions.items
  .filter(e => (e.actualCost ?? 0) > avgCost * 2)
  .sort((a, b) => (b.actualCost ?? 0) - (a.actualCost ?? 0));

console.log('Total spend:', totalSpend);
console.log('Average cost:', avgCost);
console.log('Top 10 expensive:', expensive.slice(0, 10));
```

### Cache Management

```typescript
// Clear cost calculation cache after pricing updates
await adminClient.functionCosts.clearCache();
console.log('Cost cache cleared');
```

## Advanced Usage

### Error Handling

```typescript
try {
  const result = await coreClient.functions.execute({
    functionConfigurationId: 1,
    parameters: { query: 'test' }
  });
} catch (error) {
  if (error.statusCode === 400) {
    console.error('Invalid request:', error.message);
  } else if (error.statusCode === 404) {
    console.error('Configuration not found');
  } else if (error.statusCode === 402) {
    console.error('Insufficient budget');
  } else if (error.statusCode === 500) {
    console.error('Server error:', error.message);
  }
}
```

### Batch Executions

```typescript
const queries = [
  'AI news',
  'machine learning trends',
  'deep learning papers'
];

const results = await Promise.all(
  queries.map(query =>
    coreClient.functions.execute({
      functionConfigurationId: 1,
      parameters: { query, numResults: 5 }
    })
  )
);

console.log('Total cost:', results.reduce((sum, r) => sum + (r.actualCost ?? 0), 0));
```

### Configuration Discovery

```typescript
// Find all Exa configurations
const exaConfigs = await adminClient.functionConfigurations.getByProvider(
  FunctionProviderType.Exa
);

// Find all search-purpose configurations
const searchConfigs = await adminClient.functionConfigurations.getByPurpose(
  FunctionPurpose.Search
);

// Use the first enabled one
const activeConfig = exaConfigs.find(c => c.isEnabled);
if (activeConfig) {
  const result = await coreClient.functions.execute({
    functionConfigurationId: activeConfig.id,
    parameters: { query: 'test' }
  });
}
```

### Health Checks

```typescript
// Test all credentials for a configuration
const credentials = await adminClient.functionCredentials.getByConfiguration(configId);

for (const cred of credentials) {
  const test = await adminClient.functionCredentials.testCredential({
    credentialId: cred.id
  });

  console.log(`${cred.credentialName}: ${test.success ? 'OK' : 'FAILED'}`);
  if (!test.success) {
    console.error('Error:', test.message);
  }
}
```

## Best Practices

### 1. Cost Estimation

Always review estimated cost before executing expensive operations:

```typescript
// For production, implement cost approval workflow
const result = await coreClient.functions.execute({
  functionConfigurationId: 1,
  parameters: { query: 'test', numResults: 100 }
});

if (result.estimatedCost > 0.10) {
  // Alert or require approval
  console.warn('High cost execution:', result.estimatedCost);
}
```

### 2. Error Recovery

Implement retry logic for transient failures:

```typescript
async function executeWithRetry(params, maxRetries = 3) {
  for (let i = 0; i < maxRetries; i++) {
    try {
      return await coreClient.functions.execute(params);
    } catch (error) {
      if (i === maxRetries - 1) throw error;
      await new Promise(resolve => setTimeout(resolve, 1000 * (i + 1)));
    }
  }
}
```

### 3. Configuration Management

Use separate configurations for different environments:

```typescript
const configs = {
  production: await adminClient.functionConfigurations.create({
    configurationName: 'Exa Production',
    providerType: FunctionProviderType.Exa,
    purpose: FunctionPurpose.Search,
    isEnabled: true
  }),
  development: await adminClient.functionConfigurations.create({
    configurationName: 'Exa Development',
    providerType: FunctionProviderType.Exa,
    purpose: FunctionPurpose.Search,
    timeoutSeconds: 60, // Higher timeout for debugging
    isEnabled: true
  })
};

// Use environment-specific config
const configId = process.env.NODE_ENV === 'production'
  ? configs.production.id
  : configs.development.id;
```

### 4. Monitoring

Set up regular monitoring:

```typescript
// Daily execution summary
async function dailySummary() {
  const executions = await adminClient.functionExecutions.getByConfiguration(
    configId,
    { pageSize: 1000 }
  );

  const today = executions.items.filter(e =>
    new Date(e.createdAt) > new Date(Date.now() - 86400000)
  );

  const failed = today.filter(e => e.state === ExecutionState.Failed);
  const totalCost = today.reduce((sum, e) => sum + (e.actualCost ?? 0), 0);

  console.log('24h Summary:');
  console.log('- Total executions:', today.length);
  console.log('- Failed:', failed.length);
  console.log('- Total cost:', totalCost);
  console.log('- Average cost:', totalCost / today.length);
}
```

## Troubleshooting

### Execution Returns "Configuration not found"
- Verify configuration exists and is enabled
- Check `functionConfigurationId` is correct
- Ensure configuration hasn't been deleted

### Execution Returns "Insufficient budget"
- Check virtual key budget limits
- Verify estimated cost doesn't exceed available budget
- Review spend tracking in WebAdmin

### High Cost Variance
- Check `costCalculationDetails` in execution response
- Verify pricing configuration matches provider's actual pricing
- Review usage extraction from provider response

### Execution Stuck in "Running" State
- Query expired leases: `GET /api/FunctionExecutions/expired-leases`
- Check timeout settings on configuration
- Review error logs in Admin API

## Next Steps

- [Exa Provider Configuration](./exa-provider-guide.md)
- [Functions System Architecture](../../architecture/functions/functions-system-architecture.md)
- [Cost Optimization Guide](../../../WebAdmin/docs/admin/routing/examples/cost-optimization.md)
- [WebAdmin Functions Management](../../../WebAdmin/docs/README.md)
