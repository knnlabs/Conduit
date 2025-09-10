# Cost Observability Architecture

## Executive Summary

The Conduit Cost Observability system provides comprehensive monitoring, tracking, and alerting for all cost-related operations across the platform. This architecture enables real-time cost tracking, budget management, and operational insights for managing LLM usage costs at scale.

## System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         User Requests                            │
└──────────────────────────────┬──────────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────┐
│                      Cost Calculation Layer                      │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐         │
│  │ CostCalculation│  │ ModelCost    │  │ Usage        │         │
│  │ Service       │  │ Service      │  │ Tracking     │         │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘         │
└─────────┴──────────────────┴─────────────────┴─────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────┐
│                    Metrics Collection Layer                      │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐         │
│  │ Business     │  │ Cost         │  │ Performance  │         │
│  │ Metrics      │  │ Metrics      │  │ Metrics      │         │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘         │
└─────────┴──────────────────┴─────────────────┴─────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────┐
│                     Prometheus Exporter                          │
│                    /metrics endpoint                             │
└──────────────────────────────┬──────────────────────────────────┘
                               │
                    ┌──────────┴──────────┐
                    │                     │
         ┌──────────▼────────┐  ┌────────▼──────────┐
         │  Prometheus Server │  │  Grafana          │
         │  (TSDB)           │◄─┤  Dashboards       │
         └──────────┬────────┘  └───────────────────┘
                    │
         ┌──────────▼────────┐
         │  AlertManager     │
         │  Rules & Routing  │
         └───────────────────┘
```

## Core Components

### 1. Cost Calculation Service

**Location**: `ConduitLLM.Core/Services/CostCalculationService.cs`

**Responsibilities**:
- Calculate costs based on usage patterns
- Support multiple pricing models (Standard, PerVideo, InferenceSteps, etc.)
- Handle batch processing discounts
- Process refunds for failed operations

**Key Features**:
- Polymorphic pricing model support
- Real-time cost calculation
- Cached pricing data for performance
- Audit trail for all calculations

### 2. Model Cost Service

**Location**: `ConduitLLM.Configuration/Services/ModelCostService.cs`

**Responsibilities**:
- Manage model pricing configurations
- Support time-based pricing changes
- Handle provider-specific pricing
- Cache frequently accessed pricing data

**Database Schema**:
```sql
ModelCost
├── Id (PK)
├── CostName
├── InputTokenCost
├── OutputTokenCost
├── PricingModel
├── EffectiveDate
├── ExpiryDate
└── Priority

ModelCostMapping
├── Id (PK)
├── ModelCostId (FK)
├── ModelProviderMappingId (FK)
└── IsActive
```

### 3. Business Metrics Service

**Location**: `ConduitLLM.Http/Services/BusinessMetricsService.cs`

**Metrics Collected**:
- Virtual key spend tracking
- Budget utilization percentages
- Model request counts and costs
- Provider cost aggregation
- Token consumption metrics

**Export Format**: Prometheus metrics with labels for granular filtering

### 4. Redis Model Cost Cache

**Location**: `ConduitLLM.Http/Services/RedisModelCostCache.cs`

**Purpose**: High-performance caching layer for cost lookups

**Features**:
- 5-minute cache TTL
- Automatic invalidation on cost updates
- Distributed cache for multi-instance deployments
- Fallback to database on cache miss

## Data Flow

### 1. Request Processing Flow

```
User Request
    ↓
Virtual Key Validation
    ↓
Model Selection
    ↓
Provider Routing
    ↓
LLM API Call
    ↓
Usage Extraction
    ↓
Cost Calculation ←── [Model Cost Cache]
    ↓
Spend Update ←────── [Batch Update Service]
    ↓
Metrics Export
```

### 2. Cost Calculation Flow

```
Usage Data (tokens, images, etc.)
    ↓
Pricing Model Detection
    ↓
Model Cost Lookup
    ├── Check Redis Cache
    └── Fallback to Database
    ↓
Apply Pricing Formula
    ├── Standard: tokens × cost
    ├── PerImage: images × cost
    ├── InferenceSteps: steps × cost
    └── Tiered: volume-based pricing
    ↓
