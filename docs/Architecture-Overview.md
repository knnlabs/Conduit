# Architecture Overview

ConduitLLM is a modular .NET platform providing a unified, OpenAI-compatible API gateway for multiple LLM providers. Its architecture is designed for extensibility, robust configuration, and seamless developer experience—including support for multimodal (vision) models and advanced routing.

## System Components

```
ConduitLLM
├── ConduitLLM.Admin          # Administrative API for configuration and management
├── ConduitLLM.Admin.Tests    # Tests for Admin API functionality
├── ConduitLLM.Configuration  # Central configuration, entities, and standardized DTOs
├── ConduitLLM.Core           # Core business logic, interfaces, and routing
├── ConduitLLM.Examples       # Example integrations and usage
├── ConduitLLM.Http           # OpenAI-compatible HTTP API (REST endpoints)
├── ConduitLLM.Providers      # Provider integrations (OpenAI, Anthropic, Gemini, etc.)
├── ConduitLLM.Tests          # Automated and integration tests
└── ConduitLLM.WebUI          # Blazor-based admin/configuration interface
```

## Component Responsibilities

### ConduitLLM.Admin
- Provides administrative API endpoints for system configuration and management
- Isolates administrative functions from user-facing LLM API
- Implements controllers and services for all configuration operations
- Interfaces with database repositories for data access

### ConduitLLM.Configuration
- Stores provider credentials, model mappings, and global settings
- Manages database schema for configuration and usage tracking
- Houses all standardized DTOs used across the application
- Provides repositories for database access

### ConduitLLM.Core
- Defines LLM API models (including multimodal/vision content; message content uses `object?` to support both plain text and multimodal objects)
- Implements routing, provider selection, and spend/budget logic
- Interfaces for extensibility (custom providers, routing strategies)

### ConduitLLM.Http
- Exposes OpenAI-compatible endpoints (`/v1/chat/completions`, `/v1/models`, etc.)
- Handles authentication (virtual keys), rate limiting, and error handling
- Middleware for request tracking, usage, and spend enforcement
- **IMPORTANT: Contains ALL external API functionality**
- Serves as the only entry point for LLM API clients

### ConduitLLM.Providers
- Integrates with multiple LLM providers (OpenAI, Anthropic, Gemini, Cohere, etc.)
- Maps generic model names to provider-specific models
- Supports multimodal (vision) and streaming APIs where available
- Handles provider-specific request/response formatting

### ConduitLLM.WebUI
- Modern Blazor web app for admin/configuration
- Organized navigation (Core, Configuration, Keys & Costs, System)
- Pages for provider setup, model mapping, routing, virtual key management, and usage analytics
- Real-time notification for budget, key status, and system health
- **IMPORTANT: No external API functionality** - clean separation of concerns
- Communicates with the Admin API via service adapters
- Can operate in direct database mode or API client mode

## Key Subsystems

### Routing System

The router enables intelligent, flexible distribution of requests across model deployments:

1. **DefaultLLMRouter**: Implements routing strategies (simple, random, round-robin), health checks, fallback logic, retry with backoff, and streaming support.
2. **RouterConfig**: Stores routing strategy, model deployments, and fallback settings.
3. **RouterService**: CRUD for deployments and fallback rules; hot-reloadable via WebUI.

### Virtual Key Management

Manages API access, budgets, and usage tracking:

1. **Virtual Key Entity**: Tracks spend, rate limits (RPM/RPD), expiration, and status.
2. **Middleware**: Validates keys, enforces spend/rate limits, logs usage.
3. **Notification System**: Alerts for budget, expiration, and usage anomalies.

### WebUI System

A .NET Blazor web application for system configuration:

1. **Service Adapters**: Interfaces with Admin API while maintaining backward compatibility.
2. **Admin API Client**: HTTP client for communicating with the Admin API.
3. **Pages**: Home/Dashboard, Configuration, Routing, Chat, Virtual Keys, Model Costs, System Info, etc.
4. **Navigation**: Logical grouping for Core, Configuration, Keys & Costs, System.

## Data Flow

1. **Request Flow**:
   - Client sends request (OpenAI-compatible, with virtual key)
   - System authenticates and validates key, enforces limits
   - Router selects model/provider based on mapping and strategy
   - Request is formatted and sent to provider
   - Response is returned (streaming or standard)
   - Usage and spend are tracked; notifications triggered as needed

2. **Configuration Flow**:
   - Admin configures providers, models, routing via WebUI
   - WebUI communicates with Admin API via service adapters
   - Admin API processes and validates configuration changes
   - Configuration is stored in the database and hot-reloaded
   - Virtual keys and budgets are managed from the UI

## Security Architecture
- Master key for privileged/admin operations
- Virtual key spend and rate limit enforcement
- Secure storage of provider and virtual keys
- Comprehensive request logging and audit trails

## Integration Points
- LLM provider APIs (OpenAI, Anthropic, Gemini, etc.) via HTTP/HTTPS
- Database for configuration and usage
- Admin API for administrative operations
- Blazor WebUI for management and analytics
- Notification system for user/system alerts

## Extensibility & Compatibility
- OpenAI API compatibility: Use existing OpenAI SDKs and tools
- Multimodal/vision support for compatible models/providers
- Easily add new providers, models, or routing strategies
- Designed for containerization and scalable deployment (official public Docker images available)
