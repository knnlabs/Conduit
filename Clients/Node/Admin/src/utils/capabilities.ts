import type { ModelProviderMappingDto } from '../models/modelMapping';

/**
 * Model capability types
 */
export enum ModelCapability {
  CHAT = 'chat',
  VISION = 'vision',
  IMAGE_GENERATION = 'image-generation',
  AUDIO_TRANSCRIPTION = 'audio-transcription',
  TEXT_TO_SPEECH = 'text-to-speech',
  REALTIME_AUDIO = 'realtime-audio',
}

/**
 * Check if a model mapping supports a specific capability
 * @param mapping The model mapping to check
 * @param capability The capability to check for
 * @returns True if the mapping supports the capability
 */
export function hasCapability(
  mapping: ModelProviderMappingDto,
  capability: ModelCapability
): boolean {
  switch (capability) {
    case ModelCapability.VISION:
      return mapping.supportsVision;
    case ModelCapability.IMAGE_GENERATION:
      return mapping.supportsImageGeneration;
    case ModelCapability.AUDIO_TRANSCRIPTION:
      return mapping.supportsAudioTranscription;
    case ModelCapability.TEXT_TO_SPEECH:
      return mapping.supportsTextToSpeech;
    case ModelCapability.REALTIME_AUDIO:
      return mapping.supportsRealtimeAudio;
    case ModelCapability.CHAT:
      // Chat is the base capability - if none of the special capabilities are set,
      // assume it's a chat model
      return !mapping.supportsImageGeneration && 
             !mapping.supportsAudioTranscription && 
             !mapping.supportsTextToSpeech;
    default:
      return false;
  }
}

/**
 * Get all capabilities supported by a model mapping
 * @param mapping The model mapping to analyze
 * @returns Array of supported capabilities
 */
export function getCapabilities(mapping: ModelProviderMappingDto): ModelCapability[] {
  const capabilities: ModelCapability[] = [];

  if (hasCapability(mapping, ModelCapability.CHAT)) {
    capabilities.push(ModelCapability.CHAT);
  }
  if (mapping.supportsVision) {
    capabilities.push(ModelCapability.VISION);
  }
  if (mapping.supportsImageGeneration) {
    capabilities.push(ModelCapability.IMAGE_GENERATION);
  }
  if (mapping.supportsAudioTranscription) {
    capabilities.push(ModelCapability.AUDIO_TRANSCRIPTION);
  }
  if (mapping.supportsTextToSpeech) {
    capabilities.push(ModelCapability.TEXT_TO_SPEECH);
  }
  if (mapping.supportsRealtimeAudio) {
    capabilities.push(ModelCapability.REALTIME_AUDIO);
  }

  return capabilities;
}

/**
 * Filter model mappings by capability
 * @param mappings Array of model mappings to filter
 * @param capability The capability to filter by
 * @returns Filtered array of mappings that support the capability
 */
export function filterByCapability(
  mappings: ModelProviderMappingDto[],
  capability: ModelCapability
): ModelProviderMappingDto[] {
  return mappings.filter(mapping => hasCapability(mapping, capability));
}

/**
 * Find the default mapping for a specific capability
 * @param mappings Array of model mappings to search
 * @param capability The capability to find default for
 * @returns The default mapping for the capability, or undefined if none found
 */
export function findDefaultMapping(
  mappings: ModelProviderMappingDto[],
  capability: ModelCapability
): ModelProviderMappingDto | undefined {
  return mappings.find(mapping => 
    mapping.isDefault && 
    mapping.defaultCapabilityType === capability &&
    hasCapability(mapping, capability)
  );
}

/**
 * Get the best mapping for a capability (default first, then highest priority enabled)
 * @param mappings Array of model mappings to search
 * @param capability The capability to find mapping for
 * @returns The best mapping for the capability, or undefined if none found
 */
export function getBestMapping(
  mappings: ModelProviderMappingDto[],
  capability: ModelCapability
): ModelProviderMappingDto | undefined {
  // First try to find default mapping
  const defaultMapping = findDefaultMapping(mappings, capability);
  if (defaultMapping && defaultMapping.isEnabled) {
    return defaultMapping;
  }

  // Filter by capability and enabled status, then sort by priority (lower = higher priority)
  const validMappings = mappings
    .filter(mapping => 
      mapping.isEnabled && 
      hasCapability(mapping, capability)
    )
    .sort((a, b) => a.priority - b.priority);

  return validMappings[0];
}

/**
 * Validate that a model mapping has all required fields for its capabilities
 * @param mapping The model mapping to validate
 * @returns Array of validation errors, empty if valid
 */
export function validateMappingCapabilities(mapping: ModelProviderMappingDto): string[] {
  const errors: string[] = [];

  // Validate image generation requirements
  if (mapping.supportsImageGeneration) {
    if (!mapping.providerModelId) {
      errors.push('Image generation models must have a provider model ID');
    }
  }

  // Validate audio requirements
  if (mapping.supportsAudioTranscription || mapping.supportsTextToSpeech) {
    if (mapping.supportsTextToSpeech && !mapping.supportedVoices) {
      errors.push('Text-to-speech models should specify supported voices');
    }
    if (!mapping.supportedLanguages) {
      errors.push('Audio models should specify supported languages');
    }
  }

  // Validate realtime audio requirements
  if (mapping.supportsRealtimeAudio) {
    if (!mapping.supportsAudioTranscription && !mapping.supportsTextToSpeech) {
      errors.push('Realtime audio models must support either transcription or text-to-speech');
    }
  }

  // Validate default capability type
  if (mapping.isDefault) {
    if (!mapping.defaultCapabilityType) {
      errors.push('Default mappings must specify a default capability type');
    } else {
      const capability = mapping.defaultCapabilityType as ModelCapability;
      if (!hasCapability(mapping, capability)) {
        errors.push(`Default capability type '${capability}' is not supported by this mapping`);
      }
    }
  }

  return errors;
}

/**
 * Get user-friendly display name for a capability
 * @param capability The capability to get display name for
 * @returns Human-readable display name
 */
export function getCapabilityDisplayName(capability: ModelCapability): string {
  switch (capability) {
    case ModelCapability.CHAT:
      return 'Chat Completion';
    case ModelCapability.VISION:
      return 'Vision (Image Understanding)';
    case ModelCapability.IMAGE_GENERATION:
      return 'Image Generation';
    case ModelCapability.AUDIO_TRANSCRIPTION:
      return 'Audio Transcription';
    case ModelCapability.TEXT_TO_SPEECH:
      return 'Text-to-Speech';
    case ModelCapability.REALTIME_AUDIO:
      return 'Realtime Audio';
    default:
      return capability;
  }
}