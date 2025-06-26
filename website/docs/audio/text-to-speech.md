---
sidebar_position: 3
title: Text-to-Speech
description: Comprehensive guide to text-to-speech synthesis with multiple providers and voice options
---

# Text-to-Speech

Conduit's text-to-speech capabilities provide high-quality voice synthesis with support for multiple providers and voices through a unified OpenAI-compatible API.

## Quick Start

### Basic Text-to-Speech

```javascript
import OpenAI from 'openai';
import fs from 'fs';

const openai = new OpenAI({
  apiKey: 'condt_your_virtual_key',
  baseURL: 'https://api.conduit.yourdomain.com/v1'
});

const mp3 = await openai.audio.speech.create({
  model: 'tts-1',
  voice: 'alloy',
  input: 'Hello world! This is a demonstration of text-to-speech synthesis.'
});

const buffer = Buffer.from(await mp3.arrayBuffer());
fs.writeFileSync('speech.mp3', buffer);
```

### Advanced TTS with Voice Control

```javascript
const speech = await openai.audio.speech.create({
  model: 'elevenlabs-tts',
  voice: 'rachel',
  input: 'Welcome to our premium text-to-speech service. This voice has emotional control and natural intonation.',
  response_format: 'mp3',
  speed: 1.0,
  // ElevenLabs-specific parameters
  stability: 0.5,
  similarity_boost: 0.5,
  style: 0.2,
  use_speaker_boost: true
});

const audioBuffer = Buffer.from(await speech.arrayBuffer());
fs.writeFileSync('premium-speech.mp3', audioBuffer);
```

## Supported Models and Providers

### OpenAI TTS

**Models: `tts-1`, `tts-1-hd`**
- **Quality**: Standard and HD options
- **Voices**: 6 built-in voices (alloy, echo, fable, onyx, nova, shimmer)
- **Languages**: Optimized for English, supports others
- **Cost**: $15.00 per 1M characters
- **Latency**: 200-500ms for short texts

```javascript
const speech = await openai.audio.speech.create({
  model: 'tts-1-hd',          // HD quality
  voice: 'nova',              // Female voice
  input: 'Your text here',
  response_format: 'mp3',
  speed: 1.0                  // 0.25 to 4.0
});
```

**Available Voices:**
- **alloy**: Neutral, balanced tone
- **echo**: Clear, professional male voice
- **fable**: Warm, storytelling voice
- **onyx**: Deep, authoritative male voice
- **nova**: Expressive female voice
- **shimmer**: Bright, energetic female voice

### ElevenLabs TTS

**Models: `elevenlabs-tts`, `elevenlabs-tts-v2`**
- **Quality**: Premium quality with emotional control
- **Voices**: 1000+ voices including custom clones
- **Languages**: 29+ languages
- **Cost**: $0.18-0.30 per 1K characters
- **Features**: Voice cloning, emotional control, accent preservation

```javascript
const speech = await openai.audio.speech.create({
  model: 'elevenlabs-tts',
  voice: 'rachel',            // Premium voice
  input: 'This text will be spoken with natural emotion and intonation.',
  response_format: 'mp3',
  // ElevenLabs advanced parameters
  stability: 0.5,             // 0.0-1.0, voice consistency
  similarity_boost: 0.5,      // 0.0-1.0, voice similarity
  style: 0.2,                 // 0.0-1.0, emotional range
  use_speaker_boost: true     // Enhance clarity
});
```

**Popular ElevenLabs Voices:**
- **rachel**: Natural female voice, American accent
- **drew**: Professional male voice
- **clyde**: Warm, friendly male voice
- **bella**: Expressive female voice
- **antoni**: Narrator-style male voice
- **elli**: Young, energetic female voice

### Azure Speech Services

**Models: `azure-neural-tts`**
- **Quality**: High-quality neural voices
- **Voices**: 400+ voices in 140+ languages
- **Languages**: Comprehensive global coverage
- **Cost**: $4.00 per 1M characters
- **Features**: SSML support, custom neural voices

```javascript
const speech = await openai.audio.speech.create({
  model: 'azure-neural-tts',
  voice: 'en-US-JennyNeural',
  input: 'Azure provides high-quality neural text-to-speech.',
  response_format: 'wav',
  language: 'en-US',
  // Azure-specific parameters
  pitch: '+0Hz',
  rate: '1.0',
  volume: '100'
});
```

