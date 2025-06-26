---
sidebar_position: 2
title: Virtual Keys Management
description: Comprehensive guide to managing virtual keys through the Admin API
---

# Virtual Keys Management

Virtual keys are the primary access control mechanism in Conduit, providing secure, configurable API access with granular permissions, rate limiting, and cost controls. This guide covers complete virtual key lifecycle management through the Admin API.

## Virtual Key Architecture

### Key Components

- **Key Hash**: Unique identifier for caching and logging (first 8 characters)
- **Full Key**: Complete API key starting with `condt_`
- **Permissions**: Model access, operation types, rate limits
- **Budget Controls**: Spending limits and alerts
- **Usage Tracking**: Real-time consumption monitoring

### Security Model

```
Request Flow:
Client → Virtual Key → Validation → Provider → Response
         ↓
    Rate Limiting + Budget Check + Permissions
```

## Creating Virtual Keys

### Basic Virtual Key Creation

```bash
curl -X POST http://localhost:5002/api/admin/virtual-keys \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Production API Key",
    "description": "Main API key for production environment",
    "isEnabled": true
  }'
```

Response:
```json
{
  "success": true,
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "key": "condt_pk_1234567890abcdef...",
    "keyHash": "abcd1234",
    "name": "Production API Key",
    "description": "Main API key for production environment",
    "isEnabled": true,
    "createdAt": "2024-01-15T10:30:00Z",
    "createdBy": "admin",
    "lastUsed": null,
    "totalRequests": 0,
    "totalSpent": 0.0
  }
}
```

### Advanced Virtual Key Configuration

```bash
curl -X POST http://localhost:5002/api/admin/virtual-keys \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Development Team Key",
    "description": "API key for development team with specific model access",
    "isEnabled": true,
    "maxBudget": 500.00,
    "budgetPeriod": "monthly",
    "budgetAlertThresholds": [0.5, 0.8, 0.9],
    "allowedModels": [
      "gpt-3.5-turbo",
      "gpt-4",
      "claude-3-haiku",
      "claude-3-sonnet"
    ],
    "allowedOperations": [
      "chat.completions",
      "embeddings",
      "audio.transcriptions"
    ],
    "rateLimit": {
      "requestsPerMinute": 1000,
      "requestsPerHour": 50000,
      "requestsPerDay": 100000,
      "tokensPerMinute": 100000,
      "tokensPerDay": 1000000
    },
    "ipAllowlist": [
      "192.168.1.0/24",
      "10.0.0.0/8"
    ],
    "metadata": {
      "team": "development",
      "environment": "staging",
      "project": "conduit-integration"
    },
    "webhookUrl": "https://your-app.com/webhooks/conduit",
    "webhookEvents": [
      "budget.threshold.reached",
      "rate.limit.exceeded",
      "key.disabled"
    ]
  }'
```

## Virtual Key Configuration Options

### Permission Settings

**Model Access Control:**
```json
{
  "allowedModels": [
    "gpt-4",                    // Specific model
    "gpt-3.5-turbo*",          // Wildcard matching
    "claude-3-*",              // Family matching
    "anthropic/*"              // Provider matching
  ],
  "deniedModels": [
    "gpt-4-32k",               // Expensive models
    "claude-3-opus"            // High-cost models
  ]
}
```

**Operation Restrictions:**
```json
{
  "allowedOperations": [
    "chat.completions",         // Text generation
    "embeddings",              // Vector embeddings
    "audio.transcriptions",    // Speech-to-text
    "audio.speech",            // Text-to-speech
    "images.generations"       // Image generation
  ],
  "deniedOperations": [
    "audio.realtime",          // Real-time audio (expensive)
    "video.generations"        // Video generation (very expensive)
  ]
}
```

### Rate Limiting Configuration

**Comprehensive Rate Limits:**
```json
{
  "rateLimit": {
    "requestsPerMinute": 1000,
    "requestsPerHour": 50000,
    "requestsPerDay": 500000,
    "requestsPerMonth": 10000000,
    
    "tokensPerMinute": 100000,
    "tokensPerHour": 5000000,
    "tokensPerDay": 50000000,
    
    "costPerMinute": 10.00,
    "costPerHour": 500.00,
    "costPerDay": 5000.00,
    
    "concurrentRequests": 100,
    "burstMultiplier": 2.0,
    "slidingWindow": true
  }
}
```

**Rate Limit Enforcement:**
- **Sliding Window**: More accurate than fixed windows
- **Burst Allowance**: Temporary spikes up to `burstMultiplier`
- **Concurrent Limits**: Maximum simultaneous requests
- **Cost-Based Limiting**: Budget-aware rate limiting

