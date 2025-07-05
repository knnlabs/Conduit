# Audio API Guide

This guide covers the audio capabilities available in ConduitLLM, including audio transcription (Speech-to-Text), text-to-speech (TTS), and real-time bidirectional audio streaming.

## Implementation Status

The Audio API is **fully implemented** with the following features:
- ✅ Speech-to-Text (Transcription)
- ✅ Text-to-Speech 
- ✅ Real-time Audio Streaming
- ✅ Provider routing and failover
- ✅ Virtual key tracking and cost attribution
- ✅ Export/import functionality
- ✅ Advanced security features

## Overview

ConduitLLM provides a unified interface for audio operations across multiple providers:

### Production Ready
- **OpenAI**: Whisper (transcription), TTS, and Realtime API
- **Azure OpenAI**: Azure-hosted Whisper and TTS models
- **ElevenLabs**: Premium text-to-speech and conversational AI
- **Ultravox**: Low-latency real-time conversational AI
- **Groq**: High-speed Whisper transcription
- **Deepgram**: Real-time STT with excellent accuracy

### Coming Soon
- **Google Cloud**: Speech-to-Text and Text-to-Speech
- **Amazon Polly/Transcribe**: AWS audio services

## Audio Transcription (Speech-to-Text)

### Basic Transcription

```csharp
var client = conduit.GetClient("whisper-1");
if (client is IAudioTranscriptionClient transcriptionClient)
{
    var request = new AudioTranscriptionRequest
    {
        AudioData = audioBytes,
        Language = "en", // Optional - auto-detect if not specified
        OutputFormat = TranscriptionFormat.Text
    };
    
    var response = await transcriptionClient.TranscribeAudioAsync(request);
    Console.WriteLine($"Transcription: {response.Text}");
}
```

### Supported Audio Formats

Most providers support common audio formats:
- MP3, MP4, MPEG, MPGA, M4A
- WAV, WEBM
- FLAC, OGG

### Transcription with Timestamps

```csharp
var request = new AudioTranscriptionRequest
{
    AudioData = audioBytes,
    IncludeTimestamps = true,
    OutputFormat = TranscriptionFormat.Verbose
};

var response = await transcriptionClient.TranscribeAudioAsync(request);
foreach (var segment in response.Segments)
{
    Console.WriteLine($"[{segment.StartTime:F2}s - {segment.EndTime:F2}s] {segment.Text}");
}
```

## Text-to-Speech (TTS)

### Basic TTS

```csharp
var client = conduit.GetClient("tts-1");
if (client is ITextToSpeechClient ttsClient)
{
    var request = new TextToSpeechRequest
    {
        Text = "Hello, this is a test of the text-to-speech system.",
        Voice = "alloy",
        Model = "tts-1",
        OutputFormat = AudioFormat.Mp3
    };
    
    var response = await ttsClient.CreateSpeechAsync(request);
    await File.WriteAllBytesAsync("output.mp3", response.AudioData);
}
```

### Available Voices

**OpenAI TTS Voices:**
- alloy, echo, fable, onyx, nova, shimmer

**ElevenLabs Voices:**
- rachel, sam, charlie, emily, adam, elli, josh (and many more)

### Voice Streaming

For low-latency applications, you can stream TTS audio:

```csharp
await foreach (var chunk in ttsClient.StreamSpeechAsync(request))
{
    // Process audio chunks as they arrive
    await audioPlayer.PlayChunkAsync(chunk.AudioData);
}
```

## Real-time Bidirectional Audio

Real-time audio enables interactive voice conversations, perfect for telephone systems, voice assistants, and interactive agents.

### Creating a Real-time Session

```csharp
var client = conduit.GetClient("gpt-4o-realtime");
if (client is IRealtimeAudioClient realtimeClient)
{
    var config = new RealtimeSessionConfig
    {
        Model = "gpt-4o-realtime-preview",
        Voice = "alloy",
        InputFormat = RealtimeAudioFormat.PCM16_24kHz,
        OutputFormat = RealtimeAudioFormat.PCM16_24kHz,
        SystemPrompt = "You are a helpful assistant.",
        TurnDetection = new TurnDetectionConfig
        {
            Type = TurnDetectionType.ServerVAD,
            Threshold = 0.5,
            SilenceThresholdMs = 200
        }
    };
    
    var session = await realtimeClient.CreateSessionAsync(config);
}
```