### Google Cloud Text-to-Speech

**Models: `google-wavenet`, `google-neural2`**
- **Quality**: WaveNet and Neural2 technologies
- **Voices**: 380+ voices in 40+ languages
- **Languages**: Excellent multilingual support
- **Cost**: $4.00 per 1M characters
- **Features**: Custom voice training, SSML

```javascript
const speech = await openai.audio.speech.create({
  model: 'google-neural2',
  voice: 'en-US-Neural2-F',
  input: 'Google Neural2 provides natural-sounding speech.',
  response_format: 'ogg',
  language: 'en-US',
  // Google-specific parameters
  speaking_rate: 1.0,
  pitch: 0.0,
  volume_gain_db: 0.0
});
```

## Request Parameters

### Core Parameters

```javascript
const speech = await openai.audio.speech.create({
  // Required parameters
  model: 'tts-1',                    // TTS model selection
  voice: 'alloy',                    // Voice selection
  input: 'Text to synthesize',       // Input text (max 4096 chars)
  
  // Optional parameters
  response_format: 'mp3',            // Output format
  speed: 1.0,                        // Speech rate (0.25-4.0)
  
  // Provider-specific parameters
  language: 'en-US',                 // Language code
  pitch: 0.0,                        // Voice pitch adjustment
  volume: 1.0,                       // Audio volume
  emotion: 'neutral'                 // Emotional tone
});
```

### Supported Output Formats

| Format | Extension | Quality | Compression | Use Case |
|--------|-----------|---------|-------------|----------|
| **MP3** | .mp3 | Good | High | Web streaming, mobile |
| **OPUS** | .opus | Excellent | High | Real-time communication |
| **AAC** | .aac | Good | Medium | Apple devices, streaming |
| **FLAC** | .flac | Excellent | Lossless | High-quality archival |
| **WAV** | .wav | Excellent | None | Professional audio |
| **PCM** | .pcm | Raw | None | Real-time processing |

### Language Support

**Top Languages by Provider:**

**OpenAI TTS:**
- **Primary**: English (US, UK, AU)
- **Secondary**: Spanish, French, German, Italian, Portuguese, Russian, Japanese, Korean, Chinese

**ElevenLabs:**
- **Native**: English, Spanish, French, German, Italian, Portuguese, Polish, Ukrainian, Russian
- **Supported**: 29+ languages with accent preservation

**Azure Speech:**
- **Full Support**: 140+ languages and dialects
- **Neural Voices**: 80+ languages

**Google Cloud:**
- **WaveNet**: 40+ languages
- **Standard**: 220+ voices across 40+ languages

## Advanced Features

### Voice Parameters

For providers that support it, you can adjust voice characteristics:

```javascript
// ElevenLabs emotional parameters
const emotionalSpeech = await openai.audio.speech.create({
  model: 'elevenlabs-tts',
  voice: 'rachel',
  input: 'I am feeling excited about this new technology!',
  stability: 0.3,        // Lower = more variable/emotional
  similarity_boost: 0.8, // Higher = closer to original voice
  style: 0.7,           // Higher = more expressive
  emotion: 'excited',    // Some providers support direct emotion control
  energy_level: 'high'   // Energy/enthusiasm level
});
```

### SSML Support

```javascript
// Speech Synthesis Markup Language for fine control
const ssmlText = `
<speak>
  <prosody rate="slow" pitch="low">
    This text is spoken slowly with a low pitch.
  </prosody>
  <break time="1s"/>
  <prosody rate="fast" pitch="high">
    This text is spoken quickly with a high pitch.
  </prosody>
  <emphasis level="strong">
    This text is emphasized strongly.
  </emphasis>
</speak>
`;

const speech = await openai.audio.speech.create({
  model: 'azure-neural-tts',
  voice: 'en-US-JennyNeural',
  input: ssmlText,
  input_format: 'ssml'
});
```

### Multilingual Speech

