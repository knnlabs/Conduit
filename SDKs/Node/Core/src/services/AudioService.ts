import type { FetchBasedClient } from '../client/FetchBasedClient';
import type { RequestOptions } from '../client/types';
import { AudioServiceTranscription } from './AudioServiceTranscription';
import { AudioServiceSpeech } from './AudioServiceSpeech';
import { AudioServiceHybrid } from './AudioServiceHybrid';
import type {
  AudioTranscriptionRequest,
  AudioTranscriptionResponse,
  AudioTranslationRequest,
  AudioTranslationResponse,
  TextToSpeechRequest,
  TextToSpeechResponse,
  AudioFile,
  TranscriptionModel,
  TextToSpeechModel,
  Voice,
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
export class AudioService extends AudioServiceHybrid {
  // Mix in transcription functionality
  private transcriptionService: AudioServiceTranscription;
  // Mix in speech functionality
  private speechService: AudioServiceSpeech;

  constructor(client: FetchBasedClient) {
    super(client);
    
    // Create service instances for delegation
    this.transcriptionService = new (class extends AudioServiceTranscription {})(
      client
    );
    this.speechService = new (class extends AudioServiceSpeech {})(
      client
    );
  }

  // Delegate transcription methods to transcription service
  async transcribe(
    request: AudioTranscriptionRequest,
    options?: RequestOptions
  ): Promise<AudioTranscriptionResponse> {
    return this.transcriptionService.transcribe(request, options);
  }

  async translate(
    request: AudioTranslationRequest,
    options?: RequestOptions
  ): Promise<AudioTranslationResponse> {
    return this.transcriptionService.translate(request, options);
  }

  // Delegate speech generation methods to speech service
  async generateSpeech(
    request: TextToSpeechRequest,
    options?: RequestOptions
  ): Promise<TextToSpeechResponse> {
    return this.speechService.generateSpeech(request, options);
  }

  // Hybrid processing inherited from AudioServiceHybrid

  // Delegate utility methods
  async quickTranscribe(
    audioFile: AudioFile,
    model: TranscriptionModel = 'whisper-1',
    language?: string
  ): Promise<string> {
    return this.transcriptionService.quickTranscribe(audioFile, model, language);
  }

  async quickSpeak(
    text: string,
    voice: Voice = 'alloy',
    model: TextToSpeechModel = 'tts-1'
  ): Promise<Buffer> {
    return this.speechService.quickSpeak(text, voice, model);
  }

  // Validation and form data methods are now in the base classes
}

// Export AudioUtils from separate file
export { AudioUtils } from './AudioUtils';