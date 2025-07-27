# Conduit Documentation

Welcome to the Conduit documentation! This guide will help you navigate through all available documentation.

## 🚀 Quick Start

- **[Getting Started](./getting-started.md)** - Installation and first steps
- **[Configuration Guide](./configuration-guide.md)** - Essential configuration options
- **[API Reference](./api-reference/)** - Complete API documentation

## 📚 Core Documentation

### Architecture & Design
- **[Architecture Overview](./architecture-overview.md)** - System design and components
- **[Clean Architecture Guide](./clean-architecture-guide.md)** - Development principles
- **[LLM Routing](./llm-routing.md)** - How Conduit routes requests between providers

### API Documentation
- **[Core API Reference](./core-api-detailed-reference.md)** - Chat, completions, embeddings
- **[Admin API](./admin-api/)** - Virtual keys, providers, configuration
- **[Webhook API](./webhook-api.md)** - Event notifications
- **[Real-Time API Guide](./real-time-api-guide.md)** - WebSocket/SignalR integration

### Features
- **[Virtual Keys](./virtual-keys.md)** - API key management and routing
- **[Provider Integration](./provider-integration.md)** - Supported LLM providers
- **[Budget Management](./budget-management.md)** - Cost control and limits
- **[Function Calling Guide](./function-calling-guide.md)** - Tool use and function calling

### Web UI
- **[WebUI Guide](./WebUI-Guide.md)** - Admin dashboard overview
- **[Dashboard Features](./Dashboard-Features.md)** - UI capabilities

## 🔧 Operations

### Deployment
- **[Deployment Configuration](./deployment/DEPLOYMENT-CONFIGURATION.md)** - Production setup
- **[Docker Optimization](./deployment/docker-optimization.md)** - Container best practices
- **[Environment Variables](./Environment-Variables.md)** - Configuration options

### Monitoring & Performance
- **[Health Monitoring Guide](./Health-Monitoring-Guide.md)** - System health checks
- **[Performance Metrics](./performance-metrics.md)** - Tracking and optimization
- **[Grafana Dashboards](./grafana-dashboards/README.md)** - Monitoring setup

### Runbooks
- **[Runbooks Overview](./runbooks/README.md)** - Operational procedures
- **[Troubleshooting Guide](./troubleshooting/TROUBLESHOOTING-GUIDE.md)** - Common issues

## 🛠️ Development

### SDKs & Clients
- **[SDK Quick Reference](./sdk-quick-reference.md)** - SDK usage
- **[Node.js SDK Documentation](../SDKs/Node/docs/README.md)** - JavaScript/TypeScript SDKs
- **[SDK Best Practices](./sdk-best-practices.md)** - Integration patterns

### Development Guides
- **[Development Overview](./development/SDK-MIGRATION-COMPLETE.md)** - Development setup
- **[API Patterns Best Practices](./development/API-PATTERNS-BEST-PRACTICES.md)** - API design
- **[Testing Guide](./Mutation-Testing-Guide.md)** - Testing strategies

## 💰 Pricing & Models

- **[Model Pricing Overview](./model-pricing/README.md)** - Cost information
- **[Model Costs](./Model-Costs.md)** - Detailed pricing data
- **[Advanced Pricing Patterns](./model-pricing/comprehensive-pricing-patterns-analysis.md)** - Complex pricing models

## 📖 Specialized Topics

### Audio & Media
- **[Audio API Guide](./Audio-API-Guide.md)** - Speech synthesis and recognition
- **[Audio Architecture](./Audio-Architecture.md)** - Technical implementation
- **[Video Architecture](./VIDEO_ARCHITECTURE.md)** - Video generation

### Real-Time Features
- **[SignalR Documentation](./signalr/)** - Complete SignalR/WebSocket documentation
- **[Real-Time API Guide](./real-time-api-guide.md)** - API integration patterns
- **[Real-Time Features Index](./Real-Time-Features-Index.md)** - Available features

### Security
- **[Security Guidelines](./Security-Guidelines.md)** - Best practices
- **[Admin API Security](./EPIC-Admin-API-Security-Features.md)** - Authentication & authorization


## 🔍 Finding Information

### By Feature
- **Virtual Keys**: [Overview](./virtual-keys.md) | [API](./Admin-API.md#virtual-keys)
- **Provider Config**: [Integration](./provider-integration.md) | [Models](./claude/provider-models.md)
- **Real-Time**: [Quick Start](./SignalR-Quick-Start-Guide.md) | [Architecture](./Realtime-Architecture.md)
- **Monitoring**: [Health](./Health-Monitoring-Guide.md) | [Metrics](./PERFORMANCE_METRICS.md)

### By User Role
- **Developers**: Start with [Getting Started](./getting-started.md) and [SDK Quick Reference](./sdk-quick-reference.md)
- **Administrators**: See [WebUI Guide](./WebUI-Guide.md) and [Configuration](./configuration-guide.md)
- **DevOps**: Check [Deployment](./deployment/DEPLOYMENT-CONFIGURATION.md) and [Runbooks](./runbooks/README.md)

## 📝 Contributing

When adding new documentation:
1. Use kebab-case for file names (e.g., `my-new-feature.md`)
2. Add your document to this index
3. Include "Last Updated" date in your document
4. Cross-reference related documentation

---

*For the latest updates and releases, see the [GitHub repository](https://github.com/knnlabs/Conduit).*