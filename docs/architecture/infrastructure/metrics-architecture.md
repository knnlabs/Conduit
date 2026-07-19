# Metrics & Monitoring Architecture

This document maps all metrics collection in Conduit, explains the layered design, clarifies which overlaps are intentional vs incidental, and provides guidance for adding new metrics.

## Three-Layer Architecture

Conduit collects metrics across three layers, each serving different consumers:

```
Layer 1: Real-Time Collection (middleware, interceptors)
  ↓ Prometheus counters/histograms written on every request
Layer 2: Aggregation & Analysis (background services)
  ↓ Queries Layer 1 data, computes trends, detects anomalies
Layer 3: Distributed Coordination (Redis-backed services)
  ↓ Cross-instance aggregation for multi-node deployments
```

| Layer | Technology | Latency | Scope | Consumer |
|-------|-----------|---------|-------|----------|
| Collection | Prometheus `prometheus-net` | Real-time | Per-request | Grafana, `/metrics` endpoint |
| Aggregation | In-memory queries | 5-30s delay | Per-instance | WebAdmin dashboard (SignalR) |
| Distributed | Redis Streams + Lua | 30s delay | Cross-instance | Alerting, capacity planning |

## Layer 1: Real-Time Collection

These fire on every request/event with near-zero overhead.

### HTTP Metrics

| Service | File | Scope |
|---------|------|-------|
| `HttpMetricsMiddleware` | `Gateway/Middleware/HttpMetricsMiddleware.cs` | Gateway API requests |
| `AdminHttpMetricsMiddleware` | `Admin/Middleware/AdminHttpMetricsMiddleware.cs` | Admin API requests |

Both inherit `HttpMetricsMiddlewareBase`. Key metrics: `conduit_http_requests_total`, `conduit_http_request_duration_seconds`, `conduit_http_requests_active`.

### Domain Metrics (Static Classes)

| Class | File | What It Tracks |
|-------|------|----------------|
| `UsageMetrics` | `Gateway/Middleware/UsageMetrics.cs` | Token counts, costs, billing events per model/provider |
| `BillingMetrics` | `Gateway/Metrics/BillingMetrics.cs` | Redis circuit breaker state, spend update reliability |
| `PromptCachingMetrics` | `Gateway/Metrics/PromptCachingMetrics.cs` | Prompt cache hit rates |
| `EventPublishingMetrics` | `Core/Metrics/EventPublishingMetrics.cs` | MassTransit event publish rates |
| `MediaGenerationMetrics` | `Core/Metrics/MediaGenerationMetrics.cs` | Image/video generation operations |

### Shared Parameterized Metrics

These are instantiated with a service prefix (`"gateway"` or `"admin"`) to produce service-specific Prometheus metric names:

| Class | File | Instantiated As |
|-------|------|-----------------|
| `AuthMetrics` | `Core/Metrics/AuthMetrics.cs` | `GatewayAuthMetrics`, `AdminAuthMetrics` |
| `CacheMetrics` | `Core/Metrics/CacheMetrics.cs` | `GatewayCacheMetrics`, `AdminCacheMetrics` |

### OpenTelemetry Meters

| Class | File | Meter Name | Notes |
|-------|------|-----------|-------|
| `SignalRMetrics` | `Gateway/Metrics/SignalRMetrics.cs` | `ConduitLLM.SignalR` | OTel `Meter` + `ActivitySource` for tracing |

OpenTelemetry is configured in both `Program.Monitoring.cs` files to export standard ASP.NET/Kestrel/Runtime meters via Prometheus exporter and optionally OTLP.

## Layer 2: Aggregation & Analysis

Background services that periodically query Layer 1 data, compute derived metrics, and push to consumers.

| Service | File | Interval | Leader? | Consumer |
|---------|------|----------|---------|----------|
| `MetricsAggregationService` | `Gateway/Services/MetricsAggregationService.cs` | 5s | No | `MetricsHub` → WebAdmin dashboard |
| `BusinessMetricsService` | `Gateway/Services/BusinessMetricsService.cs` | 1m | Yes | Prometheus (virtual key spend, model costs, SLA) |
| `GatewayOperationsMetricsService` | `Gateway/Services/GatewayOperationsMetricsService.cs` | 1m | Yes | Prometheus (LLM/batch/media/function ops) |
| `AdminOperationsMetricsService` | `Admin/Services/AdminOperationsMetricsService.cs` | 1m | Yes | Prometheus (admin CRUD, CSV, config changes) |
| `TaskProcessingMetricsService` | `Gateway/Services/TaskProcessingMetricsService.cs` | 30s | No | Prometheus (queue depth, webhooks, spend rate) |
| `HealthMonitoringBackgroundService` | `Gateway/Services/HealthMonitoringBackgroundService.cs` | 30s | No | Alert system |