```javascript
// Automatic language detection and appropriate voice selection
const multilingualSpeech = async (texts) => {
  const speeches = [];
  
  for (const { text, language } of texts) {
    const voiceMap = {
      'en': 'en-US-JennyNeural',
      'es': 'es-ES-ElviraNeural', 
      'fr': 'fr-FR-DeniseNeural',
      'de': 'de-DE-KatjaNeural',
      'ja': 'ja-JP-NanamiNeural',
      'zh': 'zh-CN-XiaoxiaoNeural'
    };
    
    const speech = await openai.audio.speech.create({
      model: 'azure-neural-tts',
      voice: voiceMap[language] || voiceMap['en'],
      input: text,
      language: language
    });
    
    speeches.push(await speech.arrayBuffer());
  }
  
  return speeches;
};

// Usage
const multilingual = await multilingualSpeech([
  { text: 'Hello, how are you?', language: 'en' },
  { text: 'Hola, ¿cómo estás?', language: 'es' },
  { text: 'Bonjour, comment allez-vous?', language: 'fr' }
]);
```

## Streaming Text-to-Speech

### Real-Time Speech Synthesis

```javascript
import WebSocket from 'ws';

class StreamingTTS {
  constructor(apiKey) {
    this.apiKey = apiKey;
    this.ws = null;
    this.audioChunks = [];
  }

  async startStream(voice = 'alloy', format = 'pcm') {
    this.ws = new WebSocket('wss://api.conduit.yourdomain.com/v1/audio/stream/speech', {
      headers: {
        'Authorization': `Bearer ${this.apiKey}`
      }
    });

    this.ws.on('open', () => {
      // Configure streaming session
      this.ws.send(JSON.stringify({
        type: 'configure',
        config: {
          model: 'tts-1',
          voice: voice,
          response_format: format,
          chunk_size: 1024
        }
      }));
    });

    this.ws.on('message', (data) => {
      if (data instanceof Buffer) {
        // Audio chunk received
        this.audioChunks.push(data);
        this.onAudioChunk(data);
      } else {
        // Control message
        const message = JSON.parse(data);
        this.handleControlMessage(message);
      }
    });

    this.ws.on('error', (error) => {
      console.error('Streaming TTS error:', error);
    });
  }

  sendText(text) {
    if (this.ws && this.ws.readyState === WebSocket.OPEN) {
      this.ws.send(JSON.stringify({
        type: 'synthesize',
        text: text
      }));
    }
  }

  onAudioChunk(audioData) {
    // Play audio chunk immediately
    this.playAudioChunk(audioData);
  }

  playAudioChunk(audioData) {
    // Implement audio playback (browser or Node.js)
    // This would integrate with Web Audio API or audio libraries
    console.log(`Received audio chunk: ${audioData.length} bytes`);
  }

  handleControlMessage(message) {
    switch (message.type) {
      case 'synthesis_started':
        console.log('Synthesis started');
        break;
      case 'synthesis_completed':
        console.log('Synthesis completed');
        break;
      case 'error':
        console.error('Synthesis error:', message.error);
        break;
    }
  }

  close() {
    if (this.ws) {
      this.ws.close();
    }
  }
}

// Usage
const streamingTTS = new StreamingTTS('condt_your_virtual_key');
await streamingTTS.startStream('nova', 'pcm');

// Send text for immediate synthesis
streamingTTS.sendText('Hello, this will be spoken immediately!');
streamingTTS.sendText(' This continues the speech seamlessly.');
```

### Chunked Processing for Long Texts

