# Conduit Operations Guide

*Production deployment, monitoring, and incident response documentation*

**Last Updated**: 2025-01-07

## Overview

This directory contains comprehensive operational documentation for deploying, monitoring, and maintaining Conduit in production environments. It includes monitoring setup, scaling guides, performance optimization, and incident response runbooks.

---

## 📊 Monitoring & Observability

### Core Monitoring
- **[Monitoring Guide](./monitoring.md)** - Complete Prometheus/Grafana setup, metrics catalog, and dashboards
- **[Health Monitoring](./health-monitoring.md)** - Health check system, real-time alerts, and SignalR integration
- **[Cost Observability](./cost-observability.md)** - Cost tracking, budget management, and financial metrics

### Specialized Metrics
- **[Performance Metrics](./performance-metrics.md)** - Performance tracking architecture for tokens/sec, latency, TTFT
- **[Audio Metrics Setup](./audio-metrics-setup.md)** - Audio-specific Prometheus metrics and Grafana dashboards
- **[Webhook Monitoring](./webhook-monitoring.md)** - Webhook delivery tracking and reliability metrics

---

## 🔧 Infrastructure Scaling

### Database & Cache
- **[PostgreSQL Connection Pool Scaling](./PostgreSQL-Connection-Pool-Scaling.md)** - Connection pool optimization for high concurrency
- **[Redis Resilience Improvements](./Redis-Resilience-Improvements.md)** - Redis clustering, failover, and high availability

### Message Queue
- **[RabbitMQ Scaling Guide](./RabbitMQ-Scaling-Guide.md)** - High-throughput configuration for 1,000+ async tasks/minute

### HTTP & Networking
- **[HTTP Connection Pooling Guide](./HTTP-Connection-Pooling-Guide.md)** - Optimize provider API connections (50+ connections per server)
- **[Timeout Configuration](./timeout-configuration.md)** - Request timeout strategies and circuit breakers

---

## 🔒 Security

Security best practices, secret detection, and security scanning:

- **[Security Guidelines](./security/Security-Guidelines.md)** - Log injection prevention, API key security, authentication patterns
- **[Secret Detection & Pre-commit Hooks](./security/Security-Pre-commit-Hooks.md)** - Gitleaks setup to prevent credential leaks
- **[CodeQL Suppressions](./security/CodeQL-Suppressions.md)** - Security scan false positive tracking

---

## 🚨 Incident Response (Runbooks)

Operational runbooks for responding to alerts and incidents. All runbooks follow a standard format: Alert Details → Diagnosis → Resolution → Prevention.

### Critical Alerts
- **[High Error Rate](./runbooks/high-error-rate.md)** - Error rate > 5% for 5+ minutes
- **[High Response Time](./runbooks/high-response-time.md)** - P95 latency > 1s for 10+ minutes
- **[Database Connection Pool](./runbooks/db-connection-pool.md)** - Connection pool exhaustion

### Cost & Budget Alerts
- **[Cost Observability Alerts](./runbooks/cost-observability-alerts.md)** - High burn rate, budget exceeded, cost anomalies

### Cache & Statistics
- **[Cache Statistics](./runbooks/cache-statistics.md)** - Distributed cache statistics troubleshooting

### Runbook Index
See **[Runbooks README](./runbooks/README.md)** for complete alert catalog with severity levels and response times.

---

## 📖 Quick Navigation

### By Task

#### Setting Up Monitoring
1. Start with [Monitoring Guide](./monitoring.md) for Prometheus/Grafana setup
2. Configure [Health Monitoring](./health-monitoring.md) for real-time alerts
3. Enable [Cost Observability](./cost-observability.md) for financial tracking
4. Set up specialized metrics as needed ([Audio](./audio-metrics-setup.md), [Webhooks](./webhook-monitoring.md))

#### Responding to Alerts
1. Check [Runbooks README](./runbooks/README.md) for alert severity and initial response
2. Follow specific runbook for the alert type
3. Document incident and update runbook if needed

#### Scaling for Production
1. Review [PostgreSQL Connection Pool Scaling](./PostgreSQL-Connection-Pool-Scaling.md) for database optimization
2. Configure [HTTP Connection Pooling](./HTTP-Connection-Pooling-Guide.md) for provider APIs
3. Scale message processing with [RabbitMQ Scaling Guide](./RabbitMQ-Scaling-Guide.md)
4. Implement [Redis Resilience](./Redis-Resilience-Improvements.md) for cache high availability

#### Performance Optimization
1. Monitor [Performance Metrics](./performance-metrics.md) for tokens/sec and latency
2. Configure [Timeouts](./timeout-configuration.md) to prevent cascading failures
3. Review [Health Monitoring](./health-monitoring.md) for system degradation patterns

### By Component

