/**
 * Model capability constants for the WebUI
 * Re-exports types from the Admin SDK and adds UI-specific configurations
 */

import { ModelCapability, getCapabilityDisplayName } from '@knn_labs/conduit-admin-client';

// Re-export the enum and display function from SDK
export { ModelCapability, getCapabilityDisplayName };

// UI-specific capability metadata
export interface CapabilityInfo {
  value: ModelCapability;
  label: string;
  description: string;
  icon?: string;
  color?: string;
}

// Descriptions for each capability
export const CAPABILITY_DESCRIPTIONS: Record<ModelCapability, string> = {
  [ModelCapability.CHAT]: 'Supports text-based chat conversations',
  [ModelCapability.VISION]: 'Can analyze and understand images',
  [ModelCapability.IMAGE_GENERATION]: 'Can generate images from text prompts',
  [ModelCapability.AUDIO_TRANSCRIPTION]: 'Can transcribe audio to text',
  [ModelCapability.TEXT_TO_SPEECH]: 'Can convert text to spoken audio',
  [ModelCapability.REALTIME_AUDIO]: 'Supports real-time audio streaming',
};

// Icons for capabilities (using Tabler icons)
export const CAPABILITY_ICONS: Record<ModelCapability, string> = {
  [ModelCapability.CHAT]: 'message-circle',
  [ModelCapability.VISION]: 'eye',
  [ModelCapability.IMAGE_GENERATION]: 'photo',
  [ModelCapability.AUDIO_TRANSCRIPTION]: 'microphone',
  [ModelCapability.TEXT_TO_SPEECH]: 'volume',
  [ModelCapability.REALTIME_AUDIO]: 'broadcast',
};

// Colors for capability badges
export const CAPABILITY_COLORS: Record<ModelCapability, string> = {
  [ModelCapability.CHAT]: 'blue',
  [ModelCapability.VISION]: 'purple',
  [ModelCapability.IMAGE_GENERATION]: 'green',
  [ModelCapability.AUDIO_TRANSCRIPTION]: 'orange',
  [ModelCapability.TEXT_TO_SPEECH]: 'cyan',
  [ModelCapability.REALTIME_AUDIO]: 'red',
};

// Get all capabilities as an array
export const ALL_CAPABILITIES = Object.values(ModelCapability);

// Helper to check if a value is a valid capability
export const isValidCapability = (value: string): value is ModelCapability => {
  return ALL_CAPABILITIES.includes(value as ModelCapability);
};

// Get complete capability info for UI components
export const getCapabilityInfo = (capability: ModelCapability): CapabilityInfo => {
  return {
    value: capability,
    label: getCapabilityDisplayName(capability),
    description: CAPABILITY_DESCRIPTIONS[capability],
    icon: CAPABILITY_ICONS[capability],
    color: CAPABILITY_COLORS[capability],
  };
};

// Get all capabilities as select options
export const getAllCapabilityInfos = (): CapabilityInfo[] => {
  return ALL_CAPABILITIES.map(getCapabilityInfo);
};

// Legacy capability mappings (for backward compatibility)
// These map to the capabilities used in CreateModelMappingModal
export const LEGACY_CAPABILITY_MAP: Record<string, ModelCapability> = {
  'vision': ModelCapability.VISION,
  'function_calling': ModelCapability.CHAT, // Function calling is part of chat
  'streaming': ModelCapability.CHAT, // Streaming is part of chat
  'image_generation': ModelCapability.IMAGE_GENERATION,
  'audio_transcription': ModelCapability.AUDIO_TRANSCRIPTION,
  'text_to_speech': ModelCapability.TEXT_TO_SPEECH,
  'realtime_audio': ModelCapability.REALTIME_AUDIO,
};

// Convert legacy capability array to ModelCapability array
export const convertLegacyCapabilities = (legacyCapabilities: string[]): ModelCapability[] => {
  const capabilitySet = new Set<ModelCapability>();
  
  legacyCapabilities.forEach(legacy => {
    const capability = LEGACY_CAPABILITY_MAP[legacy];
    if (capability) {
      capabilitySet.add(capability);
    }
  });
  
  return Array.from(capabilitySet);
};

// Get capability options for MultiSelect component
export const getCapabilitySelectOptions = () => {
  return getAllCapabilityInfos().map(info => ({
    value: info.value,
    label: info.label,
    description: info.description,
  }));
};