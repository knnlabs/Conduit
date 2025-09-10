# Grafana Cost Observability Dashboard Guide

## Overview

This guide provides comprehensive documentation for setting up, using, and maintaining Grafana dashboards for Conduit's cost observability system.

## Dashboard Architecture

### Dashboard Hierarchy

```
Cost Observability Suite
├── Executive Summary Dashboard
│   └── High-level KPIs and trends
├── Cost Analysis Dashboard
│   └── Detailed cost breakdowns
├── Virtual Key Management Dashboard
│   └── Per-key monitoring and budgets
├── Provider Performance Dashboard
│   └── Provider costs and efficiency
└── Alert Overview Dashboard
    └── Active alerts and incidents
```

## Installation

### Prerequisites

- Grafana 9.0+ installed
- Prometheus data source configured
- User with dashboard creation permissions

### Import Process

1. **Download Dashboard JSON**
   ```bash
   curl -O https://raw.githubusercontent.com/conduit/dashboards/main/cost-observability.json
   ```

2. **Import via Grafana UI**
   - Navigate to Dashboards → Import
   - Upload JSON file or paste contents
   - Select Prometheus data source
   - Click Import

3. **Import via API**
   ```bash
   curl -X POST http://grafana:3000/api/dashboards/db \
     -H "Authorization: Bearer $GRAFANA_API_KEY" \
     -H "Content-Type: application/json" \
     -d @cost-observability.json
   ```

## Dashboard Components

### 1. Executive Summary Dashboard

**Purpose**: C-level view of cost metrics and trends

**Key Panels**:

#### Total Cost Counter
- **Metric**: `sum(conduit_cost_total_dollars)`
- **Visualization**: Stat panel
- **Thresholds**: 
  - Green: < $10,000
  - Yellow: $10,000 - $50,000
  - Red: > $50,000

#### Burn Rate Gauge
- **Metric**: `sum(conduit_cost_rate_dollars_per_minute)`
- **Visualization**: Gauge
- **Max Value**: $200/min
- **Thresholds**:
  - Green: < $50/min
  - Yellow: $50 - $100/min
  - Red: > $100/min

#### Cost Trend Graph
- **Metric**: 
  ```promql
  sum(increase(conduit_cost_total_dollars[1h]))
  ```
- **Visualization**: Time series
- **Display**: Area chart with gradient

#### Provider Cost Distribution
- **Metric**:
  ```promql
  sum by (provider) (increase(conduit_cost_total_dollars[$__range]))
  ```
- **Visualization**: Pie chart
- **Legend**: Table with values

### 2. Cost Analysis Dashboard

**Purpose**: Detailed cost analysis and optimization opportunities

**Key Panels**:

#### Hourly Cost Heatmap
- **Configuration**:
  ```json
  {
    "type": "heatmap",
    "targets": [{
      "expr": "sum(increase(conduit_cost_total_dollars[1h])) by (hour)",
      "format": "heatmap"
    }],
    "options": {
      "calculate": true,
      "calculation": {
        "xBuckets": { "mode": "count", "value": "24" },
        "yBuckets": { "mode": "count", "value": "10" }
      }
    }
  }
  ```

#### Model Cost Comparison
- **Metric**:
  ```promql
  topk(10, 
    sum by (model) (increase(conduit_cost_total_dollars[24h]))
  )
  ```
- **Visualization**: Bar chart
- **Sort**: Descending by cost

#### Cost per Request Distribution
- **Metric**:
  ```promql
  histogram_quantile(0.5, 
    sum(rate(conduit_cost_per_request_dollars_bucket[5m])) by (le)
  )
  ```
- **Visualization**: Histogram
- **Percentiles**: P50, P90, P99

#### Token Efficiency Score
- **Metric**:
  ```promql
  sum(rate(conduit_model_tokens_total[5m]))
  / sum(rate(conduit_cost_total_dollars[5m]))
  ```
- **Visualization**: Time series
- **Unit**: Tokens per dollar

### 3. Virtual Key Management Dashboard

**Purpose**: Monitor individual virtual key usage and budgets

**Key Panels**:

#### Budget Utilization Matrix
- **Configuration**:
  ```json
  {
    "type": "table",
    "targets": [{
      "expr": "conduit_virtualkey_budget_utilization_percent",
      "format": "table",
      "instant": true
    }],
    "transformations": [{
      "id": "organize",
      "options": {
        "excludeByName": { "Time": true },
        "renameByName": {
          "virtual_key_id": "Virtual Key",
          "Value": "Budget Used %"
        }
      }
    }],
    "fieldConfig": {
      "overrides": [{
        "matcher": { "id": "byName", "options": "Budget Used %" },
        "properties": [{
          "id": "custom.displayMode",
          "value": "gradient-gauge"
        }, {
          "id": "thresholds",
          "value": {
            "steps": [
              { "value": 0, "color": "green" },
              { "value": 80, "color": "yellow" },
              { "value": 100, "color": "red" }
            ]
          }
        }]
      }]
    }
  }
  ```

#### Top Spending Keys
- **Metric**:
  ```promql
  topk(10, conduit_virtualkey_spend_total)
  ```
- **Visualization**: Bar gauge
- **Display**: Horizontal bars with values

#### Budget Alert Timeline
- **Metric**:
  ```promql
  increase(conduit_virtualkey_budget_exceeded_total[1h])
  ```
- **Visualization**: Annotations on time series
- **Alert Integration**: Link to AlertManager

#### Key Activity Heatmap
- **Shows**: Request patterns per virtual key
- **Time Buckets**: Hourly
- **Color Scale**: Blue (low) to Red (high)

### 4. Provider Performance Dashboard

**Purpose**: Compare provider costs and performance

**Key Panels**:

#### Provider Cost Efficiency
- **Metric**:
  ```promql
  sum by (provider) (rate(conduit_model_tokens_total[5m]))
  / sum by (provider) (rate(conduit_cost_total_dollars[5m]))
  ```
- **Visualization**: Bar chart
- **Unit**: Tokens per dollar

#### Provider Availability
- **Metric**:
  ```promql
  avg_over_time(conduit_provider_health[1h]) * 100
  ```
- **Visualization**: Stat panels grid
- **Thresholds**:
  - Green: > 99%
  - Yellow: 95% - 99%
  - Red: < 95%

#### Error Rate by Provider
- **Metric**:
  ```promql
  sum by (provider) (rate(conduit_provider_errors_total[5m]))
  / sum by (provider) (rate(conduit_model_requests_total[5m]))
  ```
- **Visualization**: Time series
- **Alert Threshold**: > 5%

#### Provider Latency Comparison
- **Metric**:
  ```promql
  histogram_quantile(0.95,
    sum(rate(conduit_model_response_time_seconds_bucket[5m])) 
    by (provider, le)
  )
  ```
- **Visualization**: Multi-line graph

## Dashboard Variables

### Global Variables

```yaml
datasource:
  type: datasource
  query: prometheus
  default: Prometheus

interval:
  type: interval
  values: [1m, 5m, 15m, 1h, 6h, 12h, 24h, 7d]
  default: 1h
  auto: true
  auto_min: 30s

provider:
  type: query
  query: label_values(conduit_cost_total_dollars, provider)
  multi: true
  includeAll: true
  default: All

model:
  type: query
  query: label_values(conduit_model_requests_total{provider=~"$provider"}, model)
  multi: true
  includeAll: true
  default: All

virtualkey:
  type: query
  query: label_values(conduit_virtualkey_spend_total, virtual_key_id)
  multi: false
  includeAll: false
```

### Custom Variables

```yaml
cost_threshold:
  type: custom
  values: [10, 50, 100, 500, 1000]
  default: 100
  description: Cost threshold in dollars

time_aggregation:
  type: custom
  values: [1h, 6h, 24h, 7d, 30d]
  default: 24h
  description: Aggregation period
```

## Panel Configuration Best Practices

### Time Series Panels

