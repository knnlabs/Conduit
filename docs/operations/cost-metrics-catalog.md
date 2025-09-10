# Cost Observability Metrics Reference Catalog

## Overview

This catalog provides a comprehensive reference for all cost-related metrics exposed by the Conduit platform. Each metric is documented with its purpose, labels, aggregation methods, and example queries.

## Metric Naming Convention

All Conduit metrics follow the Prometheus naming convention:
- Prefix: `conduit_`
- Component: `cost_`, `virtualkey_`, `model_`, etc.
- Measurement: descriptive name
- Unit: `_total`, `_dollars`, `_percent`, `_seconds`

## Core Cost Metrics

### conduit_cost_total_dollars

**Type**: Counter  
**Unit**: US Dollars  
**Description**: Cumulative cost of all operations  

**Labels**:
- `provider`: LLM provider name (openai, anthropic, groq, etc.)
- `model`: Model identifier (gpt-4, claude-3, llama-3-70b, etc.)
- `operation_type`: Type of operation (completion, embedding, image, video)

**Example Queries**:
```promql
# Total cost in last hour
increase(conduit_cost_total_dollars[1h])

# Cost by provider in last 24h
sum by (provider) (increase(conduit_cost_total_dollars[24h]))

# Top 5 most expensive models today
topk(5, sum by (model) (increase(conduit_cost_total_dollars[1d])))
```

**Recording Rules**:
```yaml
- record: conduit:cost_rate_1m
  expr: rate(conduit_cost_total_dollars[1m])
  
- record: conduit:cost_by_provider_5m
  expr: sum by (provider) (rate(conduit_cost_total_dollars[5m]))
```

---

### conduit_cost_rate_dollars_per_minute

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

**Alert Rule**:
```yaml
- alert: HighCostBurnRate
  expr: sum(conduit_cost_rate_dollars_per_minute) > 100
  for: 5m
  annotations:
    summary: "Cost burn rate exceeds $100/minute"
```

---

### conduit_cost_per_request_dollars

**Type**: Histogram  
**Unit**: US Dollars  
**Description**: Distribution of costs per request  

**Labels**:
- `model`: Model identifier
- `provider`: LLM provider name

**Buckets**: 0.001, 0.01, 0.1, 0.5, 1, 5, 10, 50, 100

**Example Queries**:
```promql
# Median cost per request
histogram_quantile(0.5, sum(rate(conduit_cost_per_request_dollars_bucket[5m])) by (le))

# 99th percentile cost by model
histogram_quantile(0.99, sum(rate(conduit_cost_per_request_dollars_bucket[5m])) by (model, le))

# Requests costing more than $10
sum(rate(conduit_cost_per_request_dollars_bucket{le="10"}[5m])) 
- sum(rate(conduit_cost_per_request_dollars_bucket{le="100"}[5m]))
```

---

## Virtual Key Metrics

### conduit_virtualkey_spend_total

**Type**: Gauge  
**Unit**: US Dollars  
**Description**: Total accumulated spend for each virtual key  

**Labels**:
- `virtual_key_id`: Hashed virtual key identifier

**Example Queries**:
```promql
# Top 10 spending keys
topk(10, conduit_virtualkey_spend_total)

# Keys with spend > $1000
conduit_virtualkey_spend_total > 1000

# Total spend across all keys
sum(conduit_virtualkey_spend_total)
```

**Recording Rules**:
```yaml
- record: conduit:virtualkey_spend_increase_1h
  expr: increase(conduit_virtualkey_spend_total[1h])
```

---

### conduit_virtualkey_budget_utilization_percent

**Type**: Gauge  
**Unit**: Percentage (0-100+)  
**Description**: Percentage of budget consumed  

**Labels**:
- `virtual_key_id`: Hashed virtual key identifier

**Example Queries**:
```promql
# Keys approaching budget limit (>80%)
conduit_virtualkey_budget_utilization_percent > 80

# Average budget utilization
avg(conduit_virtualkey_budget_utilization_percent)

# Keys over budget
conduit_virtualkey_budget_utilization_percent > 100
```

**Alert Rules**:
```yaml
- alert: VirtualKeyBudgetWarning
  expr: conduit_virtualkey_budget_utilization_percent > 80
  for: 5m
  labels:
    severity: warning
    
- alert: VirtualKeyBudgetExceeded
  expr: conduit_virtualkey_budget_utilization_percent >= 100
  for: 1m
  labels:
    severity: critical
```

