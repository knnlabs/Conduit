/**
 * Model-specific validation utilities for chat validation
 * Validates content against model capabilities and constraints
 */

import {
  ValidationSeverity,
  ValidationCategory,
  ValidationConstants,
  type ValidationResult,
  type ModelConstraints,
  type EnhancedValidationError,
  type ImageConstraints
} from './types';

/**
 * Available model families for validation
 */
export enum ModelFamily {
  OPENAI = 'openai',
  ANTHROPIC = 'anthropic',
  GROQ = 'groq',
  REPLICATE = 'replicate',
  GOOGLE = 'google',
  COHERE = 'cohere',
  META = 'meta',
  MISTRAL = 'mistral',
  UNKNOWN = 'unknown'
}

/**
 * Model-specific validation capabilities and constraints
 */
export interface ModelCapabilityProfile {
  /** Model identifier */
  model: string;
  /** Model family */
  family: ModelFamily;
  /** Maximum context length in tokens */
  maxTokens?: number;
  /** Supports vision/image inputs */
  supportsVision?: boolean;
  /** Supports function calling */
  supportsFunctions?: boolean;
  /** Supports JSON mode */
  supportsJsonMode?: boolean;
  /** Supports streaming */
  supportsStreaming?: boolean;
  /** Supported image formats */
  supportedImageFormats?: string[];
  /** Maximum image dimensions */
  maxImageDimensions?: { width: number; height: number };
  /** Maximum image file size */
  maxImageSize?: number;
  /** Maximum number of images per message */
  maxImagesPerMessage?: number;
  /** Additional constraints */
  customConstraints?: Record<string, unknown>;
}

/**
 * Predefined model capability profiles
 */
export const MODEL_PROFILES: Record<string, ModelCapabilityProfile> = {
  // OpenAI Models
  'gpt-4-vision-preview': {
    model: 'gpt-4-vision-preview',
    family: ModelFamily.OPENAI,
    maxTokens: 128000,
    supportsVision: true,
    supportsFunctions: true,
    supportsJsonMode: true,
    supportsStreaming: true,
    supportedImageFormats: ['image/jpeg', 'image/png', 'image/gif', 'image/webp'],
    maxImageDimensions: { width: 2048, height: 2048 },
    maxImageSize: 20 * 1024 * 1024, // 20MB
    maxImagesPerMessage: 10
  },
  'gpt-4o': {
    model: 'gpt-4o',
    family: ModelFamily.OPENAI,
    maxTokens: 128000,
    supportsVision: true,
    supportsFunctions: true,
    supportsJsonMode: true,
    supportsStreaming: true,
    supportedImageFormats: ['image/jpeg', 'image/png', 'image/gif', 'image/webp'],
    maxImageDimensions: { width: 2048, height: 2048 },
    maxImageSize: 20 * 1024 * 1024,
    maxImagesPerMessage: 10
  },
  'gpt-4-turbo': {
    model: 'gpt-4-turbo',
    family: ModelFamily.OPENAI,
    maxTokens: 128000,
    supportsVision: true,
    supportsFunctions: true,
    supportsJsonMode: true,
    supportsStreaming: true,
    supportedImageFormats: ['image/jpeg', 'image/png', 'image/gif', 'image/webp'],
    maxImageDimensions: { width: 2048, height: 2048 },
    maxImageSize: 20 * 1024 * 1024,
    maxImagesPerMessage: 10
  },
  'gpt-3.5-turbo': {
    model: 'gpt-3.5-turbo',
    family: ModelFamily.OPENAI,
    maxTokens: 16384,
    supportsVision: false,
    supportsFunctions: true,
    supportsJsonMode: true,
    supportsStreaming: true,
    supportedImageFormats: [],
    maxImagesPerMessage: 0
  },
  
  // Anthropic Models
  'claude-3-opus-20240229': {
    model: 'claude-3-opus-20240229',
    family: ModelFamily.ANTHROPIC,
    maxTokens: 200000,
    supportsVision: true,
    supportsFunctions: true,
    supportsJsonMode: false,
    supportsStreaming: true,
    supportedImageFormats: ['image/jpeg', 'image/png', 'image/gif', 'image/webp'],
    maxImageDimensions: { width: 8000, height: 8000 },
    maxImageSize: 5 * 1024 * 1024, // 5MB
    maxImagesPerMessage: 20
  },
  'claude-3-sonnet-20240229': {
    model: 'claude-3-sonnet-20240229',
    family: ModelFamily.ANTHROPIC,
    maxTokens: 200000,
    supportsVision: true,
    supportsFunctions: true,
    supportsJsonMode: false,
    supportsStreaming: true,
    supportedImageFormats: ['image/jpeg', 'image/png', 'image/gif', 'image/webp'],
    maxImageDimensions: { width: 8000, height: 8000 },
    maxImageSize: 5 * 1024 * 1024,
    maxImagesPerMessage: 20
  },
  'claude-3-haiku-20240307': {
    model: 'claude-3-haiku-20240307',
    family: ModelFamily.ANTHROPIC,
    maxTokens: 200000,
    supportsVision: true,
    supportsFunctions: true,
    supportsJsonMode: false,
    supportsStreaming: true,
    supportedImageFormats: ['image/jpeg', 'image/png', 'image/gif', 'image/webp'],
    maxImageDimensions: { width: 8000, height: 8000 },
    maxImageSize: 5 * 1024 * 1024,
    maxImagesPerMessage: 20
  },
  
  // Groq Models (text-only, high-speed)
  'llama-3.1-70b-versatile': {
    model: 'llama-3.1-70b-versatile',
    family: ModelFamily.GROQ,
    maxTokens: 131072,
    supportsVision: false,
    supportsFunctions: true,
    supportsJsonMode: true,
    supportsStreaming: true,
    supportedImageFormats: [],
    maxImagesPerMessage: 0
  },
  'mixtral-8x7b-32768': {
    model: 'mixtral-8x7b-32768',
    family: ModelFamily.GROQ,
    maxTokens: 32768,
    supportsVision: false,
    supportsFunctions: true,
    supportsJsonMode: true,
    supportsStreaming: true,
    supportedImageFormats: [],
    maxImagesPerMessage: 0
  },
  
  // Google Models
  'gemini-1.5-pro': {
    model: 'gemini-1.5-pro',
    family: ModelFamily.GOOGLE,
    maxTokens: 2000000,
    supportsVision: true,
    supportsFunctions: true,
    supportsJsonMode: true,
    supportsStreaming: true,
    supportedImageFormats: ['image/jpeg', 'image/png', 'image/webp'],
    maxImageDimensions: { width: 3072, height: 3072 },
    maxImageSize: 20 * 1024 * 1024,
    maxImagesPerMessage: 16
  }
};