### Streaming Audio

```csharp
var stream = realtimeClient.StreamAudioAsync(session);

// Send audio in a separate task
var sendTask = Task.Run(async () =>
{
    while (isRecording)
    {
        var audioFrame = new RealtimeAudioFrame
        {
            AudioData = await microphone.ReadFrameAsync()
        };
        await stream.SendAsync(audioFrame);
    }
    await stream.CompleteAsync();
});

// Receive responses
await foreach (var response in stream.ReceiveAsync())
{
    switch (response.EventType)
    {
        case RealtimeEventType.AudioDelta:
            await speaker.PlayAudioAsync(response.Audio.Data);
            break;
            
        case RealtimeEventType.TranscriptionDelta:
            Console.WriteLine($"Transcript: {response.Transcription.Text}");
            break;
            
        case RealtimeEventType.TurnEnd:
            Console.WriteLine("Assistant finished speaking");
            break;
    }
}
```

### Function Calling in Real-time

Real-time sessions can also invoke functions:

```csharp
var config = new RealtimeSessionConfig
{
    Tools = new List<Tool>
    {
        new Tool
        {
            Type = "function",
            Function = new FunctionDefinition
            {
                Name = "get_weather",
                Description = "Get current weather for a location",
                Parameters = weatherSchema
            }
        }
    }
};

// Handle function calls in the response stream
if (response.EventType == RealtimeEventType.FunctionCall)
{
    var result = await ExecuteFunction(response.FunctionCall);
    await stream.SendAsync(new RealtimeFunctionResponse
    {
        CallId = response.FunctionCall.CallId,
        Output = JsonSerializer.Serialize(result)
    });
}
```

## Advanced Features

### Routing Strategies

The Audio API supports multiple routing strategies:
- **Cost-optimized**: Routes to cheapest provider
- **Quality-based**: Routes to highest quality provider
- **Latency-based**: Routes to fastest provider
- **Geographic**: Routes based on user location
- **Language-optimized**: Routes based on language expertise

### Security Features

- **Audio Encryption**: AES-256-GCM encryption for audio streams
- **PII Detection**: Automatic detection and redaction of sensitive information
- **Content Filtering**: Filter inappropriate audio content
- **Audit Logging**: Comprehensive audit trail for compliance

### Export/Import

Export audio usage data and costs:
```bash
# Export usage data as CSV (default format)
curl -X GET "https://api.conduit.ai/admin/audio/usage/export?format=csv&startDate=2024-01-01&endDate=2024-12-31" \
  -H "X-Master-Key: your-master-key" \
  -o audio_usage_export.csv

# Export usage data as JSON
curl -X GET "https://api.conduit.ai/admin/audio/usage/export?format=json&startDate=2024-01-01&endDate=2024-12-31" \
  -H "X-Master-Key: your-master-key" \
  -o audio_usage_export.json

# Export with filters (provider and virtual key)
curl -X GET "https://api.conduit.ai/admin/audio/usage/export?format=csv&provider=openai&virtualKey=key123&startDate=2024-01-01&endDate=2024-12-31" \
  -H "X-Master-Key: your-master-key" \
  -o filtered_audio_usage.csv

# Import cost configuration
curl -X POST "https://api.conduit.ai/admin/audio/costs/import?format=json" \
  -H "X-Master-Key: your-master-key" \
  -d @costs.json
```

**Export Parameters:**
- `format`: Export format (`csv` or `json`, defaults to `csv`)
- `startDate`: Start date filter (YYYY-MM-DD format)
- `endDate`: End date filter (YYYY-MM-DD format)
- `virtualKey`: Filter by specific virtual key (optional)
- `provider`: Filter by provider name (optional)
- `operationType`: Filter by operation type (`transcription`, `tts`, `realtime`) (optional)
- `page`: Page number (defaults to 1)
- `pageSize`: Number of records per page (defaults to max for full export)

**CSV Export Format:**
```
Timestamp,VirtualKey,Provider,Operation,Model,Duration,Cost,Status,Language,Voice
2024-01-01 10:00:00,key_abc123,openai,transcription,whisper-1,120.5,0.0025,200,en,
2024-01-01 10:01:15,key_abc123,openai,tts,tts-1,,,0.0015,200,en,alloy
```

