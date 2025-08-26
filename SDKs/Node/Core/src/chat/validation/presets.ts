/**
 * Validation presets for common use cases
 * Pre-configured validation rules for different scenarios
 */

import type { ValidationRules } from './types';

/**
 * Validation presets for common use cases
 */
export class ValidationPresets {
  /**
   * Strict validation for production environments
   */
  static readonly STRICT: ValidationRules = {
    maxLength: 16000,
    minLength: 1,
    requireContent: true,
    maxImages: 5,
    maxFileSize: 10 * 1024 * 1024, // 10MB
    allowedFormats: ['image/jpeg', 'image/png', 'image/webp'],
    security: {
      checkXSS: true,
      checkSQLInjection: true,
      checkScriptTags: true,
      checkDangerousUrls: true,
      validateJson: true,
      maxJsonDepth: 5,
      checkPathTraversal: true
    }
  };
  
  /**
   * Permissive validation for development
   */
  static readonly PERMISSIVE: ValidationRules = {
    maxLength: 32000,
    requireContent: false,
    maxImages: 20,
    maxFileSize: 20 * 1024 * 1024, // 20MB
    allowedFormats: ['image/jpeg', 'image/png', 'image/gif', 'image/webp', 'image/bmp'],
    security: {
      checkXSS: false,
      checkSQLInjection: false,
      checkScriptTags: true,
      checkDangerousUrls: false,
      validateJson: false
    }
  };
  
  /**
   * Balanced validation for typical use cases
   */
  static readonly BALANCED: ValidationRules = {
    maxLength: 24000,
    minLength: 1,
    requireContent: true,
    maxImages: 10,
    maxFileSize: 15 * 1024 * 1024, // 15MB
    allowedFormats: ['image/jpeg', 'image/png', 'image/gif', 'image/webp'],
    security: {
      checkXSS: true,
      checkSQLInjection: true,
      checkScriptTags: true,
      checkDangerousUrls: true,
      validateJson: true,
      maxJsonDepth: 8
    }
  };
}