# Conduit Documentation

Conduit is an LLM gateway that provides a unified, OpenAI-compatible API across multiple AI providers. It handles routing, cost tracking, virtual API keys, rate limiting, and real-time streaming — so you can switch providers or add new ones without changing your application code.

## Getting Started

- **[Configuration Guide](./operations/configuration/configuration-guide.md)** — Database, environment variables, and provider setup
- **[Budget Management](./operations/configuration/budget-management.md)** — Cost controls and spending limits
- **[Versioning](./Versioning.md)** — Semantic versioning and release process

## API Guides

### Gateway API (OpenAI-Compatible)

The primary API for interacting with LLMs. Drop-in compatible with OpenAI client libraries.

- **[Getting Started](./api-guides/gateway/getting-started.md)** — Authentication, first request
- **[API Reference](./api-guides/gateway/api-reference.md)** — Chat completions, embeddings, images, streaming

### Admin API (Management)

Configure providers, manage virtual keys, and monitor usage programmatically.

- **[Getting Started](./api-guides/admin/getting-started.md)** — Authentication and setup
- **[TypeScript SDK](./api-guides/admin/typescript-sdk.md)** — SDK guide with examples
- **[API Reference](./api-guides/admin/api-reference.md)** — Endpoint documentation
- **[Provider Error Tracking](./api-guides/admin/provider-error-tracking.md)** — Auto-disable failing keys

### Features

- **[Function Calling](./api-guides/features/function-calling.md)** — Tool use with LLMs
- **[Multimodal Vision](./api-guides/features/multimodal-vision.md)** — Image analysis
- **[LLM Routing](./api-guides/features/llm-routing.md)** — Load balancing and failover
- **[Webhooks](./api-guides/features/webhooks.md)** — Event notifications
- **[Streaming with Tools](./api-guides/streaming-with-tools.md)** — Function calling in streaming mode
- **[Batch Operations](./api-guides/batch-operations-idempotency.md)** — Idempotent batch requests

### Functions (External Tool Execution)

- **[Getting Started](./api-guides/functions/getting-started.md)** — Function system setup
- **[Exa Provider Guide](./api-guides/functions/exa-provider-guide.md)** — Web search integration

### SDKs

- **[SDK Overview](./api-guides/sdk/README.md)** — Available client libraries
- **[Next.js Integration](./api-guides/sdk/nextjs-integration.md)** — WebAdmin patterns
- **[Best Practices](./api-guides/sdk/best-practices.md)** — Security and performance
- **[Connection Management](./api-guides/sdk/connection-management.md)** — HTTP client lifecycle
- **[Health Checks](./api-guides/sdk/health-checks.md)** — SDK health monitoring
- **[Troubleshooting](./api-guides/sdk/troubleshooting.md)** — Common issues

### Real-Time (SignalR)

- **[Overview](./api-guides/signalr/README.md)** — Real-time features and capabilities
- **[Getting Started](./api-guides/signalr/getting-started.md)** — Connection setup
- **[Hub Reference](./api-guides/signalr/hub-reference.md)** — Available hubs and events
- **[Client Examples](./api-guides/signalr/client-examples.md)** — Integration examples
- **[Authentication](./api-guides/signalr/authentication.md)** — Auth patterns for SignalR
- **[MessagePack Protocol](./api-guides/signalr/messagepack-protocol.md)** — Binary protocol for performance

### WebAdmin Proxy Layer

- **[WebAdmin Gateway Endpoints](./api-guides/gateway/webadmin-endpoints.md)** — Gateway API via the WebAdmin Next.js proxy

## Architecture

System design documentation for understanding how Conduit works internally.

- **[Architecture Overview](./architecture/README.md)** — Full architecture index

### Provider System