### Budget Management

**Budget Configuration:**
```json
{
  "maxBudget": 1000.00,
  "budgetPeriod": "monthly",        // daily, weekly, monthly, yearly
  "budgetResetDay": 1,              // Day of month for reset
  "budgetAlertThresholds": [
    0.5,                            // 50% threshold
    0.8,                            // 80% threshold  
    0.9,                            // 90% threshold
    0.95                            // 95% threshold
  ],
  "budgetExceededAction": "disable", // disable, warn, continue
  "budgetGracePeriod": 24,          // Hours before enforcement
  "autoBudgetIncrease": {
    "enabled": false,
    "maxIncrease": 2.0,             // 2x original budget
    "incrementPercentage": 0.1       // 10% increases
  }
}
```

## Virtual Key Operations

### Retrieving Virtual Keys

**List All Virtual Keys:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/virtual-keys?page=1&pageSize=50&sortBy=createdAt&sortOrder=desc"
```

**Filter Virtual Keys:**
```bash
# Active keys only
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/virtual-keys?isEnabled=true"

# Keys with budget limits
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/virtual-keys?hasMaxBudget=true"

# Keys created in date range
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/virtual-keys?createdAfter=2024-01-01&createdBefore=2024-12-31"

# Search by name/description
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/virtual-keys?search=production"
```

**Get Specific Virtual Key:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  http://localhost:5002/api/admin/virtual-keys/550e8400-e29b-41d4-a716-446655440000
```

### Updating Virtual Keys

**Partial Update:**
```bash
curl -X PATCH http://localhost:5002/api/admin/virtual-keys/550e8400-e29b-41d4-a716-446655440000 \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "maxBudget": 2000.00,
    "rateLimit": {
      "requestsPerMinute": 2000
    }
  }'
```

**Complete Update:**
```bash
curl -X PUT http://localhost:5002/api/admin/virtual-keys/550e8400-e29b-41d4-a716-446655440000 \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Updated Production Key",
    "description": "Updated description",
    "isEnabled": true,
    "maxBudget": 2000.00,
    "allowedModels": ["gpt-4", "claude-3-sonnet"],
    "rateLimit": {
      "requestsPerMinute": 2000,
      "requestsPerDay": 1000000
    }
  }'
```

### Key Rotation and Security

**Regenerate Virtual Key:**
```bash
curl -X POST http://localhost:5002/api/admin/virtual-keys/550e8400-e29b-41d4-a716-446655440000/regenerate \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "invalidateOldKey": false,      // Keep old key active temporarily
    "gracePeriodHours": 24          // Hours before old key expires
  }'
```

Response:
```json
{
  "success": true,
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "newKey": "condt_pk_new_key_here...",
    "oldKey": "condt_pk_old_key_here...",
    "oldKeyExpiresAt": "2024-01-16T10:30:00Z",
    "regeneratedAt": "2024-01-15T10:30:00Z"
  }
}
```

**Disable/Enable Virtual Key:**
```bash
# Disable key
curl -X POST http://localhost:5002/api/admin/virtual-keys/550e8400-e29b-41d4-a716-446655440000/disable \
  -H "Authorization: Bearer your-master-key"

# Enable key
curl -X POST http://localhost:5002/api/admin/virtual-keys/550e8400-e29b-41d4-a716-446655440000/enable \
  -H "Authorization: Bearer your-master-key"
```

## Usage Analytics and Monitoring

### Usage Statistics

**Get Usage Summary:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/virtual-keys/550e8400-e29b-41d4-a716-446655440000/usage?period=7d"
```

Response:
```json
{
  "success": true,
  "data": {
    "keyId": "550e8400-e29b-41d4-a716-446655440000",
    "period": "7d",
    "summary": {
      "totalRequests": 15420,
      "successfulRequests": 15180,
      "failedRequests": 240,
      "totalTokens": 2456780,
      "inputTokens": 1234567,
      "outputTokens": 1222213,
      "totalCost": 234.56,
      "averageLatency": 1.23,
      "errorRate": 0.0156
    },
    "breakdown": {
      "byModel": {
        "gpt-4": {
          "requests": 8920,
          "tokens": 1456780,
          "cost": 187.23
        },
        "gpt-3.5-turbo": {
          "requests": 6500,
          "tokens": 1000000,
          "cost": 47.33
        }
      },
      "byProvider": {
        "openai": {
          "requests": 15420,
          "cost": 234.56,
          "averageLatency": 1.23
        }
      },
      "byOperation": {
        "chat.completions": {
          "requests": 14200,
          "cost": 220.45
        },
        "embeddings": {
          "requests": 1220,
          "cost": 14.11
        }
      }
    }
  }
}
```

**Detailed Usage History:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/virtual-keys/550e8400-e29b-41d4-a716-446655440000/usage/detailed?startDate=2024-01-01&endDate=2024-01-31&groupBy=day"
```

