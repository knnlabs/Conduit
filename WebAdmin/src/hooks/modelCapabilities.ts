import type { ModelDto } from '@/lib/admin-api';

export interface DiscoveredModelCapabilities {
  supportsVision: boolean;
  supportsImageGeneration: boolean;
  supportsAudioTranscription: boolean;
  supportsTextToSpeech: boolean;
  supportsRealtimeAudio: boolean;
  supportsFunctionCalling: boolean;
  supportsStreaming: boolean;
  supportsVideoGeneration: boolean;
  supportsEmbeddings: boolean;
  supportsChat: boolean;
  maxContextLength?: number | null;
  maxOutputTokens?: number | null;
}

export function mapDiscoveredModelCapabilities(model: ModelDto): DiscoveredModelCapabilities {
  return {
    supportsVision: model.supportsVision ?? false,
    supportsImageGeneration: model.supportsImageGeneration ?? false,
    supportsAudioTranscription: model.supportsSpeechToText ?? false,
    supportsTextToSpeech: model.supportsTextToSpeech ?? false,
    // The Admin API does not currently expose a realtime-audio model capability.
    supportsRealtimeAudio: false,
    supportsFunctionCalling: model.supportsFunctionCalling ?? false,
    supportsStreaming: model.supportsStreaming ?? true,
    supportsVideoGeneration: model.supportsVideoGeneration ?? false,
    supportsEmbeddings: model.supportsEmbeddings ?? false,
    supportsChat: model.supportsChat ?? true,
    maxContextLength: model.maxInputTokens ?? null,
    maxOutputTokens: model.maxOutputTokens ?? null,
  };
}
