# Architecture Overview

ConduitLLM is a modular .NET platform providing a unified, OpenAI-compatible API gateway for multiple LLM providers. Its architecture is designed for extensibility, robust configuration, and seamless developer experience—including support for multimodal (vision) models, audio capabilities (transcription, text-to-speech, real-time streaming), and advanced routing.

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
- Audio routing system for transcription, TTS, and real-time audio
- Token counting and context management services
- Caching layer for improved performance

### ConduitLLM.Http
- Exposes OpenAI-compatible endpoints (`/v1/chat/completions`, `/v1/models`, etc.)
- Audio API endpoints (`/v1/audio/transcriptions`, `//v1/audio/speech`, `/v1/audio/translations`)
- Real-time WebSocket endpoint (`/v1/realtime`) for streaming audio conversations
- Handles authentication (virtual keys), rate limiting, and error handling
- Middleware for request tracking, usage, spend enforcement, and IP filtering
- **IMPORTANT: Contains ALL external API functionality**
- Serves as the only entry point for LLM and Audio API clients

### ConduitLLM.Providers
- Integrates with multiple LLM providers (OpenAI, Anthropic, Gemini, Cohere, etc.)
- Audio provider implementations (OpenAI Whisper/TTS, ElevenLabs, Ultravox)
- Real-time session management for streaming audio conversations
- Maps generic model names to provider-specific models
- Supports multimodal (vision) and streaming APIs where available
- Handles provider-specific request/response formatting
- Message translators for real-time provider protocols

### ConduitLLM.WebUI
- Modern Blazor web app for admin/configuration
- Organized navigation (Core, Configuration, Keys & Costs, System)
- Pages for provider setup, model mapping, routing, virtual key management, and usage analytics
- Audio usage dashboard with analytics and provider performance metrics
- Provider health monitoring with audio capability testing
- Real-time notification for budget, key status, and system health
- **IMPORTANT: No external API functionality** - clean separation of concerns
- Communicates exclusively with the Admin API (legacy direct database mode removed)
- Uses repository adapters for backward compatibility

## Key Subsystems

### LLM Routing System

The router enables intelligent, flexible distribution of requests across model deployments:

1. **DefaultLLMRouter**: Implements routing strategies (simple, random, round-robin), health checks, fallback logic, retry with backoff, and streaming support.
2. **RouterConfig**: Stores routing strategy, model deployments, and fallback settings.
3. **RouterService**: CRUD for deployments and fallback rules; hot-reloadable via WebUI.

### Audio Routing System

Manages audio operations across multiple providers with capability-based routing:

1. **DefaultAudioRouter**: Routes transcription, TTS, and real-time requests based on provider capabilities.
2. **AudioCapabilityDetector**: Determines provider support for languages, voices, and formats.
3. **SimpleAudioRouter**: Basic implementation for straightforward audio routing scenarios.

### Virtual Key Management

Manages API access, budgets, and usage tracking:

1. **Virtual Key Entity**: Tracks spend, rate limits (RPM/RPD), expiration, and status.
2. **Middleware**: Validates keys, enforces spend/rate limits, logs usage.
3. **Notification System**: Alerts for budget, expiration, and usage anomalies.

### Real-time Audio System

Enables bidirectional audio streaming for conversational AI:

1. **RealtimeController**: WebSocket endpoint that upgrades HTTP connections.
2. **RealtimeProxyService**: Proxies messages between clients and providers.
3. **RealtimeConnectionManager**: Manages concurrent connections and enforces limits.
4. **Message Translators**: Convert between Conduit's unified format and provider-specific protocols.
5. **Usage Tracking**: Real-time cost calculation based on audio duration and tokens.

### Provider Health Monitoring

Tracks provider availability and performance:

1. **ProviderHealthService**: Periodic health checks for all configured providers.
2. **Circuit Breaker Pattern**: Temporarily disables unhealthy providers.
3. **Health Records**: Historical tracking of provider uptime and response times.
4. **Audio Health Checks**: Specific tests for transcription, TTS, and real-time capabilities.

### Caching System

Improves performance and reduces costs:

1. **CachingLLMClient**: Wrapper that caches LLM responses.
2. **Redis Integration**: Distributed caching for multi-instance deployments.
3. **Cache Metrics**: Tracks hit rates and performance improvements.
4. **Configurable TTL**: Per-model cache duration settings.

### WebUI System

A .NET Blazor web application for system configuration:

1. **Admin API Client**: HTTP client for communicating with the Admin API (no direct DB access).
2. **Service Adapters**: Repository pattern adapters for backward compatibility.
3. **Pages**: Home/Dashboard, Configuration, Routing, Chat, Virtual Keys, Model Costs, System Info, Audio Usage, Provider Health.
4. **Navigation**: Logical grouping for Core, Configuration, Keys & Costs, System, Audio.

## Data Flow

1. **LLM Request Flow**:
   - Client sends request (OpenAI-compatible, with virtual key)
   - System authenticates and validates key, enforces limits
   - Router selects model/provider based on mapping and strategy
   - Request is formatted and sent to provider
   - Response is returned (streaming or standard)
   - Usage and spend are tracked; notifications triggered as needed

2. **Audio Request Flow**:
   - Client sends audio request (transcription/TTS/real-time)
   - Virtual key validated with audio permissions check
   - Audio router selects provider based on capabilities
   - For real-time: WebSocket connection established via proxy
   - Audio data streamed or returned based on operation type
   - Usage tracked (minutes, characters, tokens)

3. **Configuration Flow**:
   - Admin configures providers, models, routing via WebUI
   - WebUI communicates with Admin API exclusively
   - Admin API processes and validates configuration changes
   - Configuration is stored in the database and hot-reloaded
   - Virtual keys and budgets are managed from the UI

## Security Architecture
- Master key for privileged/admin operations
- Virtual key spend and rate limit enforcement
- IP filtering with allowlist/blocklist support
- Audio-specific permissions (transcription, TTS, real-time)
- Secure storage of provider and virtual keys
- Comprehensive request logging and audit trails
- Per-key connection limits for real-time audio

## Integration Points
- LLM provider APIs (OpenAI, Anthropic, Gemini, etc.) via HTTP/HTTPS
- Audio provider APIs (OpenAI Whisper/TTS, ElevenLabs, Ultravox) via HTTP/WebSocket
- Database for configuration and usage (PostgreSQL/MySQL/SQLite)
- Redis for distributed caching (optional)
- Admin API for administrative operations
- Blazor WebUI for management and analytics
- Notification system for user/system alerts
- Real-time WebSocket connections for audio streaming

## Extensibility & Compatibility
- OpenAI API compatibility: Use existing OpenAI SDKs and tools
- OpenAI Audio API compatibility: Compatible with Whisper and TTS endpoints
- OpenAI Realtime API compatibility: WebSocket protocol support
- Multimodal/vision support for compatible models/providers
- Easily add new providers, models, or routing strategies
- Plugin architecture for audio providers and message translators
- Designed for containerization and scalable deployment (official public Docker images available)
- Middleware pipeline for custom request processing
