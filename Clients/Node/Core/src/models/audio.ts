/**
 * Audio API models for Conduit Core client library
 * Supports speech-to-text, text-to-speech, and audio translation capabilities
 */

import type { Usage } from './common';

// Audio format enums
export type AudioFormat = 
  | 'mp3' 
  | 'wav' 
  | 'flac' 
  | 'ogg' 
  | 'aac' 
  | 'opus' 
  | 'pcm' 
  | 'm4a' 
  | 'webm';

export type TranscriptionFormat = 
  | 'json' 
  | 'text' 
  | 'srt' 
  | 'vtt' 
  | 'verbose_json';

export type TimestampGranularity = 
  | 'segment' 
  | 'word';

export type TextToSpeechModel = 
  | 'tts-1' 
  | 'tts-1-hd' 
  | 'elevenlabs-tts' 
  | 'azure-tts' 
  | 'openai-tts';

export type Voice = 
  | 'alloy' 
  | 'echo' 
  | 'fable' 
  | 'onyx' 
  | 'nova' 
  | 'shimmer'
  | 'rachel'
  | 'adam'
  | 'antoni'
  | 'arnold'
  | 'josh'
  | 'sam';

export type TranscriptionModel = 
  | 'whisper-1' 
  | 'whisper-large' 
  | 'deepgram-nova' 
  | 'azure-stt' 
  | 'openai-whisper';

// Base audio interfaces
export interface AudioFile {
  /** The audio file data as Buffer, Blob, or base64 string */
  data: Buffer | Blob | string;
  /** The filename of the audio file */
  filename: string;
  /** The MIME type of the audio file */
  contentType?: string;
}

export interface VoiceSettings {
  /** Voice stability (0.0 to 1.0) */
  stability?: number;
  /** Voice similarity boost (0.0 to 1.0) */
  similarity_boost?: number;
  /** Voice style exaggeration (0.0 to 1.0) */
  style?: number;
  /** Use speaker boost for enhanced clarity */
  use_speaker_boost?: boolean;
}

// Speech-to-Text (Transcription) interfaces
export interface AudioTranscriptionRequest {
  /** The audio file to transcribe */
  file: AudioFile;
  /** The model to use for transcription */
  model: TranscriptionModel;
  /** The language of the input audio (ISO-639-1 format, e.g., 'en', 'es') */
  language?: string;
  /** An optional text to guide the model's style or continue a previous audio segment */
  prompt?: string;
  /** The format of the transcript output */
  response_format?: TranscriptionFormat;
  /** The sampling temperature (0 to 1) */
  temperature?: number;
  /** The timestamp granularities to populate for this transcription */
  timestamp_granularities?: TimestampGranularity[];
}

export interface AudioTranscriptionResponse {
  /** The transcribed text */
  text: string;
  /** The task performed (e.g., 'transcribe') */
  task?: string;
  /** The language of the input audio */
  language?: string;
  /** The duration of the input audio in seconds */
  duration?: number;
  /** Array of transcription segments with timestamps */
  segments?: TranscriptionSegment[];
  /** Array of words with timestamps (if word-level timestamps requested) */
  words?: TranscriptionWord[];
  /** Token usage information */
  usage?: Usage;
}

export interface TranscriptionSegment {
  /** Unique identifier of the segment */
  id: number;
  /** Seek offset of the segment */
  seek: number;
  /** Start time of the segment in seconds */
  start: number;
  /** End time of the segment in seconds */
  end: number;
  /** Text content of the segment */
  text: string;
  /** Array of token IDs for the text content */
  tokens: number[];
  /** Temperature parameter used for generation */
  temperature: number;
  /** Average logprob of the segment */
  avg_logprob: number;
  /** Compression ratio of the segment */
  compression_ratio: number;
  /** Probability of no speech */
  no_speech_prob: number;
}

export interface TranscriptionWord {
  /** The text content of the word */
  word: string;
  /** Start time of the word in seconds */
  start: number;
  /** End time of the word in seconds */
  end: number;
}

// Audio Translation interfaces
export interface AudioTranslationRequest {
  /** The audio file to translate */
  file: AudioFile;
  /** The model to use for translation */
  model: TranscriptionModel;
  /** An optional text to guide the model's style or continue a previous audio segment */
  prompt?: string;
  /** The format of the transcript output */
  response_format?: TranscriptionFormat;
  /** The sampling temperature (0 to 1) */
  temperature?: number;
}

export interface AudioTranslationResponse {
  /** The translated text (always in English) */
  text: string;
  /** The task performed (e.g., 'translate') */
  task?: string;
  /** The language of the input audio */
  language?: string;
  /** The duration of the input audio in seconds */
  duration?: number;
  /** Array of translation segments with timestamps */
  segments?: TranscriptionSegment[];
  /** Array of words with timestamps (if word-level timestamps requested) */
  words?: TranscriptionWord[];
  /** Token usage information */
  usage?: Usage;
}

// Text-to-Speech interfaces
export interface TextToSpeechRequest {
  /** The model to use for speech generation */
  model: TextToSpeechModel;
  /** The text to convert to speech (max 4096 characters) */
  input: string;
  /** The voice to use for speech generation */
  voice: Voice;
  /** The format to audio in */
  response_format?: AudioFormat;
  /** The speed of the generated audio (0.25 to 4.0) */
  speed?: number;
  /** Advanced voice settings (for compatible providers) */
  voice_settings?: VoiceSettings;
}

export interface TextToSpeechResponse {
  /** The generated audio data as Buffer */
  audio: Buffer;
  /** The format of the returned audio */
  format: AudioFormat;
  /** Additional metadata about the generation */
  metadata?: {
    /** Duration of the generated audio in seconds */
    duration?: number;
    /** Size of the audio data in bytes */
    size?: number;
    /** Sample rate of the audio */
    sample_rate?: number;
    /** Number of audio channels */
    channels?: number;
  };
  /** Token usage information (if applicable) */
  usage?: Usage;
}

