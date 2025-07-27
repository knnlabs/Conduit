# Conduit Architecture Overview

Conduit is a comprehensive LLM and Audio API gateway with the following architecture:

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

## Key Features

### Admin API Architecture
- All WebUI components use Admin API exclusively
- Repository pattern implemented throughout
- Clean separation between data and presentation layers

### Audio API System
- Core interfaces and models for audio processing
- Speech-to-Text (STT) and Text-to-Speech (TTS) APIs
- Real-time streaming infrastructure
- Provider implementations (OpenAI, ElevenLabs, Ultravox, Groq, Deepgram)
- Admin API integration for configuration
- WebUI dashboards and analytics
- Advanced routing, security, and hybrid processing modes
- Performance optimization with caching, connection pooling, and CDN support

### Repository Pattern
- All data access uses repository interfaces
- Comprehensive test coverage
- No direct DbContext usage in services

### Provider Support
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

## Known Areas for Improvement

### Dynamic Model Configuration
While a dynamic model configuration system exists, some components may still reference models directly:

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

## Future Development Areas

### Platform Enhancements
1. Dynamic model configuration improvements
2. Google Cloud and AWS audio provider integration
3. Advanced audio analytics
4. Multi-region support
5. Advanced routing algorithms
6. Fine-tuning support
7. Batch processing APIs
8. Plugin architecture
9. Custom model hosting
10. Edge deployment options

## Architecture Requirements

### Core Principles
All deployments use:
- Admin API for configuration
- Repository pattern for data access
- Clean separation between layers

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