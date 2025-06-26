---
sidebar_position: 5
title: Audio Providers
description: Comprehensive comparison of audio providers, models, and capabilities
---

# Audio Providers

Conduit integrates with leading audio AI providers to offer comprehensive speech-to-text, text-to-speech, and real-time audio capabilities. Choose the right provider based on your requirements for quality, speed, cost, and language support.

## Provider Overview

| Provider | Speech-to-Text | Text-to-Speech | Real-Time Audio | Languages | Strengths |
|----------|---------------|----------------|-----------------|-----------|-----------|
| **OpenAI** | ✅ Whisper | ✅ TTS | ✅ Realtime API | 99+ | Best overall quality, multimodal |
| **ElevenLabs** | ❌ | ✅ Premium TTS | ✅ Conversational | 29+ | Voice cloning, emotional control |
| **Deepgram** | ✅ Nova-2 | ❌ | ✅ Streaming | 30+ | Ultra-low latency, specialized STT |
| **AssemblyAI** | ✅ Universal | ❌ | ❌ | 10+ | Advanced features, speaker detection |
| **Azure Speech** | ✅ Neural | ✅ Neural | ✅ Live | 140+ | Enterprise features, global reach |
| **Google Cloud** | ✅ Chirp | ✅ WaveNet | ✅ Streaming | 220+ | Multilingual excellence |
| **Amazon Polly** | ❌ | ✅ Neural | ❌ | 60+ | AWS integration, SSML support |

## Speech-to-Text Providers

### OpenAI Whisper

**Models: `whisper-1`**

**Strengths:**
- Exceptional accuracy across languages
- Robust noise handling
- Automatic language detection
- Punctuation and formatting
- Long-form audio support

**Best For:**
- High-quality transcription
- Multilingual content
- Noisy environments
- General-purpose applications

```javascript
const transcription = await openai.audio.transcriptions.create({
  file: fs.createReadStream('audio.mp3'),
  model: 'whisper-1',
  language: 'en', // Optional: auto-detect if omitted
  response_format: 'verbose_json',
  timestamp_granularities: ['word']
});

console.log('Transcript:', transcription.text);
console.log('Language:', transcription.language);
console.log('Duration:', transcription.duration);
console.log('Words:', transcription.words);
```

**Pricing:** $0.006 per minute
**Latency:** 3-15 seconds (file processing)
**Languages:** 99+ languages
**Max File Size:** 25MB
**Supported Formats:** mp3, mp4, mpeg, mpga, m4a, wav, webm

### Deepgram Nova-2

**Models: `nova-2`, `nova-2-general`, `nova-2-meeting`, `nova-2-phonecall`**

**Strengths:**
- Ultra-low latency streaming
- Real-time processing
- Specialized models for different use cases
- Advanced features (sentiment, topics)
- Excellent English accuracy

**Best For:**
- Real-time applications
- Live transcription
- Call center analytics
- Meeting transcription

```javascript
const transcription = await openai.audio.transcriptions.create({
  file: fs.createReadStream('meeting.wav'),
  model: 'deepgram-nova-2-meeting',
  response_format: 'verbose_json',
  // Deepgram-specific features
  punctuate: true,
  diarize: true, // Speaker identification
  sentiment: true,
  summarize: true
});

console.log('Speakers:', transcription.speakers);
console.log('Sentiment:', transcription.sentiment);
console.log('Summary:', transcription.summary);
```

**Pricing:** $0.0043 per minute
**Latency:** 200-800ms (streaming)
**Languages:** 30+ languages
**Specializations:** Meetings, phone calls, general
**Features:** Speaker diarization, sentiment analysis, topic detection

### AssemblyAI Universal-1

**Models: `assemblyai-universal-1`, `assemblyai-best`**

**Strengths:**
- Advanced AI features
- Speaker identification
- Content moderation
- Chapter detection
- Auto-highlights

**Best For:**
- Podcast transcription
- Content analysis
- Security and compliance
- Advanced audio intelligence

