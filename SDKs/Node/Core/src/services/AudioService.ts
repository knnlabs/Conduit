import { FetchBasedClient } from '../client/FetchBasedClient';
import { HttpMethod } from '../client/HttpMethod';
import type { RequestOptions } from '../client/types';
import type {
  AudioTranscriptionRequest,
  AudioTranscriptionResponse,
  AudioTranslationRequest,
  AudioTranslationResponse,
  TextToSpeechRequest,
  TextToSpeechResponse,
  HybridAudioRequest,
  HybridAudioResponse,
  AudioFile,
  AudioFormat,
  TranscriptionModel,
  TextToSpeechModel,
  Voice,
  AudioMetadata
} from '../models/audio';

/**
 * Service for audio operations including speech-to-text, text-to-speech, and audio translation.
 * Provides OpenAI-compatible audio API endpoints for transcription, translation, and speech synthesis.
 * 
 * @example
 * ```typescript
 * // Initialize the service
 * const audio = client.audio;
 * 
 * // Transcribe audio
 * const transcription = await audio.transcribe({
 *   file: AudioUtils.fromBuffer(audioBuffer, 'speech.mp3'),
 *   model: 'whisper-1'
 * });
 * 
 * // Generate speech
 * const speech = await audio.generateSpeech({
 *   model: 'tts-1',
 *   input: 'Hello, world!',
 *   voice: 'alloy'
 * });
 * ```
 */
export class AudioService extends FetchBasedClient {
  constructor(client: FetchBasedClient) {
    // @ts-expect-error Accessing protected property from another instance
    super(client.config);
  }

  /**
   * Transcribes audio to text using speech-to-text models.
   * Supports multiple audio formats and languages with customizable output formats.
   * 
   * @param request - The transcription request
   * @param options - Optional request options
   * @returns Promise resolving to transcription response
   * 
   * @example
   * ```typescript
   * // Basic transcription
   * const result = await audio.transcribe({
   *   file: AudioUtils.fromBuffer(audioBuffer, 'audio.mp3'),
   *   model: 'whisper-1'
   * });
   * console.warn(result.text);
   * 
   * // With language and timestamps
   * const detailed = await audio.transcribe({
   *   file: AudioUtils.fromBuffer(audioBuffer, 'audio.mp3'),
   *   model: 'whisper-1',
   *   language: 'en',
   *   response_format: 'verbose_json',
   *   timestamp_granularities: ['word', 'segment']
   * });
   * ```
   */
  async transcribe(
    request: AudioTranscriptionRequest,
    options?: RequestOptions
  ): Promise<AudioTranscriptionResponse> {
    this.validateTranscriptionRequest(request);

    const formData = this.createAudioFormData(request.file, {
      model: request.model,
      language: request.language,
      prompt: request.prompt,
      response_format: request.response_format,
      temperature: request.temperature,
      timestamp_granularities: request.timestamp_granularities,
    });

    return this.request<AudioTranscriptionResponse>(
      '/v1/audio/transcriptions',
      {
        method: HttpMethod.POST,
        body: formData as BodyInit,
        headers: {
          'Content-Type': 'multipart/form-data',
        },
        ...options
      }
    );
  }

  /**
   * Translates audio to English text using speech-to-text models.
   * @param request The translation request
   * @param options Optional request options
   * @returns Promise resolving to translation response
   */
  async translate(
    request: AudioTranslationRequest,
    options?: RequestOptions
  ): Promise<AudioTranslationResponse> {
    this.validateTranslationRequest(request);

    const formData = this.createAudioFormData(request.file, {
      model: request.model,
      prompt: request.prompt,
      response_format: request.response_format,
      temperature: request.temperature,
    });

    return this.request<AudioTranslationResponse>(
      '/v1/audio/translations',
      {
        method: HttpMethod.POST,
        body: formData as BodyInit,
        headers: {
          'Content-Type': 'multipart/form-data',
        },
        ...options
      }
    );
  }

