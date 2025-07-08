export { ConduitProvider, useConduit } from './ConduitProvider';
export { conduitQueryKeys } from './queryKeys';
export * from './hooks';

// Re-export types from the main SDK for convenience
export type {
  ChatCompletionMessage,
  ChatCompletionRequest,
  ChatCompletionResponse,
} from '../models/chat';

export type {
  Model,
  ModelsResponse,
} from '../models/models';

export type {
  ImageGenerationRequest,
  ImageGenerationResponse,
} from '../models/images';

export type {
  AudioTranscriptionRequest,
  AudioTranscriptionResponse,
  AudioTranslationRequest,
  AudioTranslationResponse,
  TextToSpeechRequest as AudioSpeechRequest,
} from '../models/audio';

export type {
  AsyncVideoGenerationRequest,
  AsyncVideoGenerationResponse,
} from '../models/videos';

export type {
  EmbeddingRequest,
  EmbeddingResponse,
} from '../models/embeddings';