```javascript
const transcription = await openai.audio.transcriptions.create({
  file: fs.createReadStream('podcast.mp3'),
  model: 'assemblyai-best',
  response_format: 'verbose_json',
  // AssemblyAI-specific features
  speaker_labels: true,
  content_safety: true,
  auto_chapters: true,
  auto_highlights: true,
  sentiment_analysis: true
});

console.log('Chapters:', transcription.chapters);
console.log('Highlights:', transcription.auto_highlights_result);
console.log('Content Safety:', transcription.content_safety_labels);
```

**Pricing:** $0.00037 per second ($1.33 per hour)
**Languages:** 10+ languages (English-focused)
**Features:** Speaker labels, content safety, auto-chapters, PII redaction

### Azure Speech Services

**Models: `azure-neural-stt`**

**Strengths:**
- Enterprise-grade reliability
- Custom model training
- Batch processing
- Integration with Microsoft ecosystem
- Extensive language support

**Best For:**
- Enterprise applications
- Custom vocabularies
- Batch processing
- Microsoft integrations

```javascript
const transcription = await openai.audio.transcriptions.create({
  file: fs.createReadStream('corporate-call.wav'),
  model: 'azure-neural-stt',
  language: 'en-US',
  // Azure-specific parameters
  profanity_filter: 'Masked',
  word_level_timestamps: true,
  display_form_word_level_timestamps: true
});
```

**Pricing:** $1.00 per hour
**Languages:** 140+ languages and dialects
**Features:** Custom models, batch API, profanity filtering

### Google Cloud Speech-to-Text

**Models: `google-chirp`, `google-latest_long`, `google-latest_short`**

**Strengths:**
- Excellent multilingual support
- Automatic punctuation
- Model adaptation
- Video intelligence integration
- Strong for non-English languages

**Best For:**
- Global applications
- Non-English content
- Video transcription
- Google Cloud ecosystem

```javascript
const transcription = await openai.audio.transcriptions.create({
  file: fs.createReadStream('multilingual.wav'),
  model: 'google-chirp',
  language: 'auto', // Auto-detect language
  // Google-specific features
  enable_automatic_punctuation: true,
  enable_word_time_offsets: true,
  model_adaptation: 'custom_class_id'
});
```

**Pricing:** $0.024 per minute (Chirp model)
**Languages:** 125+ languages
**Features:** Auto-punctuation, model adaptation, video intelligence

## Text-to-Speech Providers

### OpenAI TTS

**Models: `tts-1`, `tts-1-hd`**

**Strengths:**
- High-quality neural voices
- Consistent quality
- Simple API
- Good speed control
- Reliable and stable

**Best For:**
- General applications
- Consistent quality needs
- Simple integration
- Reliable production use

```javascript
const speech = await openai.audio.speech.create({
  model: 'tts-1-hd',
  voice: 'nova',
  input: 'Hello, this is a high-quality text-to-speech demonstration.',
  response_format: 'mp3',
  speed: 1.0
});

const buffer = Buffer.from(await speech.arrayBuffer());
fs.writeFileSync('speech.mp3', buffer);
```

**Voices:** 6 built-in voices (alloy, echo, fable, onyx, nova, shimmer)
**Pricing:** $15.00 per 1M characters
**Languages:** Optimized for English, supports others
**Quality:** Standard and HD options

### ElevenLabs

**Models: `elevenlabs-tts`, `elevenlabs-turbo-v2`**

**Strengths:**
- Premium voice quality
- Voice cloning capability
- Emotional control
- Multilingual accent preservation
- Real-time generation

**Best For:**
- Premium applications
- Voice cloning
- Emotional speech
- Content creation
- Character voices

```javascript
const speech = await openai.audio.speech.create({
  model: 'elevenlabs-tts',
  voice: 'rachel', // Premium voice
  input: 'This voice has natural emotion and perfect intonation.',
  // ElevenLabs advanced controls
  stability: 0.5,        // Voice consistency
  similarity_boost: 0.8, // Voice similarity to original
  style: 0.3,           // Emotional range
  use_speaker_boost: true
});
```

