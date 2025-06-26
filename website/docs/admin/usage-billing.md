---
sidebar_position: 4
title: Usage & Billing
description: Comprehensive analytics, usage tracking, and billing management through the Admin API
---

# Usage & Billing

Conduit provides comprehensive usage tracking, cost analytics, and billing management through the Admin API. This guide covers real-time usage monitoring, cost attribution, billing analytics, and budget management.

## Overview

### Key Features

- **Real-Time Usage Tracking**: Live monitoring of requests, tokens, and costs
- **Granular Attribution**: Cost tracking by virtual key, provider, model, and user
- **Budget Management**: Automated budget enforcement and alerting
- **Analytics & Reporting**: Comprehensive usage and cost analytics
- **Export & Integration**: Data export for external billing systems

### Data Hierarchy

```
Organization
├── Virtual Keys
│   ├── Usage Sessions
│   ├── Request Logs
│   └── Cost Breakdowns
├── Providers
│   ├── Model Usage
│   ├── Rate Limits
│   └── Cost Analysis
└── System Metrics
    ├── Performance Data
    ├── Error Rates
    └── Health Status
```

## Real-Time Usage Monitoring

### Live Usage Dashboard

**Get Current Usage Statistics:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/analytics/usage/live"
```

Response:
```json
{
  "success": true,
  "data": {
    "timestamp": "2024-01-15T10:30:00Z",
    "realTimeMetrics": {
      "activeRequests": 45,
      "requestsThisMinute": 1234,
      "requestsThisHour": 67890,
      "requestsToday": 1234567,
      "tokensThisMinute": 156780,
      "tokensThisHour": 8945678,
      "costThisMinute": 12.34,
      "costThisHour": 678.90,
      "costToday": 12345.67
    },
    "topVirtualKeys": [
      {
        "keyId": "550e8400-e29b-41d4-a716-446655440000",
        "keyName": "Production API Key",
        "requestsThisHour": 12345,
        "costThisHour": 234.56,
        "percentageOfTotal": 0.18
      }
    ],
    "topModels": [
      {
        "model": "gpt-4",
        "provider": "openai",
        "requestsThisHour": 8920,
        "costThisHour": 456.78,
        "percentageOfTotal": 0.67
      }
    ],
    "providerDistribution": {
      "openai": {
        "requests": 45670,
        "cost": 789.12,
        "percentage": 0.67
      },
      "anthropic": {
        "requests": 15680,
        "cost": 234.56,
        "percentage": 0.23
      }
    }
  }
}
```

### Usage Trends

**Get Usage Trends:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/analytics/usage/trends?period=7d&granularity=hour"
```

Response:
```json
{
  "success": true,
  "data": {
    "period": "7d",
    "granularity": "hour",
    "totalDataPoints": 168,
    "metrics": {
      "totalRequests": 1250000,
      "totalTokens": 125000000,
      "totalCost": 15234.56,
      "averageRequestsPerHour": 7440,
      "peakRequestsPerHour": 12345,
      "averageCostPerRequest": 0.0122
    },
    "timeline": [
      {
        "timestamp": "2024-01-15T10:00:00Z",
        "requests": 8920,
        "tokens": 1456780,
        "cost": 187.23,
        "activeVirtualKeys": 145,
        "errorRate": 0.012
      }
    ],
    "trends": {
      "requestGrowth": 0.15,      // 15% growth
      "costGrowth": 0.12,         // 12% growth
      "efficiencyImprovement": 0.03 // 3% better cost per token
    }
  }
}
```

## Virtual Key Usage Analytics

### Individual Virtual Key Analytics

