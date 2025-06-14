# Audio Provider Configuration

This guide covers the configuration and setup of audio providers in ConduitLLM, including Google Cloud, AWS, OpenAI, and other supported providers.

## Google Cloud Audio Services

Google Cloud offers comprehensive audio services with support for 125+ languages and advanced features like speaker diarization and word-level confidence scores.

### Prerequisites

1. Create a Google Cloud Platform account
2. Enable the Speech-to-Text and Text-to-Speech APIs
3. Create a service account and download credentials
4. Set up billing (required for audio services)

### Configuration

Add Google Cloud credentials to your configuration:

```json
{
  "Providers": {
    "GoogleCloud": {
      "ApiKey": "your-google-cloud-api-key",
      "ProjectId": "your-project-id",
      "ServiceAccountJson": "/path/to/service-account.json"
    }
  }
}
```

Or use environment variables:
```bash
export GOOGLE_CLOUD_API_KEY="your-api-key"
export GOOGLE_APPLICATION_CREDENTIALS="/path/to/service-account.json"
```

### Setting Up Audio Configuration

```csharp
// Configure via Admin API
POST /api/admin/audio/providers
{
    "providerName": "GoogleCloud",
    "transcriptionEnabled": true,
    "defaultTranscriptionModel": "latest_long",
    "textToSpeechEnabled": true,
    "defaultTTSModel": "en-US-Neural2-F",
    "defaultTTSVoice": "en-US-Neural2-F",
    "customSettings": {
        "enableAutomaticPunctuation": true,
        "enableSpeakerDiarization": true,
        "maxSpeakers": 10,
        "profanityFilter": false
    }
}
```

### Available Models

**Speech-to-Text Models:**
- `latest_long` - Best for audio longer than 1 minute
- `latest_short` - Optimized for short audio clips
- `command_and_search` - For short queries and commands
- `phone_call` - Enhanced for telephony audio
- `video` - Optimized for video transcription

**Text-to-Speech Voices:**
- Neural2 voices: Higher quality, more natural (e.g., `en-US-Neural2-A` through `en-US-Neural2-J`)
- WaveNet voices: Good quality, lower cost (e.g., `en-US-Wavenet-A` through `en-US-Wavenet-J`)
- Standard voices: Basic quality, lowest cost (e.g., `en-US-Standard-A` through `en-US-Standard-J`)

## AWS Audio Services

AWS provides audio services through Amazon Transcribe for speech-to-text and Amazon Polly for text-to-speech.

### Prerequisites

1. Create an AWS account
2. Set up IAM user with appropriate permissions
3. Configure AWS credentials

### Required IAM Permissions

```json
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "transcribe:StartTranscriptionJob",
                "transcribe:GetTranscriptionJob",
                "polly:SynthesizeSpeech",
                "polly:DescribeVoices",
                "s3:GetObject",
                "s3:PutObject"
            ],
            "Resource": "*"
        }
    ]
}
```

### Configuration

```json
{
  "Providers": {
    "AWS": {
      "AccessKeyId": "your-access-key-id",
      "SecretAccessKey": "your-secret-access-key",
      "Region": "us-east-1"
    }
  }
}
```

Or use environment variables:
```bash
export AWS_ACCESS_KEY_ID="your-access-key-id"
export AWS_SECRET_ACCESS_KEY="your-secret-access-key"
export AWS_DEFAULT_REGION="us-east-1"
```

### Setting Up Audio Configuration

```csharp
// Configure via Admin API
POST /api/admin/audio/providers
{
    "providerName": "AWS",
    "transcriptionEnabled": true,
    "defaultTranscriptionModel": "transcribe",
    "textToSpeechEnabled": true,
    "defaultTTSModel": "neural",
    "defaultTTSVoice": "Joanna",
    "customSettings": {
        "transcribeSettings": {
            "showSpeakerLabels": true,
            "maxSpeakerLabels": 10,
            "channelIdentification": false,
            "vocabularyName": null
        },
        "pollySettings": {
            "engine": "neural",
            "outputFormat": "mp3",
            "sampleRate": "22050"
        }
    }
}
```

### Available Voices

**Amazon Polly Neural Voices:**
- English (US): Danielle, Gregory, Ivy, Joanna, Kendra, Kimberly, Salli, Joey, Justin, Kevin, Matthew, Ruth, Stephen
- English (British): Amy, Emma, Brian, Arthur
- Other languages: Available for Spanish, French, German, Italian, Japanese, Korean, and more