// Hybrid Audio interfaces (STT + LLM + TTS pipeline)
export interface HybridAudioRequest {
  /** The input audio file for processing */
  file: AudioFile;
  /** The model configuration for each stage */
  models: {
    /** Speech-to-text model */
    transcription: TranscriptionModel;
    /** Chat completion model for LLM processing */
    chat: string;
    /** Text-to-speech model */
    speech: TextToSpeechModel;
  };
  /** Voice configuration for TTS output */
  voice: Voice;
  /** System prompt for the LLM stage */
  system_prompt?: string;
  /** Additional context for the conversation */
  context?: string;
  /** Language settings */
  language?: string;
  /** Temperature settings for each stage */
  temperature?: {
    transcription?: number;
    chat?: number;
  };
  /** Voice settings for TTS */
  voice_settings?: VoiceSettings;
  /** Session ID for conversation continuity */
  session_id?: string;
}

export interface HybridAudioResponse {
  /** The generated audio response */
  audio: Buffer;
  /** The transcribed input text */
  transcription: string;
  /** The LLM's text response */
  llm_response: string;
  /** Metadata for each processing stage */
  stages: {
    transcription: {
      duration: number;
      confidence?: number;
      language?: string;
    };
    llm: {
      duration: number;
      tokens_used: number;
      model_used: string;
    };
    speech: {
      duration: number;
      audio_duration: number;
      format: AudioFormat;
    };
  };
  /** Combined usage statistics */
  usage: {
    transcription_tokens?: number;
    llm_tokens: Usage;
    total_processing_time_ms: number;
  };
  /** Session information */
  session_id?: string;
}

// Real-time Audio interfaces (for WebSocket connections)
export interface RealtimeConnectionRequest {
  /** The model to use for real-time processing */
  model: string;
  /** Voice configuration for real-time TTS */
  voice?: Voice;
  /** Audio format for input/output */
  audio_format?: AudioFormat;
  /** Sample rate for audio processing */
  sample_rate?: number;
  /** Whether to enable voice activity detection */
  enable_vad?: boolean;
  /** Session configuration */
  session_config?: RealtimeSessionConfig;
}

export interface RealtimeSessionConfig {
  /** Instructions for the assistant */
  instructions?: string;
  /** Audio input/output configuration */
  input_audio_format?: AudioFormat;
  output_audio_format?: AudioFormat;
  /** Voice configuration */
  voice?: Voice;
  /** Model configuration */
  model?: string;
  /** Temperature for responses */
  temperature?: number;
  /** Maximum response tokens */
  max_response_output_tokens?: number;
  /** Tools available to the assistant */
  tools?: Array<{
    type: 'function';
    name: string;
    description?: string;
    parameters?: Record<string, unknown>;
  }>;
  /** Turn detection configuration */
  turn_detection?: {
    type: 'server_vad' | 'none';
    threshold?: number;
    prefix_padding_ms?: number;
    silence_duration_ms?: number;
  };
}

export interface RealtimeMessage {
  /** The type of real-time message */
  type: 
    | 'session.created'
    | 'session.updated' 
    | 'input_audio_buffer.append'
    | 'input_audio_buffer.commit'
    | 'input_audio_buffer.clear'
    | 'conversation.item.create'
    | 'response.create'
    | 'response.cancel'
    | 'error';
  /** The message data */
  data?: Record<string, unknown>;
  /** Message ID for tracking */
  event_id?: string;
}

export interface RealtimeSession {
  /** Unique session identifier */
  id: string;
  /** Current session status */
  status: 'active' | 'idle' | 'ended' | 'error';
  /** Session configuration */
  config: RealtimeSessionConfig;
  /** Connection metadata */
  connection: {
    created_at: string;
    last_activity: string;
    duration_seconds: number;
  };
  /** Usage statistics for the session */
  usage: {
    total_audio_minutes: number;
    total_tokens: number;
    input_tokens: number;
    output_tokens: number;
  };
}

// Audio utility types
export interface AudioMetadata {
  /** Duration in seconds */
  duration: number;
  /** File size in bytes */
  size: number;
  /** Audio format */
  format: AudioFormat;
  /** Sample rate in Hz */
  sample_rate: number;
  /** Number of channels */
  channels: number;
  /** Bit depth */
  bit_depth?: number;
  /** Bitrate in kbps */
  bitrate?: number;
}

export interface AudioProcessingOptions {
  /** Maximum file size in bytes (default: 25MB) */
  max_file_size?: number;
  /** Supported audio formats */
  supported_formats?: AudioFormat[];
  /** Quality settings */
  quality?: 'low' | 'medium' | 'high' | 'ultra';
  /** Whether to normalize audio */
  normalize?: boolean;
  /** Whether to remove noise */
  denoise?: boolean;
}

// Error types specific to audio processing
export interface AudioError {
  code: 
    | 'invalid_audio_format'
    | 'file_too_large'
    | 'audio_too_long'
    | 'unsupported_language'
    | 'transcription_failed'
    | 'synthesis_failed'
    | 'realtime_connection_failed';
  message: string;
  details?: Record<string, unknown>;
}

// Audio validation utilities
export interface AudioValidation {
  /** Validate audio file format and size */
  validateAudioFile(file: AudioFile, options?: AudioProcessingOptions): Promise<boolean>;
  /** Get audio metadata */
  getAudioMetadata(file: AudioFile): Promise<AudioMetadata>;
  /** Convert audio format */
  convertAudioFormat(file: AudioFile, targetFormat: AudioFormat): Promise<AudioFile>;
}

// Types are exported automatically by TypeScript