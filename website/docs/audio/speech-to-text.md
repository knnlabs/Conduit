---
sidebar_position: 2
title: Speech-to-Text
description: Comprehensive guide to speech-to-text transcription with multiple providers and advanced features
---

# Speech-to-Text

Conduit's speech-to-text capabilities provide high-accuracy transcription services with support for multiple providers, languages, and advanced features like speaker diarization, timestamps, and real-time streaming.

## Quick Start

### Basic Transcription

```javascript
import OpenAI from 'openai';
import fs from 'fs';

const openai = new OpenAI({
  apiKey: 'condt_your_virtual_key',
  baseURL: 'https://api.conduit.yourdomain.com/v1'
});

const transcription = await openai.audio.transcriptions.create({
  file: fs.createReadStream('meeting.mp3'),
  model: 'whisper-1'
});

console.log(transcription.text);
```

### Advanced Transcription with Timestamps

```javascript
const transcription = await openai.audio.transcriptions.create({
  file: fs.createReadStream('interview.wav'),
  model: 'whisper-1',
  language: 'en',
  response_format: 'verbose_json',
  timestamp_granularities: ['word', 'segment'],
  temperature: 0.0,
  prompt: 'This is an interview about artificial intelligence and machine learning.'
});

console.log('Full text:', transcription.text);
console.log('Word-level timestamps:', transcription.words);
console.log('Segment timestamps:', transcription.segments);
```

## Supported Models

### OpenAI Whisper

**Model: `whisper-1`**
- **Languages**: 100+ languages supported
- **Accuracy**: State-of-the-art for most languages
- **Features**: Automatic language detection, punctuation, timestamps
- **Cost**: $0.006 per minute

```javascript
const transcription = await openai.audio.transcriptions.create({
  file: audioFile,
  model: 'whisper-1',
  language: 'auto',  // Automatic language detection
  temperature: 0.0   // More deterministic output
});
```

### Deepgram Nova-2

**Model: `deepgram-nova-2`**
- **Languages**: 30+ languages with high accuracy
- **Speed**: Ultra-fast processing
- **Features**: Smart formatting, speaker diarization, custom models
- **Cost**: $0.0043 per minute

```javascript
const transcription = await openai.audio.transcriptions.create({
  file: audioFile,
  model: 'deepgram-nova-2',
  smart_format: true,
  diarize: true,
  punctuate: true,
  utterances: true
});
```

### Google Cloud Speech

**Model: `google-speech-latest`**
- **Languages**: 125+ languages and dialects
- **Features**: Auto-punctuation, profanity filtering, speaker recognition
- **Accuracy**: Excellent for diverse accents and dialects
- **Cost**: $0.004 per minute

```javascript
const transcription = await openai.audio.transcriptions.create({
  file: audioFile,
  model: 'google-speech-latest',
  enable_automatic_punctuation: true,
  enable_speaker_diarization: true,
  diarization_speaker_count: 2
});
```

### Azure Speech Services

**Model: `azure-speech-universal`**
- **Languages**: 100+ languages
- **Features**: Custom speech models, real-time transcription
- **Specialization**: Enterprise scenarios, custom vocabularies
- **Cost**: $0.005 per minute

```javascript
const transcription = await openai.audio.transcriptions.create({
  file: audioFile,
  model: 'azure-speech-universal',
  profanity_filter: 'masked',
  add_word_level_timestamps: true,
  add_diarization: true
});
```

## Request Parameters

### Core Parameters

```javascript
const transcription = await openai.audio.transcriptions.create({
  // Required parameters
  file: fs.createReadStream('audio.mp3'),    // Audio file
  model: 'whisper-1',                        // Model selection
  
  // Optional parameters
  language: 'en',                            // Language code
  prompt: 'Context about the audio',         // Improve accuracy
  response_format: 'verbose_json',           // Output format
  temperature: 0.0,                          // Randomness (0.0-1.0)
  timestamp_granularities: ['word']          // Timestamp detail
});
```

### Supported Audio Formats

| Format | Extension | Max Size | Quality |
|--------|-----------|----------|---------|
| **MP3** | .mp3 | 100MB | Good compression |
| **WAV** | .wav | 100MB | Uncompressed, high quality |
| **M4A** | .m4a | 100MB | Apple format, good quality |
| **FLAC** | .flac | 100MB | Lossless compression |
| **OGG** | .ogg | 100MB | Open source format |
| **WEBM** | .webm | 100MB | Web optimized |

### Language Support