### Cost Analysis

**Cost Breakdown:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/virtual-keys/550e8400-e29b-41d4-a716-446655440000/costs?period=30d&groupBy=model"
```

Response:
```json
{
  "success": true,
  "data": {
    "totalCost": 1234.56,
    "budgetUtilization": 0.62,
    "remainingBudget": 765.44,
    "projectedMonthlyCost": 1987.34,
    "costTrend": "increasing",
    "breakdown": {
      "byModel": {
        "gpt-4": {
          "cost": 987.23,
          "percentage": 0.8,
          "requests": 25680,
          "avgCostPerRequest": 0.0384
        },
        "gpt-3.5-turbo": {
          "cost": 247.33,
          "percentage": 0.2,
          "requests": 15420,
          "avgCostPerRequest": 0.0160
        }
      },
      "dailyTrend": [
        {
          "date": "2024-01-01",
          "cost": 42.15,
          "requests": 1234
        }
      ]
    }
  }
}
```

### Real-Time Monitoring

**Current Usage Status:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/virtual-keys/550e8400-e29b-41d4-a716-446655440000/status"
```

Response:
```json
{
  "success": true,
  "data": {
    "keyId": "550e8400-e29b-41d4-a716-446655440000",
    "isEnabled": true,
    "isHealthy": true,
    "lastUsed": "2024-01-15T10:25:00Z",
    "currentPeriod": {
      "startDate": "2024-01-01T00:00:00Z",
      "endDate": "2024-01-31T23:59:59Z",
      "budgetUsed": 789.23,
      "budgetRemaining": 210.77,
      "budgetUtilization": 0.789
    },
    "rateLimitStatus": {
      "requestsThisMinute": 45,
      "requestsThisHour": 2340,
      "requestsThisDay": 15420,
      "tokensThisMinute": 15680,
      "remainingRequests": {
        "perMinute": 955,
        "perHour": 47660,
        "perDay": 84580
      }
    },
    "recentActivity": {
      "lastRequest": "2024-01-15T10:25:00Z",
      "recentErrors": 3,
      "averageLatency": 1.23
    }
  }
}
```

## Bulk Operations

### Bulk Virtual Key Creation

```bash
curl -X POST http://localhost:5002/api/admin/virtual-keys/bulk \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "keys": [
      {
        "name": "Team Alpha Key",
        "description": "API key for Team Alpha",
        "maxBudget": 500.00,
        "allowedModels": ["gpt-3.5-turbo", "claude-3-haiku"]
      },
      {
        "name": "Team Beta Key", 
        "description": "API key for Team Beta",
        "maxBudget": 1000.00,
        "allowedModels": ["gpt-4", "claude-3-sonnet"]
      }
    ],
    "commonSettings": {
      "budgetPeriod": "monthly",
      "rateLimit": {
        "requestsPerMinute": 1000,
        "requestsPerDay": 100000
      },
      "webhookUrl": "https://your-app.com/webhooks/conduit"
    }
  }'
```

### Bulk Updates

```bash
curl -X PATCH http://localhost:5002/api/admin/virtual-keys/bulk \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "keyIds": [
      "550e8400-e29b-41d4-a716-446655440000",
      "550e8400-e29b-41d4-a716-446655440001"
    ],
    "updates": {
      "maxBudget": 2000.00,
      "rateLimit": {
        "requestsPerMinute": 2000
      }
    }
  }'
```

### Bulk Export/Import

