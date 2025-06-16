# Audio API Reference

Comprehensive API documentation for audio services in ConduitLLM, including transcription, text-to-speech, and real-time audio endpoints.

## Transcription API

### Transcribe Audio

Converts audio to text using speech recognition.

```http
POST /v1/audio/transcriptions
Content-Type: multipart/form-data
X-Virtual-Key: your-virtual-key

Parameters:
- file: Audio file (required)
- model: Model to use (default: "whisper-1")
- language: ISO-639-1 language code (optional)
- prompt: Optional prompt to guide the model
- response_format: json|text|srt|verbose_json|vtt (default: "json")
- temperature: Sampling temperature 0-1 (default: 0)
```

**Example Request:**

```bash
curl -X POST https://api.conduit.ai/v1/audio/transcriptions \
  -H "X-Virtual-Key: your-key" \
  -F file="@audio.mp3" \
  -F model="whisper-1" \
  -F language="en" \
  -F response_format="verbose_json"
```

**Example Response (verbose_json):**

```json
{
  "task": "transcribe",
  "language": "english",
  "duration": 8.47,
  "text": "Hello, this is a test of the transcription system.",
  "segments": [
    {
      "id": 0,
      "seek": 0,
      "start": 0.0,
      "end": 3.2,
      "text": "Hello, this is a test",
      "tokens": [50364, 15947, 11, 341, 307, 257, 1500],
      "temperature": 0.0,
      "avg_logprob": -0.2743,
      "compression_ratio": 1.235,
      "no_speech_prob": 0.0012
    },
    {
      "id": 1,
      "seek": 0,
      "start": 3.2,
      "end": 5.8,
      "text": "of the transcription system.",
      "tokens": [295, 264, 35288, 1185, 13],
      "temperature": 0.0,
      "avg_logprob": -0.1859,
      "compression_ratio": 1.235,
      "no_speech_prob": 0.0008
    }
  ]
}
```

### Translate Audio

Transcribes audio in any language and translates to English.

```http
POST /v1/audio/translations
Content-Type: multipart/form-data
X-Virtual-Key: your-virtual-key

Parameters:
- file: Audio file (required)
- model: Model to use (default: "whisper-1")
- prompt: Optional prompt to guide the model
- response_format: json|text|srt|verbose_json|vtt (default: "json")
- temperature: Sampling temperature 0-1 (default: 0)
```

## Text-to-Speech API

### Create Speech

Generates audio from text input.

```http
POST /v1/audio/speech
Content-Type: application/json
X-Virtual-Key: your-virtual-key

{
  "input": "Text to convert to speech",
  "model": "tts-1-hd",
  "voice": "nova",
  "response_format": "mp3",
  "speed": 1.0
}
```

**Parameters:**

- `input` (required): Text to generate audio for (max 4096 chars)
- `model` (required): TTS model ID ("tts-1" or "tts-1-hd")
- `voice` (required): Voice to use (alloy, echo, fable, onyx, nova, shimmer)
- `response_format`: Audio format - mp3, opus, aac, flac, wav, pcm (default: "mp3")
- `speed`: Speed of generated audio 0.25-4.0 (default: 1.0)

**Example Response:**

Returns audio file data in the requested format.

### Stream Speech

Streams audio generation for lower latency.

```http
POST /v1/audio/speech/stream
Content-Type: application/json
X-Virtual-Key: your-virtual-key

{
  "input": "Text to stream as speech",
  "model": "tts-1",
  "voice": "alloy",
  "response_format": "opus"
}
```

Returns chunked audio data via Server-Sent Events (SSE).

## Real-time Audio API

### Create Session

Establishes a real-time audio session.

```http
POST /v1/realtime/sessions
Content-Type: application/json
X-Virtual-Key: your-virtual-key

{
  "model": "gpt-4o-realtime-preview",
  "voice": "alloy",
  "instructions": "You are a helpful assistant.",
  "input_audio_format": "pcm16",
  "output_audio_format": "pcm16",
  "input_audio_transcription": {
    "enabled": true,
    "model": "whisper-1"
  },
  "turn_detection": {
    "type": "server_vad",
    "threshold": 0.5,
    "prefix_padding_ms": 300,
    "silence_duration_ms": 200
  },
  "tools": [],
  "temperature": 0.8,
  "max_response_output_tokens": 4096
}
```

**Response:**

```json
{
  "id": "sess_abc123",
  "object": "realtime.session",
  "model": "gpt-4o-realtime-preview",
  "created_at": 1234567890,
  "expires_at": 1234571490,
  "status": "created",
  "ws_url": "wss://api.conduit.ai/v1/realtime/sessions/sess_abc123/ws"
}
```

### WebSocket Connection

Connect to the session WebSocket for bidirectional audio streaming.

```javascript
const ws = new WebSocket('wss://api.conduit.ai/v1/realtime/sessions/sess_abc123/ws');

// Send audio
ws.send(JSON.stringify({
  type: "input_audio_buffer.append",
  audio: btoa(audioData) // base64 encoded PCM16 audio
}));

// Commit audio buffer to trigger response
ws.send(JSON.stringify({
  type: "input_audio_buffer.commit"
}));

// Receive events
ws.onmessage = (event) => {
  const data = JSON.parse(event.data);
  
  switch(data.type) {
    case "session.created":
      console.log("Session ready");
      break;
      
    case "response.audio.delta":
      // Decode and play audio chunk
      const audio = atob(data.delta);
      playAudio(audio);
      break;
      
    case "response.text.delta":
      // Show transcript
      console.log("Transcript:", data.delta);
      break;
      
    case "response.done":
      console.log("Response complete");
      break;
  }
};
```

### Real-time Events

**Client to Server Events:**