**Get Virtual Key Usage Details:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/virtual-keys/550e8400-e29b-41d4-a716-446655440000/analytics?period=30d&breakdown=daily"
```

Response:
```json
{
  "success": true,
  "data": {
    "keyId": "550e8400-e29b-41d4-a716-446655440000",
    "keyName": "Production API Key",
    "period": "30d",
    "summary": {
      "totalRequests": 125680,
      "successfulRequests": 124156,
      "failedRequests": 1524,
      "totalTokens": 15678900,
      "inputTokens": 7834567,
      "outputTokens": 7844333,
      "totalCost": 2134.56,
      "averageLatency": 1.23,
      "errorRate": 0.0121,
      "costPerRequest": 0.0170,
      "costPerToken": 0.000136
    },
    "budgetStatus": {
      "maxBudget": 5000.00,
      "usedBudget": 2134.56,
      "remainingBudget": 2865.44,
      "utilizationPercentage": 0.427,
      "projectedMonthlySpend": 2845.67,
      "onTrackForBudget": true
    },
    "usageBreakdown": {
      "byModel": {
        "gpt-4": {
          "requests": 67890,
          "tokens": 8945678,
          "cost": 1678.90,
          "percentage": 0.787
        },
        "gpt-3.5-turbo": {
          "requests": 45670,
          "tokens": 5678900,
          "cost": 345.67,
          "percentage": 0.162
        },
        "claude-3-sonnet": {
          "requests": 12120,
          "tokens": 1054322,
          "cost": 109.99,
          "percentage": 0.051
        }
      },
      "byOperation": {
        "chat.completions": {
          "requests": 118560,
          "cost": 2001.23,
          "percentage": 0.937
        },
        "embeddings": {
          "requests": 5680,
          "cost": 89.45,
          "percentage": 0.042
        },
        "audio.transcriptions": {
          "requests": 1440,
          "cost": 43.88,
          "percentage": 0.021
        }
      },
      "byTimeOfDay": {
        "00-06": {"requests": 5670, "cost": 89.12},
        "06-12": {"requests": 45680, "cost": 678.90},
        "12-18": {"requests": 56780, "cost": 987.65},
        "18-24": {"requests": 17550, "cost": 378.89}
      }
    },
    "dailyTrend": [
      {
        "date": "2024-01-15",
        "requests": 4567,
        "tokens": 567890,
        "cost": 89.12,
        "peakHour": 14,
        "errors": 23
      }
    ]
  }
}
```

### Virtual Key Comparison

**Compare Multiple Virtual Keys:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/virtual-keys/compare" \
  -H "Content-Type: application/json" \
  -d '{
    "keyIds": [
      "550e8400-e29b-41d4-a716-446655440000",
      "550e8400-e29b-41d4-a716-446655440001",
      "550e8400-e29b-41d4-a716-446655440002"
    ],
    "period": "7d",
    "metrics": ["requests", "cost", "latency", "errorRate"]
  }'
```

## Provider Analytics

### Provider Performance Analysis

**Get Provider Analytics:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/providers/analytics?period=30d&breakdown=model"
```

Response:
```json
{
  "success": true,
  "data": {
    "period": "30d",
    "totalCost": 15234.56,
    "totalRequests": 1250000,
    "providers": {
      "openai": {
        "summary": {
          "requests": 875000,
          "cost": 10678.23,
          "percentage": 0.701,
          "averageLatency": 1.23,
          "errorRate": 0.012,
          "uptime": 0.9995
        },
        "models": {
          "gpt-4": {
            "requests": 450000,
            "cost": 8234.45,
            "inputTokens": 56780000,
            "outputTokens": 45670000,
            "averageLatency": 1.45,
            "costPerRequest": 0.0183,
            "costPerToken": 0.0000805
          },
          "gpt-3.5-turbo": {
            "requests": 425000,
            "cost": 2443.78,
            "inputTokens": 78900000,
            "outputTokens": 67890000,
            "averageLatency": 0.89,
            "costPerRequest": 0.0057,
            "costPerToken": 0.0000166
          }
        },
        "operations": {
          "chat.completions": {
            "requests": 825000,
            "cost": 9987.65
          },
          "embeddings": {
            "requests": 35000,
            "cost": 456.78
          },
          "audio.transcriptions": {
            "requests": 15000,
            "cost": 233.80
          }
        }
      },
      "anthropic": {
        "summary": {
          "requests": 300000,
          "cost": 3678.90,
          "percentage": 0.241,
          "averageLatency": 1.45,
          "errorRate": 0.008,
          "uptime": 0.9998
        },
        "models": {
          "claude-3-sonnet": {
            "requests": 200000,
            "cost": 2456.78,
            "inputTokens": 34560000,
            "outputTokens": 28900000,
            "averageLatency": 1.34,
            "costPerRequest": 0.0123
          },
          "claude-3-haiku": {
            "requests": 100000,
            "cost": 1222.12,
            "inputTokens": 45670000,
            "outputTokens": 38900000,
            "averageLatency": 1.56,
            "costPerRequest": 0.0122
          }
        }
      }
    }
  }
}
```

### Cost Optimization Analysis

**Get Cost Optimization Insights:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/analytics/cost-optimization?period=30d"
```

