/**
 * Validation helper utilities
 * Convenience functions for common validation tasks
 */

import { MessageValidator } from './message-validator';
import { ImageValidator } from './image-validator';
import { ModelValidator, type ModelCapabilityProfile } from './model-validator';
import { SecurityValidator } from './security-validator';
import {
  ValidationSeverity,
  type ValidationResult,
  type ValidationError,
  type ValidationWarning,
  type EnhancedValidationError
} from './types';

/**
 * Validation helpers and utilities
 */
export class ValidationHelpers {
  /**
   * Quick message validation with default rules
   */
  static validateMessage(message: { content?: string }): ValidationResult {
    return MessageValidator.validate(message as Parameters<typeof MessageValidator.validate>[0]);
  }
  
  /**
   * Quick image validation for common constraints
   */
  static async validateImage(file: File | Blob): Promise<ValidationResult> {
    return ImageValidator.validateFile(file);
  }
  
  /**
   * Quick security scan with default options
   */
  static validateSecurity(content: string): ValidationResult {
    return SecurityValidator.validateSecurity(content);
  }
  
  /**
   * Quick model capability check
   */
  static checkModelCapability(modelId: string, capability: string): boolean {
    return ModelValidator.supportsCapability(modelId, capability as keyof ModelCapabilityProfile);
  }
  
  /**
   * Sanitize content with safe defaults
   */
  static sanitizeContent(content: string): string {
    return SecurityValidator.sanitizeContent(content, {
      removeScripts: true,
      escapeSpecialChars: true,
      trim: true
    });
  }
  
  /**
   * Validate complete chat message with all checks
   */
  static async validateChatMessage(
    message: {
      content?: string;
      images?: File[];
    },
    options: {
      modelId?: string;
      maxLength?: number;
      enableSecurity?: boolean;
    } = {}
  ): Promise<ValidationResult> {
    const errors: ValidationError[] = [];
    const warnings: ValidationWarning[] = [];
    
    // Validate message content
    if (message.content) {
      const messageResult = MessageValidator.validate(message as Parameters<typeof MessageValidator.validate>[0], {
        maxLength: options.maxLength,
        requireContent: true
      });
      
      if (!messageResult.valid) {
        errors.push(...(messageResult.errors ?? []));
      }
      warnings.push(...(messageResult.warnings ?? []));
      
      // Security validation
      if (options.enableSecurity !== false) {
        const securityResult = SecurityValidator.validateSecurity(message.content);
        if (!securityResult.valid) {
          errors.push(...(securityResult.errors ?? []));
        }
        warnings.push(...(securityResult.warnings ?? []));
      }
    }
    
    // Validate images
    if (message.images && message.images.length > 0) {
      for (let i = 0; i < message.images.length; i++) {
        const imageResult = await ImageValidator.validateFile(message.images[i]);
        if (!imageResult.valid) {
          const imageErrors = (imageResult.errors ?? []).map((error: ValidationError) => ({
            ...error,
            field: `images[${i}].${error.field ?? 'file'}`,
            path: `images.${i}`
          }));
          errors.push(...imageErrors);
        }
      }
    }
    
    // Model-specific validation
    if (options.modelId) {
      const modelResult = ModelValidator.validateModelConstraints(
        {
          text: message.content,
          images: message.images?.map(img => ({
            type: img.type,
            size: img.size
          }))
        },
        options.modelId
      );
      
      if (!modelResult.valid) {
        errors.push(...(modelResult.errors ?? []));
      }
      warnings.push(...(modelResult.warnings ?? []));
    }
    
    return {
      valid: errors.length === 0,
      errors,
      warnings,
      metadata: {
        validatedComponents: {
          message: !!message.content,
          images: (message.images?.length ?? 0) > 0,
          model: !!options.modelId,
          security: options.enableSecurity !== false
        }
      }
    };
  }
  
  /**
   * Get validation summary for UI display
   */
  static getValidationSummary(result: ValidationResult): {
    status: 'valid' | 'warning' | 'error';
    message: string;
    details?: string[];
  } {
    if (!result.errors?.length && !result.warnings?.length) {
      return { status: 'valid', message: 'Content is valid' };
    }
    
    if (result.errors?.length) {
      return {
        status: 'error',
        message: `${result.errors.length} validation error${result.errors.length > 1 ? 's' : ''}`,
        details: result.errors.map((e: ValidationError) => e.message)
      };
    }
    
    return {
      status: 'warning',
      message: `${result.warnings?.length ?? 0} validation warning${(result.warnings?.length ?? 0) > 1 ? 's' : ''}`,
      details: result.warnings?.map((w: ValidationWarning) => w.message)
    };
  }
  
  /**
   * Check if validation result should block submission
   */
  static shouldBlockSubmission(result: ValidationResult): boolean {
    if (!result.errors?.length) return false;
    
    return result.errors.some((error: ValidationError) => {
      const enhanced = error as EnhancedValidationError;
      return enhanced.severity === ValidationSeverity.CRITICAL || 
             enhanced.severity === ValidationSeverity.HIGH;
    });
  }
}