Apply Modifiers
    ├── Batch Processing Discount
    ├── Cached Token Discount
    └── Priority Multiplier
    ↓
Return Total Cost
```

## Metrics Taxonomy

### Cost Metrics

| Metric Name | Type | Labels | Description |
|------------|------|--------|-------------|
| `conduit_cost_total_dollars` | Counter | provider, model, operation_type | Cumulative cost in dollars |
| `conduit_cost_rate_dollars_per_minute` | Gauge | provider | Current cost burn rate |
| `conduit_cost_per_request_dollars` | Histogram | model, provider | Cost distribution per request |
| `conduit_virtualkey_spend_total` | Gauge | virtual_key_id | Total spend per virtual key |
| `conduit_virtualkey_budget_utilization_percent` | Gauge | virtual_key_id | Budget usage percentage |

### Usage Metrics

| Metric Name | Type | Labels | Description |
|------------|------|--------|-------------|
| `conduit_model_tokens_total` | Counter | model, provider, token_type | Total tokens processed |
| `conduit_model_requests_total` | Counter | model, provider, status | Total model requests |
| `conduit_virtualkey_requests_total` | Counter | virtual_key_id, model, status | Requests per virtual key |

### Performance Metrics

| Metric Name | Type | Labels | Description |
|------------|------|--------|-------------|
| `conduit_cost_calculation_duration_ms` | Histogram | pricing_model | Cost calculation latency |
| `conduit_cost_cache_hit_ratio` | Gauge | - | Cache effectiveness |
| `conduit_batch_update_lag_seconds` | Gauge | - | Spend update delay |

## Caching Strategy

### Redis Cache Layers

1. **Model Cost Cache**
   - TTL: 5 minutes
   - Key Pattern: `model:cost:{modelId}:{providerId}`
   - Invalidation: On cost configuration change

2. **Virtual Key Spend Cache**
   - TTL: 30 seconds
   - Key Pattern: `vkey:spend:{virtualKeyId}`
   - Update: Batch processing every 10 seconds

3. **Budget Alert Cache**
   - TTL: 1 minute
   - Key Pattern: `vkey:budget:alert:{virtualKeyId}`
   - Purpose: Prevent alert flooding

### Cache Invalidation Strategy

```
Cost Configuration Update
    ↓
Database Transaction Commit
    ↓
Publish Invalidation Event
    ↓
┌────────────┬────────────┬────────────┐
│ Clear Redis │ Update     │ Notify     │
│ Cache Keys  │ Prometheus │ Consumers  │
└────────────┴────────────┴────────────┘
```

## Batch Processing

### Spend Update Batching

**Service**: `BatchSpendUpdateService`

**Configuration**:
```json
{
  "BatchProcessing": {
    "BatchSize": 100,
    "FlushInterval": "00:00:10",
    "MaxRetries": 3,
    "RetryDelay": "00:00:01"
  }
}
```

**Benefits**:
- Reduces database write load by 90%
- Improves response time by 200ms
- Enables efficient bulk updates
- Maintains eventual consistency

## Alerting Thresholds

### Critical Alerts

| Alert | Condition | Duration | Action |
|-------|-----------|----------|--------|
| Budget Exceeded | utilization > 100% | Immediate | Block requests, notify admin |
| High Burn Rate | cost_rate > $100/min | 5 min | Page on-call, investigate |
| Cost Spike | cost_increase > 500% | 10 min | Alert team, review traffic |

### Warning Alerts

| Alert | Condition | Duration | Action |
|-------|-----------|----------|--------|
| Budget Warning | utilization > 80% | 5 min | Email notification |
| Unusual Activity | requests > 3σ | 15 min | Monitor, prepare scaling |
| Cache Miss Rate | hit_ratio < 80% | 10 min | Check Redis health |

## Security Considerations

### Data Protection

1. **Cost Data Encryption**
   - At rest: AES-256 in database
   - In transit: TLS 1.3
   - In cache: Redis AUTH + SSL

2. **Access Control**
   - Role-based metrics access
   - API key scoping for cost data
   - Audit logging for cost changes

3. **PII Handling**
   - No PII in metrics labels
   - Virtual key IDs are hashed
   - User identifiers are anonymized

### Rate Limiting

```yaml
RateLimits:
  MetricsEndpoint:
    RequestsPerMinute: 60
    BurstSize: 10
  CostAPI:
    RequestsPerMinute: 1000
    BurstSize: 100