```json
{
  "gridPos": { "h": 8, "w": 12, "x": 0, "y": 0 },
  "fieldConfig": {
    "defaults": {
      "custom": {
        "drawStyle": "line",
        "lineInterpolation": "smooth",
        "lineWidth": 2,
        "fillOpacity": 10,
        "gradientMode": "opacity",
        "spanNulls": false,
        "showPoints": "never",
        "pointSize": 5,
        "stacking": { "mode": "none" },
        "axisPlacement": "auto",
        "axisLabel": "",
        "scaleDistribution": { "type": "linear" }
      },
      "unit": "currencyUSD",
      "min": 0,
      "decimals": 2
    }
  }
}
```

### Stat Panels

```json
{
  "gridPos": { "h": 4, "w": 6, "x": 0, "y": 0 },
  "fieldConfig": {
    "defaults": {
      "mappings": [],
      "thresholds": {
        "mode": "absolute",
        "steps": [
          { "color": "green", "value": null },
          { "color": "yellow", "value": 80 },
          { "color": "red", "value": 100 }
        ]
      },
      "unit": "currencyUSD"
    }
  },
  "options": {
    "reduceOptions": {
      "values": false,
      "calcs": ["lastNotNull"],
      "fields": ""
    },
    "orientation": "auto",
    "textMode": "auto",
    "colorMode": "background",
    "graphMode": "area",
    "justifyMode": "auto"
  }
}
```

### Table Panels

```json
{
  "gridPos": { "h": 10, "w": 24, "x": 0, "y": 0 },
  "fieldConfig": {
    "defaults": {
      "custom": {
        "align": "auto",
        "displayMode": "auto",
        "filterable": true
      }
    },
    "overrides": [
      {
        "matcher": { "id": "byName", "options": "Cost" },
        "properties": [
          { "id": "unit", "value": "currencyUSD" },
          { "id": "decimals", "value": 2 },
          { "id": "custom.displayMode", "value": "color-background" }
        ]
      }
    ]
  },
  "options": {
    "showHeader": true,
    "sortBy": [{ "displayName": "Cost", "desc": true }],
    "footer": { "show": true, "reducer": ["sum"] }
  }
}
```

## Alert Integration

### Alert Rules Configuration

```yaml
apiVersion: 1
groups:
  - orgId: 1
    name: cost_alerts
    folder: Cost Observability
    interval: 1m
    rules:
      - uid: high_burn_rate
        title: High Cost Burn Rate
        condition: C
        data:
          - refId: A
            queryModel:
              expr: sum(conduit_cost_rate_dollars_per_minute)
              refId: A
          - refId: B
            queryModel:
              expr: 100
              refId: B
          - refId: C
            queryModel:
              conditions:
                - evaluator:
                    params: [100]
                    type: gt
                  operator:
                    type: and
                  query:
                    params: [A]
                  reducer:
                    params: []
                    type: last
                  type: query
              datasource: __expr__
              expression: A > B
              refId: C
        noDataState: NoData
        execErrState: Alerting
        for: 5m
        annotations:
          description: Cost burn rate is {{ $values.A.Value }} USD/min
          runbook_url: /docs/runbooks/high-cost-burn-rate.md
          summary: High cost burn rate detected
        labels:
          severity: critical
          team: platform
```

### Alert Annotations

Add annotations to panels:

```json
{
  "annotations": {
    "list": [
      {
        "datasource": "Prometheus",
        "enable": true,
        "expr": "ALERTS{alertname=\"HighCostBurnRate\"}",
        "iconColor": "red",
        "name": "Cost Alerts",
        "step": "30s",
        "tagKeys": "severity",
        "textFormat": "{{ alertname }}",
        "titleFormat": "Cost Alert"
      }
    ]
  }
}
```

## Performance Optimization

### Query Optimization

**Use Recording Rules**:
```yaml
# Instead of complex queries in dashboards
record: conduit:hourly_cost
expr: sum(increase(conduit_cost_total_dollars[1h]))
```

**Limit Time Ranges**:
```json
{
  "targets": [{
    "expr": "metric_name[$__interval]",
    "intervalFactor": 2
  }]
}
```

**Use Instant Queries for Current Values**:
```json
{
  "targets": [{
    "expr": "metric_name",
    "instant": true
  }]
}
```

