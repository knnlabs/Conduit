# Audio Architecture

This document describes the audio subsystem architecture in Conduit, including transcription, text-to-speech, and real-time audio streaming capabilities.

## Overview

The audio architecture in Conduit provides a unified interface for audio operations across multiple providers, supporting:

- **Speech-to-Text (STT)**: Audio transcription using various providers
- **Text-to-Speech (TTS)**: Generate spoken audio from text
- **Real-time Audio**: Bidirectional audio streaming for conversational AI
- **Audio Translation**: Translate audio from one language to another

## Core Components

### 1. Audio Interfaces

The audio system is built on three core interfaces defined in `ConduitLLM.Core`:

```csharp
public interface IAudioTranscriptionClient
{
    Task<AudioTranscriptionResponse> TranscribeAudioAsync(
        AudioTranscriptionRequest request,
        string? apiKey = null,
        CancellationToken cancellationToken = default);
}

public interface ITextToSpeechClient
{
    Task<TextToSpeechResponse> GenerateSpeechAsync(
        TextToSpeechRequest request,
        string? apiKey = null,
        CancellationToken cancellationToken = default);
}

public interface IRealtimeAudioClient
{
    Task<IRealtimeSession> CreateSessionAsync(
        RealtimeSessionConfig config,
        string? apiKey = null,
        CancellationToken cancellationToken = default);
}
```

### 2. Audio Router

The `IAudioRouter` interface manages routing decisions for audio operations:

- **DefaultAudioRouter**: Main implementation that considers provider capabilities, costs, and availability
- **SimpleAudioRouter**: Simplified implementation for basic routing scenarios

Routing factors include:
- Provider availability and health
- Language support
- Voice availability (for TTS)
- Cost optimization
- Format support

### 3. Audio Capability Detection

The `AudioCapabilityDetector` determines provider capabilities:

```csharp
public class AudioCapabilityDetector : IAudioCapabilityDetector
{
    public AudioProviderCapabilities GetCapabilities(string provider);
    public bool SupportsTranscription(string provider, string model);
    public bool SupportsTextToSpeech(string provider, string model);
    public bool SupportsRealtimeAudio(string provider, string model);
}
```

## Provider Implementations

### OpenAI Audio

- **Transcription**: Whisper model (`whisper-1`)
- **TTS**: Multiple voices (alloy, echo, fable, onyx, nova, shimmer)
- **Real-time**: OpenAI Realtime API with WebSocket support
- **Formats**: mp3, opus, aac, flac, wav, pcm

### ElevenLabs

- **TTS**: Advanced voice synthesis with custom voices
- **Real-time**: Conversational AI (`conversational-v1`)
- **Voice Cloning**: Support for custom voice creation
- **Languages**: 29+ languages supported

### Ultravox

- **Real-time**: Specialized for low-latency conversational AI
- **Model**: `ultravox-v2`
- **Optimized for**: Voice assistants and interactive applications

### Azure OpenAI

- **Transcription**: Whisper deployments
- **TTS**: Azure-hosted TTS models
- **Integration**: Uses Azure-specific endpoints and authentication

## Real-time Audio Architecture

### WebSocket Proxy Pattern

The real-time audio system uses a proxy pattern to bridge client and provider WebSockets:

```
Client <--WebSocket--> RealtimeController <---> RealtimeProxyService <--WebSocket--> Provider
                                                        |
                                                RealtimeMessageTranslator
```

### Key Components

1. **RealtimeController**: HTTP endpoint that upgrades to WebSocket
2. **RealtimeProxyService**: Core proxy managing bidirectional message flow
3. **RealtimeConnectionManager**: Manages active connections and enforces limits
4. **RealtimeMessageTranslator**: Translates between Conduit and provider formats
5. **RealtimeSessionStore**: Hybrid storage for session state and lifecycle
6. **RealtimeUsageTracker**: Real-time cost and usage monitoring

### Message Translation

Each provider has a specific translator implementing `IRealtimeMessageTranslator`:

- `OpenAIRealtimeTranslatorV2`: Handles OpenAI's real-time protocol
- `ElevenLabsRealtimeTranslator`: Manages ElevenLabs conversational format
- `UltravoxRealtimeTranslator`: Translates Ultravox protocol

### Connection Management

The `RealtimeConnectionManager` handles:
- Per-key connection limits (default: 5 per virtual key)
- Total connection limits (default: 1000 system-wide)
- Connection health monitoring
- Automatic cleanup of stale connections
- Graceful shutdown on disconnection

## Audio Request Flow

### Transcription Flow

1. Client sends audio file to `/v1/audio/transcriptions`
2. Virtual key validated with transcription permission check
3. Audio router selects appropriate provider
4. Request forwarded to provider (e.g., OpenAI Whisper)
5. Response returned with transcribed text
6. Usage logged (audio duration, cost)

