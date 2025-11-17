/**
 * Model identifier validation logic
 */

import { 
  ProviderTypeAssociationInput, 
  ValidationResult
} from '../types/models';
import { 
  normalizeProviderType, 
  isValidProviderType,
  getProviderConstraints,
  getProviderMetadata
} from '../types/providers';

/**
 * Validate provider type association input
 */
export function validateProviderTypeAssociation(
  input: Partial<ProviderTypeAssociationInput>
): ValidationResult<ProviderTypeAssociationInput> {
  const errors: Record<string, string> = {};
  const constraints = getProviderConstraints();
  
  // Validate identifier
  if (!input.identifier) {
    errors.identifier = 'Identifier is required';
  } else if (input.identifier.trim().length === 0) {
    errors.identifier = 'Identifier cannot be empty';
  } else if (input.identifier.length > 255) {
    errors.identifier = 'Identifier must be less than 255 characters';
  }
  
  // Validate and normalize provider
  if (input.provider !== undefined && input.provider !== null) {
    // Handle both numeric and string provider values
    const providerValue = typeof input.provider === 'number' 
      ? input.provider 
      : String(input.provider);
      
    if (!isValidProviderType(providerValue)) {
      errors.provider = `Invalid provider type: ${input.provider}`;
    } else {
      // Normalize the provider type
      const normalized = normalizeProviderType(providerValue);
      if (normalized) {
        // Keep as numeric value for API
        input.provider = normalized;
      }
    }
  }
  
  // Validate speed score
  if (input.speedScore !== null && input.speedScore !== undefined) {
    const score = input.speedScore;
    if (typeof score !== 'number' || isNaN(score)) {
      errors.speedScore = 'Speed score must be a number';
    } else if (score < constraints.speedScore.min || score > constraints.speedScore.max) {
      errors.speedScore = `Speed score must be between ${constraints.speedScore.min} and ${constraints.speedScore.max}`;
    }
    
    // Check if provider supports speed score
    if (input.provider) {
      const metadata = getProviderMetadata(input.provider);
      if (metadata && !metadata.supportsSpeedScore) {
        errors.speedScore = `Provider ${metadata.label} does not support speed scores`;
      }
    }
  }
  
  // Validate quality score
  if (input.qualityScore !== null && input.qualityScore !== undefined) {
    const score = input.qualityScore;
    if (typeof score !== 'number' || isNaN(score)) {
      errors.qualityScore = 'Quality score must be a number';
    } else if (score < constraints.qualityScore.min || score > constraints.qualityScore.max) {
      errors.qualityScore = `Quality score must be between ${constraints.qualityScore.min} and ${constraints.qualityScore.max}`;
    }
    
    // Check if provider supports quality score
    if (input.provider) {
      const metadata = getProviderMetadata(input.provider);
      if (metadata && !metadata.supportsQualityScore) {
        errors.qualityScore = `Provider ${metadata.label} does not support quality scores`;
      }
    }
  }
  
  // Validate max input tokens
  if (input.maxInputTokens !== null && input.maxInputTokens !== undefined) {
    const tokens = input.maxInputTokens;
    if (typeof tokens !== 'number' || isNaN(tokens)) {
      errors.maxInputTokens = 'Max input tokens must be a number';
    } else if (tokens < constraints.maxInputTokens.min) {
      errors.maxInputTokens = `Max input tokens must be at least ${constraints.maxInputTokens.min}`;
    } else if (constraints.maxInputTokens.max && tokens > constraints.maxInputTokens.max) {
      errors.maxInputTokens = `Max input tokens must be at most ${constraints.maxInputTokens.max}`;
    }
  }
  
  // Validate max output tokens
  if (input.maxOutputTokens !== null && input.maxOutputTokens !== undefined) {
    const tokens = input.maxOutputTokens;
    if (typeof tokens !== 'number' || isNaN(tokens)) {
      errors.maxOutputTokens = 'Max output tokens must be a number';
    } else if (tokens < constraints.maxOutputTokens.min) {
      errors.maxOutputTokens = `Max output tokens must be at least ${constraints.maxOutputTokens.min}`;
    } else if (constraints.maxOutputTokens.max && tokens > constraints.maxOutputTokens.max) {
      errors.maxOutputTokens = `Max output tokens must be at most ${constraints.maxOutputTokens.max}`;
    }
  }
  
  // Validate provider variation
  if (input.providerVariation) {
    // Check if provider supports variations
    if (input.provider) {
      const metadata = getProviderMetadata(input.provider);
      if (metadata && !metadata.supportsVariation) {
        errors.providerVariation = `Provider ${metadata.label} does not support variations`;
      }
    }
    
    if (input.providerVariation.length > 100) {
      errors.providerVariation = 'Provider variation must be less than 100 characters';
    }
  }
  
  // Return validation result
  if (Object.keys(errors).length > 0) {
    return {
      valid: false,
      errors
    };
  }
  
  return {
    valid: true,
    data: input as ProviderTypeAssociationInput
  };
}

/**
 * Check if an identifier/provider combination would be a duplicate
 * @param identifier The identifier to check
 * @param provider The provider type
 * @param existingAssociations Existing associations to check against
 * @param excludeId Optional ID to exclude from check (for updates)
 * @returns true if duplicate exists
 */
export function checkDuplicateIdentifier(
  identifier: string,
  provider: string,
  existingAssociations: Array<{ identifier: string; provider: string }>,
  excludeId?: number
): boolean {
  const normalizedProvider = normalizeProviderType(provider);
  if (!normalizedProvider) return false;
  
  return existingAssociations.some((assoc, index) => {
    // Skip if this is the association being edited
    if (excludeId !== undefined && index === excludeId) return false;
    
    const existingNormalized = normalizeProviderType(assoc.provider);
    return (
      assoc.identifier.toLowerCase() === identifier.toLowerCase() &&
      existingNormalized === normalizedProvider
    );
  });
}

/**
 * Check if there's already a primary identifier for a provider
 */
export function checkPrimaryConflict(
  provider: string,
  existingAssociations: Array<{ provider: string; isPrimary: boolean }>,
  excludeId?: number
): boolean {
  const normalizedProvider = normalizeProviderType(provider);
  if (!normalizedProvider) return false;
  
  return existingAssociations.some((assoc, index) => {
    // Skip if this is the association being edited
    if (excludeId !== undefined && index === excludeId) return false;
    
    const existingNormalized = normalizeProviderType(assoc.provider);
    return existingNormalized === normalizedProvider && assoc.isPrimary;
  });
}

/**
 * Apply default values based on provider metadata
 */
export function applyProviderDefaults(
  input: ProviderTypeAssociationInput
): ProviderTypeAssociationInput {
  if (!input.provider) return input;
  
  const metadata = getProviderMetadata(input.provider);
  if (!metadata) return input;
  
  const result = { ...input };
  
  // Apply default token limits if not specified
  if (result.maxInputTokens === undefined && metadata.defaultMaxInputTokens) {
    result.maxInputTokens = metadata.defaultMaxInputTokens;
  }
  
  if (result.maxOutputTokens === undefined && metadata.defaultMaxOutputTokens) {
    result.maxOutputTokens = metadata.defaultMaxOutputTokens;
  }
  
  // Clear unsupported fields
  if (!metadata.supportsSpeedScore) {
    result.speedScore = null;
  }
  
  if (!metadata.supportsQualityScore) {
    result.qualityScore = null;
  }
  
  if (!metadata.supportsVariation) {
    result.providerVariation = null;
  }
  
  return result;
}