/**
 * Model-specific validation utilities
 */
export class ModelValidator {
  /**
   * Validate content against model capabilities
   */
  static validateModelConstraints(
    content: {
      text?: string;
      images?: Array<{ type: string; size?: number; width?: number; height?: number }>;
      functions?: unknown[];
      jsonMode?: boolean;
    },
    modelId: string,
    customConstraints?: ModelConstraints
  ): ValidationResult {
    const errors: EnhancedValidationError[] = [];
    const warnings: EnhancedValidationError[] = [];
    
    // Get model profile
    const profile = this.getModelProfile(modelId);
    const baseConstraints = profile ?? {};
    const constraints = { ...baseConstraints, ...customConstraints };
    
    if (!profile) {
      errors.push({
        code: 'UNKNOWN_MODEL',
        message: `Unknown model: ${modelId}. Using default constraints.`,
        severity: ValidationSeverity.MEDIUM,
        category: ValidationCategory.MODEL,
        field: 'model',
        value: modelId,
        suggestion: 'Use a known model identifier or provide custom constraints'
      });
    }
    
    // Validate token limits (approximate)
    if (content.text && constraints.maxTokens) {
      const estimatedTokens = this.estimateTokenCount(content.text);
      if (estimatedTokens > constraints.maxTokens) {
        errors.push({
          code: 'CONTENT_TOO_LONG',
          message: `Estimated token count (${estimatedTokens}) exceeds model limit (${constraints.maxTokens})`,
          severity: ValidationSeverity.HIGH,
          category: ValidationCategory.MODEL,
          field: 'content',
          value: estimatedTokens,
          suggestion: `Reduce content length to under ${constraints.maxTokens} tokens`
        });
      }
    }
    
    // Validate vision capabilities
    if (content.images && content.images.length > 0) {
      if (!constraints.supportsVision) {
        errors.push({
          code: 'VISION_NOT_SUPPORTED',
          message: `Model ${modelId} does not support vision/image inputs`,
          severity: ValidationSeverity.HIGH,
          category: ValidationCategory.MODEL,
          field: 'images',
          value: content.images.length,
          suggestion: 'Use a vision-capable model or remove images'
        });
      } else {
        // Validate image constraints
        this.validateImageConstraints(content.images, constraints, errors);
      }
    }
    
    // Validate function calling
    if (content.functions && content.functions.length > 0 && !constraints.supportsFunctions) {
      errors.push({
        code: 'FUNCTIONS_NOT_SUPPORTED',
        message: `Model ${modelId} does not support function calling`,
        severity: ValidationSeverity.HIGH,
        category: ValidationCategory.MODEL,
        field: 'functions',
        value: content.functions.length,
        suggestion: 'Use a function-capable model or remove function calls'
      });
    }
    
    // Validate JSON mode
    if (content.jsonMode && !constraints.supportsJsonMode) {
      errors.push({
        code: 'JSON_MODE_NOT_SUPPORTED',
        message: `Model ${modelId} does not support JSON mode`,
        severity: ValidationSeverity.MEDIUM,
        category: ValidationCategory.MODEL,
        field: 'jsonMode',
        value: content.jsonMode,
        suggestion: 'Use a JSON-capable model or disable JSON mode'
      });
    }
    
    return {
      valid: errors.length === 0,
      errors,
      warnings,
      metadata: {
        modelProfile: profile,
        estimatedTokens: content.text ? this.estimateTokenCount(content.text) : 0
      }
    };
  }
  
