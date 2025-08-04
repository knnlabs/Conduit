# Conduit Documentation

*Last Updated: 2025-08-01*

Welcome to the Conduit documentation! This consolidated guide helps you navigate through all available documentation with improved organization and reduced duplication.

## 🚀 Quick Start

- **[Getting Started](./user-guides/getting-started.md)** - Installation and first steps
- **[Configuration Guide](./user-guides/configuration.md)** - Essential configuration options
- **[API Reference](./api-reference/)** - Complete API documentation

## 👥 User Guides

Perfect starting point for developers and administrators:

- **[Getting Started](./user-guides/getting-started.md)** - Installation and setup
- **[Configuration](./user-guides/configuration.md)** - System configuration
- **[Virtual Keys](./user-guides/virtual-keys.md)** - API key management
- **[Budget Management](./user-guides/budget-management.md)** - Cost control and limits
- **[WebUI Guide](./user-guides/webui-guide.md)** - Admin dashboard usage

## 🎯 Features

Comprehensive guides for major Conduit features:

### Audio API
- **[Audio Overview](./features/audio/README.md)** - Complete audio capabilities guide
- **[Audio Architecture](./features/audio/architecture.md)** - Technical implementation
- **[Audio API Guide](./features/audio/api-guide.md)** - Detailed API reference
- **[Audio Migration](./features/audio/migration.md)** - Provider type migration

### Real-Time Communication (SignalR)
- **[SignalR Overview](./features/signalr/README.md)** - Real-time features and setup
- **[Implementation Guide](./features/signalr/implementation.md)** - Hub architecture and patterns
- **[Quick Reference](./features/signalr/quick-reference.md)** - Common tasks and examples
- **[Troubleshooting](./features/signalr/troubleshooting.md)** - Debug and fix issues

### Provider Management
- **[Provider Integration](./features/providers.md)** - Multi-instance provider architecture
- **[Security Features](./features/security.md)** - Authentication, IP filtering, audit logging
- **[Model Costs](./model-costs.md)** - Pricing and cost management

## 🏗️ Architecture & Development

System design and development resources:

### Architecture
- **[Architecture Overview](./architecture-overview.md)** - System design and components
- **[Clean Architecture Guide](./clean-architecture-guide.md)** - Development principles
- **[Provider Multi-Instance](./architecture/provider-multi-instance.md)** - Provider architecture
- **[Model Cost Mapping](./architecture/model-cost-mapping.md)** - Cost configuration system
- **[DTO Guidelines](./architecture/dto-guidelines.md)** - Data transfer patterns
- **[Repository Pattern](./architecture/Repository-Pattern.md)** - Data access patterns

### Development
- **[Development Guide](./development/README.md)** - Developer setup and workflows
- **[API Patterns](./development/API-PATTERNS-BEST-PRACTICES.md)** - Best practices
- **[SDK Integration](./development/sdk-integration.md)** - Client SDK usage

### Claude-Specific Documentation
High-quality technical documentation for AI assistant context:

- **[Claude Documentation](./claude/README.md)** - Overview of Claude-specific docs
- **[Database Migration Guide](./claude/database-migration-guide.md)** - PostgreSQL migration procedures
- **[Event-Driven Architecture](./claude/event-driven-architecture.md)** - MassTransit and domain events
- **[Media Storage Configuration](./claude/media-storage-configuration.md)** - S3/CDN setup
- **[RabbitMQ High-Throughput](./claude/rabbitmq-high-throughput.md)** - Production scaling
- **[SignalR Configuration](./claude/signalr-configuration.md)** - Real-time setup
- **[XML Documentation Standards](./claude/xml-documentation-standards.md)** - Documentation requirements

## 📚 API Documentation

Complete API references:

- **[Core API Reference](./core-api-detailed-reference.md)** - Chat, completions, embeddings
- **[Admin API](./admin-api/)** - Administrative operations
- **[Webhook API](./webhook-api.md)** - Event notifications
- **[Real-Time API Guide](./real-time-api-guide.md)** - WebSocket/SignalR integration

## 🔧 Operations

Production deployment and monitoring:

### Deployment
- **[Deployment Configuration](./deployment/DEPLOYMENT-CONFIGURATION.md)** - Production setup
- **[Docker Optimization](./deployment/docker-optimization.md)** - Container best practices
- **[Environment Variables](./environment-variables.md)** - Configuration options

### Monitoring & Performance
- **[Health Monitoring Guide](./Health-Monitoring-Guide.md)** - System health checks
- **[Performance Metrics](./performance-metrics.md)** - Tracking and optimization
- **[Grafana Dashboards](./grafana-dashboards/README.md)** - Monitoring setup
- **[Prometheus Setup](./prometheus-metrics-setup.md)** - Metrics collection

### Runbooks
- **[Runbooks Overview](./runbooks/README.md)** - Operational procedures
- **[Troubleshooting Guide](./troubleshooting/TROUBLESHOOTING-GUIDE.md)** - Common issues

## 💰 Pricing & Models

Cost management and model information:

- **[Model Pricing Overview](./model-pricing/README.md)** - Cost information across providers
- **[Model Costs](./model-costs.md)** - Detailed pricing configuration
- **[Comprehensive Pricing Analysis](./model-pricing/comprehensive-pricing-patterns-analysis.md)** - Complex pricing patterns

## 📖 Additional Topics

### Media Generation
- **[Video Architecture](./VIDEO_ARCHITECTURE.md)** - Video generation system
- **[Media Generation Analysis](./media-generation-analysis.md)** - Implementation details

### Testing & Quality
- **[Test Coverage](./test-coverage-report.md)** - Quality metrics
- **[Mutation Testing Guide](./Mutation-Testing-Guide.md)** - Advanced testing strategies

### Integration Examples
- **[Integration Examples](./examples/INTEGRATION-EXAMPLES.md)** - Usage patterns
- **[OpenAI Compatible Example](./examples/openai-compatible-example.md)** - Custom provider setup

## 🗂️ Archived Documentation

Historical and reference material:

- **[Implementation Summaries](./archive/implementation-summaries/)** - Completed implementation docs
- **[Technical Debt](./archive/technical-debt/)** - Known technical debt and future migrations
- **[Migration Notes](./archive/migration-notes/)** - Historical migration information
- **[SignalR Consolidation](./archive/signalr-consolidation/)** - Original SignalR documentation
- **[Audio Consolidation](./archive/audio-consolidation/)** - Original audio documentation

## 🔍 Finding Information

### By User Role
- **Developers**: Start with [User Guides](./user-guides/) and [Features](./features/)
- **Administrators**: See [WebUI Guide](./user-guides/webui-guide.md) and [Operations](#operations)
- **DevOps**: Check [Deployment](./deployment/) and [Runbooks](./runbooks/)
- **AI Assistants**: Reference [Claude Documentation](./claude/) for technical context

### By Feature Area
- **Authentication & Security**: [Security Features](./features/security.md) | [Virtual Keys](./user-guides/virtual-keys.md)
- **Audio Processing**: [Audio Overview](./features/audio/README.md) | [Audio API](./features/audio/api-guide.md)
- **Real-Time Features**: [SignalR Overview](./features/signalr/README.md) | [Implementation](./features/signalr/implementation.md)
- **Provider Management**: [Provider Integration](./features/providers.md) | [Multi-Instance Architecture](./architecture/provider-multi-instance.md)
- **Cost Management**: [Model Costs](./model-costs.md) | [Budget Management](./user-guides/budget-management.md)

### By Task
- **Getting Started**: [Installation](./user-guides/getting-started.md) → [Configuration](./user-guides/configuration.md) → [First API Call](./user-guides/getting-started.md#first-api-call)
- **Adding a Provider**: [Provider Integration](./features/providers.md#adding-a-new-provider-instance)
- **Setting Up Audio**: [Audio Quick Start](./features/audio/README.md#quick-start)
- **Implementing Real-Time**: [SignalR Quick Reference](./features/signalr/quick-reference.md)
- **Troubleshooting**: [General Troubleshooting](./troubleshooting/TROUBLESHOOTING-GUIDE.md) | [SignalR Issues](./features/signalr/troubleshooting.md)

## 📝 Contributing to Documentation

When adding new documentation:

1. **Follow the new structure** - Place documents in appropriate feature/category directories
2. **Use consistent naming** - kebab-case for file names (e.g., `my-new-feature.md`)
3. **Add to navigation** - Update this README and relevant section READMEs
4. **Include metadata** - Add "Last Updated" date and clear headings
5. **Cross-reference** - Link to related documentation
6. **Consider consolidation** - Can this be merged with existing docs?

### Documentation Quality Standards

- **Lead with purpose**: Clear opening explaining what the doc covers
- **Progressive disclosure**: Start with common tasks, then advanced topics
- **Consistent structure**: Overview → Quick Start → Detailed Reference → Troubleshooting
- **Practical examples**: Include working code samples and API calls
- **Current information**: Ensure all examples work with the current version

## 📊 Documentation Structure Changes

This documentation has been significantly reorganized for better usability:

### What's New ✨
- **Consolidated Features**: SignalR (15+ files → 4 files), Audio (6+ files → 4 files)
- **Clear User Paths**: Organized by user role and common tasks
- **Reduced Duplication**: Eliminated redundant information across files
- **Better Navigation**: Logical grouping and clear cross-references
- **Quality Focus**: High-quality, maintained documentation prioritized

### What's Archived 📦
- Implementation summary files (completed work)
- Outdated EPIC files (historical planning)
- Technical debt tracking (moved to archive)
- Duplicate/redundant documentation

### Benefits 📈
- **50% fewer files** while maintaining all useful information
- **Faster information finding** with logical organization
- **Reduced maintenance** through consolidation
- **Better user experience** with clear navigation paths

---

## 🆘 Need Help?

1. **Check the appropriate feature guide** in the navigation above
2. **Search this README** for your topic or task
3. **Review troubleshooting guides** for common issues
4. **Consult the API reference** for specific endpoint details
5. **Check archived documentation** for historical context

*For the latest updates and releases, see the [GitHub repository](https://github.com/knnlabs/Conduit).*

---

*Documentation consolidated and restructured on 2025-08-01. Previous file structure preserved in archive directories.*