Services marked **Leader: Yes** use distributed leader election so only one instance collects, preventing duplicate counters.

### MetricsAggregationService — The Dashboard Feeder

This is the central aggregator for the WebAdmin real-time dashboard. It:
1. Queries Prometheus counters for HTTP/error rates
2. Checks infrastructure connectivity (Redis, RabbitMQ, DB, SignalR)
3. Computes business metrics (active keys, model usage, costs)
4. Reads system metrics (CPU, memory)
5. Pushes a `MetricsSnapshot` to `MetricsHub` every 5 seconds

It does **not** create new Prometheus metrics — it reads existing ones and delivers them over SignalR.

## Layer 3: Distributed Coordination

Redis-backed services for cross-instance visibility. Required in multi-node deployments where per-instance Prometheus metrics are insufficient.

| Service | File | Redis Keys | Purpose |
|---------|------|-----------|---------|
| `DistributedPerformanceMonitoringService` | `Gateway/Services/DistributedPerformanceMonitoringService.cs` | `perf_metrics:*`, `endpoint_metrics:*`, `request_metrics_stream` | Cross-instance request/DB/cache/pool metrics |
| `DistributedSignalRMetricsService` | `Gateway/Services/DistributedSignalRMetricsService.cs` | `signalr_connections:*`, `signalr_metrics_instances:*`, `signalr_events_stream` | Global SignalR connection counts |
| `RedisWebhookMetricsService` | `Core/Services/RedisWebhookMetricsService.cs` | `webhook_metrics:*`, `webhook_events:*` | Webhook delivery stats |
| `SecurityEventMonitoringService` | `Security/Services/SecurityEventMonitoringService.cs` | Configurable | IP threat tracking, risk scores |

Both `DistributedPerformanceMonitoringService` and `DistributedSignalRMetricsService` maintain instance heartbeats (expiring after 2 minutes) and aggregate across all live instances.

## Supplementary Services

These are not background collectors — they compute metrics on-demand for specific operations:

| Service | File | Purpose |
|---------|------|---------|
| `CacheMetricsService` | `Core/Caching/CacheMetricsService.cs` | In-memory hit/miss counters for LLM response cache |
| `PerformanceMetricsService` | `Core/Services/PerformanceMetricsService.cs` | Tokens/sec, time-to-first-token calculations |
| `StreamingMetricsCollector` | `Core/Services/StreamingMetricsCollector.cs` | Per-stream inter-token latency tracking |
| `CacheStatisticsHealthCheck` | `Core/Services/CacheStatisticsHealthCheck.cs` | ASP.NET health check that reports cache stats |

## Intentional Overlaps

Several metrics are tracked by multiple services. This is **by design** — each serves a different consumer or operates at a different granularity.

### Cache Metrics (3 systems)

| System | What It Provides | Consumer |
|--------|-----------------|----------|
| `CacheMetricsService` | Per-model hit/miss counters, retrieval times | LLM caching layer (internal optimization) |
| `GatewayCacheMetrics` / `AdminCacheMetrics` | Prometheus counters by cache name | Grafana dashboards, alerting rules |
| `DistributedPerformanceMonitoringService` | Redis-aggregated cache ops across instances | Cross-instance capacity alerts |

**Why all three?** `CacheMetricsService` is the source of truth for the caching layer itself. Prometheus metrics are for ops dashboards. Distributed metrics are for multi-node alerting where per-instance Prometheus isn't sufficient.

### HTTP Request Metrics (3 systems)

| System | Granularity | Consumer |
|--------|------------|----------|
| `HttpMetricsMiddleware` | Per-request, real-time | Prometheus → Grafana |
| `MetricsAggregationService` | 5s snapshots from Prometheus | WebAdmin dashboard (SignalR) |
| `DistributedPerformanceMonitoringService` | Cross-instance, Redis | Infrastructure alerting |