  /**
   * Get model capability profile
   */
  static getModelProfile(modelId: string): ModelCapabilityProfile | null {
    // Direct match
    if (MODEL_PROFILES[modelId]) {
      return MODEL_PROFILES[modelId];
    }
    
    // Fuzzy match for versioned models
    const baseModel = modelId.split(':')[0]; // Handle model:version format
    if (MODEL_PROFILES[baseModel]) {
      return MODEL_PROFILES[baseModel];
    }
    
    // Pattern matching for common model families
    if (modelId.includes('gpt-4') && modelId.includes('vision')) {
      return MODEL_PROFILES['gpt-4-vision-preview'];
    }
    
    if (modelId.includes('gpt-4o') || modelId.includes('gpt-4-turbo')) {
      return MODEL_PROFILES['gpt-4o'];
    }
    
    if (modelId.includes('gpt-3.5')) {
      return MODEL_PROFILES['gpt-3.5-turbo'];
    }
    
    if (modelId.includes('claude-3-opus')) {
      return MODEL_PROFILES['claude-3-opus-20240229'];
    }
    
    if (modelId.includes('claude-3-sonnet')) {
      return MODEL_PROFILES['claude-3-sonnet-20240229'];
    }
    
    if (modelId.includes('claude-3-haiku')) {
      return MODEL_PROFILES['claude-3-haiku-20240307'];
    }
    
    if (modelId.includes('llama-3') || modelId.includes('llama3')) {
      return MODEL_PROFILES['llama-3.1-70b-versatile'];
    }
    
    if (modelId.includes('mixtral')) {
      return MODEL_PROFILES['mixtral-8x7b-32768'];
    }
    
    if (modelId.includes('gemini')) {
      return MODEL_PROFILES['gemini-1.5-pro'];
    }
    
    return null;
  }
  