**Most Common Languages:**
```javascript
// Specify language for better accuracy
const languages = {
  'en': 'English',
  'es': 'Spanish', 
  'fr': 'French',
  'de': 'German',
  'it': 'Italian',
  'pt': 'Portuguese',
  'ru': 'Russian',
  'ja': 'Japanese',
  'ko': 'Korean',
  'zh': 'Chinese',
  'ar': 'Arabic',
  'hi': 'Hindi'
};

const transcription = await openai.audio.transcriptions.create({
  file: audioFile,
  model: 'whisper-1',
  language: 'es'  // Spanish
});
```

## Response Formats

### Text Format (Default)

```javascript
const transcription = await openai.audio.transcriptions.create({
  file: audioFile,
  model: 'whisper-1',
  response_format: 'text'
});

// Returns: "Hello, this is a test transcription."
console.log(transcription);
```

### JSON Format

```javascript
const transcription = await openai.audio.transcriptions.create({
  file: audioFile,
  model: 'whisper-1',
  response_format: 'json'
});

// Returns: { "text": "Hello, this is a test transcription." }
console.log(transcription.text);
```

### Verbose JSON with Timestamps

```javascript
const transcription = await openai.audio.transcriptions.create({
  file: audioFile,
  model: 'whisper-1',
  response_format: 'verbose_json',
  timestamp_granularities: ['word', 'segment']
});

console.log('Response format:', transcription);
```

**Verbose JSON Response:**
```json
{
  "task": "transcribe",
  "language": "english",
  "duration": 8.470000267028809,
  "text": "The beach was a popular spot on a hot summer day. People were swimming in the ocean and building sandcastles in the sand.",
  "words": [
    {
      "word": "The",
      "start": 0.0,
      "end": 0.23
    },
    {
      "word": "beach",
      "start": 0.23,
      "end": 0.52
    }
  ],
  "segments": [
    {
      "id": 0,
      "seek": 0,
      "start": 0.0,
      "end": 8.470000267028809,
      "text": " The beach was a popular spot on a hot summer day. People were swimming in the ocean and building sandcastles in the sand.",
      "tokens": [1033, 7534, 390, 257, 2968, 4633, 319, 257, 3024, 3931, 1110, 13, 4380, 547, 10899, 287, 262, 9151, 290, 2615, 6450, 2701, 829, 287, 262, 6450, 13],
      "temperature": 0.0,
      "avg_logprob": -0.2860786020755768,
      "compression_ratio": 1.2363636493682861,
      "no_speech_prob": 0.00985979475080967
    }
  ]
}
```

### SRT Subtitle Format

```javascript
const transcription = await openai.audio.transcriptions.create({
  file: audioFile,
  model: 'whisper-1',
  response_format: 'srt'
});

// Returns SRT format:
// 1
// 00:00:00,000 --> 00:00:04,000
// The beach was a popular spot on a hot summer day.
//
// 2  
// 00:00:04,000 --> 00:00:08,470
// People were swimming in the ocean and building sandcastles.
```

### VTT Format

```javascript
const transcription = await openai.audio.transcriptions.create({
  file: audioFile,
  model: 'whisper-1',
  response_format: 'vtt'
});

// Returns WebVTT format for web players
```

## Advanced Features

### Speaker Diarization

Identify different speakers in audio:

```javascript
const transcription = await openai.audio.transcriptions.create({
  file: audioFile,
  model: 'deepgram-nova-2',
  diarize: true,
  utterances: true,
  punctuate: true
});

// Response includes speaker labels
console.log('Speakers identified:', transcription.metadata.speakers);
transcription.results.utterances.forEach(utterance => {
  console.log(`Speaker ${utterance.speaker}: ${utterance.transcript}`);
});
```

### Custom Vocabulary and Context

Improve accuracy with context:

```javascript
const transcription = await openai.audio.transcriptions.create({
  file: audioFile,
  model: 'whisper-1',
  prompt: 'This is a technical discussion about machine learning, artificial intelligence, neural networks, and deep learning algorithms.',
  temperature: 0.0  // More consistent with prompt
});
```

### Noise Reduction and Enhancement

```javascript
// Some providers support audio enhancement
const transcription = await openai.audio.transcriptions.create({
  file: audioFile,
  model: 'deepgram-nova-2',
  noise_reduction: true,
  smart_format: true,
  redact: ['pii'],  // Remove personally identifiable information
  search: ['machine learning', 'AI', 'neural networks']  // Highlight keywords
});
```

## Real-Time Streaming

### WebSocket Streaming

