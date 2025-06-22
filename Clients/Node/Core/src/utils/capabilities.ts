import { IMAGE_MODEL_CAPABILITIES, type ImageModel } from '../models/images';

/**
 * Model capability types for Core client
 */
export enum CoreModelCapability {
  CHAT = 'chat',
  VISION = 'vision',
  IMAGE_GENERATION = 'image-generation',
  IMAGE_EDIT = 'image-edit',
  IMAGE_VARIATION = 'image-variation',
}

/**
 * Check if a model supports a specific capability
 * @param modelId The model identifier
 * @param capability The capability to check for
 * @returns True if the model supports the capability
 */
export function modelSupportsCapability(
  modelId: string,
  capability: CoreModelCapability
): boolean {
  // For image generation models, check specific capabilities
  if (modelId in IMAGE_MODEL_CAPABILITIES) {
    const imageCapabilities = IMAGE_MODEL_CAPABILITIES[modelId as ImageModel];
    switch (capability) {
      case CoreModelCapability.IMAGE_GENERATION:
        return true; // All models in IMAGE_MODEL_CAPABILITIES support generation
      case CoreModelCapability.IMAGE_EDIT:
        return imageCapabilities.supportsEdit;
      case CoreModelCapability.IMAGE_VARIATION:
        return imageCapabilities.supportsVariation;
      case CoreModelCapability.VISION:
      case CoreModelCapability.CHAT:
        return false; // Image generation models don't support chat/vision
      default:
        return false;
    }
  }

  // For other models, make reasonable assumptions based on model names
  const lowerModelId = modelId.toLowerCase();
  
  switch (capability) {
    case CoreModelCapability.CHAT:
      // Most models support chat unless they're specifically image models
      return !lowerModelId.includes('dall-e') && 
             !lowerModelId.includes('image') &&
             !lowerModelId.includes('stable-diffusion');
    
    case CoreModelCapability.VISION:
      // Vision models typically have 'vision' in the name or are GPT-4 variants
      return lowerModelId.includes('vision') ||
             lowerModelId.includes('gpt-4') ||
             lowerModelId.includes('claude-3');
    
    case CoreModelCapability.IMAGE_GENERATION:
      // Only specific image generation models
      return lowerModelId.includes('dall-e') ||
             lowerModelId.includes('image') ||
             lowerModelId.includes('stable-diffusion') ||
             lowerModelId.includes('minimax-image');
    
    default:
      return false;
  }
}

/**
 * Get all capabilities supported by a model
 * @param modelId The model identifier
 * @returns Array of supported capabilities
 */
export function getModelCapabilities(modelId: string): CoreModelCapability[] {
  const capabilities: CoreModelCapability[] = [];

  Object.values(CoreModelCapability).forEach(capability => {
    if (modelSupportsCapability(modelId, capability)) {
      capabilities.push(capability);
    }
  });

  return capabilities;
}

/**
 * Validate that a request is compatible with the specified model
 * @param modelId The model identifier
 * @param requestType The type of request being made
 * @returns Validation result with any errors
 */
export function validateModelCompatibility(
  modelId: string,
  requestType: 'chat' | 'image-generation' | 'image-edit' | 'image-variation'
): {
  isValid: boolean;
  errors: string[];
  suggestions?: string[];
} {
  const errors: string[] = [];
  const suggestions: string[] = [];

  const capabilityMap: Record<string, CoreModelCapability> = {
    'chat': CoreModelCapability.CHAT,
    'image-generation': CoreModelCapability.IMAGE_GENERATION,
    'image-edit': CoreModelCapability.IMAGE_EDIT,
    'image-variation': CoreModelCapability.IMAGE_VARIATION,
  };

  const requiredCapability = capabilityMap[requestType];
  
  if (!modelSupportsCapability(modelId, requiredCapability)) {
    errors.push(`Model '${modelId}' does not support ${requestType}`);
    
    // Provide suggestions based on request type
    switch (requestType) {
      case 'image-generation':
        suggestions.push('Try using models like: dall-e-3, dall-e-2, or minimax-image');
        break;
      case 'image-edit':
      case 'image-variation':
        suggestions.push('Try using dall-e-2 for image editing and variations');
        break;
      case 'chat':
        suggestions.push('Try using models like: gpt-4, gpt-3.5-turbo, or claude-3');
        break;
    }
  }

  return {
    isValid: errors.length === 0,
    errors,
    suggestions: suggestions.length > 0 ? suggestions : undefined,
  };
}