**Voices:** 1000+ voices including custom clones
**Pricing:** $0.18-0.30 per 1K characters
**Languages:** 29+ languages with accent preservation
**Features:** Voice cloning, emotional control, real-time streaming

### Azure Neural Text-to-Speech

**Models: `azure-neural-tts`**

**Strengths:**
- Extensive voice library
- SSML support
- Custom neural voices
- Enterprise features
- Global deployment

**Best For:**
- Enterprise applications
- Custom voices
- SSML requirements
- Global deployment
- Microsoft ecosystem

```javascript
const speech = await openai.audio.speech.create({
  model: 'azure-neural-tts',
  voice: 'en-US-JennyNeural',
  input: 'Azure provides enterprise-grade text-to-speech.',
  language: 'en-US',
  // Azure-specific parameters
  pitch: '+0Hz',
  rate: '1.0',
  volume: '100'
});
```

**Voices:** 400+ neural voices
**Pricing:** $4.00 per 1M characters
**Languages:** 140+ languages
**Features:** Custom neural voices, SSML, batch synthesis

### Google Cloud Text-to-Speech

**Models: `google-wavenet`, `google-neural2`, `google-polyglot`**

**Strengths:**
- WaveNet technology
- Natural intonation
- Multilingual excellence
- SSML support
- Custom voice training

**Best For:**
- Natural-sounding speech
- Multilingual applications
- Custom training needs
- Google Cloud ecosystem

```javascript
const speech = await openai.audio.speech.create({
  model: 'google-neural2',
  voice: 'en-US-Neural2-F',
  input: 'Google Neural2 provides natural-sounding speech synthesis.',
  language: 'en-US',
  // Google-specific parameters
  speaking_rate: 1.0,
  pitch: 0.0,
  volume_gain_db: 0.0
});
```

**Voices:** 380+ voices
**Pricing:** $4.00 per 1M characters (Neural2)
**Languages:** 40+ languages
**Features:** WaveNet, custom voices, SSML, Studio voices

### Amazon Polly

**Models: `amazon-neural`, `amazon-long-form`**

**Strengths:**
- AWS ecosystem integration
- SSML support
- Speech marks
- Lexicon support
- Cost-effective

**Best For:**
- AWS applications
- Long-form content
- Custom pronunciations
- Cost-sensitive projects

```javascript
const speech = await openai.audio.speech.create({
  model: 'amazon-neural',
  voice: 'Joanna',
  input: 'Amazon Polly integrates seamlessly with AWS services.',
  // Polly-specific features
  text_type: 'ssml', // or 'text'
  speech_mark_types: ['word', 'sentence'],
  lexicon_names: ['custom-lexicon']
});
```

**Voices:** 60+ neural voices
**Pricing:** $4.00 per 1M characters (Neural)
**Languages:** 30+ languages
**Features:** SSML, speech marks, lexicons, long-form synthesis

## Real-Time Audio Providers

### OpenAI Realtime API

**Model: `gpt-4o-realtime-preview`**

**Capabilities:**
- Bidirectional audio streaming
- Voice-to-voice conversation
- Function calling in conversations
- Interruption handling
- Conversation memory

**Best For:**
- Voice assistants
- Interactive applications
- AI companions
- Voice-controlled interfaces

```javascript
const connection = new WebSocket('wss://api.conduit.yourdomain.com/v1/audio/realtime', {
  headers: { 'Authorization': `Bearer ${apiKey}` }
});

connection.send(JSON.stringify({
  type: 'session.update',
  session: {
    model: 'gpt-4o-realtime-preview',
    modalities: ['text', 'audio'],
    voice: 'alloy',
    instructions: 'You are a helpful voice assistant.',
    turn_detection: {
      type: 'server_vad',
      threshold: 0.5,
      prefix_padding_ms: 300,
      silence_duration_ms: 500
    }
  }
}));
```

