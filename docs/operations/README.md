# Conduit Operations Guide

*Production deployment, monitoring, and incident response documentation*

## Overview

This directory contains operational documentation for deploying, monitoring, and maintaining Conduit in production environments.

---

## Deployment

- **[Deployment Configuration](./deployment/DEPLOYMENT-CONFIGURATION.md)** - Production setup
- **[Docker Optimization](./deployment/docker-optimization.md)** - Container best practices
- **[CI/CD Maintenance](./deployment/ci-cd-maintenance-guide.md)** - Pipeline maintenance
- **[Media Cleanup](./deployment/media-cleanup-configuration.md)** - S3/R2 storage cleanup

## Configuration

- **[Configuration Guide](./configuration/configuration-guide.md)** - System configuration options
- **[Budget Management](./configuration/budget-management.md)** - Cost controls and spending limits
- **[Cache Configuration](./configuration/cache-configuration.md)** - Redis/memory cache setup
- **[Timeout Configuration](./configuration/timeout-configuration.md)** - Request timeout strategies

---

## Monitoring & Observability

### Core Monitoring
- **[Monitoring Setup](./monitoring/setup-guide.md)** - Prometheus/Grafana setup, metrics catalog, dashboards
- **[Health Checks](./monitoring/health-checks.md)** - Health check system and alerting
- **[Cost Tracking](./monitoring/cost-tracking.md)** - Cost observability and financial metrics

### Specialized Metrics
- **[Performance Metrics](./monitoring/performance-metrics.md)** - Tokens/sec, latency, TTFT tracking
- **[Audio Metrics](./monitoring/audio-metrics.md)** - Audio-specific Prometheus metrics
- **[Webhook Monitoring](./monitoring/webhooks.md)** - Webhook delivery tracking

---

## Infrastructure Scaling

### Database & Cache
- **[PostgreSQL Scaling](./infrastructure/postgresql-scaling.md)** - Connection pool optimization for high concurrency
- **[Redis Resilience](./infrastructure/redis-resilience.md)** - Redis clustering, failover, and high availability

### Message Queue
- **[RabbitMQ Scaling](./infrastructure/rabbitmq-scaling.md)** - High-throughput configuration for 1,000+ async tasks/minute

### HTTP & Networking
- **[HTTP Connection Pooling](./infrastructure/http-connection-pooling.md)** - Provider API connection optimization (50+ connections per server)

---

## Security

- **[Security Guidelines](./security/Security-Guidelines.md)** - Log injection prevention, API key security, authentication
- **[Secret Detection](./security/Security-Pre-commit-Hooks.md)** - Gitleaks pre-commit hooks
- **[CodeQL Suppressions](./security/CodeQL-Suppressions.md)** - Security scan false positive tracking

---

## Incident Response (Runbooks)

All runbooks follow a standard format: Alert Details → Diagnosis → Resolution → Prevention.

### Critical Alerts
- **[High Error Rate](./runbooks/high-error-rate.md)** - Error rate > 5% for 5+ minutes
- **[High Response Time](./runbooks/high-response-time.md)** - P95 latency > 1s for 10+ minutes
- **[Database Connection Pool](./runbooks/db-connection-pool.md)** - Connection pool exhaustion

### Cost & Budget Alerts
- **[Cost Observability Alerts](./runbooks/cost-observability-alerts.md)** - High burn rate, budget exceeded, cost anomalies
- **[Cost Troubleshooting](./runbooks/cost-observability-troubleshooting.md)** - Cost tracking issues

### Cache & Error Tracking
- **[Cache Statistics](./runbooks/cache-statistics.md)** - Distributed cache diagnostics
- **[Error Tracking Runbook](./error-tracking-runbook.md)** - Provider error investigation

### Runbook Index
See **[Runbooks README](./runbooks/README.md)** for complete alert catalog with severity levels.

---

## Providers

- **[Provider Compatibility](./providers/compatibility-report.md)** - Provider feature support matrix
- **[Error Tracking](./providers/error-tracking.md)** - Operational error tracking
- **[Usage Mappings](./providers/usage-mappings.md)** - Usage tracking configuration

## SignalR Operations

- **[SignalR Configuration](./signalr/configuration.md)** - Production SignalR setup
- **[Redis Backplane Testing](./signalr/redis-backplane-testing.md)** - Horizontal scaling tests

---

## Quick Navigation

### By Task

**Setting Up Monitoring:**
1. [Monitoring Setup](./monitoring/setup-guide.md) for Prometheus/Grafana
2. [Health Checks](./monitoring/health-checks.md) for alerting
3. [Cost Tracking](./monitoring/cost-tracking.md) for financial metrics

**Responding to Alerts:**
1. Check [Runbooks README](./runbooks/README.md) for alert severity
2. Follow the specific runbook for the alert type

**Scaling for Production:**
1. [PostgreSQL Scaling](./infrastructure/postgresql-scaling.md) for database
2. [HTTP Connection Pooling](./infrastructure/http-connection-pooling.md) for provider APIs
3. [RabbitMQ Scaling](./infrastructure/rabbitmq-scaling.md) for message processing
4. [Redis Resilience](./infrastructure/redis-resilience.md) for cache HA

### By Component

| Component | Scaling | Monitoring | Troubleshooting |
|-----------|---------|------------|-----------------|
| **Database** | [PostgreSQL](./infrastructure/postgresql-scaling.md) | [Monitoring](./monitoring/setup-guide.md) | [DB Pool Runbook](./runbooks/db-connection-pool.md) |
| **Cache** | [Redis](./infrastructure/redis-resilience.md) | [Monitoring](./monitoring/setup-guide.md) | [Cache Runbook](./runbooks/cache-statistics.md) |
| **Message Queue** | [RabbitMQ](./infrastructure/rabbitmq-scaling.md) | [Monitoring](./monitoring/setup-guide.md) | - |
| **HTTP Clients** | [Pooling](./infrastructure/http-connection-pooling.md) | [Monitoring](./monitoring/setup-guide.md) | [Response Time](./runbooks/high-response-time.md) |
| **Cost Tracking** | - | [Cost Tracking](./monitoring/cost-tracking.md) | [Cost Alerts](./runbooks/cost-observability-alerts.md) |

---

## Related Documentation

- **[Architecture Overview](../architecture/README.md)** - System design and components
- **[Deployment Configuration](./deployment/DEPLOYMENT-CONFIGURATION.md)** - Docker and production setup