  /**
   * Generates speech from text using text-to-speech models.
   * Supports multiple voices and audio formats with adjustable speed.
   * 
   * @param request - The speech generation request
   * @param options - Optional request options
   * @returns Promise resolving to speech response with audio data
   * 
   * @example
   * ```typescript
   * // Generate speech with default settings
   * const speech = await audio.generateSpeech({
   *   model: 'tts-1',
   *   input: 'Welcome to our service!',
   *   voice: 'nova'
   * });
   * 
   * // High quality with specific format
   * const hdSpeech = await audio.generateSpeech({
   *   model: 'tts-1-hd',
   *   input: 'This is high quality audio.',
   *   voice: 'alloy',
   *   response_format: 'mp3',
   *   speed: 1.0
   * });
   * 
   * // Save to file
   * fs.writeFileSync('output.mp3', speech.audio);
   * ```
   */
  async generateSpeech(
    request: TextToSpeechRequest,
    options?: RequestOptions
  ): Promise<TextToSpeechResponse> {
    this.validateSpeechRequest(request);

    const response = await this.request<ArrayBuffer>(
      '/v1/audio/speech',
      {
        method: HttpMethod.POST,
        body: request,
        responseType: 'arraybuffer',
        ...options
      }
    );

    // Convert ArrayBuffer to Buffer for Node.js compatibility
    const audioBuffer = Buffer.from(response);
    
    return {
      audio: audioBuffer,
      format: request.response_format ?? 'mp3',
      metadata: {
        size: audioBuffer.length,
      },
    };
  }

  /**
   * Processes audio through the hybrid pipeline (STT + LLM + TTS).
   * @param request The hybrid audio processing request
   * @param options Optional request options
   * @returns Promise resolving to hybrid audio response
   */
  async processHybrid(
    request: HybridAudioRequest,
    options?: RequestOptions
  ): Promise<HybridAudioResponse> {
    this.validateHybridRequest(request);

    const formData = this.createAudioFormData(request.file, {
      models: request.models,
      voice: request.voice,
      system_prompt: request.system_prompt,
      context: request.context,
      language: request.language,
      temperature: request.temperature,
      voice_settings: request.voice_settings,
      session_id: request.session_id,
    });

    const response = await this.request<ArrayBuffer>(
      '/v1/audio/hybrid/process',
      {
        method: HttpMethod.POST,
        body: formData as BodyInit,
        headers: {
          'Content-Type': 'multipart/form-data',
        },
        responseType: 'arraybuffer',
        ...options
      }
    );

    // Parse the hybrid response (assuming it includes metadata in headers)
    // This would need to be adapted based on the actual API response format
    const audioBuffer = Buffer.from(response);
    
    return {
      audio: audioBuffer,
      transcription: '', // Would be populated from response headers or separate endpoint
      llm_response: '', // Would be populated from response headers or separate endpoint
      stages: {
        transcription: { duration: 0 },
        llm: { duration: 0, tokens_used: 0, model_used: request.models.chat },
        speech: { duration: 0, audio_duration: 0, format: 'mp3' as AudioFormat },
      },
      usage: {
        llm_tokens: { prompt_tokens: 0, completion_tokens: 0, total_tokens: 0 },
        total_processing_time_ms: 0,
      },
    };
  }

  /**
   * Creates a simple transcription request for quick speech-to-text conversion.
   * @param audioFile The audio file to transcribe
   * @param model Optional model to use (defaults to 'whisper-1')
   * @param language Optional language code
   * @returns Promise resolving to transcription text
   */
  async quickTranscribe(
    audioFile: AudioFile,
    model: TranscriptionModel = 'whisper-1',
    language?: string
  ): Promise<string> {
    const request: AudioTranscriptionRequest = {
      file: audioFile,
      model,
      language,
      response_format: 'text',
    };

    const response = await this.transcribe(request);
    return response.text;
  }

  /**
   * Creates a simple speech generation request for quick text-to-speech conversion.
   * @param text The text to convert to speech
   * @param voice Optional voice to use (defaults to 'alloy')
   * @param model Optional model to use (defaults to 'tts-1')
   * @returns Promise resolving to audio buffer
   */
  async quickSpeak(
    text: string,
    voice: Voice = 'alloy',
    model: TextToSpeechModel = 'tts-1'
  ): Promise<Buffer> {
    const request: TextToSpeechRequest = {
      model,
      input: text,
      voice,
      response_format: 'mp3',
    };

    const response = await this.generateSpeech(request);
    return response.audio;
  }