/**
 * Get optimal model recommendations for a specific capability
 * @param capability The desired capability
 * @param preferences Optional preferences for model selection
 * @returns Array of recommended model IDs, ordered by preference
 */
export function getRecommendedModels(
  capability: CoreModelCapability,
  preferences?: {
    prioritizeQuality?: boolean;
    prioritizeSpeed?: boolean;
    prioritizeCost?: boolean;
  }
): string[] {
  const { prioritizeQuality, prioritizeSpeed, prioritizeCost } = preferences || {};

  switch (capability) {
    case CoreModelCapability.CHAT:
      if (prioritizeQuality) {
        return ['gpt-4', 'claude-3-sonnet', 'gpt-3.5-turbo'];
      }
      if (prioritizeSpeed) {
        return ['gpt-3.5-turbo', 'gpt-4', 'claude-3-haiku'];
      }
      if (prioritizeCost) {
        return ['gpt-3.5-turbo', 'claude-3-haiku', 'gpt-4'];
      }
      return ['gpt-4', 'gpt-3.5-turbo', 'claude-3-sonnet'];

    case CoreModelCapability.VISION:
      if (prioritizeQuality) {
        return ['gpt-4-vision-preview', 'claude-3-sonnet', 'gpt-4'];
      }
      return ['gpt-4-vision-preview', 'claude-3-sonnet'];

    case CoreModelCapability.IMAGE_GENERATION:
      if (prioritizeQuality) {
        return ['dall-e-3', 'minimax-image', 'dall-e-2'];
      }
      if (prioritizeSpeed) {
        return ['dall-e-2', 'minimax-image', 'dall-e-3'];
      }
      if (prioritizeCost) {
        return ['dall-e-2', 'minimax-image', 'dall-e-3'];
      }
      return ['dall-e-3', 'dall-e-2', 'minimax-image'];

    case CoreModelCapability.IMAGE_EDIT:
    case CoreModelCapability.IMAGE_VARIATION:
      return ['dall-e-2']; // Currently only DALL-E 2 supports these

    default:
      return [];
  }
}

/**
 * Get user-friendly display name for a capability
 * @param capability The capability to get display name for
 * @returns Human-readable display name
 */
export function getCapabilityDisplayName(capability: CoreModelCapability): string {
  switch (capability) {
    case CoreModelCapability.CHAT:
      return 'Chat Completion';
    case CoreModelCapability.VISION:
      return 'Vision (Image Understanding)';
    case CoreModelCapability.IMAGE_GENERATION:
      return 'Image Generation';
    case CoreModelCapability.IMAGE_EDIT:
      return 'Image Editing';
    case CoreModelCapability.IMAGE_VARIATION:
      return 'Image Variation';
    default:
      return capability;
  }
}

/**
 * Check if two models are functionally equivalent for a given capability
 * @param modelA First model to compare
 * @param modelB Second model to compare
 * @param capability The capability to compare for
 * @returns True if models are equivalent for the capability
 */
export function areModelsEquivalent(
  modelA: string,
  modelB: string,
  capability: CoreModelCapability
): boolean {
  // Both must support the capability
  if (!modelSupportsCapability(modelA, capability) || 
      !modelSupportsCapability(modelB, capability)) {
    return false;
  }

  // For image generation, check if they have similar constraints
  if (capability === CoreModelCapability.IMAGE_GENERATION) {
    const capabilitiesA = IMAGE_MODEL_CAPABILITIES[modelA as ImageModel];
    const capabilitiesB = IMAGE_MODEL_CAPABILITIES[modelB as ImageModel];
    
    if (capabilitiesA && capabilitiesB) {
      return capabilitiesA.maxImages === capabilitiesB.maxImages &&
             JSON.stringify(capabilitiesA.supportedSizes) === JSON.stringify(capabilitiesB.supportedSizes);
    }
  }

  // For other capabilities, simple name-based equivalence
  const normalizeModel = (model: string) => 
    model.toLowerCase().replace(/[^a-z0-9]/g, '');
  
  return normalizeModel(modelA) === normalizeModel(modelB);
}