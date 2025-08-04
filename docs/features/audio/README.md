# Audio API Documentation

*Last Updated: 2025-08-01*

Conduit provides a comprehensive Audio API that supports speech-to-text transcription, text-to-speech synthesis, and real-time bidirectional audio streaming across multiple providers.

## Table of Contents
- [Quick Start](#quick-start)
- [Supported Providers](#supported-providers)
- [API Endpoints](#api-endpoints)
- [Real-time Streaming](#real-time-streaming)
- [Configuration](#configuration)
- [Cost Management](#cost-management)

## Implementation Status

The Audio API is **fully implemented** with the following features:
- ✅ Speech-to-Text (Transcription)
- ✅ Text-to-Speech 
- ✅ Real-time Audio Streaming
- ✅ Provider routing and failover
- ✅ Virtual key tracking and cost attribution
- ✅ Export/import functionality
- ✅ Advanced security features

## Quick Start

### 1. Enable Audio for a Virtual Key

```bash
curl -X POST "https://api.conduit.ai/admin/virtual-keys" \
  -H "X-Master-Key: your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Audio App Key",
    "canUseAudioTranscription": true,
    "canUseTextToSpeech": true,
    "canUseRealtimeAudio": true,
    "maxConcurrentRealtimeSessions": 5,
    "spendingLimit": 100.00
  }'
```

### 2. Transcribe Audio (Speech-to-Text)

```bash
curl -X POST "https://api.conduit.ai/v1/audio/transcriptions" \
  -H "Authorization: Bearer condt_YOUR_KEY" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@audio.mp3" \
  -F "model=whisper-1"
```

Response:
```json
{
  "text": "Hello, this is a transcription of the audio file.",
  "duration": 3.5,
  "language": "en"
}
```

### 3. Generate Speech (Text-to-Speech)

```bash
curl -X POST "https://api.conduit.ai/v1/audio/speech" \
  -H "Authorization: Bearer condt_YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "tts-1",
    "input": "Hello, this is generated speech.",
    "voice": "alloy",
    "response_format": "mp3"
  }' \
  --output speech.mp3
```

### 4. Real-time Audio Streaming

```javascript
// Connect to real-time audio endpoint
const ws = new WebSocket('wss://api.conduit.ai/v1/realtime?model=gpt-4o-realtime-preview');

// Configure session
ws.send(JSON.stringify({
  type: 'session.update',
  session: {
    modalities: ['text', 'audio'],
    instructions: 'You are a helpful assistant.',
    voice: 'alloy',
    input_audio_format: 'pcm16',
    output_audio_format: 'pcm16'
  }
}));
```

## Supported Providers

### Production Ready

#### OpenAI
- **Models**: `whisper-1`, `tts-1`, `tts-1-hd`, `gpt-4o-realtime-preview`
- **Features**: Transcription, TTS, Real-time streaming
- **Languages**: 100+ languages for transcription
- **Voices**: 6 voices (alloy, echo, fable, onyx, nova, shimmer)

#### Azure OpenAI
- **Models**: Azure-hosted Whisper and TTS models
- **Features**: Enterprise-grade security and compliance
- **Integration**: Uses same API as OpenAI with Azure endpoints

#### ElevenLabs
- **Models**: Various TTS models including conversational AI
- **Features**: Premium voice synthesis, voice cloning
- **Voices**: 100+ premium voices in multiple languages
- **Real-time**: Low-latency conversational AI

#### Ultravox
- **Models**: Real-time conversational models
- **Features**: Ultra-low latency audio streaming
- **Use Cases**: Real-time conversation applications

#### Groq
- **Models**: High-speed Whisper transcription
- **Features**: Fastest transcription processing
- **Languages**: Same language support as OpenAI Whisper

#### Deepgram
- **Models**: Real-time STT with excellent accuracy
- **Features**: Live transcription, custom models
- **Languages**: 30+ languages with dialect support

### Coming Soon
- **Google Cloud**: Speech-to-Text and Text-to-Speech
- **Amazon**: Polly (TTS) and Transcribe (STT)

## API Endpoints

### Transcription (Speech-to-Text)

```http
POST /v1/audio/transcriptions
Content-Type: multipart/form-data
Authorization: Bearer condt_YOUR_KEY

file: <audio_file>
model: whisper-1
language: en (optional)
prompt: "Custom prompt" (optional)
response_format: text|json|srt|verbose_json|vtt
temperature: 0.0 (optional)
```

**Supported Audio Formats:**
- MP3, MP4, MPEG, MPGA, M4A, WAV, WEBM
- Maximum file size: 25MB

### Text-to-Speech

```http
POST /v1/audio/speech
Content-Type: application/json
Authorization: Bearer condt_YOUR_KEY

{
  "model": "tts-1",
  "input": "Text to convert to speech",
  "voice": "alloy",
  "response_format": "mp3",
  "speed": 1.0
}
```

**Available Voices:**
- `alloy` - Neutral, balanced
- `echo` - Male, clear
- `fable` - British accent
- `onyx` - Deep, authoritative
- `nova` - Young, energetic
- `shimmer` - Soft, whispery

**Response Formats:**
- `mp3` (default)
- `opus`
- `aac`
- `flac`
- `wav`
- `pcm`

### Translation

```http
POST /v1/audio/translations
Content-Type: multipart/form-data
Authorization: Bearer condt_YOUR_KEY

file: <audio_file>
model: whisper-1
prompt: "Context for translation" (optional)
response_format: text|json|srt|verbose_json|vtt
temperature: 0.0 (optional)
```

Translates audio from any supported language to English.

## Real-time Streaming

### WebSocket Connection

```javascript
const ws = new WebSocket('wss://api.conduit.ai/v1/realtime?model=gpt-4o-realtime-preview');

ws.addEventListener('open', () => {
  // Configure the session
  ws.send(JSON.stringify({
    type: 'session.update',
    session: {
      modalities: ['text', 'audio'],
      instructions: 'You are a helpful assistant.',
      voice: 'alloy',
      input_audio_format: 'pcm16',
      output_audio_format: 'pcm16',
      input_audio_transcription: {
        model: 'whisper-1'
      }
    }
  }));
});
```

### Audio Streaming

```javascript
// Send audio data (PCM16, 24kHz)
function sendAudio(audioBuffer) {
  const base64Audio = btoa(String.fromCharCode(...new Uint8Array(audioBuffer)));
  ws.send(JSON.stringify({
    type: 'input_audio_buffer.append',
    audio: base64Audio
  }));
}

// Commit audio buffer for processing
function commitAudio() {
  ws.send(JSON.stringify({
    type: 'input_audio_buffer.commit'
  }));
}

// Handle audio response
ws.addEventListener('message', (event) => {
  const data = JSON.parse(event.data);
  
  if (data.type === 'response.audio.delta') {
    // Play audio chunk
    const audioData = atob(data.delta);
    playAudioChunk(audioData);
  }
});
```

### Session Management

```javascript
// Create conversation
ws.send(JSON.stringify({
  type: 'response.create',
  response: {
    modalities: ['text', 'audio'],
    instructions: 'Please respond to the user\'s question.'
  }
}));

// Cancel current response
ws.send(JSON.stringify({
  type: 'response.cancel'
}));
```

## Configuration

### Provider Configuration

Audio providers are configured via the Admin API:

```bash
# Configure OpenAI for audio
curl -X POST "https://admin.conduit.ai/api/providers" \
  -H "X-Master-Key: your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "OpenAI Audio",
    "providerType": "OpenAI",
    "isEnabled": true,
    "supportsAudioTranscription": true,
    "supportsTextToSpeech": true,
    "supportsRealtimeAudio": true
  }'
```

### Model Mapping

Map model aliases to specific providers:

```bash
curl -X POST "https://admin.conduit.ai/api/model-provider-mappings" \
  -H "X-Master-Key: your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "modelAlias": "whisper-1",
    "providerId": 1,
    "actualModelName": "whisper-1",
    "supportsAudioTranscription": true
  }'
```

### Cost Configuration

Configure pricing for audio models:

```bash
curl -X POST "https://admin.conduit.ai/api/model-costs" \
  -H "X-Master-Key: your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "modelPattern": "whisper-1",
    "inputCostPer1kTokens": 0.006,
    "outputCostPer1kTokens": 0,
    "costCalculationType": "AudioMinute"
  }'
```

### Virtual Key Audio Permissions

```json
{
  "canUseAudioTranscription": true,
  "canUseTextToSpeech": true,
  "canUseRealtimeAudio": true,
  "maxConcurrentRealtimeSessions": 10,
  "maxAudioFileSizeMB": 25,
  "allowedAudioFormats": ["mp3", "wav", "m4a", "flac"]
}
```

## Cost Management

### Usage Tracking

Audio usage is tracked by:
- **Transcription**: Per minute of audio processed
- **Text-to-Speech**: Per character or token converted
- **Real-time**: Per minute of active session time

### Cost Attribution

All audio operations are attributed to the virtual key used:

```json
{
  "virtualKeyId": "vk_123",
  "operation": "audio_transcription",
  "model": "whisper-1",
  "inputMinutes": 2.5,
  "cost": 0.015,
  "timestamp": "2025-08-01T12:00:00Z"
}
```

### Budget Controls

Set spending limits and alerts:

```bash
curl -X PUT "https://admin.conduit.ai/api/virtual-keys/vk_123" \
  -H "X-Master-Key: your-master-key" \
  -H "Content-Type: application/json" \
  -d '{
    "audioSpendingLimit": 50.00,
    "alertThresholds": [0.5, 0.8, 0.9]
  }'
```

## Security Features

### Content Filtering

```json
{
  "contentFiltering": {
    "enabled": true,
    "blockProfanity": true,
    "blockPII": true,
    "customBlocklist": ["sensitive-term-1", "sensitive-term-2"]
  }
}
```

### Audio Encryption

- All audio data is encrypted in transit using TLS 1.3
- Real-time streams use WebSocket Secure (WSS)
- Audio files are temporarily stored with AES-256 encryption

### Access Controls

```json
{
  "ipFiltering": {
    "enabled": true,
    "allowedIPs": ["192.168.1.0/24", "10.0.0.1"]
  },
  "rateLimiting": {
    "requestsPerMinute": 60,
    "concurrentSessions": 5
  }
}
```

## Error Handling

### Common Error Codes

| Code | Description | Solution |
|------|-------------|----------|
| 400 | Invalid audio format | Use supported formats (MP3, WAV, etc.) |
| 413 | File too large | Reduce file size to under 25MB |
| 429 | Rate limit exceeded | Reduce request frequency |
| 451 | Content filtered | Review content filtering settings |
| 503 | Provider unavailable | Provider experiencing issues |

### Error Response Format

```json
{
  "error": {
    "message": "Unsupported audio format",
    "type": "invalid_request_error",
    "param": "file",
    "code": "unsupported_format"
  }
}
```

## SDK Examples

### Node.js SDK

```javascript
import { ConduitClient } from '@conduit/core';

const conduit = new ConduitClient({
  apiKey: 'condt_your_key',
  baseURL: 'https://api.conduit.ai'
});

// Transcribe audio
const transcription = await conduit.audio.transcriptions.create({
  file: fs.createReadStream('audio.mp3'),
  model: 'whisper-1',
  language: 'en'
});

// Generate speech
const speech = await conduit.audio.speech.create({
  model: 'tts-1',
  input: 'Hello world!',
  voice: 'alloy'
});
```

### Python SDK

```python
from conduit import Conduit

conduit = Conduit(api_key="condt_your_key")

# Transcribe audio
with open("audio.mp3", "rb") as audio_file:
    transcription = conduit.audio.transcriptions.create(
        file=audio_file,
        model="whisper-1"
    )

# Generate speech
response = conduit.audio.speech.create(
    model="tts-1",
    input="Hello world!",
    voice="alloy"
)
```

## Monitoring and Analytics

### Usage Analytics

Track audio usage through the Admin API:

```bash
curl -X GET "https://admin.conduit.ai/api/audio/usage" \
  -H "X-Master-Key: your-master-key" \
  -G \
  -d "startDate=2025-07-01" \
  -d "endDate=2025-07-31" \
  -d "groupBy=provider"
```

### Performance Metrics

- **Transcription Accuracy**: Word Error Rate (WER) tracking
- **Latency**: Time from audio upload to transcription completion
- **Throughput**: Requests processed per minute
- **Availability**: Provider uptime and failover rates

### Real-time Session Monitoring

```bash
curl -X GET "https://admin.conduit.ai/api/realtime/sessions/active" \
  -H "X-Master-Key: your-master-key"
```

## Best Practices

### Audio Quality

- **Sample Rate**: Use 16kHz or higher for best transcription accuracy
- **Format**: WAV or FLAC for highest quality, MP3 for smaller files
- **Noise**: Reduce background noise for better transcription results

### Performance Optimization

- **Chunking**: Split large audio files into smaller chunks
- **Parallel Processing**: Process multiple files concurrently
- **Caching**: Cache transcription results for repeated content

### Cost Optimization

- **Model Selection**: Use `tts-1` for cost efficiency, `tts-1-hd` for quality
- **Language Detection**: Let Whisper auto-detect rather than specifying
- **Batch Processing**: Group multiple requests to reduce overhead

## Migration Notes

### Provider Type Migration (Issue #654)

The Audio system has migrated from string-based provider names to the `ProviderType` enum:

```csharp
// Old approach (deprecated)
audioProvider.ProviderName = "OpenAI";

// New approach
audioProvider.ProviderType = ProviderType.OpenAI;
```

This change affects:
- `AudioCost` entities
- `AudioUsageLog` entities  
- `AudioProviderConfig` entities

See [Provider Multi-Instance Architecture](../../architecture/provider-multi-instance.md) for details.

## Related Documentation

- [Audio Architecture](./architecture.md) - Technical implementation details
- [API Guide](./api-guide.md) - Comprehensive API reference  
- [Migration Guide](./migration.md) - Provider type migration information
- [Real-Time API Guide](../../real-time-api-guide.md) - WebSocket implementation
- [Provider Integration](../../provider-integration.md) - Adding new audio providers

---

*For the latest features and updates, see the [Audio Implementation Status](./implementation-status.md) document.*