```javascript
import WebSocket from 'ws';

class StreamingTranscriber {
  constructor(apiKey) {
    this.ws = new WebSocket('wss://api.conduit.yourdomain.com/v1/audio/stream/transcriptions', {
      headers: {
        'Authorization': `Bearer ${apiKey}`
      }
    });
    
    this.setupConnection();
  }

  setupConnection() {
    this.ws.on('open', () => {
      // Configure streaming session
      this.ws.send(JSON.stringify({
        type: 'configure',
        config: {
          model: 'deepgram-nova-2',
          language: 'en-US',
          smart_format: true,
          interim_results: true,
          punctuate: true,
          diarize: true,
          encoding: 'linear16',
          sample_rate: 16000
        }
      }));
    });

    this.ws.on('message', (data) => {
      const result = JSON.parse(data);
      this.handleTranscriptionResult(result);
    });

    this.ws.on('error', (error) => {
      console.error('WebSocket error:', error);
    });
  }

  sendAudioChunk(audioBuffer) {
    if (this.ws.readyState === WebSocket.OPEN) {
      this.ws.send(audioBuffer);
    }
  }

  handleTranscriptionResult(result) {
    if (result.type === 'Results') {
      const transcript = result.channel.alternatives[0].transcript;
      
      if (result.is_final) {
        console.log('Final:', transcript);
        this.onFinalTranscript(transcript);
      } else {
        console.log('Interim:', transcript);
        this.onInterimTranscript(transcript);
      }
    }
  }

  onFinalTranscript(transcript) {
    // Handle final transcript
    document.getElementById('final-transcript').textContent += transcript + ' ';
  }

  onInterimTranscript(transcript) {
    // Handle interim results
    document.getElementById('interim-transcript').textContent = transcript;
  }

  close() {
    this.ws.close();
  }
}
```

### Live Audio Capture

```javascript
class LiveAudioTranscriber {
  constructor(apiKey) {
    this.transcriber = new StreamingTranscriber(apiKey);
    this.mediaRecorder = null;
    this.audioStream = null;
  }

  async startRecording() {
    try {
      this.audioStream = await navigator.mediaDevices.getUserMedia({
        audio: {
          sampleRate: 16000,
          channelCount: 1,
          echoCancellation: true,
          noiseSuppression: true
        }
      });

      this.mediaRecorder = new MediaRecorder(this.audioStream, {
        mimeType: 'audio/webm;codecs=opus'
      });

      this.mediaRecorder.ondataavailable = (event) => {
        if (event.data.size > 0) {
          this.transcriber.sendAudioChunk(event.data);
        }
      };

      this.mediaRecorder.start(100); // Send data every 100ms
    } catch (error) {
      console.error('Error starting recording:', error);
    }
  }

  stopRecording() {
    if (this.mediaRecorder && this.mediaRecorder.state !== 'inactive') {
      this.mediaRecorder.stop();
    }
    
    if (this.audioStream) {
      this.audioStream.getTracks().forEach(track => track.stop());
    }
    
    this.transcriber.close();
  }
}
```

## Batch Processing

### Processing Multiple Files

```javascript
class BatchTranscriber {
  constructor(conduitClient, concurrency = 5) {
    this.client = conduitClient;
    this.concurrency = concurrency;
  }

  async transcribeFiles(audioFiles, options = {}) {
    const results = [];
    
    // Process files in batches to respect rate limits
    for (let i = 0; i < audioFiles.length; i += this.concurrency) {
      const batch = audioFiles.slice(i, i + this.concurrency);
      
      const batchPromises = batch.map(async (file, index) => {
        try {
          const transcription = await this.client.audio.transcriptions.create({
            file: fs.createReadStream(file.path),
            model: 'whisper-1',
            response_format: 'verbose_json',
            ...options
          });

          return {
            file: file.path,
            success: true,
            transcription: transcription,
            duration: transcription.duration,
            wordCount: transcription.text.split(' ').length
          };
        } catch (error) {
          return {
            file: file.path,
            success: false,
            error: error.message
          };
        }
      });

      const batchResults = await Promise.allSettled(batchPromises);
      results.push(...batchResults.map(result => result.value));
      
      // Optional delay between batches
      if (i + this.concurrency < audioFiles.length) {
        await new Promise(resolve => setTimeout(resolve, 1000));
      }
    }

    return results;
  }

  generateReport(results) {
    const successful = results.filter(r => r.success);
    const failed = results.filter(r => !r.success);
    
    const totalDuration = successful.reduce((sum, r) => sum + r.duration, 0);
    const totalWords = successful.reduce((sum, r) => sum + r.wordCount, 0);
    
    return {
      totalFiles: results.length,
      successful: successful.length,
      failed: failed.length,
      totalDuration: totalDuration,
      totalWords: totalWords,
      failedFiles: failed.map(f => ({ file: f.file, error: f.error }))
    };
  }
}

// Usage
const transcriber = new BatchTranscriber(openai);
const audioFiles = [
  { path: 'meeting1.mp3' },
  { path: 'interview2.wav' },
  { path: 'podcast3.m4a' }
];

const results = await transcriber.transcribeFiles(audioFiles, {
  language: 'en',
  temperature: 0.0
});

const report = transcriber.generateReport(results);
console.log('Transcription report:', report);
```

