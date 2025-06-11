# Current Architecture Status

This document provides a snapshot of Conduit's current architecture state as of June 2025.

## Architecture Overview

Conduit has evolved into a comprehensive LLM and Audio API gateway with the following architecture:

### Core Architecture
- **Clean Architecture**: Fully implemented with clear separation of concerns
- **Repository Pattern**: All data access goes through repositories
- **Admin API**: All configuration and management operations use the Admin API
- **No Direct Database Access**: WebUI exclusively uses Admin API (legacy mode removed)

### Major Components
1. **LLM Gateway**: OpenAI-compatible API for multiple providers
2. **Audio System**: Complete audio API with transcription, TTS, and real-time streaming
3. **Admin API**: Comprehensive management API for all configuration
4. **WebUI**: Modern Blazor interface for administration
5. **Real-time Proxy**: WebSocket-based proxy for audio streaming

## Completed Implementations

### ✅ Admin API Migration (100% Complete)
- All WebUI components use Admin API exclusively
- Direct database access completely removed from WebUI
- Repository pattern implemented throughout
- Legacy mode eliminated

### ✅ Audio API Implementation (85% Complete)
- **Phase 1**: Core interfaces and models ✅
- **Phase 2**: Basic audio APIs (STT/TTS) ✅
- **Phase 3**: Real-time infrastructure ✅
- **Phase 4**: Provider implementations (OpenAI, ElevenLabs, Ultravox, Groq, Deepgram) ✅
- **Phase 5**: Admin API integration ✅
- **Phase 6**: WebUI dashboards and analytics ✅
- **Phase 7**: Advanced features (routing, security, hybrid mode) ✅
- **Phase 8.1**: Performance optimization (caching, pooling, CDN) ✅
- **Phase 8.2-8.4**: Monitoring and production readiness (pending)
- **Phase 9**: Launch preparation (pending)

### ✅ Repository Pattern
- All data access uses repository interfaces
- Comprehensive test coverage
- No direct DbContext usage in services

### ✅ Provider Support
**LLM Providers**:
- OpenAI / Azure OpenAI
- Anthropic (Claude)
- Google (Gemini)
- Amazon Bedrock
- Cohere
- Mistral
- Meta (Llama via various providers)
- Many more...

**Audio Providers**:
- OpenAI (Whisper, TTS, Realtime)
- ElevenLabs (TTS, Conversational)
- Ultravox (Realtime)
- Azure OpenAI (Whisper, TTS)
- Groq (High-speed Whisper)
- Deepgram (Real-time STT)

## Architecture Patterns

### Middleware Pipeline
```
Request → IP Filter → Authentication → Rate Limiting → Request Tracking → Controller
```

### Data Flow
```
WebUI → Admin API Client → Admin API → Service → Repository → Database
```

### Real-time Audio Flow
```
Client WebSocket → Realtime Proxy → Message Translator → Provider WebSocket
```

## Current Limitations

### ❌ Hardcoded Models (Partially Complete)
While a dynamic model configuration system exists, some components still have hardcoded model references:

1. **ModelCapabilityDetector**: Hardcoded vision-capable model patterns
2. **AudioCapabilityDetector**: Hardcoded audio model definitions
3. **TiktokenCounter**: Hardcoded tokenizer model patterns
4. **ProviderDefaultModels**: Hardcoded default model selections

**Impact**: Adding new models requires code changes in these components.

## Technology Stack

### Backend
- **.NET 8.0**: Latest LTS version
- **Entity Framework Core**: Database ORM
- **PostgreSQL/MySQL/SQLite**: Supported databases
- **Redis**: Optional distributed caching
- **WebSockets**: Real-time communication

### Frontend
- **Blazor Server**: Interactive web UI
- **Bootstrap 5**: UI framework
- **Chart.js**: Data visualization

### Infrastructure
- **Docker**: Containerization
- **Docker Compose**: Multi-container orchestration
- **GitHub Actions**: CI/CD

## Security Features

- **Virtual Keys**: API key management with budgets and permissions
- **IP Filtering**: Allowlist/blocklist support
- **Master Key**: Administrative access control
- **Rate Limiting**: Per-key and global limits
- **Audit Logging**: Comprehensive request tracking
- **Audio Security**: Encryption, PII detection, content filtering
- **Session Management**: Real-time session tracking and limits

## Monitoring and Observability

- **Health Checks**: Provider availability monitoring
- **Circuit Breakers**: Automatic failover for unhealthy providers
- **Request Logging**: Detailed API usage tracking
- **Performance Metrics**: Response time tracking
- **Cost Tracking**: Real-time usage and spend monitoring

## Configuration Management

All configuration is managed through the Admin API:
- Provider credentials
- Model mappings
- Routing rules
- Virtual keys
- Cost settings
- Audio configurations
- Real-time session limits
- Security policies
- Export/import operations

## Next Steps

### Short-term (Q3 2025)
1. Complete removal of hardcoded models
2. Complete audio phases 8.2-9 (monitoring, production readiness, launch)
3. Add Google Cloud and AWS audio providers
4. Implement advanced audio analytics

### Medium-term (Q4 2025)
1. Multi-region support
2. Advanced routing algorithms
3. Fine-tuning support
4. Batch processing APIs

### Long-term (2026)
1. Plugin architecture
2. Custom model hosting
3. Edge deployment options
4. Advanced analytics

## Migration Notes

### From Legacy Mode
Legacy mode has been completely removed. All deployments must use:
- Admin API for configuration
- Repository pattern for data access
- No direct database access from WebUI

### Database Compatibility
The system supports:
- PostgreSQL (recommended for production)
- MySQL/MariaDB
- SQLite (development only)
- SQL Server (community supported)

## Development Guidelines

1. **Use Repository Pattern**: All data access through repositories
2. **Use Admin API**: All configuration through Admin API
3. **Follow Clean Architecture**: Maintain layer separation
4. **Add Tests**: Comprehensive test coverage required
5. **Document APIs**: All public APIs need XML documentation

## Support and Resources

- **Documentation**: `/docs` folder in repository
- **API Reference**: OpenAPI/Swagger at `/swagger`
- **Examples**: `ConduitLLM.Examples` project
- **Issues**: GitHub Issues for bug reports
- **Discussions**: GitHub Discussions for questions

## Version Information

- **Current Version**: 2025.06
- **Admin API Version**: v1
- **Minimum .NET Version**: 8.0
- **Database Schema Version**: Latest migration applied