```javascript
class LongTextTTS {
  constructor(apiKey) {
    this.apiKey = apiKey;
    this.maxChunkLength = 4000; // Stay under API limits
  }

  splitText(text) {
    const sentences = text.match(/[^\.!?]+[\.!?]+/g) || [text];
    const chunks = [];
    let currentChunk = '';

    for (const sentence of sentences) {
      if (currentChunk.length + sentence.length > this.maxChunkLength) {
        if (currentChunk) {
          chunks.push(currentChunk.trim());
          currentChunk = sentence;
        } else {
          // Single sentence too long, force split
          chunks.push(sentence.trim());
        }
      } else {
        currentChunk += sentence;
      }
    }

    if (currentChunk) {
      chunks.push(currentChunk.trim());
    }

    return chunks;
  }

  async synthesizeLongText(text, options = {}) {
    const chunks = this.splitText(text);
    const audioChunks = [];
    
    console.log(`Processing ${chunks.length} chunks...`);

    for (let i = 0; i < chunks.length; i++) {
      console.log(`Synthesizing chunk ${i + 1}/${chunks.length}`);
      
      try {
        const speech = await openai.audio.speech.create({
          model: 'tts-1',
          voice: 'alloy',
          input: chunks[i],
          response_format: 'mp3',
          ...options
        });

        const audioBuffer = Buffer.from(await speech.arrayBuffer());
        audioChunks.push(audioBuffer);
        
        // Optional delay to respect rate limits
        if (i < chunks.length - 1) {
          await new Promise(resolve => setTimeout(resolve, 100));
        }
      } catch (error) {
        console.error(`Error synthesizing chunk ${i + 1}:`, error);
        throw error;
      }
    }

    return this.concatenateAudio(audioChunks);
  }

  concatenateAudio(audioChunks) {
    // Simple buffer concatenation (works for most formats)
    return Buffer.concat(audioChunks);
  }
}

// Usage
const longTTS = new LongTextTTS('condt_your_virtual_key');

const longText = `
  This is a very long piece of text that needs to be converted to speech.
  It contains multiple sentences and paragraphs that exceed the typical
  character limits of text-to-speech APIs. The system will automatically
  split this into appropriate chunks and synthesize each part separately,
  then combine them into a single audio file.
`;

const audioBuffer = await longTTS.synthesizeLongText(longText, {
  voice: 'nova',
  speed: 1.1
});

fs.writeFileSync('long-speech.mp3', audioBuffer);
```

## Use Case Examples

### Audiobook Generation

```javascript
class AudiobookGenerator {
  constructor(apiKey) {
    this.apiKey = apiKey;
    this.chapterBreakDuration = 2000; // 2 seconds silence
  }

  async generateAudiobook(chapters, options = {}) {
    const audioChapters = [];
    
    for (let i = 0; i < chapters.length; i++) {
      console.log(`Generating chapter ${i + 1}: ${chapters[i].title}`);
      
      // Generate chapter introduction
      const chapterIntro = `Chapter ${i + 1}: ${chapters[i].title}`;
      const introAudio = await this.synthesizeText(chapterIntro, {
        voice: 'onyx',  // Different voice for chapter titles
        speed: 0.9,
        ...options
      });
      
      // Generate chapter content
      const contentAudio = await this.synthesizeText(chapters[i].content, {
        voice: 'nova',  // Main narration voice
        speed: 1.0,
        ...options
      });
      
      // Add chapter break
      const silenceBuffer = this.generateSilence(this.chapterBreakDuration);
      
      audioChapters.push(introAudio, contentAudio, silenceBuffer);
    }
    
    return Buffer.concat(audioChapters);
  }

  async synthesizeText(text, options) {
    const speech = await openai.audio.speech.create({
      model: 'tts-1-hd',
      input: text,
      response_format: 'wav',
      ...options
    });
    
    return Buffer.from(await speech.arrayBuffer());
  }

  generateSilence(durationMs) {
    // Generate silence buffer (simplified)
    const sampleRate = 44100;
    const samples = Math.floor(sampleRate * durationMs / 1000);
    return Buffer.alloc(samples * 2); // 16-bit audio
  }
}

// Usage
const audiobookGen = new AudiobookGenerator('condt_your_virtual_key');

const chapters = [
  {
    title: 'The Beginning',
    content: 'It was the best of times, it was the worst of times...'
  },
  {
    title: 'The Journey',
    content: 'Our hero embarked on a quest that would change everything...'
  }
];

const audiobook = await audiobookGen.generateAudiobook(chapters);
fs.writeFileSync('audiobook.wav', audiobook);
```

### Interactive Voice Response (IVR)