| Component | Setup | Scaling | Monitoring | Troubleshooting |
|-----------|-------|---------|------------|-----------------|
| **Database** | - | [PostgreSQL Pool](./PostgreSQL-Connection-Pool-Scaling.md) | [Monitoring](./monitoring.md) | [DB Pool Runbook](./runbooks/db-connection-pool.md) |
| **Cache** | - | [Redis Resilience](./Redis-Resilience-Improvements.md) | [Monitoring](./monitoring.md) | [Cache Stats Runbook](./runbooks/cache-statistics.md) |
| **Message Queue** | - | [RabbitMQ Scaling](./RabbitMQ-Scaling-Guide.md) | [Monitoring](./monitoring.md) | - |
| **HTTP Clients** | [Connection Pooling](./HTTP-Connection-Pooling-Guide.md) | - | [Monitoring](./monitoring.md) | [High Response Time](./runbooks/high-response-time.md) |
| **Health Checks** | [Health Monitoring](./health-monitoring.md) | - | [Health Monitoring](./health-monitoring.md) | - |
| **Cost Tracking** | [Cost Observability](./cost-observability.md) | - | [Cost Observability](./cost-observability.md) | [Cost Alerts](./runbooks/cost-observability-alerts.md) |
| **Webhooks** | - | - | [Webhook Monitoring](./webhook-monitoring.md) | - |

### By Metric Category

**Business Metrics**:
- Virtual key usage and spend: [Cost Observability](./cost-observability.md)
- Budget utilization: [Cost Observability](./cost-observability.md)
- Model usage patterns: [Monitoring Guide](./monitoring.md)

**Infrastructure Metrics**:
- Database connections: [Monitoring Guide](./monitoring.md)
- Redis operations: [Monitoring Guide](./monitoring.md)
- RabbitMQ queues: [Monitoring Guide](./monitoring.md)
- HTTP metrics: [Monitoring Guide](./monitoring.md)

**Performance Metrics**:
- Tokens/second: [Performance Metrics](./performance-metrics.md)
- Time to first token: [Performance Metrics](./performance-metrics.md)
- Request latency: [Monitoring Guide](./monitoring.md)

**Health Metrics**:
- Component health: [Health Monitoring](./health-monitoring.md)
- Provider health: [Monitoring Guide](./monitoring.md)
- System resource usage: [Monitoring Guide](./monitoring.md)

---

## 🎯 Production Deployment Checklist

### Pre-Deployment
- [ ] Prometheus and Grafana deployed and configured
- [ ] AlertManager configured with notification channels
- [ ] Health monitoring enabled with appropriate thresholds
- [ ] Cost tracking enabled with budget alerts
- [ ] Database connection pool sized for expected load
- [ ] Redis configured with persistence and replication
- [ ] RabbitMQ configured for high throughput
- [ ] HTTP connection pooling configured
- [ ] Timeout strategies implemented
- [ ] All runbooks reviewed by operations team

### Post-Deployment
- [ ] Verify all metrics exporting correctly
- [ ] Test alert delivery (create test alerts)
- [ ] Confirm dashboard data visualization
- [ ] Validate health check endpoints
- [ ] Monitor initial traffic patterns
- [ ] Review and adjust alert thresholds
- [ ] Document any environment-specific configurations

### Ongoing Operations
- [ ] Daily health check review (see [Cost Observability](./cost-observability.md))
- [ ] Weekly budget and cost review
- [ ] Monthly performance trend analysis
- [ ] Quarterly runbook review and updates
- [ ] Continuous alert noise reduction

---

## 📊 Metrics Reference

### Common Metric Patterns

**Request Rate**:
```promql
rate(conduit_http_requests_total[5m])
```

**Error Rate**:
```promql
sum(rate(conduit_http_requests_total{status_code=~"5.."}[5m])) /
sum(rate(conduit_http_requests_total[5m]))
```

**Response Time (P95)**:
```promql
histogram_quantile(0.95, sum(rate(conduit_http_request_duration_seconds_bucket[5m])) by (le))
```

**Cost Burn Rate**:
```promql
sum(conduit_cost_rate_dollars_per_minute)
```

**Database Pool Utilization**:
```promql
(conduit_database_connections_active /
(conduit_database_connections_active + conduit_database_connections_available)) * 100
```

---

## 🆘 Getting Help

### Escalation Path
1. **L1 Response**: Check appropriate runbook in [runbooks/](./runbooks/)
2. **L2 Escalation**: Consult component-specific guide (scaling, monitoring)
3. **L3/Management**: See [Runbooks README](./runbooks/README.md) for escalation contacts

### Documentation Hierarchy
1. **Runbooks** - Immediate incident response procedures
2. **Setup Guides** - Initial configuration and deployment
3. **Scaling Guides** - Performance optimization and capacity planning
4. **Monitoring Guides** - Observability and metrics

### Related Documentation
- **[Deployment Configuration](../deployment/DEPLOYMENT-CONFIGURATION.md)** - Docker and production deployment
- **[Architecture Overview](../architecture-overview.md)** - System design and components
- **[Troubleshooting Guide](../troubleshooting/TROUBLESHOOTING-GUIDE.md)** - General troubleshooting procedures

---

## 📝 Contributing to Operations Docs

When updating operational documentation:

1. **Runbooks**: Follow the standard format (Alert Details → Diagnosis → Resolution → Prevention)
2. **Setup Guides**: Include configuration examples, common pitfalls, and verification steps
3. **Scaling Guides**: Document performance targets, resource requirements, and testing procedures
4. **Metrics**: Include example queries, alert thresholds, and visualization recommendations

---

*For deployment and infrastructure setup, see [Deployment Documentation](../deployment/)*

*For general troubleshooting, see [Troubleshooting Guide](../troubleshooting/TROUBLESHOOTING-GUIDE.md)*
