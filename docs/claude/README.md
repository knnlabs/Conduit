# Claude Documentation

This directory contains detailed documentation for Claude-specific implementations and configurations in Conduit.

## Contents

- **[Distributed Cache Statistics](./distributed-cache-statistics.md)** - Horizontal scaling architecture for cache metrics collection
- **[Event-Driven Architecture](./event-driven-architecture.md)** - MassTransit events, domain events, and troubleshooting
- **[Incident Health History Requirements](./incident-health-history-requirements.md)** - Health monitoring specifications
- **[Media Storage Configuration](./media-storage-configuration.md)** - S3/CDN setup, Docker SignalR configuration
- **[Provider Models](./provider-models.md)** - Supported models by provider (MiniMax, OpenAI, Replicate)
- **[RabbitMQ High-Throughput](./rabbitmq-high-throughput.md)** - Production scaling, 1000+ tasks/minute configuration
- **[SignalR Configuration](./signalr-configuration.md)** - Real-time updates, Redis backplane, multi-instance setup
- **[XML Documentation Standards](./xml-documentation-standards.md)** - Comprehensive XML documentation requirements

## Overview

These documents were created to provide Claude (AI assistant) with detailed context about specific Conduit subsystems. They serve as:

1. **Technical Reference** - Deep dives into complex implementations
2. **Configuration Guides** - Production-ready settings and optimizations
3. **Best Practices** - Patterns and standards for maintaining the codebase

## Usage

When working with Claude on Conduit development:
- Reference these documents for accurate implementation details
- Use them as templates for documenting new complex features
- Keep them updated as the system evolves

## Note

These documents contain production-critical information and should be kept up-to-date with any architectural changes.