import { HttpMethod } from '../client/HttpMethod';
import type { RequestOptions } from '../client/types';
import type {
  TextToSpeechRequest,
  TextToSpeechResponse,
  TextToSpeechModel,
  Voice,
} from '../models/audio';
import { AudioServiceBase } from './AudioServiceBase';

/**
 * Audio speech generation functionality.
 */
export abstract class AudioServiceSpeech extends AudioServiceBase {
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
}