**Export Virtual Keys:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/virtual-keys/export?format=json&includeUsage=true" > virtual-keys-backup.json
```

**Import Virtual Keys:**
```bash
curl -X POST http://localhost:5002/api/admin/virtual-keys/import \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d @virtual-keys-backup.json
```

## Alert Configuration

### Budget Alerts

```json
{
  "budgetAlerts": {
    "enabled": true,
    "thresholds": [
      {
        "percentage": 0.5,
        "actions": ["webhook", "email"],
        "recipients": ["admin@company.com"]
      },
      {
        "percentage": 0.8,
        "actions": ["webhook", "email", "slack"],
        "channels": ["#alerts"]
      },
      {
        "percentage": 0.9,
        "actions": ["webhook", "email", "slack", "pagerduty"],
        "urgency": "high"
      }
    ]
  }
}
```

### Usage Alerts

```json
{
  "usageAlerts": {
    "rateLimitApproaching": {
      "threshold": 0.8,
      "window": "1m",
      "actions": ["webhook"]
    },
    "unusualUsagePattern": {
      "enabled": true,
      "threshold": 3.0,
      "actions": ["email", "webhook"]
    },
    "keyNotUsed": {
      "days": 7,
      "actions": ["email"]
    }
  }
}
```

## Event-Driven Updates

### Virtual Key Events

When virtual keys are modified through the Admin API, events are published for real-time coordination:

```json
{
  "eventType": "VirtualKeyUpdated",
  "keyId": "550e8400-e29b-41d4-a716-446655440000",
  "keyHash": "abcd1234",
  "timestamp": "2024-01-15T10:30:00Z",
  "changedProperties": [
    "maxBudget",
    "rateLimit.requestsPerMinute"
  ],
  "data": {
    "maxBudget": 2000.00,
    "rateLimit": {
      "requestsPerMinute": 2000
    }
  }
}
```

### Cache Invalidation

Virtual key changes trigger immediate cache invalidation across all Core API instances:

1. Admin API publishes `VirtualKeyUpdated` event
2. Core API instances receive event via RabbitMQ
3. Redis cache entries invalidated immediately
4. Next request loads fresh data from database

## Security Best Practices

### Key Generation

- **Cryptographically Secure**: Keys use secure random generation
- **Prefix Identification**: All keys start with `condt_` for easy identification
- **Hash-Based Caching**: Only key hash stored in logs and cache

### Access Control

```json
{
  "securitySettings": {
    "ipAllowlist": [
      "192.168.1.0/24",
      "10.0.0.0/8"
    ],
    "ipDenylist": [
      "192.168.100.0/24"
    ],
    "requireHttps": true,
    "allowedUserAgents": [
      "MyApp/1.0",
      "Python/3.9"
    ],
    "blockedUserAgents": [
      "curl/7.68.0"
    ]
  }
}
```

### Audit Logging

All virtual key operations are logged:

```json
{
  "timestamp": "2024-01-15T10:30:00Z",
  "level": "Information",
  "message": "Virtual key updated",
  "properties": {
    "operation": "VirtualKey.Update",
    "keyId": "550e8400-e29b-41d4-a716-446655440000",
    "keyHash": "abcd1234",
    "changedFields": ["maxBudget", "rateLimit"],
    "performedBy": "admin",
    "ipAddress": "192.168.1.100",
    "correlationId": "req-abc123"
  }
}
```

## Troubleshooting

### Common Issues

**Virtual Key Not Working:**
```bash
# Check key status
curl -H "Authorization: Bearer your-master-key" \
  http://localhost:5002/api/admin/virtual-keys/search?key=condt_pk_partial_key

# Check recent usage
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/virtual-keys/550e8400-e29b-41d4-a716-446655440000/usage?period=1h"
```

**Rate Limiting Issues:**
```bash
# Check rate limit status
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/virtual-keys/550e8400-e29b-41d4-a716-446655440000/rate-limits"

# View rate limit history
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/virtual-keys/550e8400-e29b-41d4-a716-446655440000/rate-limit-events"
```

**Budget Issues:**
```bash
# Check budget status
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/virtual-keys/550e8400-e29b-41d4-a716-446655440000/budget"

# View spending history
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/virtual-keys/550e8400-e29b-41d4-a716-446655440000/spending-history"
```

## Performance Considerations

### Caching Strategy

- Virtual key data cached for 5 minutes in Redis
- Rate limit counters use sliding window in Redis
- Usage statistics cached for 1 minute
- Budget calculations cached for 30 seconds

### Database Optimization

```sql
-- Indexes for virtual key queries
CREATE INDEX CONCURRENTLY idx_virtual_keys_hash ON virtual_keys(key_hash);
CREATE INDEX CONCURRENTLY idx_virtual_keys_enabled ON virtual_keys(is_enabled) WHERE is_enabled = true;
CREATE INDEX CONCURRENTLY idx_virtual_keys_created ON virtual_keys(created_at DESC);
CREATE INDEX CONCURRENTLY idx_virtual_key_usage_key_date ON virtual_key_usage(key_id, date DESC);
```

## Next Steps

- **Provider Configuration**: Set up [provider credentials and health monitoring](provider-configuration)
- **Usage & Billing**: Explore [detailed analytics and billing](usage-billing)
- **WebUI Guide**: Use the [administrative web interface](webui-guide)
- **Core APIs**: Learn about [using virtual keys with Core APIs](../core-apis/overview)