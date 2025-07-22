/**
 * Model capability definitions shared across Conduit SDK clients
 */

/**
 * Core model capabilities supported by Conduit
 */
export enum ModelCapability {
  CHAT = 'chat',
  VISION = 'vision',
  IMAGE_GENERATION = 'image-generation',
  IMAGE_EDIT = 'image-edit',
  IMAGE_VARIATION = 'image-variation',
  AUDIO_TRANSCRIPTION = 'audio-transcription',
  TEXT_TO_SPEECH = 'text-to-speech',
  REALTIME_AUDIO = 'realtime-audio',
  EMBEDDINGS = 'embeddings',
  VIDEO_GENERATION = 'video-generation',
}

/**
 * Model capability metadata
 */
export interface ModelCapabilityInfo {
  id: ModelCapability;
  displayName: string;
  description?: string;
  category: 'text' | 'vision' | 'audio' | 'video';
}

/**
 * Model capabilities definition for a specific model
 */
export interface ModelCapabilities {
  modelId: string;
  capabilities: ModelCapability[];
  constraints?: ModelConstraints;
}

/**
 * Model-specific constraints
 */
export interface ModelConstraints {
  maxTokens?: number;
  maxImages?: number;
  supportedImageSizes?: string[];
  supportedImageFormats?: string[];
  supportedAudioFormats?: string[];
  supportedVideoSizes?: string[];
  supportedLanguages?: string[];
  supportedVoices?: string[];
  maxDuration?: number;
}

/**
 * Get user-friendly display name for a capability
 */
export function getCapabilityDisplayName(capability: ModelCapability): string {
  switch (capability) {
    case ModelCapability.CHAT:
      return 'Chat Completion';
    case ModelCapability.VISION:
      return 'Vision (Image Understanding)';
    case ModelCapability.IMAGE_GENERATION:
      return 'Image Generation';
    case ModelCapability.IMAGE_EDIT:
      return 'Image Editing';
    case ModelCapability.IMAGE_VARIATION:
      return 'Image Variation';
    case ModelCapability.AUDIO_TRANSCRIPTION:
      return 'Audio Transcription';
    case ModelCapability.TEXT_TO_SPEECH:
      return 'Text-to-Speech';
    case ModelCapability.REALTIME_AUDIO:
      return 'Realtime Audio';
    case ModelCapability.EMBEDDINGS:
      return 'Embeddings';
    case ModelCapability.VIDEO_GENERATION:
      return 'Video Generation';
    default:
      return capability;
  }
}

/**
 * Get capability category
 */
export function getCapabilityCategory(capability: ModelCapability): 'text' | 'vision' | 'audio' | 'video' {
  switch (capability) {
    case ModelCapability.CHAT:
    case ModelCapability.EMBEDDINGS:
      return 'text';
    case ModelCapability.VISION:
    case ModelCapability.IMAGE_GENERATION:
    case ModelCapability.IMAGE_EDIT:
    case ModelCapability.IMAGE_VARIATION:
      return 'vision';
    case ModelCapability.AUDIO_TRANSCRIPTION:
    case ModelCapability.TEXT_TO_SPEECH:
    case ModelCapability.REALTIME_AUDIO:
      return 'audio';
    case ModelCapability.VIDEO_GENERATION:
      return 'video';
    default:
      return 'text';
  }
}