# Conduit Architecture Documentation

This directory contains comprehensive architecture documentation for ConduitLLM, organized by domain and topic.

**Last Updated**: 2025-11-08

## 📋 Table of Contents

- [Design Patterns](#design-patterns)
- [Provider System](#provider-system)
- [Media Generation](#media-generation)
- [Real-Time Communication](#real-time-communication)
- [Data Transfer](#data-transfer)
- [Infrastructure](#infrastructure)
- [API Integration](#api-integration)

---

## 🎯 Design Patterns

**Location**: `patterns/`

### [Repository and Data Access](patterns/repository-and-data-access.md)
Comprehensive guide to repository patterns, data access strategies, and Entity Framework Core implementation.

**Topics Covered**:
- Generic repository pattern implementation
- Concrete vs interface DbContext usage (critical for EF Core features)
- Factory pattern vs Scoped pattern
- Best practices for Include() and lazy loading
- Testing strategies with in-memory databases

### [Background Services and Workers](patterns/background-services-and-workers.md)
Complete guide to background processing patterns, distributed locking, and eventual consistency.

**Topics Covered**:
- Worker pattern with queue-based processing
- Task lease pattern for distributed processing
- Distributed locking with Redis
- Optimistic concurrency control
- Eventual consistency with self-healing
- Lease recovery and fault tolerance

### [Naming Conventions](patterns/naming-conventions.md)
Event handler naming standards and conventions.

**Topics Covered**:
- Single event handler naming
- Multi-event handler naming
- Special purpose handler patterns
- Migration guidelines

### [Utility Classes](patterns/utility-classes.md)
Guide to using standardized utility classes across the codebase.

**Topics Covered**:
- StreamHelper for SSE processing
- ExceptionHandler for error handling
- BaseLLMClient for provider clients
- FileHelper for file operations
- ValidationHelper for input validation

---

## 🔌 Provider System

**Location**: `provider-system/`

### [Provider Architecture](provider-system/provider-architecture.md)
Comprehensive guide to the provider system architecture, supporting multiple instances of the same provider type.

**Topics Covered**:
- Provider ID vs ProviderType distinction (critical concept)
- Multi-instance provider support
- ProviderAccountGroup for external account separation
- Provider Registry pattern with automatic discovery
- Health monitoring and failover
- Provider credential management

### [Model and Cost Mapping](provider-system/model-and-cost-mapping.md)
Unified guide for model mapping flow and cost tracking across providers.

**Topics Covered**:
- Model alias to provider model ID mapping
- Consistent mapping flow pattern
- Cost configuration and tracking
- ModelCostMapping for flexible cost structures
- Usage tracking and billing

### [Error Tracking](provider-system/error-tracking.md)
Provider error tracking and automatic key disabling system.

**Topics Covered**:
- Provider-specific error registry
- Automatic key health monitoring
- Error pattern detection
- Key disabling/enabling logic

### [Provider System Analysis](provider-system/provider-system-analysis.md)
Architectural analysis and potential improvements for the provider system.

**Topics Covered**:
- Current provider integration challenges
- Switch statement patterns and code smells
- Provider manifest concept
- Plugin-style architecture considerations
- Code generation for cross-language types

---

## 🎬 Media Generation

**Location**: `media-generation/`

### [Async Media Generation](media-generation/async-media-generation.md)
Complete architecture for asynchronous image and video generation with event-driven processing.

**Topics Covered**:
- Event-driven video generation flow
- Redis Streams for task distribution
- Background service worker pattern
- Retry mechanism with exponential backoff
- Task lifecycle and state management
- Image generation with async orchestration

### [Progress and Notifications](media-generation/progress-and-notifications.md)
SignalR-based real-time progress tracking with polling fallback.

**Topics Covered**:
- SignalR video generation hub
- Real-time progress updates
- Polling fallback mechanism
- Client integration patterns

---

## ⚡ Real-Time Communication

**Location**: `real-time/`

### [Streaming and WebSockets](real-time/streaming-and-websockets.md)
Unified architecture for real-time communication including WebSocket audio streaming and SSE chat streaming.

**Topics Covered**:
- SignalR architecture with multi-hub design
- WebSocket audio streaming with base64 encoding
- SSE chat streaming with performance metrics
- TTFT (Time To First Token) measurement
- Tokens per second calculation
- Hybrid audio/video streaming patterns

### [Webhook Delivery](real-time/webhook-delivery.md)
Distributed webhook delivery system with circuit breaker pattern.

**Topics Covered**:
- Redis-based distributed circuit breaker
- Retry policy with exponential backoff
- Connection pooling and performance
- Monitoring and health checks

---

## 📦 Data Transfer

**Location**: `data-transfer/`

### [DTO Guidelines](data-transfer/dto-guidelines.md)
Comprehensive guide to Data Transfer Objects (DTOs) standardization and usage patterns.

**Topics Covered**:
- DTO patterns and when to use them
- Direct entity to API DTO (recommended for CRUD)
- Service layer DTOs for business logic
- Mapping strategies and extension methods
- Cross-service communication patterns
- Validation and error handling

---

## 🏗️ Infrastructure

**Location**: `infrastructure/`

### [Scaling Architecture](infrastructure/scaling-architecture.md)
Complete scaling architecture supporting 10,000+ concurrent sessions.

**Topics Covered**:
- Scaling from 0 to 10,000+ concurrent sessions
- RabbitMQ message queue scaling (1,000+ tasks/minute)
- SignalR Redis backplane for horizontal scaling
- HTTP connection pooling and circuit breakers
- Distributed cache statistics
- Performance benchmarks and monitoring

### [Cache Usage](infrastructure/cache-usage.md)
Gateway API cache implementation analysis and patterns.

**Topics Covered**:
- IMemoryCache vs IDistributedCache usage
- Virtual Key caching with Redis
- Security and rate limiting cache
- Async task management cache
- Cache invalidation patterns
- Cache statistics and monitoring

---

## 🔗 API Integration

**Location**: `api/`

### [Admin API Integration](api/admin-api-integration.md)
Guide to integrating with the Admin API using HTTP client and adapter pattern.

**Topics Covered**:
- Admin API client implementation
- Adapter pattern for switching between direct DB and API access
- Service registration and dependency injection
- Migration strategies from direct DB access
- Error handling and resilience

---

## 🗂️ Directory Structure

```
architecture/
├── README.md (this file)
├── patterns/
│   ├── repository-and-data-access.md
│   ├── background-services-and-workers.md
│   ├── naming-conventions.md
│   └── utility-classes.md
├── provider-system/
│   ├── provider-architecture.md
│   ├── model-and-cost-mapping.md
│   ├── error-tracking.md
│   └── provider-system-analysis.md
├── media-generation/
│   ├── async-media-generation.md
│   └── progress-and-notifications.md
├── real-time/
│   ├── streaming-and-websockets.md
│   └── webhook-delivery.md
├── data-transfer/
│   └── dto-guidelines.md
├── infrastructure/
│   ├── scaling-architecture.md
│   └── cache-usage.md
└── api/
    └── admin-api-integration.md
```

---

## 📝 Documentation Standards

All architecture documentation follows these standards:

1. **Comprehensive Examples**: Real code examples from the codebase
2. **Best Practices**: Clearly marked with ✅ (correct) and ❌ (incorrect) patterns
3. **Cross-References**: Links to related documentation
4. **Migration Guides**: Step-by-step migration instructions where applicable
5. **Troubleshooting**: Common issues and solutions
6. **Testing Strategies**: How to test the patterns and implementations

---

## 🔄 Recent Changes

### November 2025
- **Major Reorganization**: Consolidated 30 files into 13 well-organized documents
- **Created Subdirectories**: Organized by domain (patterns, provider-system, media-generation, etc.)
- **Merged Redundant Content**: Eliminated duplicate documentation
- **Improved Navigation**: Created comprehensive index and cross-references

---

## 🚀 Quick Start

1. **New to Conduit Architecture?** Start with [Scaling Architecture](infrastructure/scaling-architecture.md) for system overview
2. **Building Provider Integrations?** Read [Provider Architecture](provider-system/provider-architecture.md)
3. **Implementing Data Access?** Check [Repository and Data Access](patterns/repository-and-data-access.md)
4. **Adding Background Jobs?** Review [Background Services and Workers](patterns/background-services-and-workers.md)
5. **Working with Real-Time Features?** See [Streaming and WebSockets](real-time/streaming-and-websockets.md)

---

## 📚 Related Documentation

- **[API Guides](../api-guides/README.md)** - API endpoint documentation and integration guides
- **[Repository & Data Access](./patterns/repository-and-data-access.md)** - EF Core patterns and migration best practices

---

## 🤝 Contributing

When adding or updating architecture documentation:

1. Place documentation in the appropriate subdirectory
2. Follow the established documentation standards
3. Include code examples from the actual codebase
4. Add cross-references to related documentation
5. Update this README.md index
6. Add deprecation notices to old files if consolidating

---

*For questions or clarifications about architecture, please refer to the specific documentation files or create an issue in the repository.*
