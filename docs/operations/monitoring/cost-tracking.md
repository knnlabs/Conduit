# Cost Observability Guide

*Comprehensive guide for cost tracking, monitoring, and management in Conduit*

**Last Updated**: 2025-01-07

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Metrics Reference](#metrics-reference)
- [Operational Procedures](#operational-procedures)
- [Configuration](#configuration)
- [Troubleshooting](#troubleshooting)

---

## Overview

The Conduit Cost Observability system provides comprehensive monitoring, tracking, and alerting for all cost-related operations across the platform. This enables real-time cost tracking, budget management, and operational insights for managing LLM usage costs at scale.

### Key Capabilities

- **Real-time cost tracking** across all providers and models
- **Budget management** with automated enforcement
- **Cost alerting** for anomalies and threshold breaches
- **Usage analytics** for optimization opportunities
- **Distributed metrics** with Prometheus integration

---

## Architecture

### System Components

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

### Core Services

**1. Cost Calculation Service**
Location: `ConduitLLM.Core/Services/CostCalculationService.cs`

- Calculates costs based on usage patterns
- Supports multiple pricing models (Standard, PerVideo, InferenceSteps, etc.)
- Handles batch processing discounts
- Processes refunds for failed operations

**2. Model Cost Service**
Location: `ConduitLLM.Configuration/Services/ModelCostService.cs`

- Manages model pricing configurations
- Supports time-based pricing changes
- Handles provider-specific pricing
- Caches frequently accessed pricing data

**3. Business Metrics Service**
Location: `ConduitLLM.Gateway/Services/BusinessMetricsService.cs`

- Tracks virtual key spend
- Monitors budget utilization
- Aggregates model and provider costs
- Exports Prometheus metrics

**4. Redis Model Cost Cache**
Location: `ConduitLLM.Gateway/Services/RedisModelCostCache.cs`

- 5-minute cache TTL
- Automatic invalidation on cost updates
- Distributed cache for multi-instance deployments
- Fallback to database on cache miss

### Data Flow

**Request Processing Flow**:
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

**Cost Calculation Flow**:
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

---

## Metrics Reference

### Metric Naming Convention

All Conduit metrics follow the Prometheus naming convention:
- Prefix: `conduit_`
- Component: `cost_`, `virtualkey_`, `model_`, etc.
- Measurement: descriptive name
- Unit: `_total`, `_dollars`, `_percent`, `_seconds`

### Core Cost Metrics

#### conduit_cost_total_dollars

**Type**: Counter
**Unit**: US Dollars
**Description**: Cumulative cost of all operations

**Labels**:
- `provider`: LLM provider (openai, anthropic, groq, etc.)
- `model`: Model identifier (gpt-4, claude-3, etc.)
- `operation_type`: Operation type (completion, embedding, image, video)

**Example Queries**:
```promql
# Total cost in last hour
increase(conduit_cost_total_dollars[1h])

# Cost by provider in last 24h
sum by (provider) (increase(conduit_cost_total_dollars[24h]))

# Top 5 most expensive models today
topk(5, sum by (model) (increase(conduit_cost_total_dollars[1d])))
```

#### conduit_cost_rate_dollars_per_minute

**Type**: Gauge
**Unit**: Dollars per minute
**Description**: Current cost burn rate

**Labels**:
- `provider`: LLM provider name

**Example Queries**:
```promql
# Current total burn rate
sum(conduit_cost_rate_dollars_per_minute)

# Providers with burn rate > $10/min
conduit_cost_rate_dollars_per_minute > 10
```

#### conduit_virtualkey_spend_total

**Type**: Gauge
**Unit**: US Dollars
**Description**: Total spend per virtual key

**Labels**:
- `virtual_key_id`: Virtual key identifier

**Example Queries**:
```promql
# Top 10 spending keys
topk(10, conduit_virtualkey_spend_total)

# Keys approaching budget (>80%)
conduit_virtualkey_budget_utilization_percent > 80
```

#### conduit_virtualkey_budget_utilization_percent

**Type**: Gauge
**Unit**: Percent
**Description**: Budget usage percentage per virtual key

**Labels**:
- `virtual_key_id`: Virtual key identifier

**Example Queries**:
```promql
# Keys over budget
conduit_virtualkey_budget_utilization_percent >= 100

# Average budget utilization
avg(conduit_virtualkey_budget_utilization_percent)
```

### Usage Metrics

#### conduit_model_tokens_total

**Type**: Counter
**Unit**: Tokens
**Description**: Total tokens processed

**Labels**:
- `model`: Model identifier
- `provider`: LLM provider
- `token_type`: input, output, or cached

#### conduit_model_requests_total

**Type**: Counter
**Unit**: Requests
**Description**: Total model requests

**Labels**:
- `model`: Model identifier
- `provider`: LLM provider
- `status`: success or error

### Performance Metrics

#### conduit_cost_calculation_duration_ms

**Type**: Histogram
**Unit**: Milliseconds
**Description**: Cost calculation latency

**Labels**:
- `pricing_model`: Type of pricing model

#### conduit_cost_cache_hit_ratio

**Type**: Gauge
**Unit**: Ratio (0-1)
**Description**: Cache effectiveness

**Example Query**:
```promql
# Cache hit percentage
conduit_cost_cache_hit_ratio * 100
```

---

## Operational Procedures

### Daily Operations

#### Morning Health Check (9:00 AM)

**Objective**: Ensure cost tracking systems are functioning correctly

**Procedure**:

1. **Check System Status**
   ```bash
   # Run morning health check script
   ./scripts/daily-health-check.sh

   # Expected output:
   # ✓ Prometheus: UP
   # ✓ Grafana: UP
   # ✓ AlertManager: UP
   # ✓ Redis Cache: UP (97% hit ratio)
   # ✓ Batch Processor: UP (lag: 5s)
   ```

2. **Review Overnight Alerts**
   ```bash
   # Check alert history
   curl -s http://alertmanager:9093/api/v1/alerts | jq '.data[] | {name: .labels.alertname, time: .startsAt}'
   ```

3. **Verify Cost Metrics**
   ```promql
   # Check last 24h cost
   sum(increase(conduit_cost_total_dollars[24h]))

   # Verify all providers reporting
   count(sum by (provider) (rate(conduit_cost_total_dollars[1h])))
   ```

4. **Check Budget Status**
   ```sql
   -- Keys approaching limits
   SELECT
     id,
     name,
     budget_amount,
     current_period_spend,
     (current_period_spend / budget_amount * 100) as utilization_percent
   FROM virtual_keys
   WHERE (current_period_spend / budget_amount) > 0.7
   ORDER BY utilization_percent DESC;
   ```

#### Cost Report Generation (2:00 PM)

**Objective**: Generate daily cost reports for stakeholders

**Procedure**:

1. **Generate Executive Summary**
   ```bash
   ./scripts/generate-cost-report.sh \
     --type daily \
     --format pdf \
     --recipients "executives@conduit.ai" \
     --include-forecast
   ```

2. **Create Department Breakdowns**
   ```sql
   SELECT
     vk.department,
     COUNT(DISTINCT vk.id) as active_keys,
     SUM(ul.total_cost) as daily_cost,
     AVG(ul.total_cost) as avg_per_request
   FROM virtual_keys vk
   JOIN llm_usage_log ul ON vk.id = ul.virtual_key_id
   WHERE ul.created_at > CURRENT_DATE
   GROUP BY vk.department
   ORDER BY daily_cost DESC;
   ```

### Weekly Procedures

#### Monday: Budget Review (10:00 AM)

Review and adjust budgets based on usage patterns:

```bash
# Generate budget utilization report
./scripts/analyze-budget-utilization.sh --period weekly --output report.pdf

# Identify keys needing budget adjustments
./scripts/recommend-budget-changes.sh --threshold 90
```

#### Wednesday: Cost Optimization Analysis

Identify cost optimization opportunities:

```promql
# Find expensive low-usage models
sum by (model) (increase(conduit_cost_total_dollars[7d])) /
sum by (model) (increase(conduit_model_requests_total[7d]))
```

#### Friday: Cache Performance Review

```promql
# Weekly cache hit ratio
avg_over_time(conduit_cost_cache_hit_ratio[7d]) * 100

# Identify cache misses by model
topk(10, sum by (model) (increase(conduit_cost_cache_misses_total[7d])))
```

### Monthly Procedures

#### Cost Trend Analysis

```bash
# Generate monthly cost trends
./scripts/cost-trend-analysis.sh --month $(date +%Y-%m) --compare-previous

# Provider cost comparison
./scripts/provider-cost-comparison.sh --period monthly
```

#### Budget Period Resets

```sql
-- Reset monthly budgets
UPDATE virtual_keys
SET current_period_spend = 0,
    budget_period_start = CURRENT_DATE
WHERE budget_period = 'monthly'
  AND budget_period_start < DATE_TRUNC('month', CURRENT_DATE);
```

---

## Configuration

### Environment Variables

```bash
# Core monitoring settings
export CONDUITLLM__MONITORING__ENABLED=true
export CONDUITLLM__MONITORING__PROMETHEUS__ENABLED=true
export CONDUITLLM__MONITORING__PROMETHEUS__PORT=9090

# Metric collection intervals
export CONDUITLLM__MONITORING__INFRASTRUCTURE__INTERVAL=15
export CONDUITLLM__MONITORING__BUSINESS__INTERVAL=60
export CONDUITLLM__MONITORING__TASK__INTERVAL=30
```

### Prometheus Configuration

Create `monitoring/prometheus/prometheus.yml`:

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

alerting:
  alertmanagers:
    - static_configs:
        - targets: ['alertmanager:9093']

rule_files:
  - "cost-alerts.yml"

scrape_configs:
  - job_name: 'conduit-api'
    static_configs:
      - targets: ['api:80']
    metrics_path: '/metrics'
    scrape_interval: 15s

  - job_name: 'conduit-admin'
    static_configs:
      - targets: ['admin:80']
    metrics_path: '/metrics'
    scrape_interval: 15s
    bearer_token: 'your-admin-api-key'
```

### Alert Rules

Create `monitoring/prometheus/cost-alerts.yml`:

```yaml
groups:
  - name: cost_alerts
    interval: 30s
    rules:
      # Critical: High burn rate
      - alert: HighCostBurnRate
        expr: sum(conduit_cost_rate_dollars_per_minute) > 100
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "Cost burn rate exceeds $100/minute"
          description: "Current burn rate: {{ $value }}/min"

      # High: Budget exceeded
      - alert: VirtualKeyBudgetExceeded
        expr: conduit_virtualkey_budget_utilization_percent >= 100
        for: 1m
        labels:
          severity: high
        annotations:
          summary: "Virtual key {{ $labels.virtual_key_id }} exceeded budget"

      # Warning: Budget approaching
      - alert: VirtualKeyBudgetWarning
        expr: conduit_virtualkey_budget_utilization_percent > 80
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Virtual key {{ $labels.virtual_key_id }} at {{ $value }}% of budget"
```

### Cache Configuration

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
```

---

## Troubleshooting

### High Cache Miss Rate

**Symptoms**: Cache hit ratio < 80%

**Resolution**:

1. **Check Cache Health**
   ```bash
   redis-cli INFO stats | grep -E "keyspace_hits|keyspace_misses"
   ```

2. **Identify Miss Patterns**
   ```promql
   topk(10, sum by (model) (increase(conduit_cost_cache_misses_total[1h])))
   ```

3. **Warm Cache**
   ```bash
   ./scripts/warm-cost-cache.sh --top-models 100
   ```

### Batch Processing Lag

**Symptoms**: `conduit_batch_spend_update_lag_seconds` > 30

**Resolution**:

1. **Check Queue Size**
   ```promql
   conduit_batch_spend_update_size
   ```

2. **Force Immediate Processing**
   ```bash
   ./scripts/process-spend-batch.sh --force --timeout 60
   ```

3. **Review Database Performance**
   ```sql
   SELECT pid, query, state_change
   FROM pg_stat_activity
   WHERE query LIKE '%spend%';
   ```

### Inaccurate Cost Tracking

**Symptoms**: Spend doesn't match usage logs

**Resolution**:

1. **Verify Calculation**
   ```sql
   SELECT
     vk.id,
     vk.total_spend as recorded,
     SUM(ul.total_cost) as calculated,
     ABS(vk.total_spend - SUM(ul.total_cost)) as difference
   FROM virtual_keys vk
   JOIN llm_usage_log ul ON vk.id = ul.virtual_key_id
   WHERE ul.created_at > NOW() - INTERVAL '1 hour'
   GROUP BY vk.id
   HAVING ABS(vk.total_spend - SUM(ul.total_cost)) > 0.01;
   ```

2. **Check for Failed Batches**
   ```bash
   tail -100 /var/log/conduit/batch-processor.log | grep ERROR
   ```

---

## Related Documentation

- **[Alert Runbooks](./runbooks/cost-observability-alerts.md)** - Incident response procedures for cost alerts
- **[Monitoring Setup](./monitoring-setup.md)** - General Prometheus/Grafana configuration
- **[Performance Metrics](./performance-metrics.md)** - System performance tracking

---

**For incident response and alert handling, see [Cost Observability Alert Runbooks](./runbooks/cost-observability-alerts.md)**