```javascript
class IVRSystem {
  constructor(apiKey) {
    this.apiKey = apiKey;
    this.voiceSettings = {
      model: 'tts-1',
      voice: 'alloy',
      response_format: 'wav',
      speed: 0.9  // Slightly slower for clarity
    };
  }

  async generateIVRPrompts() {
    const prompts = {
      welcome: 'Welcome to our customer service. Please listen carefully as our menu options have changed.',
      mainMenu: 'Press 1 for account information, Press 2 for technical support, Press 3 for billing questions, or Press 0 to speak with an operator.',
      invalidOption: 'I\'m sorry, that\'s not a valid option. Please try again.',
      transferring: 'Please hold while I transfer your call to the appropriate department.',
      goodbye: 'Thank you for calling. Have a great day!'
    };

    const audioPrompts = {};
    
    for (const [key, text] of Object.entries(prompts)) {
      console.log(`Generating ${key} prompt...`);
      
      const speech = await openai.audio.speech.create({
        ...this.voiceSettings,
        input: text
      });
      
      audioPrompts[key] = Buffer.from(await speech.arrayBuffer());
    }
    
    return audioPrompts;
  }

  async generateDynamicPrompt(template, variables) {
    const text = template.replace(/\{(\w+)\}/g, (match, key) => {
      return variables[key] || match;
    });
    
    const speech = await openai.audio.speech.create({
      ...this.voiceSettings,
      input: text
    });
    
    return Buffer.from(await speech.arrayBuffer());
  }
}

// Usage
const ivr = new IVRSystem('condt_your_virtual_key');

// Generate static prompts
const prompts = await ivr.generateIVRPrompts();

// Generate dynamic prompt
const dynamicPrompt = await ivr.generateDynamicPrompt(
  'Hello {customerName}, your account balance is {balance} dollars.',
  { customerName: 'John Smith', balance: '1,250.75' }
);
```

### Podcast/Content Creation

```javascript
class PodcastGenerator {
  constructor(apiKey) {
    this.apiKey = apiKey;
    this.hosts = {
      host1: { voice: 'nova', name: 'Sarah' },
      host2: { voice: 'onyx', name: 'Michael' }
    };
  }

  async generatePodcastSegment(script) {
    const audioSegments = [];
    
    for (const segment of script) {
      const host = this.hosts[segment.speaker];
      
      if (!host) {
        throw new Error(`Unknown speaker: ${segment.speaker}`);
      }
      
      console.log(`${host.name}: ${segment.text}`);
      
      const speech = await openai.audio.speech.create({
        model: 'tts-1-hd',
        voice: host.voice,
        input: segment.text,
        response_format: 'mp3',
        speed: segment.speed || 1.0
      });
      
      const audioBuffer = Buffer.from(await speech.arrayBuffer());
      audioSegments.push(audioBuffer);
      
      // Add natural pause between speakers
      if (segment.pause) {
        const pauseBuffer = this.generatePause(segment.pause);
        audioSegments.push(pauseBuffer);
      }
    }
    
    return Buffer.concat(audioSegments);
  }

  generatePause(durationMs) {
    // Generate brief pause/silence
    const sampleRate = 44100;
    const samples = Math.floor(sampleRate * durationMs / 1000);
    return Buffer.alloc(samples * 2); // 16-bit silence
  }
}

// Usage
const podcastGen = new PodcastGenerator('condt_your_virtual_key');

const script = [
  {
    speaker: 'host1',
    text: 'Welcome back to Tech Talk Tuesday! I\'m your host Sarah.',
    pause: 500
  },
  {
    speaker: 'host2', 
    text: 'And I\'m Michael. Today we\'re diving deep into artificial intelligence.',
    pause: 1000
  },
  {
    speaker: 'host1',
    text: 'That\'s right! We have some fascinating developments to discuss.',
    speed: 1.1
  }
];

const podcastAudio = await podcastGen.generatePodcastSegment(script);
fs.writeFileSync('podcast-segment.mp3', podcastAudio);
```

## Error Handling and Optimization

### Common TTS Errors

```javascript
try {
  const speech = await openai.audio.speech.create({
    model: 'tts-1',
    voice: 'alloy',
    input: 'Hello world!'
  });
} catch (error) {
  switch (error.code) {
    case 'text_too_long':
      console.log('Text exceeds maximum length (4096 characters)');
      // Split text into chunks
      break;
    case 'invalid_voice':
      console.log('Voice not available for selected model');
      // Fallback to default voice
      break;
    case 'unsupported_language':
      console.log('Language not supported by selected voice');
      // Switch to appropriate voice
      break;
    case 'rate_limit_exceeded':
      console.log('Too many requests, please wait');
      // Implement retry with backoff
      break;
    case 'quota_exceeded':
      console.log('Monthly quota exceeded');
      // Switch to different provider or upgrade plan
      break;
    default:
      console.log('TTS error:', error.message);
  }
}
```