Response:
```json
{
  "success": true,
  "data": {
    "totalCost": 15234.56,
    "potentialSavings": 2145.67,
    "savingsPercentage": 0.141,
    "recommendations": [
      {
        "type": "model_substitution",
        "description": "Replace gpt-4 with gpt-3.5-turbo for simple tasks",
        "estimatedSavings": 1234.56,
        "affectedRequests": 125000,
        "confidence": 0.85,
        "implementation": {
          "virtualKeys": ["550e8400-e29b-41d4-a716-446655440000"],
          "modelMapping": {
            "from": "gpt-4",
            "to": "gpt-3.5-turbo",
            "conditions": ["input_tokens < 1000", "no_function_calls"]
          }
        }
      },
      {
        "type": "provider_routing",
        "description": "Route Claude requests to Anthropic direct instead of AWS Bedrock",
        "estimatedSavings": 567.89,
        "affectedRequests": 45000,
        "confidence": 0.92
      },
      {
        "type": "caching",
        "description": "Enable response caching for repeated queries",
        "estimatedSavings": 343.22,
        "cacheHitRateProjected": 0.15,
        "confidence": 0.78
      }
    ],
    "costTrends": {
      "increasing": [
        {
          "category": "gpt-4_usage",
          "growthRate": 0.25,
          "projectedMonthlyCost": 12000.00
        }
      ],
      "decreasing": [
        {
          "category": "embeddings",
          "reductionRate": 0.10,
          "savings": 123.45
        }
      ]
    }
  }
}
```

## Budget Management

### Budget Configuration and Monitoring

**Get Budget Status:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/budgets/status"
```

Response:
```json
{
  "success": true,
  "data": {
    "globalBudget": {
      "maxBudget": 50000.00,
      "currentSpend": 32145.67,
      "remainingBudget": 17854.33,
      "utilizationPercentage": 0.643,
      "projectedMonthlySpend": 42567.89,
      "onTrack": true,
      "daysRemaining": 16,
      "averageDailySpend": 1072.91
    },
    "budgetAlerts": [
      {
        "level": "warning",
        "message": "Virtual key 'Production API Key' has reached 80% of budget",
        "keyId": "550e8400-e29b-41d4-a716-446655440000",
        "percentage": 0.80,
        "remainingBudget": 400.00
      }
    ],
    "budgetBreakdown": {
      "byVirtualKey": [
        {
          "keyId": "550e8400-e29b-41d4-a716-446655440000",
          "keyName": "Production API Key",
          "maxBudget": 5000.00,
          "currentSpend": 4000.00,
          "utilizationPercentage": 0.80
        }
      ],
      "byProvider": {
        "openai": {
          "spend": 22678.90,
          "percentage": 0.705
        },
        "anthropic": {
          "spend": 7890.12,
          "percentage": 0.245
        },
        "google": {
          "spend": 1576.65,
          "percentage": 0.050
        }
      }
    }
  }
}
```

### Budget Alerts and Enforcement

**Configure Budget Alerts:**
```bash
curl -X PUT http://localhost:5002/api/admin/budgets/alerts \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "globalBudgetAlerts": {
      "enabled": true,
      "thresholds": [
        {
          "percentage": 0.7,
          "actions": ["webhook", "email"],
          "recipients": ["admin@company.com"],
          "message": "Global budget 70% utilized"
        },
        {
          "percentage": 0.9,
          "actions": ["webhook", "email", "slack", "pagerduty"],
          "recipients": ["admin@company.com"],
          "channels": ["#alerts"],
          "urgency": "high",
          "message": "Global budget 90% utilized - immediate attention required"
        }
      ]
    },
    "virtualKeyBudgetAlerts": {
      "enabled": true,
      "defaultThresholds": [0.5, 0.8, 0.95],
      "actions": {
        "0.5": ["webhook"],
        "0.8": ["webhook", "email"],
        "0.95": ["webhook", "email", "disable_key"]
      }
    },
    "costSpikesDetection": {
      "enabled": true,
      "thresholdMultiplier": 3.0,
      "timeWindow": 3600,
      "actions": ["webhook", "email"]
    }
  }'
```

### Budget Forecasting

**Get Budget Forecasts:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/analytics/budget-forecast?horizon=90d"
```