**Latency:** ~200ms end-to-end
**Features:** Function calling, interruptions, conversation context
**Cost:** $5.00 per 1M input tokens, $20.00 per 1M output tokens

### ElevenLabs Conversational AI

**Model: `elevenlabs-conversational`**

**Capabilities:**
- Ultra-low latency conversation
- Voice cloning in real-time
- Emotional responses
- Custom conversation flows

**Best For:**
- Character interactions
- Emotional AI
- Voice cloning applications
- Entertainment

```javascript
// WebSocket connection to ElevenLabs conversational AI
const conversation = new WebSocket('wss://api.conduit.yourdomain.com/v1/audio/elevenlabs-conversation', {
  headers: { 'Authorization': `Bearer ${apiKey}` }
});

conversation.send(JSON.stringify({
  type: 'configure',
  config: {
    voice_id: 'custom-cloned-voice',
    model: 'elevenlabs-conversational',
    stability: 0.5,
    similarity_boost: 0.8,
    latency_optimized: true
  }
}));
```

**Latency:** ~300ms end-to-end
**Features:** Voice cloning, emotional control, custom personalities
**Cost:** $0.10-0.30 per minute

### Deepgram Live Streaming

**Models: `deepgram-nova-2-live`**

**Capabilities:**
- Real-time transcription
- Ultra-low latency
- Speaker identification
- Custom vocabulary
- WebSocket streaming

**Best For:**
- Live transcription
- Real-time captions
- Call transcription
- Meeting notes

```javascript
const liveTranscription = new WebSocket('wss://api.conduit.yourdomain.com/v1/audio/deepgram-live', {
  headers: { 'Authorization': `Bearer ${apiKey}` }
});

liveTranscription.send(JSON.stringify({
  type: 'configure',
  config: {
    model: 'nova-2-general',
    language: 'en',
    punctuate: true,
    interim_results: true,
    endpointing: 300
  }
}));
```

**Latency:** 200-800ms
**Features:** Live results, speaker ID, custom vocabulary
**Cost:** $0.0043 per minute

## Provider Selection Guide

### By Use Case

**Voice Assistants & Chatbots**
1. **OpenAI Realtime API** - Best overall for conversational AI
2. **ElevenLabs Conversational** - Premium voice quality
3. **Azure Speech Services** - Enterprise reliability

**Content Creation**
1. **ElevenLabs** - Voice cloning and emotional control
2. **Azure Neural TTS** - Extensive voice library
3. **Google WaveNet** - Natural intonation

**Transcription Services**
1. **OpenAI Whisper** - Best accuracy and language support
2. **Deepgram Nova-2** - Real-time and specialized models
3. **AssemblyAI** - Advanced AI features

**Global Applications**
1. **Google Cloud** - Best multilingual support
2. **Azure Speech** - Global deployment options
3. **OpenAI Whisper** - Excellent language detection

**Cost-Sensitive Projects**
1. **Deepgram** - Competitive STT pricing
2. **OpenAI Whisper** - Good value for quality
3. **Amazon Polly** - Cost-effective TTS

### By Technical Requirements

**Ultra-Low Latency**
- Deepgram Live (200ms)
- OpenAI Realtime (200ms)
- ElevenLabs Conversational (300ms)

**Highest Quality**
- ElevenLabs TTS (premium voices)
- OpenAI Whisper (transcription)
- Google Neural2 (natural speech)

**Most Languages**
- Google Cloud (220+ languages TTS, 125+ STT)
- Azure Speech (140+ languages)
- OpenAI Whisper (99+ languages STT)

**Advanced Features**
- AssemblyAI (AI-powered analysis)
- Azure Speech (custom models)
- ElevenLabs (voice cloning)

## Performance Comparison

### Latency Benchmarks

| Provider | STT Latency | TTS Latency | Real-Time |
|----------|-------------|-------------|-----------|
| **OpenAI** | 3-15s (batch) | 0.5-2s | ~200ms |
| **ElevenLabs** | N/A | 0.3-1s | ~300ms |
| **Deepgram** | 200-800ms | N/A | ~250ms |
| **AssemblyAI** | 10-30s (batch) | N/A | N/A |
| **Azure** | 1-5s | 0.5-2s | ~400ms |
| **Google** | 2-8s | 0.5-2s | ~500ms |

