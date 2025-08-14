# Audio API Quick Start

## Overview

Conduit provides a unified Audio API that supports:
- **Speech-to-Text** (Transcription)
- **Text-to-Speech** (TTS)
- **Real-time Audio** (Bidirectional streaming)

## Getting Started

### 1. Enable Audio for a Virtual Key

```bash
# Create a virtual key with audio permissions
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
# Transcribe an audio file
curl -X POST "https://api.conduit.ai/v1/audio/transcriptions" \
  -H "Authorization: Bearer condt_YOUR_KEY" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@audio.mp3" \
  -F "model=whisper-1"
```

### 3. Generate Speech (Text-to-Speech)

```bash
# Generate speech from text
curl -X POST "https://api.conduit.ai/v1/audio/speech" \
  -H "Authorization: Bearer condt_YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "tts-1",
    "input": "Hello, this is a test of the audio system.",
    "voice": "nova"
  }' \
  --output speech.mp3
```

### 4. Real-time Audio (WebSocket)

```javascript
// Connect to real-time audio
const ws = new WebSocket('wss://api.conduit.ai/v1/realtime?model=gpt-4o-realtime-preview');
ws.headers = { 'Authorization': 'Bearer condt_YOUR_KEY' };

ws.onopen = () => {
  // Send session configuration
  ws.send(JSON.stringify({
    type: 'session.update',
    session: {
      voice: 'alloy',
      instructions: 'You are a helpful assistant.'
    }
  }));
};

ws.onmessage = (event) => {
  const message = JSON.parse(event.data);
  if (message.type === 'response.audio.delta') {
    // Play audio chunk
    playAudioChunk(message.delta);
  }
};
```

## Supported Providers

| Provider | Transcription | TTS | Real-time |
|----------|---------------|-----|-----------|
| OpenAI | ✅ Whisper | ✅ 6 voices | ✅ GPT-4o |
| ElevenLabs | ❌ | ✅ Premium | ✅ Conversational |
| Deepgram | ✅ Nova-2 | ❌ | ✅ Streaming |
| Groq | ✅ Fast Whisper | ❌ | ❌ |
| Ultravox | ❌ | ❌ | ✅ Low-latency |
| Azure OpenAI | ✅ Whisper | ✅ | ❌ |

## Cost Examples

- **Transcription**: ~$0.006/minute (OpenAI Whisper)
- **TTS**: ~$0.015/1K chars (OpenAI TTS)
- **Real-time**: ~$0.06/minute + token costs (OpenAI Realtime)

## Next Steps

- Read the full [Audio API Guide](Audio-API-Guide.md)
- Learn about [Audio Architecture](Audio-Architecture.md)
- Explore [Real-time Architecture](Realtime-Architecture.md)
- Monitor usage in the [Audio Dashboard](/audio/usage)