### Performance Optimization

```javascript
class OptimizedTTS {
  constructor(apiKey) {
    this.apiKey = apiKey;
    this.cache = new Map();
    this.requestQueue = [];
    this.processing = false;
  }

  getCacheKey(text, options) {
    return crypto.createHash('md5')
      .update(JSON.stringify({ text, ...options }))
      .digest('hex');
  }

  async synthesizeWithCache(text, options = {}) {
    const cacheKey = this.getCacheKey(text, options);
    
    // Check cache first
    if (this.cache.has(cacheKey)) {
      console.log('Using cached audio');
      return this.cache.get(cacheKey);
    }
    
    // Generate new audio
    const audio = await this.synthesize(text, options);
    
    // Cache the result
    this.cache.set(cacheKey, audio);
    
    return audio;
  }

  async synthesize(text, options) {
    return new Promise((resolve, reject) => {
      this.requestQueue.push({ text, options, resolve, reject });
      this.processQueue();
    });
  }

  async processQueue() {
    if (this.processing || this.requestQueue.length === 0) {
      return;
    }
    
    this.processing = true;
    
    while (this.requestQueue.length > 0) {
      const { text, options, resolve, reject } = this.requestQueue.shift();
      
      try {
        const speech = await openai.audio.speech.create({
          model: 'tts-1',
          input: text,
          ...options
        });
        
        const buffer = Buffer.from(await speech.arrayBuffer());
        resolve(buffer);
        
        // Rate limiting - wait between requests
        if (this.requestQueue.length > 0) {
          await new Promise(r => setTimeout(r, 100));
        }
      } catch (error) {
        reject(error);
      }
    }
    
    this.processing = false;
  }
}
```

## Integration with Real-Time Communication

### WebSocket TTS for Real-Time Applications

```javascript
class RealTimeTTSClient {
  constructor(apiKey) {
    this.apiKey = apiKey;
    this.connection = null;
    this.audioQueue = [];
  }

  async connect() {
    this.connection = new WebSocket('wss://api.conduit.yourdomain.com/v1/audio/realtime-tts', {
      headers: {
        'Authorization': `Bearer ${this.apiKey}`
      }
    });

    this.connection.on('open', () => {
      console.log('Real-time TTS connected');
      this.sendConfiguration();
    });

    this.connection.on('message', (data) => {
      if (data instanceof Buffer) {
        this.handleAudioData(data);
      } else {
        this.handleControlMessage(JSON.parse(data));
      }
    });
  }

  sendConfiguration() {
    this.connection.send(JSON.stringify({
      type: 'configure',
      config: {
        voice: 'nova',
        format: 'pcm16',
        sample_rate: 24000,
        streaming: true
      }
    }));
  }

  speakText(text) {
    if (this.connection && this.connection.readyState === WebSocket.OPEN) {
      this.connection.send(JSON.stringify({
        type: 'speak',
        text: text,
        immediate: true
      }));
    }
  }

  handleAudioData(audioData) {
    // Stream audio directly to speakers
    this.playAudioChunk(audioData);
  }

  playAudioChunk(audioData) {
    // Implement real-time audio playback
    // This would use Web Audio API in browser or audio libraries in Node.js
    console.log(`Playing audio chunk: ${audioData.length} bytes`);
  }
}

// Usage for real-time applications
const realtimeTTS = new RealTimeTTSClient('condt_your_virtual_key');
await realtimeTTS.connect();

// Speak text immediately with minimal latency
realtimeTTS.speakText('This will be spoken immediately!');
realtimeTTS.speakText('And this will follow seamlessly.');
```

## Next Steps

- **Real-Time Audio**: Build conversational applications with [real-time audio](real-time-audio)
- **Speech-to-Text**: Combine with [transcription services](speech-to-text) for complete audio workflows
- **Audio Providers**: Compare features across [audio providers](providers)
- **Integration Examples**: See complete [client integration patterns](../clients/overview)