### Accuracy Ratings

| Provider | English STT | Multilingual STT | TTS Naturalness |
|----------|-------------|------------------|-----------------|
| **OpenAI** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ |
| **ElevenLabs** | N/A | N/A | ⭐⭐⭐⭐⭐ |
| **Deepgram** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | N/A |
| **AssemblyAI** | ⭐⭐⭐⭐ | ⭐⭐⭐ | N/A |
| **Azure** | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ |
| **Google** | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ |

## Cost Analysis

### Speech-to-Text Costs (per hour)

| Provider | Model | Cost | Best For |
|----------|-------|------|----------|
| **Deepgram** | Nova-2 | $0.26 | Real-time, call centers |
| **OpenAI** | Whisper-1 | $0.36 | General purpose, multilingual |
| **Google** | Chirp | $1.44 | Global applications |
| **Azure** | Neural STT | $1.00 | Enterprise applications |
| **AssemblyAI** | Universal-1 | $1.33 | Advanced AI features |

### Text-to-Speech Costs (per 1M characters)

| Provider | Model | Cost | Best For |
|----------|-------|------|----------|
| **Azure** | Neural TTS | $4.00 | Enterprise, custom voices |
| **Google** | Neural2 | $4.00 | Natural speech, multilingual |
| **Amazon** | Polly Neural | $4.00 | AWS integration, cost-effective |
| **OpenAI** | TTS-1 | $15.00 | Simplicity, reliability |
| **ElevenLabs** | Premium | $180-300 | Voice cloning, premium quality |

## Migration Guide

### From OpenAI to ElevenLabs (TTS)

```javascript
// OpenAI TTS
const openaiSpeech = await openai.audio.speech.create({
  model: 'tts-1',
  voice: 'alloy',
  input: 'Hello world'
});

// ElevenLabs equivalent
const elevenLabsSpeech = await openai.audio.speech.create({
  model: 'elevenlabs-tts',
  voice: 'rachel', // Similar neutral voice
  input: 'Hello world',
  stability: 0.5,    // Voice consistency
  similarity_boost: 0.5 // Voice similarity
});
```

### From Whisper to Deepgram (STT)

```javascript
// OpenAI Whisper
const whisperResult = await openai.audio.transcriptions.create({
  file: audioFile,
  model: 'whisper-1',
  response_format: 'verbose_json'
});

// Deepgram equivalent
const deepgramResult = await openai.audio.transcriptions.create({
  file: audioFile,
  model: 'deepgram-nova-2',
  response_format: 'verbose_json',
  punctuate: true,
  diarize: true // Additional feature
});
```

## Troubleshooting

### Common Issues by Provider

**OpenAI Whisper:**
- File size limits (25MB max)
- Processing time for long files
- Language detection accuracy

**ElevenLabs:**
- Rate limits on voice cloning
- Higher costs for premium features
- Character limits per request

**Deepgram:**
- WebSocket connection stability
- Model-specific vocabulary limitations
- Streaming audio format requirements

**Azure Speech:**
- Authentication complexity
- Region-specific deployments
- Custom model training time

### Best Practices

1. **Test with Your Data**: Each provider excels with different audio types
2. **Monitor Costs**: Implement usage tracking and alerts
3. **Have Fallbacks**: Use multiple providers for reliability
4. **Optimize for Use Case**: Match provider strengths to your needs
5. **Consider Latency**: Real-time vs. batch processing requirements

## Next Steps

- **Speech-to-Text**: Implement [transcription workflows](speech-to-text)
- **Text-to-Speech**: Create [voice synthesis applications](text-to-speech)
- **Real-Time Audio**: Build [conversational interfaces](real-time-audio)
- **Integration Examples**: See complete [client patterns](../clients/overview)