- **[Provider Architecture](./architecture/provider-system/provider-architecture.md)** — Multi-instance provider design
- **[Model & Cost Mapping](./architecture/provider-system/model-and-cost-mapping.md)** — How costs are tracked
- **[Error Tracking](./architecture/provider-system/error-tracking.md)** — Provider error detection
- **[System Analysis](./architecture/provider-system/provider-system-analysis.md)** — Provider system internals

### Patterns

- **[Repository & Data Access](./architecture/patterns/repository-and-data-access.md)** — EF Core patterns
- **[Background Services](./architecture/patterns/background-services-and-workers.md)** — Worker patterns
- **[Batch Operations](./architecture/patterns/batch-operation-framework.md)** — Batch processing framework
- **[Naming Conventions](./architecture/patterns/naming-conventions.md)** — Codebase naming standards
- **[Utility Classes](./architecture/patterns/utility-classes.md)** — Shared utilities

### Infrastructure

- **[Scaling Architecture](./architecture/infrastructure/scaling-architecture.md)** — 10,000+ concurrent sessions
- **[Cache Usage](./architecture/infrastructure/cache-usage.md)** — IMemoryCache vs IDistributedCache

### Other

- **[DTO Guidelines](./architecture/data-transfer/dto-guidelines.md)** — Data transfer patterns
- **[Streaming & WebSockets](./architecture/real-time/streaming-and-websockets.md)** — Real-time architecture
- **[Webhook Delivery](./architecture/real-time/webhook-delivery.md)** — Distributed delivery system
- **[Async Media Generation](./architecture/media-generation/async-media-generation.md)** — Event-driven image/video generation
- **[Media Progress](./architecture/media-generation/progress-and-notifications.md)** — Real-time generation progress
- **[Functions System](./architecture/functions/functions-system-architecture.md)** — External tool execution architecture
- **[MassTransit Events](./architecture/events/masstransit-event-inventory.md)** — Event inventory
- **[Admin API Integration](./architecture/api/admin-api-integration.md)** — Admin API internals

## Operations

Production deployment, monitoring, scaling, and incident response.

- **[Operations Overview](./operations/README.md)** — Full operations index

### Deployment

- **[Deployment Configuration](./operations/deployment/DEPLOYMENT-CONFIGURATION.md)** — Production setup
- **[Docker Optimization](./operations/deployment/docker-optimization.md)** — Container best practices
- **[CI/CD Maintenance](./operations/deployment/ci-cd-maintenance-guide.md)** — Pipeline maintenance
- **[Media Cleanup](./operations/deployment/media-cleanup-configuration.md)** — S3/R2 storage cleanup

### Configuration

- **[Configuration Guide](./operations/configuration/configuration-guide.md)** — System configuration
- **[Budget Management](./operations/configuration/budget-management.md)** — Cost controls
- **[Cache Configuration](./operations/configuration/cache-configuration.md)** — Redis/memory cache setup
- **[Timeout Configuration](./operations/configuration/timeout-configuration.md)** — Request timeout tuning

### Monitoring

- **[Monitoring Setup](./operations/monitoring/setup-guide.md)** — Prometheus/Grafana
- **[Health Checks](./operations/monitoring/health-checks.md)** — Health monitoring and alerting
- **[Cost Tracking](./operations/monitoring/cost-tracking.md)** — Cost observability
- **[Performance Metrics](./operations/monitoring/performance-metrics.md)** — Token/sec, latency, TTFT
- **[Audio Metrics](./operations/monitoring/audio-metrics.md)** — Audio-specific metrics
- **[Webhook Monitoring](./operations/monitoring/webhooks.md)** — Delivery tracking

### Infrastructure Scaling

- **[PostgreSQL Scaling](./operations/infrastructure/postgresql-scaling.md)** — Connection pool optimization
- **[RabbitMQ Scaling](./operations/infrastructure/rabbitmq-scaling.md)** — High-throughput messaging
- **[Redis Resilience](./operations/infrastructure/redis-resilience.md)** — Cache high availability
- **[HTTP Connection Pooling](./operations/infrastructure/http-connection-pooling.md)** — Provider API connections