```

## Scalability Considerations

### Horizontal Scaling

- **Stateless Services**: All cost calculation services are stateless
- **Distributed Cache**: Redis cluster for cache layer
- **Load Balancing**: Round-robin for API endpoints
- **Database Sharding**: Virtual key ID-based sharding

### Performance Targets

| Metric | Target | Current |
|--------|--------|---------|
| Cost Calculation Latency | < 10ms | 7ms p99 |
| Metrics Export Time | < 100ms | 45ms p99 |
| Cache Hit Ratio | > 95% | 97.3% |
| Batch Update Latency | < 15s | 10s p99 |

## Disaster Recovery

### Backup Strategy

1. **Metrics Data**
   - Prometheus snapshots every 6 hours
   - 30-day retention in cold storage
   - Point-in-time recovery capability

2. **Cost Configuration**
   - Database backups every hour
   - Configuration versioning in Git
   - Automated restore testing

### Failure Modes

| Component | Failure Impact | Recovery Strategy |
|-----------|---------------|-------------------|
| Redis Cache | Increased latency | Fallback to database |
| Prometheus | No metrics collection | Buffer in memory, replay |
| Cost Service | No cost tracking | Circuit breaker, queue requests |
| Database | Complete outage | Failover to replica |

## Integration Points

### External Systems

1. **Billing System**
   - Webhook on spend thresholds
   - Daily cost reports via API
   - Invoice generation triggers

2. **Admin Dashboard**
   - Real-time cost widgets
   - Budget management UI
   - Alert configuration

3. **Customer Portal**
   - Usage dashboard
   - Cost breakdown views
   - Budget alerts

### API Endpoints

```yaml
Cost APIs:
  - GET /api/v1/costs/current
  - GET /api/v1/costs/history
  - POST /api/v1/costs/calculate
  - GET /api/v1/budgets/status
  
Metrics APIs:
  - GET /metrics (Prometheus)
  - GET /api/v1/metrics/summary
  - GET /api/v1/metrics/export
```

## Monitoring the Monitoring

### Meta-Metrics

- Prometheus scrape duration
- Metrics collection errors
- Cache operation latency
- Alert delivery success rate

### Health Checks

```yaml
HealthChecks:
  - Name: cost-calculation
    Endpoint: /health/cost
    Interval: 30s
    
  - Name: metrics-export
    Endpoint: /health/metrics
    Interval: 60s
    
  - Name: cache-connectivity
    Endpoint: /health/redis
    Interval: 15s
```

## Future Enhancements

### Planned Features

1. **Machine Learning Cost Prediction**
   - Predict daily/monthly spend
   - Anomaly detection for cost spikes
   - Usage pattern analysis

2. **Advanced Alerting**
   - Predictive budget alerts
   - Custom alert routing
   - Integration with PagerDuty/Slack

3. **Cost Optimization**
   - Model recommendation engine
   - Batch processing suggestions
   - Provider cost comparison

### Technical Debt

1. **Known Issues**
   - Cache invalidation race condition (rare)
   - Metrics cardinality with 10K+ keys
   - Batch processing lag during peaks

2. **Improvement Opportunities**
   - Implement ClickHouse for long-term storage
   - Add distributed tracing for cost flow
   - Optimize database queries for reports

## Appendix

### Configuration Reference

```yaml
CostObservability:
  Enabled: true
  
  Metrics:
    ExportInterval: 15s
    RetentionDays: 30
    
  Cache:
    Provider: Redis
    TTL: 300s
    MaxSize: 10GB
    
  Batching:
    Size: 100
    Interval: 10s
    
  Alerting:
    Provider: Prometheus
    EvaluationInterval: 30s
```

### Useful Queries

```promql
# Daily cost by provider
sum(rate(conduit_cost_total_dollars[1d])) by (provider)

# Budget utilization top 10
topk(10, conduit_virtualkey_budget_utilization_percent)

# Cost per request p99
histogram_quantile(0.99, conduit_cost_per_request_dollars)

# Hourly burn rate
sum(rate(conduit_cost_total_dollars[1h])) * 3600
```