Response:
```json
{
  "success": true,
  "data": {
    "horizon": "90d",
    "currentTrend": "increasing",
    "confidenceLevel": 0.85,
    "globalForecast": {
      "projectedSpend": 156780.90,
      "budgetRequired": 180000.00,
      "overBudgetRisk": 0.15,
      "recommendedBudget": 175000.00
    },
    "monthlyForecasts": [
      {
        "month": "2024-02",
        "projectedSpend": 47890.12,
        "confidence": 0.90,
        "factors": ["historical_trend", "seasonal_adjustment"]
      },
      {
        "month": "2024-03",
        "projectedSpend": 52145.67,
        "confidence": 0.82,
        "factors": ["growth_trend", "new_virtual_keys"]
      }
    ],
    "keyForecasts": [
      {
        "keyId": "550e8400-e29b-41d4-a716-446655440000",
        "keyName": "Production API Key",
        "projectedSpend": 6789.01,
        "budgetRequired": 8000.00,
        "riskLevel": "medium"
      }
    ],
    "growthFactors": [
      {
        "factor": "increased_gpt4_usage",
        "impact": 1234.56,
        "confidence": 0.88
      },
      {
        "factor": "new_audio_features",
        "impact": 567.89,
        "confidence": 0.72
      }
    ]
  }
}
```

## Advanced Analytics

### Usage Patterns Analysis

**Get Usage Patterns:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/analytics/patterns?period=30d&analysis=detailed"
```

Response:
```json
{
  "success": true,
  "data": {
    "period": "30d",
    "patterns": {
      "temporalPatterns": {
        "peakHours": [9, 10, 11, 14, 15, 16],
        "lowUsageHours": [0, 1, 2, 3, 4, 5],
        "weekdayVsWeekend": {
          "weekdayAverage": 45670,
          "weekendAverage": 12345,
          "ratio": 3.7
        },
        "seasonality": {
          "detected": true,
          "pattern": "business_hours",
          "strength": 0.87
        }
      },
      "usageDistribution": {
        "powerUsers": {
          "count": 12,
          "percentage": 0.08,
          "costContribution": 0.67
        },
        "mediumUsers": {
          "count": 45,
          "percentage": 0.30,
          "costContribution": 0.28
        },
        "lightUsers": {
          "count": 93,
          "percentage": 0.62,
          "costContribution": 0.05
        }
      },
      "modelPreferences": {
        "gpt-4": {
          "usage": 0.45,
          "growthTrend": "increasing",
          "userSegments": ["power_users", "enterprise"]
        },
        "gpt-3.5-turbo": {
          "usage": 0.35,
          "growthTrend": "stable",
          "userSegments": ["all"]
        }
      }
    },
    "anomalies": [
      {
        "type": "usage_spike",
        "timestamp": "2024-01-14T15:30:00Z",
        "description": "300% increase in GPT-4 usage",
        "impact": "cost_increase",
        "severity": "medium"
      }
    ]
  }
}
```

### Performance Analytics

**Get Performance Metrics:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/analytics/performance?period=7d&breakdown=provider"
```

## Data Export and Integration

### Export Usage Data

**Export Detailed Usage Data:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/analytics/export" \
  -H "Content-Type: application/json" \
  -d '{
    "format": "csv",
    "period": "30d",
    "granularity": "hourly",
    "includeFields": [
      "timestamp",
      "virtual_key_id",
      "virtual_key_name",
      "provider",
      "model",
      "operation",
      "requests",
      "tokens",
      "cost",
      "latency",
      "errors"
    ],
    "filters": {
      "virtualKeys": ["550e8400-e29b-41d4-a716-446655440000"],
      "providers": ["openai", "anthropic"],
      "minCost": 1.00
    }
  }' > usage-export.csv
```

**Export for Billing Integration:**
```bash
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/analytics/billing-export" \
  -H "Content-Type: application/json" \
  -d '{
    "format": "json",
    "period": "monthly",
    "billingCycle": "2024-01",
    "groupBy": "virtual_key",
    "includeMetadata": true,
    "includeTaxCalculations": true
  }' > billing-2024-01.json
```

### Integration with External Systems

**Webhook Configuration for Real-Time Data:**
```bash
curl -X PUT http://localhost:5002/api/admin/analytics/webhooks \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "enabled": true,
    "endpoints": [
      {
        "url": "https://billing.company.com/webhooks/conduit",
        "events": ["usage.recorded", "cost.calculated", "budget.exceeded"],
        "authentication": {
          "type": "bearer",
          "token": "webhook-auth-token"
        },
        "retryPolicy": {
          "maxRetries": 3,
          "retryDelay": 5000
        }
      }
    ],
    "batchSettings": {
      "enabled": true,
      "batchSize": 100,
      "flushInterval": 300
    }
  }'