---

### conduit_virtualkey_budget_exceeded_total

**Type**: Counter  
**Description**: Count of budget exceeded events  

**Labels**:
- `virtual_key_id`: Hashed virtual key identifier

**Example Queries**:
```promql
# Budget violations in last hour
increase(conduit_virtualkey_budget_exceeded_total[1h])

# Keys with repeated violations
topk(5, increase(conduit_virtualkey_budget_exceeded_total[24h]))
```

---

### conduit_virtualkey_requests_total

**Type**: Counter  
**Description**: Total requests per virtual key  

**Labels**:
- `virtual_key_id`: Hashed virtual key identifier
- `model`: Model used
- `status`: success, failure, rate_limited, budget_exceeded

**Example Queries**:
```promql
# Request rate by key
rate(conduit_virtualkey_requests_total[5m])

# Failure rate by key
rate(conduit_virtualkey_requests_total{status!="success"}[5m]) 
/ rate(conduit_virtualkey_requests_total[5m])

# Most active keys
topk(10, sum by (virtual_key_id) (rate(conduit_virtualkey_requests_total[5m])))
```

---

## Model Usage Metrics

### conduit_model_requests_total

**Type**: Counter  
**Description**: Total requests per model  

**Labels**:
- `model`: Model identifier
- `provider`: Provider name
- `status`: success, failure, timeout, rate_limited

**Example Queries**:
```promql
# Model request rates
sum by (model) (rate(conduit_model_requests_total[5m]))

# Model error rates
sum by (model) (rate(conduit_model_requests_total{status!="success"}[5m]))
/ sum by (model) (rate(conduit_model_requests_total[5m]))

# Provider availability
1 - (sum by (provider) (rate(conduit_model_requests_total{status="failure"}[5m]))
/ sum by (provider) (rate(conduit_model_requests_total[5m])))
```

---

### conduit_model_tokens_total

**Type**: Counter  
**Unit**: Tokens  
**Description**: Total tokens processed  

**Labels**:
- `model`: Model identifier
- `provider`: Provider name
- `token_type`: prompt, completion, cached_prompt, cached_write

**Example Queries**:
```promql
# Token throughput by model
rate(conduit_model_tokens_total[5m])

# Prompt vs completion ratio
sum(rate(conduit_model_tokens_total{token_type="prompt"}[5m]))
/ sum(rate(conduit_model_tokens_total{token_type="completion"}[5m]))

# Cached token savings
sum(rate(conduit_model_tokens_total{token_type="cached_prompt"}[1h]))
/ sum(rate(conduit_model_tokens_total{token_type="prompt"}[1h]))
```

**Recording Rules**:
```yaml
- record: conduit:token_rate_by_model
  expr: sum by (model) (rate(conduit_model_tokens_total[5m]))
  
- record: conduit:cached_token_ratio
  expr: |
    sum(rate(conduit_model_tokens_total{token_type="cached_prompt"}[5m]))
    / sum(rate(conduit_model_tokens_total{token_type=~"prompt|cached_prompt"}[5m]))
```

---

### conduit_model_response_time_seconds

**Type**: Histogram  
**Unit**: Seconds  
**Description**: Model response latency  

**Labels**:
- `model`: Model identifier
- `provider`: Provider name

**Buckets**: 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60, 120, 300, 600

**Example Queries**:
```promql
# P95 latency by model
histogram_quantile(0.95, 
  sum(rate(conduit_model_response_time_seconds_bucket[5m])) by (model, le)
)

# Models with P99 > 30s
histogram_quantile(0.99,
  sum(rate(conduit_model_response_time_seconds_bucket[5m])) by (model, le)
) > 30
```

---

## Provider Metrics

### conduit_provider_health

**Type**: Gauge  
**Description**: Provider health status (1=healthy, 0=unhealthy)  

**Labels**:
- `provider`: Provider name

**Example Queries**:
```promql
# Unhealthy providers
conduit_provider_health == 0

# Provider uptime percentage
avg_over_time(conduit_provider_health[1h]) * 100
```

---

### conduit_provider_errors_total

