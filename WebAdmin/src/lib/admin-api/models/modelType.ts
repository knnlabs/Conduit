/**
 * Model Types and Related Business Logic
 * This file contains the canonical definitions for AI model types and their properties
 */

/** AI Model Type Enumeration */
export enum ModelType {
  Chat = 'chat',
  Embedding = 'embedding',
  Image = 'image',
  Audio = 'audio',
  Video = 'video'
}

/** Display configuration for model types */
export interface ModelTypeDisplayInfo {
  label: string;
  description: string;
  icon?: string;
  category: 'generation' | 'processing' | 'analysis';
}

/** Model type display information */
export const MODEL_TYPE_DISPLAY: Record<ModelType, ModelTypeDisplayInfo> = {
  [ModelType.Chat]: {
    label: 'Chat/Completion',
    description: 'Text generation and chat completion models',
    category: 'generation'
  },
  [ModelType.Embedding]: {
    label: 'Embedding',
    description: 'Text embedding and semantic similarity models',
    category: 'processing'
  },
  [ModelType.Image]: {
    label: 'Image Generation',
    description: 'Image creation and manipulation models',
    category: 'generation'
  },
  [ModelType.Audio]: {
    label: 'Audio (TTS/STT)',
    description: 'Text-to-speech and speech-to-text models',
    category: 'processing'
  },
  [ModelType.Video]: {
    label: 'Video Generation',
    description: 'Video creation and editing models',
    category: 'generation'
  }
};

/** Model type capabilities */
export interface ModelTypeCapabilities {
  supportsStreaming: boolean;
  supportsBatch: boolean;
  supportsRealtime: boolean;
  maxContextLength?: number;
  supportedFormats: string[];
}

/** Capabilities by model type */
export const MODEL_TYPE_CAPABILITIES: Record<ModelType, ModelTypeCapabilities> = {
  [ModelType.Chat]: {
    supportsStreaming: true,
    supportsBatch: true,
    supportsRealtime: true,
    supportedFormats: ['text', 'json']
  },
  [ModelType.Embedding]: {
    supportsStreaming: false,
    supportsBatch: true,
    supportsRealtime: false,
    supportedFormats: ['text']
  },
  [ModelType.Image]: {
    supportsStreaming: false,
    supportsBatch: true,
    supportsRealtime: false,
    supportedFormats: ['png', 'jpg', 'webp']
  },
  [ModelType.Audio]: {
    supportsStreaming: true,
    supportsBatch: true,
    supportsRealtime: true,
    supportedFormats: ['wav', 'mp3', 'flac', 'ogg']
  },
  [ModelType.Video]: {
    supportsStreaming: false,
    supportsBatch: false,
    supportsRealtime: false,
    supportedFormats: ['mp4', 'webm', 'avi']
  }
};

/** Utility functions for model types */
export const ModelTypeUtils = {
  /**
   * Get all model types as select options
   */
  getSelectOptions: () => {
    return Object.values(ModelType).map(type => ({
      value: type,
      label: MODEL_TYPE_DISPLAY[type].label,
      description: MODEL_TYPE_DISPLAY[type].description
    }));
  },

  /**
   * Get model types by category
   */
  getByCategory: (category: 'generation' | 'processing' | 'analysis') => {
    return Object.entries(MODEL_TYPE_DISPLAY)
      .filter(([, info]) => info.category === category)
      .map(([type]) => type as ModelType);
  },

  /**
   * Check if a model type supports a specific feature
   */
  supportsFeature: (modelType: ModelType, feature: keyof ModelTypeCapabilities) => {
    const capabilities = MODEL_TYPE_CAPABILITIES[modelType];
    return capabilities[feature] === true;
  },

  /**
   * Get display info for a model type
   */
  getDisplayInfo: (modelType: ModelType) => {
    return MODEL_TYPE_DISPLAY[modelType];
  },

  /**
   * Get capabilities for a model type
   */
  getCapabilities: (modelType: ModelType) => {
    return MODEL_TYPE_CAPABILITIES[modelType];
  },

  /**
   * Validate if a string is a valid model type
   */
  isValidModelType: (value: string): value is ModelType => {
    return Object.values(ModelType).includes(value as ModelType);
  }
};