**JSON Export Format:**
```json
[
  {
    "timestamp": "2024-01-01T10:00:00Z",
    "virtualKey": "key_abc123",
    "provider": "openai",
    "operation": "transcription",
    "model": "whisper-1",
    "duration": 120.5,
    "cost": 0.0025,
    "status": 200,
    "language": "en",
    "voice": null,
    "error": null
  }
]
```

## Audio Configuration in Admin API

### Configure Audio Providers

```http
POST /api/admin/audio/providers
{
    "providerCredentialId": 1,
    "transcriptionEnabled": true,
    "defaultTranscriptionModel": "whisper-1",
    "textToSpeechEnabled": true,
    "defaultTTSModel": "tts-1-hd",
    "defaultTTSVoice": "nova",
    "realtimeEnabled": true,
    "defaultRealtimeModel": "gpt-4o-realtime-preview",
    "routingPriority": 100
}
```

### Set Audio Costs

```http
POST /api/admin/audio/costs
{
    "provider": "openai",
    "operationType": "transcription",
    "model": "whisper-1",
    "costUnit": "per_minute",
    "costPerUnit": 0.006,
    "isActive": true
}
```

### Monitor Audio Usage

```http
GET /api/admin/audio/usage/summary?startDate=2024-01-01&endDate=2024-12-31

Response:
{
    "totalOperations": 15420,
    "totalCost": 92.50,
    "totalDurationSeconds": 925000,
    "operationBreakdown": [
        {
            "operationType": "transcription",
            "count": 8500,
            "totalCost": 51.00
        },
        {
            "operationType": "tts",
            "count": 5920,
            "totalCost": 29.60
        },
        {
            "operationType": "realtime",
            "count": 1000,
            "totalCost": 11.90
        }
    ]
}
```

## Best Practices

### 1. Audio Format Selection

- Use PCM16 for best quality and compatibility
- Use Opus for bandwidth-constrained environments
- G.711 μ-law/A-law for telephony integration

### 2. Error Handling

```csharp
try
{
    var response = await transcriptionClient.TranscribeAudioAsync(request);
}
catch (ValidationException ex)
{
    // Invalid audio format or parameters
    Console.WriteLine($"Validation error: {ex.Message}");
}
catch (LLMCommunicationException ex)
{
    // Provider API error
    Console.WriteLine($"API error: {ex.Message}");
}
```

### 3. Cost Optimization

- Use standard quality models (tts-1) for non-critical applications
- Implement client-side VAD to reduce real-time session duration
- Cache TTS outputs for frequently used phrases

### 4. Latency Optimization

- Use streaming for TTS when possible
- Choose providers based on geographic proximity
- Pre-warm connections for real-time sessions

## Provider-Specific Features

### OpenAI
- Supports 50+ languages for transcription
- High-quality neural TTS voices
- Real-time API with GPT-4 integration

### ElevenLabs
- Industry-leading voice cloning
- Emotional voice synthesis
- Multi-language voice support

### Ultravox
- Optimized for telephony (8kHz support)
- Ultra-low latency (<100ms)
- Built-in echo cancellation

## Troubleshooting

### Common Issues

1. **Audio Format Not Supported**
   - Check supported formats with `GetSupportedFormatsAsync()`
   - Convert audio to a supported format before sending

2. **Real-time Connection Drops**
   - Implement reconnection logic
   - Monitor session health with heartbeat messages
   - Check connection limits per virtual key

3. **High Latency**
   - Use appropriate audio chunk sizes (100-200ms)
   - Enable server-side VAD for turn detection
   - Consider using latency-based routing

4. **Cost Attribution**
   - Ensure virtual keys have proper audio permissions
   - Monitor usage through the admin dashboard
   - Set spending limits on virtual keys

### Debug Logging

Enable detailed logging for audio operations:

```csharp
services.AddLogging(builder =>
{
    builder.AddFilter("ConduitLLM.Providers", LogLevel.Debug);
    builder.AddFilter("ConduitLLM.Http.Services.RealtimeProxyService", LogLevel.Debug);
});
```

## See Also

- [API Reference](API-Reference.md) - Complete API documentation
- [Provider Integration](Provider-Integration.md) - Provider-specific details
- [Virtual Keys](Virtual-Keys.md) - Authentication and usage tracking