**Type**: Counter  
**Description**: Total provider errors  

**Labels**:
- `provider`: Provider name
- `error_type`: rate_limit, timeout, api_error, network_error

**Example Queries**:
```promql
# Error rate by provider
sum by (provider) (rate(conduit_provider_errors_total[5m]))

# Rate limit errors
rate(conduit_provider_errors_total{error_type="rate_limit"}[5m])
```

---

### conduit_provider_rate_limit_remaining

**Type**: Gauge  
**Description**: Remaining rate limit capacity  

**Labels**:
- `provider`: Provider name
- `limit_type`: requests_per_minute, tokens_per_minute

**Example Queries**:
```promql
# Providers near rate limit
conduit_provider_rate_limit_remaining < 100

# Rate limit utilization
1 - (conduit_provider_rate_limit_remaining / conduit_provider_rate_limit_total)
```

---

## Cache Metrics

### conduit_cost_cache_hits_total / conduit_cost_cache_misses_total

**Type**: Counter  
**Description**: Cache hit/miss counts for cost lookups  

**Labels**: None

**Example Queries**:
```promql
# Cache hit ratio
sum(rate(conduit_cost_cache_hits_total[5m]))
/ (sum(rate(conduit_cost_cache_hits_total[5m])) + sum(rate(conduit_cost_cache_misses_total[5m])))

# Cache miss rate
rate(conduit_cost_cache_misses_total[5m])
```

---

### conduit_cost_cache_operation_duration_seconds

**Type**: Histogram  
**Unit**: Seconds  
**Description**: Cache operation latency  

**Labels**:
- `operation`: get, set, delete, flush

**Example Queries**:
```promql
# P99 cache latency
histogram_quantile(0.99,
  sum(rate(conduit_cost_cache_operation_duration_seconds_bucket[5m])) by (operation, le)
)
```

---

## Batch Processing Metrics

### conduit_batch_spend_update_size

**Type**: Histogram  
**Description**: Size of spend update batches  

**Labels**: None

**Buckets**: 1, 10, 25, 50, 100, 250, 500, 1000

**Example Queries**:
```promql
# Average batch size
histogram_quantile(0.5,
  sum(rate(conduit_batch_spend_update_size_bucket[5m])) by (le)
)
```

---

### conduit_batch_spend_update_lag_seconds

**Type**: Gauge  
**Unit**: Seconds  
**Description**: Delay between cost incurrence and database update  

**Labels**: None

**Example Queries**:
```promql
# Current update lag
conduit_batch_spend_update_lag_seconds

# Alert on high lag
conduit_batch_spend_update_lag_seconds > 30
```

---

### conduit_batch_spend_update_failures_total

**Type**: Counter  
**Description**: Failed batch update attempts  

**Labels**:
- `reason`: database_error, validation_error, timeout

**Example Queries**:
```promql
# Batch failure rate
rate(conduit_batch_spend_update_failures_total[5m])
```

---

## System Metrics

### conduit_cost_calculation_duration_ms

**Type**: Histogram  
**Unit**: Milliseconds  
**Description**: Time to calculate cost  

**Labels**:
- `pricing_model`: standard, per_video, inference_steps, tiered

**Buckets**: 0.1, 0.5, 1, 5, 10, 25, 50, 100

**Example Queries**:
```promql
# P95 calculation time by pricing model
histogram_quantile(0.95,
  sum(rate(conduit_cost_calculation_duration_ms_bucket[5m])) by (pricing_model, le)
)
```

---

### conduit_cost_service_active_calculations

**Type**: Gauge  
**Description**: Number of concurrent cost calculations  

**Labels**: None

**Example Queries**:
```promql
# Current active calculations
conduit_cost_service_active_calculations

# Peak calculations
max_over_time(conduit_cost_service_active_calculations[1h])
```

---

## Business Intelligence Metrics

### conduit_revenue_total_dollars

**Type**: Counter  
**Unit**: US Dollars  
**Description**: Total revenue (markup over cost)  

**Labels**:
- `customer_tier`: free, starter, pro, enterprise

**Example Queries**:
```promql
# Daily revenue
increase(conduit_revenue_total_dollars[1d])

# Revenue by tier
sum by (customer_tier) (increase(conduit_revenue_total_dollars[1d]))
```