### TTS Flow

1. Client sends text to `/v1/audio/speech`
2. Virtual key validated with TTS permission check
3. Audio router selects provider based on voice/language
4. Provider generates audio stream
5. Audio streamed back to client
6. Usage tracked (characters processed, cost)

### Real-time Flow

1. Client connects to `/v1/realtime` WebSocket endpoint
2. Virtual key validated with real-time permission
3. Connection manager checks limits
4. WebSocket upgraded and proxy established
5. Messages translated bidirectionally
6. Usage tracked in real-time (duration, tokens)

## Usage Tracking

Audio usage is tracked comprehensively:

### Metrics Tracked

- **Transcription**: Audio duration (seconds), file size
- **TTS**: Character count, voice used, output format
- **Real-time**: Session duration, input/output tokens, audio minutes

### Cost Calculation

Audio cost calculation is handled through the standard cost calculation service:
- Uses provider-specific pricing models
- Integrates with database for dynamic pricing
- Supports tiered pricing and volume discounts
- Automatically updates virtual key spending

Note: The dedicated `AudioCostCalculationService` has been deprecated and removed. Audio costs are now calculated through the unified cost calculation infrastructure.

## Configuration

### Audio Provider Configuration

Audio providers are configured in the database:

```json
{
  "provider": "openai",
  "models": {
    "transcription": ["whisper-1"],
    "tts": ["tts-1", "tts-1-hd"],
    "realtime": ["gpt-4o-realtime-preview"]
  },
  "costs": {
    "whisper-1": { "perMinute": 0.006 },
    "tts-1": { "perCharacter": 0.000015 },
    "gpt-4o-realtime-preview": { 
      "perMinute": 0.06,
      "inputTokenPer1k": 0.005,
      "outputTokenPer1k": 0.020
    }
  }
}
```

### Virtual Key Permissions

Virtual keys can have specific audio permissions:

```csharp
public class VirtualKey
{
    public bool CanUseAudioTranscription { get; set; }
    public bool CanUseTextToSpeech { get; set; }
    public bool CanUseRealtimeAudio { get; set; }
    public int MaxConcurrentRealtimeSessions { get; set; } = 5;
}
```

## Error Handling

The audio system implements comprehensive error handling:

### Provider-Specific Errors

- Rate limiting with exponential backoff
- Provider-specific error code mapping
- Automatic failover to alternate providers

### Connection Errors

- WebSocket reconnection logic
- Graceful degradation
- Client notification of connection issues

## Monitoring and Health

### Health Checks

Audio providers are monitored via health checks:

```csharp
public class AudioProviderHealthCheck
{
    public async Task<HealthCheckResult> CheckTranscriptionHealth(string provider);
    public async Task<HealthCheckResult> CheckTTSHealth(string provider);
    public async Task<HealthCheckResult> CheckRealtimeHealth(string provider);
}
```

### Metrics

- Provider response times
- Success/failure rates
- Active real-time connections
- Usage patterns by provider

## Security Considerations

### Authentication

- Virtual key required for all audio endpoints
- Master key support for admin operations
- Provider API keys stored securely

### Input Validation

- File size limits for transcription (25MB default)
- Character limits for TTS (5000 chars default)
- Supported format validation
- Language code validation

### Real-time Security

- Connection authentication before upgrade
- Message size limits
- Rate limiting per connection
- Automatic disconnect on abuse

## Recent Enhancements

Completed improvements to the audio architecture:

1. **Real-time Session Management**: Hybrid storage with Redis and in-memory caching
2. **Advanced Routing**: Multiple strategies including latency, cost, quality, and geographic
3. **Security Features**: Encryption, PII detection, content filtering, audit logging
4. **Performance Optimization**: Connection pooling, stream caching, CDN integration
5. **Virtual Key Tracking**: Complete integration throughout audio pipeline
6. **Export/Import**: Full data portability for usage and costs

## Future Enhancements

Potential improvements for the audio architecture:

1. **Hybrid Processing**: Combine multiple providers for better accuracy
2. **Audio Preprocessing**: Enhanced noise reduction, format conversion
3. **ML-based Routing**: Use machine learning for provider selection
4. **Batch Operations**: Support bulk transcription/TTS
5. **Custom Models**: Support for fine-tuned audio models
6. **Voice Cloning**: Expand voice cloning capabilities beyond ElevenLabs

## Best Practices

When working with the audio system:

1. **Check Capabilities**: Always verify provider supports required features
2. **Handle Streaming**: Use streaming for large TTS responses
3. **Monitor Costs**: Track audio usage to control costs
4. **Test Failover**: Ensure graceful handling of provider failures
5. **Validate Input**: Check audio formats and sizes before processing
6. **Use Appropriate Models**: Select models based on quality/cost requirements