- `session.update` - Update session configuration
- `input_audio_buffer.append` - Add audio to input buffer
- `input_audio_buffer.commit` - Process buffered audio
- `input_audio_buffer.clear` - Clear input buffer
- `response.cancel` - Cancel in-progress response

**Server to Client Events:**

- `session.created` - Session established
- `session.updated` - Configuration updated
- `input_audio_buffer.speech_started` - Speech detected
- `input_audio_buffer.speech_stopped` - Speech ended
- `response.created` - Response generation started
- `response.audio.delta` - Audio chunk
- `response.text.delta` - Transcript chunk
- `response.done` - Response complete
- `error` - Error occurred

## Admin Audio APIs

### List Audio Providers

```http
GET /api/admin/audio/providers
X-Master-Key: your-master-key
```

### Configure Audio Provider

```http
POST /api/admin/audio/providers
Content-Type: application/json
X-Master-Key: your-master-key

{
  "providerCredentialId": 1,
  "transcriptionEnabled": true,
  "defaultTranscriptionModel": "whisper-1",
  "textToSpeechEnabled": true,
  "defaultTTSModel": "tts-1-hd",
  "defaultTTSVoice": "nova",
  "realtimeEnabled": true,
  "defaultRealtimeModel": "gpt-4o-realtime-preview",
  "routingPriority": 100,
  "customSettings": {}
}
```

### Set Audio Costs

```http
POST /api/admin/audio/costs
Content-Type: application/json
X-Master-Key: your-master-key

{
  "provider": "openai",
  "operationType": "transcription",
  "model": "whisper-1",
  "costUnit": "per_minute",
  "costPerUnit": 0.006,
  "effectiveDate": "2024-01-01",
  "isActive": true
}
```

### Get Audio Usage

```http
GET /api/admin/audio/usage/summary?startDate=2024-01-01&endDate=2024-12-31
X-Master-Key: your-master-key
```

**Response:**

```json
{
  "totalOperations": 15420,
  "totalCost": 92.50,
  "totalDurationSeconds": 925000,
  "operationBreakdown": [
    {
      "operationType": "transcription",
      "count": 8500,
      "totalCost": 51.00,
      "totalDurationSeconds": 510000,
      "averageDurationSeconds": 60
    },
    {
      "operationType": "tts",
      "count": 5920,
      "totalCost": 29.60,
      "totalCharacters": 1480000
    },
    {
      "operationType": "realtime",
      "count": 1000,
      "totalCost": 11.90,
      "totalDurationSeconds": 119000
    }
  ],
  "providerBreakdown": [
    {
      "provider": "openai",
      "operationCount": 10000,
      "totalCost": 65.00
    },
    {
      "provider": "googlecloud",
      "operationCount": 5420,
      "totalCost": 27.50
    }
  ]
}
```

## Error Responses

All endpoints return consistent error responses:

```json
{
  "error": {
    "code": "invalid_request",
    "message": "The audio file format is not supported",
    "type": "validation_error",
    "param": "file",
    "details": {
      "supported_formats": ["mp3", "mp4", "mpeg", "mpga", "m4a", "wav", "webm"]
    }
  }
}
```

### Common Error Codes

- `invalid_request` - Invalid parameters
- `authentication_failed` - Invalid API key
- `insufficient_credits` - Virtual key limit exceeded
- `model_not_found` - Requested model doesn't exist
- `provider_error` - Upstream provider error
- `rate_limit_exceeded` - Too many requests
- `audio_too_large` - File size exceeds limit
- `audio_too_long` - Duration exceeds limit

## Rate Limits

Default rate limits per virtual key:

- Transcription: 50 requests/minute, 500 MB/hour
- Text-to-Speech: 100 requests/minute, 1M characters/hour
- Real-time Sessions: 5 concurrent, 60 minutes/session

## Webhooks

Configure webhooks for async processing:

```http
POST /api/admin/audio/webhooks
Content-Type: application/json
X-Master-Key: your-master-key

{
  "url": "https://your-app.com/webhook",
  "events": ["transcription.completed", "tts.completed", "realtime.session.ended"],
  "secret": "webhook-secret",
  "active": true
}
```

## SDK Examples

### C# SDK

```csharp
// Initialize client
var conduit = new ConduitClient(virtualKey);

// Transcription
var transcription = await conduit.Audio.TranscribeAsync(
    audioFile: File.OpenRead("audio.mp3"),
    model: "whisper-1",
    language: "en",
    responseFormat: TranscriptionFormat.VerboseJson
);

// Text-to-Speech
var audio = await conduit.Audio.CreateSpeechAsync(
    input: "Hello world",
    model: "tts-1-hd",
    voice: "nova"
);
File.WriteAllBytes("output.mp3", audio.AudioData);

// Real-time
var session = await conduit.Realtime.CreateSessionAsync(new RealtimeSessionConfig
{
    Model = "gpt-4o-realtime-preview",
    Voice = "alloy"
});

await using var stream = await conduit.Realtime.ConnectAsync(session);
// Stream audio...
```

### Python SDK

```python
from conduit import ConduitClient

client = ConduitClient(api_key="your-virtual-key")

# Transcription
with open("audio.mp3", "rb") as audio_file:
    transcription = client.audio.transcribe(
        file=audio_file,
        model="whisper-1",
        language="en"
    )
    print(transcription.text)

# Text-to-Speech
response = client.audio.speech.create(
    input="Hello world",
    model="tts-1-hd",
    voice="nova"
)
response.stream_to_file("output.mp3")
```

## Next Steps

- [Audio Services Overview](../features/audio-services.md) - Learn about audio capabilities
- [Provider Configuration](../features/audio-providers.md) - Set up audio providers
- [Cost Management](../guides/budget-management.md) - Control audio costs