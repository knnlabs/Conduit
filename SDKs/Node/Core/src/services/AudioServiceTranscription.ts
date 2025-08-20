import { HttpMethod } from '../client/HttpMethod';
import type { RequestOptions } from '../client/types';
import type {
  AudioTranscriptionRequest,
  AudioTranscriptionResponse,
  AudioTranslationRequest,
  AudioTranslationResponse,
  AudioFile,
  TranscriptionModel,
} from '../models/audio';
import { AudioServiceBase } from './AudioServiceBase';

/**
 * Audio transcription and translation functionality.
 */
export abstract class AudioServiceTranscription extends AudioServiceBase {
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
}