  /**
   * Validates an audio transcription request.
   * @private
   */
  private validateTranscriptionRequest(request: AudioTranscriptionRequest): void {
    if (!request.file) {
      throw new Error('Audio file is required for transcription');
    }

    if (!request.model) {
      throw new Error('Model is required for transcription');
    }

    if (request.temperature !== undefined && (request.temperature < 0 || request.temperature > 1)) {
      throw new Error('Temperature must be between 0 and 1');
    }

    this.validateAudioFile(request.file);
  }

  /**
   * Validates an audio translation request.
   * @private
   */
  private validateTranslationRequest(request: AudioTranslationRequest): void {
    if (!request.file) {
      throw new Error('Audio file is required for translation');
    }

    if (!request.model) {
      throw new Error('Model is required for translation');
    }

    if (request.temperature !== undefined && (request.temperature < 0 || request.temperature > 1)) {
      throw new Error('Temperature must be between 0 and 1');
    }

    this.validateAudioFile(request.file);
  }

  /**
   * Validates a text-to-speech request.
   * @private
   */
  private validateSpeechRequest(request: TextToSpeechRequest): void {
    if (!request.input || request.input.trim().length === 0) {
      throw new Error('Input text is required for speech generation');
    }

    if (request.input.length > 4096) {
      throw new Error('Input text must be 4096 characters or less');
    }

    if (!request.model) {
      throw new Error('Model is required for speech generation');
    }

    if (!request.voice) {
      throw new Error('Voice is required for speech generation');
    }

    if (request.speed !== undefined && (request.speed < 0.25 || request.speed > 4.0)) {
      throw new Error('Speed must be between 0.25 and 4.0');
    }
  }

  /**
   * Validates a hybrid audio request.
   * @private
   */
  private validateHybridRequest(request: HybridAudioRequest): void {
    if (!request.file) {
      throw new Error('Audio file is required for hybrid processing');
    }

    if (!request.models) {
      throw new Error('Models configuration is required for hybrid processing');
    }

    if (!request.models.transcription) {
      throw new Error('Transcription model is required for hybrid processing');
    }

    if (!request.models.chat) {
      throw new Error('Chat model is required for hybrid processing');
    }

    if (!request.models.speech) {
      throw new Error('Speech model is required for hybrid processing');
    }

    if (!request.voice) {
      throw new Error('Voice is required for hybrid processing');
    }

    this.validateAudioFile(request.file);
  }

  /**
   * Validates an audio file.
   * @private
   */
  private validateAudioFile(file: AudioFile): void {
    if (!file.data) {
      throw new Error('Audio file data is required');
    }

    if (!file.filename) {
      throw new Error('Audio filename is required');
    }

    // Validate file extension
    const extension = file.filename.toLowerCase().split('.').pop();
    const supportedExtensions = ['mp3', 'wav', 'flac', 'ogg', 'aac', 'opus', 'pcm', 'm4a', 'webm'];
    
    if (!extension || !supportedExtensions.includes(extension)) {
      throw new Error(`Unsupported audio format. Supported formats: ${supportedExtensions.join(', ')}`);
    }

    // Validate file size (25MB limit for most providers)
    const maxSize = 25 * 1024 * 1024; // 25MB
    let fileSize = 0;

    if (Buffer.isBuffer(file.data)) {
      fileSize = file.data.length;
    } else if (file.data instanceof Blob) {
      fileSize = file.data.size;
    } else if (typeof file.data === 'string') {
      // Assume base64 string
      fileSize = Math.ceil(file.data.length * 0.75); // Approximate decoded size
    }

    if (fileSize > maxSize) {
      throw new Error(`Audio file too large. Maximum size is ${maxSize / (1024 * 1024)}MB`);
    }
  }

