import { HttpMethod } from '../client/HttpMethod';
import type { RequestOptions } from '../client/types';
import type {
  HybridAudioRequest,
  HybridAudioResponse,
  AudioFormat,
} from '../models/audio';
import { AudioServiceBase } from './AudioServiceBase';

/**
 * Audio hybrid processing functionality (STT + LLM + TTS pipeline).
 */
export abstract class AudioServiceHybrid extends AudioServiceBase {
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
}