## Use Case Examples

### Meeting Transcription

```javascript
class MeetingTranscriber {
  async transcribeMeeting(audioFile, meetingInfo = {}) {
    const prompt = `This is a ${meetingInfo.type || 'business'} meeting ` +
                  `with ${meetingInfo.participants || 'multiple participants'}. ` +
                  `Topics include: ${meetingInfo.topics || 'general discussion'}.`;

    const transcription = await openai.audio.transcriptions.create({
      file: fs.createReadStream(audioFile),
      model: 'deepgram-nova-2',
      response_format: 'verbose_json',
      diarize: true,
      smart_format: true,
      prompt: prompt
    });

    return {
      fullTranscript: transcription.text,
      speakers: this.extractSpeakers(transcription),
      summary: await this.generateSummary(transcription.text),
      actionItems: await this.extractActionItems(transcription.text),
      duration: transcription.duration,
      timestamp: new Date().toISOString()
    };
  }

  extractSpeakers(transcription) {
    // Extract speaker information from diarization
    const speakers = new Set();
    transcription.results?.utterances?.forEach(utterance => {
      speakers.add(utterance.speaker);
    });
    return Array.from(speakers);
  }

  async generateSummary(transcript) {
    const response = await openai.chat.completions.create({
      model: 'gpt-4',
      messages: [{
        role: 'user',
        content: `Please provide a concise summary of this meeting transcript:\n\n${transcript}`
      }],
      max_tokens: 500
    });
    
    return response.choices[0].message.content;
  }

  async extractActionItems(transcript) {
    const response = await openai.chat.completions.create({
      model: 'gpt-4',
      messages: [{
        role: 'user',
        content: `Extract action items from this meeting transcript. Format as a bullet list:\n\n${transcript}`
      }],
      max_tokens: 300
    });
    
    return response.choices[0].message.content;
  }
}
```

### Podcast Transcription with Chapters