### Security

- **[Security Guidelines](./operations/security/Security-Guidelines.md)** — API key security, authentication
- **[Pre-commit Hooks](./operations/security/Security-Pre-commit-Hooks.md)** — Secret detection
- **[CodeQL Suppressions](./operations/security/CodeQL-Suppressions.md)** — Security scan tracking

### Runbooks

- **[Runbook Index](./operations/runbooks/README.md)** — Alert catalog and response procedures
- **[High Error Rate](./operations/runbooks/high-error-rate.md)** — Error spike response
- **[High Response Time](./operations/runbooks/high-response-time.md)** — Latency incidents
- **[DB Connection Pool](./operations/runbooks/db-connection-pool.md)** — Pool exhaustion
- **[Cost Alerts](./operations/runbooks/cost-observability-alerts.md)** — Budget alert handling
- **[Cost Troubleshooting](./operations/runbooks/cost-observability-troubleshooting.md)** — Cost tracking issues
- **[Cache Statistics](./operations/runbooks/cache-statistics.md)** — Cache diagnostics
- **[Error Tracking](./operations/error-tracking-runbook.md)** — Provider error investigation

### Providers

- **[Provider Compatibility](./operations/providers/compatibility-report.md)** — Provider feature support
- **[Usage Mappings](./operations/providers/usage-mappings.md)** — Usage tracking configuration

### SignalR Operations

- **[SignalR Configuration](./operations/signalr/configuration.md)** — Production SignalR setup
- **[Redis Backplane Testing](./operations/signalr/redis-backplane-testing.md)** — Horizontal scaling tests

## Model Pricing

- **[Pricing Overview](./model-pricing/README.md)** — How model data and pricing are managed
- **[Model Costs](./model-pricing/model-costs.md)** — Cost storage architecture (ModelCost, ModelCostMapping)
- **[Polymorphic Pricing](./model-pricing/polymorphic-pricing.md)** — 8 pricing models (per-token, per-image, tiered, etc.)
- **[Pricing Quick Reference](./model-pricing/pricing-quick-reference.md)** — Enum values and configuration templates
- **[WebUI Pricing Guide](./model-pricing/webui-pricing-guide.md)** — Managing pricing in the dashboard

Model definitions and per-provider pricing are managed via SQL scripts in [`scripts/db/providers/`](../scripts/db/providers/README.md).

## Development

For contributors working on the Conduit codebase.

- **[Development Overview](./development/README.md)** — Setup and workflows
- **[API Patterns](./development/API-PATTERNS-BEST-PRACTICES.md)** — Backend API conventions
- **[Adding a New Provider](./development/adding-a-provider.md)** — End-to-end guide for new providers
- **[LLM Client Factory Guide](./development/llm-client-factory-guide.md)** — Factory architecture and decorators
- **[Provider API Research](./development/provider-api-research.md)** — Provider integration research
- **[SDK Gaps](./development/sdk-gaps.md)** — Known SDK limitations
- **[Documentation Style Guide](./development/documentation-style-guide.md)** — Writing standards

### WebAdmin Development

- **[WebAdmin Guide](./development/webui/README.md)** — WebAdmin contribution guide
- **[Component Library](./development/webui/component-library.md)** — UI components
- **[Error Handling](./development/webui/error-handling.md)** — Frontend error patterns
- **[Responsive Design](./development/webui/responsive-design-patterns.md)** — Layout patterns
- **[Troubleshooting](./development/webui/troubleshooting.md)** — WebAdmin debugging

### Testing

- **[Coverage Report](./development/testing/test-coverage-report.md)** — Test coverage metrics
- **[Coverage Setup](./development/testing/coverage.md)** — Coverage system configuration
- **[Mutation Testing](./development/testing/Mutation-Testing-Guide.md)** — Stryker mutation testing
- **[CDN Testing](./development/testing/testing-cdn-locally.md)** — Local CDN testing