**Why all three?** Middleware is the source of truth. Aggregator reshapes it for the dashboard. Distributed service aggregates across nodes for cluster-wide alerting.

### SignalR Connection Tracking (2 systems)

| System | Technology | Consumer |
|--------|-----------|----------|
| `SignalRMetrics` | OpenTelemetry `Meter` | OTLP export, APM tools (Datadog, Honeycomb) |
| `DistributedSignalRMetricsService` | Redis + Prometheus | Cross-instance connection counts, Grafana |

**Why both?** OpenTelemetry provides distributed tracing integration (correlating SignalR events with request traces). The Redis-backed service provides a global connection count that no single instance can compute alone.

### Cost/Spending Metrics (4 sources)

| Source | What | Consumer |
|--------|------|----------|
| `UsageMetrics` | Per-request cost and token counts | Prometheus → Grafana |
| `BusinessMetricsService` | Aggregated cost per provider/model, spend rates | Prometheus → business dashboards |
| `BillingMetrics` | Spend update reliability, circuit breaker state | Prometheus → ops alerting |
| `TaskProcessingMetricsService` | Virtual key spend rate gauge | Prometheus → rate monitoring |

**Why all four?** Each answers a different question: "What did this request cost?" (UsageMetrics), "What's the total spend trend?" (Business), "Is the billing pipeline healthy?" (Billing), "Is any key spending too fast?" (TaskProcessing).

## Alert Threshold Reference

Two systems define alert thresholds independently:

| Metric | MetricsAggregationService | HealthMonitoringBackgroundService |
|--------|--------------------------|----------------------------------|
| Error Rate | 5% | 5% |
| Response Time | 5000ms | 5000ms |
| CPU | 80% | Warning: 70%, Critical: 90% |
| Memory | 85% | Warning: 70%, Critical: 90% |
| Connection Pool | — | 80% |
| Queue Depth | 1000 | — |

`MetricsAggregationService` thresholds drive the dashboard status indicators. `HealthMonitoringBackgroundService` thresholds drive alerts via `IAlertManagementService`.

## Adding New Metrics

### Decision Guide

1. **Is it per-request?** → Add to the appropriate middleware or static metrics class (Layer 1)
2. **Is it an aggregate/trend?** → Add to the appropriate BackgroundService (Layer 2)
3. **Does it need cross-instance visibility?** → Add to `DistributedPerformanceMonitoringService` (Layer 3)
4. **Is it a business/cost metric?** → Add to `BusinessMetricsService` (leader-elected)
5. **Is it admin-specific?** → Add to `AdminOperationsMetricsService`

### Naming Convention

All Prometheus metrics follow: `conduit_{service}_{domain}_{metric}_{unit}`

Examples:
- `conduit_http_requests_total` (Gateway HTTP)
- `conduit_admin_virtualkey_operations_total` (Admin ops)
- `conduit_gateway_llm_operation_duration_seconds` (Gateway LLM)
- `conduit_signalr_connections_active_distributed` (Distributed SignalR)

### Registration

- Real-time metrics: No registration needed (static Prometheus counters)
- Background services: Register in `Program.Monitoring.cs` with leader election if the service should run on only one instance
- Distributed services: Register in `HealthMonitoringExtensions.cs`

## Data Flow Diagram

```
Request → HttpMetricsMiddleware → Prometheus counters
                                       ↓
                              MetricsAggregationService (5s)
                                       ↓
                              MetricsHub → WebAdmin Dashboard

Request → UsageTrackingMiddleware → UsageMetrics (Prometheus)
                                         ↓
                              BusinessMetricsService (1m, leader)
                                         ↓
                              Prometheus → Grafana

Request → DistributedPerformanceMonitoring → Redis Streams
                                                  ↓
                                          Aggregation timer
                                                  ↓
                                          IAlertManagementService
                                                  ↓
                                          Webhook/Email/Slack alerts

SignalR → SignalRMetrics (OTel) → OTLP → APM tools
SignalR → DistributedSignalRMetricsService → Redis → Prometheus

Health → HealthMonitoringBackgroundService (30s) → IAlertManagementService
```