```javascript
class PodcastTranscriber {
  async transcribePodcast(audioFile, showInfo = {}) {
    const transcription = await openai.audio.transcriptions.create({
      file: fs.createReadStream(audioFile),
      model: 'whisper-1',
      response_format: 'verbose_json',
      timestamp_granularities: ['segment'],
      prompt: `This is a podcast episode titled "${showInfo.title}". ` +
              `Hosts: ${showInfo.hosts}. Topics: ${showInfo.topics}.`
    });

    const chapters = this.generateChapters(transcription.segments);
    const transcript = this.formatTranscript(transcription);
    
    return {
      metadata: {
        title: showInfo.title,
        duration: transcription.duration,
        language: transcription.language,
        processingTime: Date.now()
      },
      chapters: chapters,
      fullTranscript: transcript,
      searchableText: transcription.text,
      srtSubtitles: this.generateSRT(transcription.segments)
    };
  }

  generateChapters(segments, chapterLengthMinutes = 10) {
    const chapterLength = chapterLengthMinutes * 60; // Convert to seconds
    const chapters = [];
    let currentChapter = {
      title: 'Chapter 1',
      startTime: 0,
      segments: []
    };

    segments.forEach(segment => {
      if (segment.start - currentChapter.startTime >= chapterLength) {
        // Start new chapter
        currentChapter.endTime = segment.start;
        currentChapter.text = currentChapter.segments.map(s => s.text).join(' ');
        chapters.push(currentChapter);

        currentChapter = {
          title: `Chapter ${chapters.length + 1}`,
          startTime: segment.start,
          segments: []
        };
      }
      
      currentChapter.segments.push(segment);
    });

    // Add final chapter
    if (currentChapter.segments.length > 0) {
      currentChapter.endTime = segments[segments.length - 1].end;
      currentChapter.text = currentChapter.segments.map(s => s.text).join(' ');
      chapters.push(currentChapter);
    }

    return chapters;
  }

  formatTranscript(transcription) {
    return transcription.segments.map((segment, index) => {
      const timestamp = this.formatTimestamp(segment.start);
      return `[${timestamp}] ${segment.text.trim()}`;
    }).join('\n\n');
  }

  formatTimestamp(seconds) {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const secs = Math.floor(seconds % 60);
    
    if (hours > 0) {
      return `${hours}:${minutes.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
    } else {
      return `${minutes}:${secs.toString().padStart(2, '0')}`;
    }
  }

  generateSRT(segments) {
    return segments.map((segment, index) => {
      const startTime = this.formatSRTTimestamp(segment.start);
      const endTime = this.formatSRTTimestamp(segment.end);
      
      return `${index + 1}\n${startTime} --> ${endTime}\n${segment.text.trim()}\n`;
    }).join('\n');
  }

  formatSRTTimestamp(seconds) {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const secs = seconds % 60;
    const milliseconds = Math.floor((secs % 1) * 1000);
    const wholeSeconds = Math.floor(secs);
    
    return `${hours.toString().padStart(2, '0')}:${minutes.toString().padStart(2, '0')}:${wholeSeconds.toString().padStart(2, '0')},${milliseconds.toString().padStart(3, '0')}`;
  }
}
```

## Error Handling and Troubleshooting

### Common Errors

```javascript
try {
  const transcription = await openai.audio.transcriptions.create({
    file: fs.createReadStream('audio.mp3'),
    model: 'whisper-1'
  });
} catch (error) {
  switch (error.code) {
    case 'invalid_file_format':
      console.log('File format not supported. Use mp3, wav, m4a, etc.');
      break;
      
    case 'file_too_large':
      console.log('File exceeds 100MB limit. Consider splitting the audio.');
      break;
      
    case 'audio_too_long':
      console.log('Audio exceeds maximum duration. Split into shorter segments.');
      break;
      
    case 'unsupported_language':
      console.log('Language not supported by selected model.');
      break;
      
    case 'provider_unavailable':
      console.log('Transcription provider temporarily unavailable.');
      // Try alternative provider
      break;
      
    case 'rate_limit_exceeded':
      console.log('Rate limit exceeded. Wait before retrying.');
      break;
      
    default:
      console.log('Transcription error:', error.message);
  }
}
```

### Audio Quality Issues

```javascript
class AudioQualityChecker {
  static analyzeAudio(audioFile) {
    // This would typically use an audio analysis library
    const analysis = {
      sampleRate: 44100,
      bitRate: 128,
      duration: 300,
      channels: 2,
      format: 'mp3'
    };
    
    const recommendations = [];
    
    if (analysis.sampleRate < 16000) {
      recommendations.push('Consider using higher sample rate (16kHz+) for better accuracy');
    }
    
    if (analysis.bitRate < 64) {
      recommendations.push('Low bit rate may affect transcription quality');
    }
    
    if (analysis.channels > 1) {
      recommendations.push('Mono audio often works better for transcription');
    }
    
    return { analysis, recommendations };
  }
}
```

## Performance Optimization

### Caching Transcriptions

```javascript
class CachedTranscriber {
  constructor(conduitClient, cacheDir = './transcription-cache') {
    this.client = conduitClient;
    this.cacheDir = cacheDir;
    this.ensureCacheDir();
  }

  ensureCacheDir() {
    if (!fs.existsSync(this.cacheDir)) {
      fs.mkdirSync(this.cacheDir, { recursive: true });
    }
  }

  getCacheKey(audioFile, options) {
    const stats = fs.statSync(audioFile);
    const content = `${audioFile}-${stats.mtime.getTime()}-${JSON.stringify(options)}`;
    return require('crypto').createHash('md5').update(content).digest('hex');
  }

  async transcribeWithCache(audioFile, options = {}) {
    const cacheKey = this.getCacheKey(audioFile, options);
    const cacheFile = path.join(this.cacheDir, `${cacheKey}.json`);
    
    // Check cache first
    if (fs.existsSync(cacheFile)) {
      console.log('Using cached transcription');
      return JSON.parse(fs.readFileSync(cacheFile, 'utf8'));
    }
    
    // Transcribe and cache
    const transcription = await this.client.audio.transcriptions.create({
      file: fs.createReadStream(audioFile),
      ...options
    });
    
    fs.writeFileSync(cacheFile, JSON.stringify(transcription, null, 2));
    console.log('Transcription cached');
    
    return transcription;
  }
}
```

## Next Steps

- **Text-to-Speech**: Convert text to natural speech with [voice synthesis](text-to-speech)
- **Real-Time Audio**: Build interactive voice applications with [real-time audio](real-time-audio)
- **Audio Providers**: Compare capabilities across [different providers](providers)
- **Integration Examples**: See complete [integration patterns](../clients/overview)