  /**
   * Validate image constraints for model
   */
  private static validateImageConstraints(
    images: Array<{ type: string; size?: number; width?: number; height?: number }>,
    constraints: Partial<ModelCapabilityProfile>,
    errors: EnhancedValidationError[]
  ): void {
    // Check image count
    if (constraints.maxImagesPerMessage && images.length > constraints.maxImagesPerMessage) {
      errors.push({
        code: 'TOO_MANY_IMAGES',
        message: `Too many images (${images.length}). Model supports max ${constraints.maxImagesPerMessage}`,
        severity: ValidationSeverity.HIGH,
        category: ValidationCategory.MODEL,
        field: 'images',
        value: images.length,
        suggestion: `Reduce to ${constraints.maxImagesPerMessage} or fewer images`
      });
    }
    
    // Check each image
    images.forEach((image, index) => {
      // Check format support
      if (constraints.supportedImageFormats && !constraints.supportedImageFormats.includes(image.type)) {
        errors.push({
          code: 'UNSUPPORTED_IMAGE_FORMAT',
          message: `Unsupported image format: ${image.type}`,
          severity: ValidationSeverity.HIGH,
          category: ValidationCategory.MODEL,
          field: `images[${index}].type`,
          value: image.type,
          suggestion: `Use supported formats: ${constraints.supportedImageFormats.join(', ')}`
        });
      }
      
      // Check file size
      if (image.size && constraints.maxImageSize && image.size > constraints.maxImageSize) {
        const sizeMB = (image.size / (1024 * 1024)).toFixed(1);
        const maxSizeMB = (constraints.maxImageSize / (1024 * 1024)).toFixed(1);
        
        errors.push({
          code: 'IMAGE_TOO_LARGE',
          message: `Image ${index + 1} is too large (${sizeMB}MB). Max size: ${maxSizeMB}MB`,
          severity: ValidationSeverity.HIGH,
          category: ValidationCategory.MODEL,
          field: `images[${index}].size`,
          value: image.size,
          suggestion: `Compress image to under ${maxSizeMB}MB`
        });
      }
      
      // Check dimensions
      if (image.width && image.height && constraints.maxImageDimensions) {
        const { width: maxWidth, height: maxHeight } = constraints.maxImageDimensions;
        
        if (image.width > maxWidth || image.height > maxHeight) {
          errors.push({
            code: 'IMAGE_DIMENSIONS_TOO_LARGE',
            message: `Image ${index + 1} dimensions (${image.width}x${image.height}) exceed limit (${maxWidth}x${maxHeight})`,
            severity: ValidationSeverity.MEDIUM,
            category: ValidationCategory.MODEL,
            field: `images[${index}].dimensions`,
            value: `${image.width}x${image.height}`,
            suggestion: `Resize to max ${maxWidth}x${maxHeight} pixels`
          });
        }
      }
    });
  }
  
  /**
   * Estimate token count for text content
   * This is a rough approximation - actual tokenization varies by model
   */
  static estimateTokenCount(text: string): number {
    if (!text || typeof text !== 'string') return 0;
    
    // Rough approximation: ~4 characters per token for English text
    // This varies significantly by model and language
    const baseCount = Math.ceil(text.length / 4);
    
    // Account for special tokens and formatting
    const specialTokens = (text.match(/\n|\t|[^\w\s]/g) ?? []).length;
    
    return baseCount + Math.ceil(specialTokens * 0.5);
  }
  
  /**
   * Get image constraints for model
   */
  static getImageConstraints(modelId: string): ImageConstraints {
    const profile = this.getModelProfile(modelId);
    
    if (!profile?.supportsVision) {
      return {
        maxFileSize: 0,
        allowedFormats: [],
        maxDimensions: { width: 0, height: 0 }
      };
    }
    
    return {
      maxFileSize: profile.maxImageSize ?? ValidationConstants.FILE_LIMITS.MAX_IMAGE_SIZE,
      allowedFormats: profile.supportedImageFormats ?? [],
      maxDimensions: profile.maxImageDimensions ?? { 
        width: ValidationConstants.IMAGE_DIMENSIONS.MAX_WIDTH, 
        height: ValidationConstants.IMAGE_DIMENSIONS.MAX_HEIGHT 
      },
      security: {
        verifyFileSignature: true,
        checkMaliciousMetadata: true,
        scanHiddenData: true
      }
    };
  }
  
  /**
   * Check if model supports specific capability
   */
  static supportsCapability(modelId: string, capability: keyof ModelCapabilityProfile): boolean {
    const profile = this.getModelProfile(modelId);
    if (!profile) return false;
    
    return Boolean(profile[capability]);
  }
  
  /**
   * Get all supported models for a capability
   */
  static getModelsWithCapability(capability: keyof ModelCapabilityProfile): string[] {
    return Object.entries(MODEL_PROFILES)
      .filter(([, profile]) => Boolean(profile[capability]))
      .map(([modelId]) => modelId);
  }
  
  /**
   * Register custom model profile
   */
  static registerModel(modelId: string, profile: ModelCapabilityProfile): void {
    MODEL_PROFILES[modelId] = profile;
  }
  
  /**
   * Get model family from model ID
   */
  static getModelFamily(modelId: string): ModelFamily {
    const profile = this.getModelProfile(modelId);
    if (profile) return profile.family;
    
    // Fallback pattern matching
    if (modelId.includes('gpt')) return ModelFamily.OPENAI;
    if (modelId.includes('claude')) return ModelFamily.ANTHROPIC;
    if (modelId.includes('llama')) return ModelFamily.META;
    if (modelId.includes('mixtral') || modelId.includes('mistral')) return ModelFamily.MISTRAL;
    if (modelId.includes('gemini')) return ModelFamily.GOOGLE;
    
    return ModelFamily.UNKNOWN;
  }
}