## OpenAI Audio Services

OpenAI provides high-quality audio services including Whisper for transcription and neural TTS.

### Configuration

```json
{
  "Providers": {
    "OpenAI": {
      "ApiKey": "sk-your-openai-api-key"
    }
  }
}
```

### Audio-Specific Settings

```csharp
POST /api/admin/audio/providers
{
    "providerName": "OpenAI",
    "transcriptionEnabled": true,
    "defaultTranscriptionModel": "whisper-1",
    "textToSpeechEnabled": true,
    "defaultTTSModel": "tts-1-hd",
    "defaultTTSVoice": "nova",
    "realtimeEnabled": true,
    "defaultRealtimeModel": "gpt-4o-realtime-preview"
}
```

## Provider Comparison

| Feature | Google Cloud | AWS | OpenAI |
|---------|--------------|-----|---------|
| **Transcription Languages** | 125+ | 30+ | 50+ |
| **TTS Languages** | 40+ | 20+ | 25+ |
| **Real-time Support** | ❌ | ❌ | ✅ |
| **Speaker Diarization** | ✅ | ✅ | ❌ |
| **Custom Vocabulary** | ✅ | ✅ | ❌ |
| **Voice Cloning** | ❌ | ❌ | ❌ |
| **SSML Support** | ✅ | ✅ | ❌ |
| **Batch Processing** | ✅ | ✅ | ❌ |

## Advanced Configuration

### Multi-Provider Setup

Configure multiple providers for failover and load balancing:

```csharp
// Primary provider
POST /api/admin/audio/providers
{
    "providerName": "GoogleCloud",
    "routingPriority": 100,
    "transcriptionEnabled": true,
    "textToSpeechEnabled": true
}

// Fallback provider
POST /api/admin/audio/providers
{
    "providerName": "AWS",
    "routingPriority": 50,
    "transcriptionEnabled": true,
    "textToSpeechEnabled": true
}
```

### Language-Based Routing

Route requests to providers based on language expertise:

```csharp
POST /api/admin/audio/routing
{
    "rules": [
        {
            "condition": "language == 'ja'",
            "provider": "GoogleCloud",
            "reason": "Best Japanese support"
        },
        {
            "condition": "language == 'en' && quality == 'premium'",
            "provider": "OpenAI",
            "reason": "Highest quality English"
        },
        {
            "condition": "cost_sensitive == true",
            "provider": "AWS",
            "reason": "Most cost-effective"
        }
    ]
}
```

### Cost Configuration

Set up custom pricing for accurate cost tracking:

```csharp
POST /api/admin/audio/costs
[
    {
        "provider": "GoogleCloud",
        "operationType": "transcription",
        "model": "latest_long",
        "costUnit": "per_minute",
        "costPerUnit": 0.009
    },
    {
        "provider": "AWS",
        "operationType": "tts",
        "model": "neural",
        "costUnit": "per_character",
        "costPerUnit": 0.000016
    }
]
```

## Health Monitoring

Enable health checks for audio providers:

```csharp
POST /api/admin/audio/health
{
    "enableHealthChecks": true,
    "checkIntervalSeconds": 300,
    "timeoutSeconds": 30,
    "degradedThreshold": 3,
    "unhealthyThreshold": 5
}
```

## Troubleshooting

### Google Cloud Issues

1. **Authentication Failed**
   - Verify service account has required permissions
   - Check if APIs are enabled in GCP console
   - Ensure billing is active

2. **Language Not Supported**
   - Check supported languages list
   - Use language code format (e.g., "en-US", not "english")

### AWS Issues

1. **Access Denied**
   - Verify IAM permissions
   - Check if region supports the service
   - Ensure S3 bucket permissions for Transcribe

2. **Voice Not Available**
   - Some voices are region-specific
   - Neural voices require specific regions

### Common Issues

1. **High Latency**
   - Use regional endpoints
   - Enable connection pooling
   - Consider provider geographic location

2. **Rate Limiting**
   - Implement exponential backoff
   - Use multiple provider accounts
   - Configure rate limits in ConduitLLM

## Next Steps

- [Audio API Reference](../api-reference/audio.md) - Detailed API documentation
- [Audio Services Overview](audio-services.md) - General audio capabilities
- [Cost Management](../guides/budget-management.md) - Control audio costs
- [Monitoring Setup](../monitoring/metrics-monitoring.md) - Track provider performance