  /**
   * Creates FormData for audio file uploads.
   * @private
   */
  private createAudioFormData(file: AudioFile, additionalFields: Record<string, unknown>): FormData {
    const formData = new FormData();
    
    // Handle different file data types
    let fileBlob: Blob;
    
    if (Buffer.isBuffer(file.data)) {
      fileBlob = new Blob([file.data], { type: file.contentType ?? 'audio/mpeg' });
    } else if (file.data instanceof Blob) {
      fileBlob = file.data;
    } else if (typeof file.data === 'string') {
      // Assume base64 string
      const binaryString = atob(file.data);
      const bytes = new Uint8Array(binaryString.length);
      for (let i = 0; i < binaryString.length; i++) {
        bytes[i] = binaryString.charCodeAt(i);
      }
      fileBlob = new Blob([bytes], { type: file.contentType ?? 'audio/mpeg' });
    } else {
      throw new Error('Unsupported file data type');
    }

    formData.append('file', fileBlob, file.filename);

    // Add additional fields
    Object.entries(additionalFields).forEach(([key, value]) => {
      if (value !== undefined) {
        if (typeof value === 'object') {
          formData.append(key, JSON.stringify(value));
        } else {
          formData.append(key, String(value));
        }
      }
    });

    return formData;
  }
}

/**
 * Audio utility functions for working with audio files.
 * Provides helper methods for creating AudioFile objects from various sources.
 * 
 * @example
 * ```typescript
 * // From Buffer (Node.js)
 * const audioFile = AudioUtils.fromBuffer(buffer, 'audio.mp3', 'audio/mpeg');
 * 
 * // From Blob (Browser)
 * const audioFile = AudioUtils.fromBlob(blob, 'recording.wav');
 * 
 * // From Base64
 * const audioFile = AudioUtils.fromBase64(base64String, 'speech.mp3');
 * ```
 */
export class AudioUtils {
  /**
   * Creates an AudioFile from a Buffer with specified filename.
   */
  static fromBuffer(data: Buffer, filename: string, contentType?: string): AudioFile {
    return {
      data,
      filename,
      contentType,
    };
  }

  /**
   * Creates an AudioFile from a Blob with specified filename.
   */
  static fromBlob(data: Blob, filename: string): AudioFile {
    return {
      data,
      filename,
      contentType: data.type,
    };
  }

  /**
   * Creates an AudioFile from a base64 string with specified filename.
   */
  static fromBase64(data: string, filename: string, contentType?: string): AudioFile {
    return {
      data,
      filename,
      contentType,
    };
  }

  /**
   * Gets audio file metadata (basic validation).
   */
  static getBasicMetadata(file: AudioFile): AudioMetadata {
    let size = 0;
    
    if (Buffer.isBuffer(file.data)) {
      size = file.data.length;
    } else if (file.data instanceof Blob) {
      size = file.data.size;
    } else if (typeof file.data === 'string') {
      size = Math.ceil(file.data.length * 0.75);
    }

    const extension = file.filename.toLowerCase().split('.').pop() ?? 'unknown';
    
    return {
      duration: 0, // Would need audio analysis library to determine
      size,
      format: extension as AudioFormat,
      sample_rate: 0, // Would need audio analysis
      channels: 0, // Would need audio analysis
    };
  }

  /**
   * Validates if the audio format is supported.
   */
  static isFormatSupported(format: string): boolean {
    const supportedFormats = ['mp3', 'wav', 'flac', 'ogg', 'aac', 'opus', 'pcm', 'm4a', 'webm'];
    return supportedFormats.includes(format.toLowerCase());
  }

  /**
   * Gets the appropriate content type for an audio format.
   */
  static getContentType(format: AudioFormat): string {
    const contentTypes: Record<AudioFormat, string> = {
      mp3: 'audio/mpeg',
      wav: 'audio/wav',
      flac: 'audio/flac',
      ogg: 'audio/ogg',
      aac: 'audio/aac',
      opus: 'audio/opus',
      pcm: 'audio/pcm',
      m4a: 'audio/mp4',
      webm: 'audio/webm',
    };
    
    return contentTypes[format] || 'audio/mpeg';
  }
}