---

### conduit_margin_percent

**Type**: Gauge  
**Unit**: Percentage  
**Description**: Profit margin percentage  

**Labels**:
- `provider`: Provider name

**Example Queries**:
```promql
# Average margin
avg(conduit_margin_percent)

# Providers with low margin
conduit_margin_percent < 10
```

---

## Aggregation and Recording Rules

### Essential Recording Rules

```yaml
groups:
  - name: cost_aggregations
    interval: 30s
    rules:
      # Hourly cost rate
      - record: conduit:hourly_cost_rate
        expr: sum(rate(conduit_cost_total_dollars[1h])) * 3600
        
      # Daily cost projection
      - record: conduit:daily_cost_projection
        expr: sum(rate(conduit_cost_total_dollars[3h])) * 86400
        
      # Cost efficiency (tokens per dollar)
      - record: conduit:tokens_per_dollar
        expr: |
          sum(rate(conduit_model_tokens_total[5m]))
          / sum(rate(conduit_cost_total_dollars[5m]))
          
      # Virtual key risk score (high spend + high utilization)
      - record: conduit:virtualkey_risk_score
        expr: |
          (conduit_virtualkey_budget_utilization_percent / 100)
          * (rate(conduit_virtualkey_spend_total[1h]) / 10)
```

---

## Grafana Variable Definitions

### Dashboard Variables

```yaml
variables:
  - name: provider
    query: label_values(conduit_cost_total_dollars, provider)
    multi: true
    includeAll: true
    
  - name: model
    query: label_values(conduit_model_requests_total{provider=~"$provider"}, model)
    multi: true
    includeAll: true
    
  - name: virtual_key
    query: label_values(conduit_virtualkey_spend_total, virtual_key_id)
    multi: false
    includeAll: false
    
  - name: time_range
    type: interval
    options: [1m, 5m, 15m, 1h, 6h, 24h, 7d, 30d]
    default: 1h
```

---

## Best Practices

### Label Cardinality Management

**DO:**
- Use hashed IDs for high-cardinality labels
- Limit provider/model labels to known values
- Aggregate before exposing metrics

**DON'T:**
- Include user IDs directly
- Add unbounded labels (request IDs, timestamps)
- Create metrics per individual user

### Query Optimization

**Efficient Queries:**
```promql
# Good: Pre-aggregate with recording rules
conduit:hourly_cost_rate

# Bad: Calculate on every dashboard refresh
sum(rate(conduit_cost_total_dollars[1h])) * 3600
```

**Index Usage:**
```promql
# Good: Filter by label first
sum(rate(conduit_cost_total_dollars{provider="openai"}[5m]))

# Bad: Filter after aggregation
sum(rate(conduit_cost_total_dollars[5m])) * (provider == "openai")
```

### Metric Lifecycle

1. **Development**: Add metric with `_dev` suffix
2. **Staging**: Test cardinality and performance
3. **Production**: Deploy with monitoring
4. **Deprecation**: Add `_deprecated` suffix, maintain for 30 days
5. **Removal**: Remove after migration period

---

## Troubleshooting Common Issues

### Missing Metrics

```bash
# Check if metrics are being exposed
curl -s localhost:8080/metrics | grep conduit_cost

# Verify Prometheus scraping
curl -s localhost:9090/api/v1/targets | jq '.data.activeTargets[] | select(.labels.job=="conduit")'

# Check for errors in logs
grep -i "metric" /var/log/conduit/api.log
```

### High Cardinality

```promql
# Find high cardinality metrics
count by (__name__)({__name__=~"conduit_.*"})

# Identify problematic labels
count(count by (virtual_key_id) (conduit_virtualkey_spend_total))
```

### Slow Queries

```promql
# Use query_log_file in Prometheus
# Analyze with:
tail -f /var/log/prometheus/queries.log | jq 'select(.duration_seconds > 1)'
```

---

## Metric Deprecation Notice

The following metrics are scheduled for deprecation:

| Metric | Deprecation Date | Replacement | Reason |
|--------|-----------------|-------------|---------|
| None currently | - | - | - |

---

## Support and Contact

For questions about metrics:
- Documentation: This catalog
- Slack: #conduit-observability
- Email: platform-team@conduit.ai
- Runbook: See alert runbooks for incident response