### Dashboard Loading

**Progressive Loading**:
```json
{
  "refresh": "30s",
  "time": { "from": "now-6h", "to": "now" },
  "timepicker": {
    "refresh_intervals": ["10s", "30s", "1m", "5m"],
    "time_options": ["5m", "15m", "1h", "6h", "12h", "24h", "7d"]
  }
}
```

**Lazy Loading**:
```json
{
  "panels": [{
    "datasource": {
      "type": "prometheus",
      "uid": "${datasource}"
    },
    "maxDataPoints": 300
  }]
}
```

## Maintenance

### Dashboard Versioning

```bash
# Export dashboard
curl -X GET http://grafana:3000/api/dashboards/uid/cost-observability \
  -H "Authorization: Bearer $GRAFANA_API_KEY" \
  > cost-dashboard-v1.0.0.json

# Track in Git
git add cost-dashboard-v1.0.0.json
git commit -m "feat: Cost observability dashboard v1.0.0"
```

### Backup and Restore

```bash
# Backup all dashboards
for dashboard in $(curl -s http://grafana:3000/api/search | jq -r '.[].uid'); do
  curl -s http://grafana:3000/api/dashboards/uid/$dashboard \
    -H "Authorization: Bearer $GRAFANA_API_KEY" \
    > backup/$dashboard.json
done

# Restore dashboard
curl -X POST http://grafana:3000/api/dashboards/db \
  -H "Authorization: Bearer $GRAFANA_API_KEY" \
  -H "Content-Type: application/json" \
  -d @backup/cost-observability.json
```

### Health Checks

```yaml
dashboard_health_checks:
  - name: query_performance
    query: histogram_quantile(0.99, grafana_api_dashboard_get_milliseconds_bucket) < 1000
    
  - name: panel_errors
    query: rate(grafana_api_dataproxy_request_all_milliseconds_count{statuscode!~"2.."}[5m]) < 0.01
    
  - name: datasource_availability
    query: up{job="prometheus"} == 1
```

## Troubleshooting

### Common Issues

#### No Data Points
```bash
# Check Prometheus targets
curl http://prometheus:9090/api/v1/targets | jq '.data.activeTargets[] | select(.job=="conduit")'

# Verify metrics exist
curl http://prometheus:9090/api/v1/label/__name__ | jq '.data[] | select(. | startswith("conduit_cost"))'
```

#### Slow Queries
```bash
# Enable query logging
echo "query_log_file: /var/log/prometheus/queries.log" >> prometheus.yml

# Analyze slow queries
tail -f /var/log/prometheus/queries.log | jq 'select(.duration_seconds > 1)'
```

#### Panel Errors
```javascript
// Browser console
console.log(window.grafanaBootData);

// Check panel queries
Dashboard.panels.forEach(p => console.log(p.targets));
```

### Debug Mode

Enable debug mode in panel:
```json
{
  "options": {
    "showDebug": true
  }
}
```

## Security

### Access Control

```yaml
# Dashboard permissions
permissions:
  - role: Admin
    permission: Admin
  - role: Editor
    permission: Edit
  - role: Viewer
    permission: View
```

### Sensitive Data

- Never include API keys in queries
- Use variables for sensitive values
- Mask virtual key IDs in displays

## Appendix

### Useful Transformations

```json
{
  "transformations": [
    {
      "id": "calculateField",
      "options": {
        "mode": "binary",
        "reduce": { "reducer": "sum" }
      }
    },
    {
      "id": "organize",
      "options": {
        "excludeByName": { "Time": true }
      }
    },
    {
      "id": "sortBy",
      "options": {
        "fields": {},
        "sort": [{ "field": "Cost", "desc": true }]
      }
    }
  ]
}
```

### Color Schemes

```json
{
  "color": {
    "mode": "palette-classic",
    "seriesBy": "last",
    "palettes": [
      {
        "name": "Cost Severity",
        "colors": [
          "#37872D",  // Green
          "#FADE2A",  // Yellow
          "#F2495C"   // Red
        ]
      }
    ]
  }
}
```