```

## Custom Reports

### Creating Custom Reports

**Generate Custom Analytics Report:**
```bash
curl -X POST http://localhost:5002/api/admin/analytics/reports \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "reportName": "Monthly Executive Summary",
    "period": "30d",
    "sections": [
      {
        "type": "summary",
        "metrics": ["total_cost", "total_requests", "unique_users"]
      },
      {
        "type": "breakdown",
        "groupBy": "provider",
        "metrics": ["cost", "requests", "latency"]
      },
      {
        "type": "trends",
        "granularity": "daily",
        "metrics": ["cost", "requests"]
      },
      {
        "type": "top_items",
        "category": "virtual_keys",
        "sortBy": "cost",
        "limit": 10
      }
    ],
    "filters": {
      "excludeTestKeys": true,
      "minCost": 10.00
    },
    "schedule": {
      "frequency": "monthly",
      "dayOfMonth": 1,
      "recipients": ["admin@company.com", "finance@company.com"]
    }
  }'
```

### Scheduled Reports

**Configure Automated Reports:**
```bash
curl -X PUT http://localhost:5002/api/admin/analytics/scheduled-reports \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "reports": [
      {
        "name": "Daily Operations Report",
        "schedule": "daily",
        "time": "09:00",
        "timezone": "UTC",
        "period": "24h",
        "recipients": ["ops@company.com"],
        "format": "pdf",
        "sections": ["summary", "alerts", "top_usage"]
      },
      {
        "name": "Weekly Cost Analysis",
        "schedule": "weekly",
        "dayOfWeek": "monday",
        "time": "08:00",
        "period": "7d",
        "recipients": ["finance@company.com"],
        "format": "excel",
        "sections": ["cost_breakdown", "trends", "forecasts"]
      }
    ]
  }'
```

## Security and Compliance

### Data Privacy

- **Data Anonymization**: Personal identifiers removed from analytics
- **Access Control**: Role-based access to usage data
- **Audit Logging**: All data access logged
- **GDPR Compliance**: Data deletion and export capabilities

### Audit Trails

```json
{
  "timestamp": "2024-01-15T10:30:00Z",
  "level": "Information",
  "message": "Usage analytics accessed",
  "properties": {
    "operation": "Analytics.GetUsage",
    "virtualKeyId": "550e8400-e29b-41d4-a716-446655440000",
    "period": "30d",
    "requestedBy": "admin",
    "ipAddress": "192.168.1.100",
    "correlationId": "req-abc123"
  }
}
```

## Troubleshooting

### Common Analytics Issues

**Missing Usage Data:**
```bash
# Check data ingestion status
curl -H "Authorization: Bearer your-master-key" \
  http://localhost:5002/api/admin/analytics/ingestion-status

# Verify data pipeline health
curl -H "Authorization: Bearer your-master-key" \
  http://localhost:5002/api/admin/analytics/pipeline-health
```

**Incorrect Cost Calculations:**
```bash
# Recalculate costs for specific period
curl -X POST http://localhost:5002/api/admin/analytics/recalculate-costs \
  -H "Authorization: Bearer your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "startDate": "2024-01-01",
    "endDate": "2024-01-31",
    "virtualKeys": ["550e8400-e29b-41d4-a716-446655440000"]
  }'
```

**Performance Issues:**
```bash
# Check analytics query performance
curl -H "Authorization: Bearer your-master-key" \
  "http://localhost:5002/api/admin/analytics/performance-metrics"
```

## Best Practices

### Usage Monitoring

1. **Regular Review**: Monitor usage patterns weekly
2. **Budget Alerts**: Set up proactive budget alerts
3. **Cost Optimization**: Review cost optimization recommendations monthly
4. **Anomaly Detection**: Enable automated anomaly detection
5. **Data Export**: Regular backups of usage data

### Performance Optimization

1. **Caching**: Analytics data cached appropriately
2. **Aggregation**: Pre-computed aggregations for common queries
3. **Indexing**: Optimized database indexes for analytics queries
4. **Archiving**: Old data archived to reduce query complexity

## Next Steps

- **Provider Configuration**: Optimize [provider settings](provider-configuration) based on analytics
- **Virtual Keys**: Adjust [virtual key limits](virtual-keys) based on usage patterns
- **WebUI Guide**: Use the [administrative interface](webui-guide) for visual analytics
- **Core APIs**: Understand how usage is tracked in [Core APIs](../core-apis/overview)