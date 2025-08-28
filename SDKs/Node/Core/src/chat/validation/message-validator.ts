/**
 * Message validation utilities for chat messages
 * Framework-agnostic message content validation and sanitization
 */

import {
  ValidationConstants,
  ValidationSeverity,
  ValidationCategory,
  type ValidationResult,
  type ValidationWarning,
  type ValidationRules,
  type ModelConstraints,
  type SecurityValidationOptions,
  type SanitizeOptions,
  type EnhancedValidationError
} from './types';

/**
 * Message content types (similar to SDK message content)
 */
export type MessageContent = string | Array<{ type: string; [key: string]: unknown }>;

/**
 * Validatable message interface
 */
export interface ValidatableMessage {
  id?: string;
  role?: 'user' | 'assistant' | 'system' | 'function';
  content: MessageContent;
  images?: Array<{
    url?: string;
    width?: number;
    height?: number;
    size?: number;
    mimeType?: string;
  }>;
  functionCall?: {
    name: string;
    arguments: string;
  };
  toolCalls?: Array<{
    id: string;
    type: 'function';
    function: { name: string; arguments: string };
  }>;
}

/**
 * Message validation utilities
 */
export class MessageValidator {
  /**
   * Validate a complete chat message
   * @param message Message to validate
   * @param rules Validation rules
   * @returns Validation result
   */
  static validate(
    message: ValidatableMessage,
    rules: ValidationRules = {}
  ): ValidationResult {
    const errors: EnhancedValidationError[] = [];
    const warnings: ValidationWarning[] = [];

    // Validate message structure
    const structureResult = this.validateStructure(message);
    if (structureResult.errors) {
      errors.push(...(structureResult.errors as EnhancedValidationError[]));
    }
    if (structureResult.warnings) {
      warnings.push(...structureResult.warnings);
    }

    // Validate content
    const contentResult = this.validateContent(message.content, rules);
    if (contentResult.errors) {
      errors.push(...(contentResult.errors as EnhancedValidationError[]));
    }
    if (contentResult.warnings) {
      warnings.push(...contentResult.warnings);
    }

    // Validate images if present
    if (message.images && message.images.length > 0) {
      const imageResult = this.validateMessageImages(message.images, rules);
      if (imageResult.errors) {
        errors.push(...(imageResult.errors as EnhancedValidationError[]));
      }
      if (imageResult.warnings) {
        warnings.push(...imageResult.warnings);
      }
    }

    // Validate against model constraints
    if (rules.modelConstraints) {
      const modelResult = this.validateModelConstraints(message, rules.modelConstraints);
      if (modelResult.errors) {
        errors.push(...(modelResult.errors as EnhancedValidationError[]));
      }
      if (modelResult.warnings) {
        warnings.push(...modelResult.warnings);
      }
    }

    // Run custom validation rules
    if (rules.customRules && rules.customRules.length > 0) {
      for (const rule of rules.customRules) {
        try {
          const ruleResult = rule.validate(message, { rules, field: 'message' });
          if (ruleResult.errors) {
            errors.push(...(ruleResult.errors as EnhancedValidationError[]));
          }
          if (ruleResult.warnings) {
            warnings.push(...ruleResult.warnings);
          }
        } catch (error) {
          errors.push({
            code: 'CUSTOM_RULE_ERROR',
            message: `Custom rule '${rule.name}' failed: ${error instanceof Error ? error.message : String(error)}`,
            field: 'customRules',
            severity: ValidationSeverity.MEDIUM,
            category: ValidationCategory.CUSTOM
          });
        }
      }
    }

    return {
      valid: errors.length === 0,
      errors: errors.length > 0 ? errors : undefined,
      warnings: warnings.length > 0 ? warnings : undefined,
      metadata: {
        messageLength: this.getContentLength(message.content),
        imageCount: message.images?.length ?? 0,
        hasFunction: Boolean(message.functionCall ?? (message.toolCalls && message.toolCalls.length > 0))
      }
    };
  }

  /**
   * Validate message content
   * @param content Content to validate
   * @param rules Validation rules
   * @returns Validation result
   */
  static validateContent(
    content: MessageContent,
    rules: ValidationRules = {}
  ): ValidationResult {
    const errors: EnhancedValidationError[] = [];
    const warnings: ValidationWarning[] = [];
    const contentLength = this.getContentLength(content);

    // Length validation
    const maxLength = rules.maxLength ?? ValidationConstants.MESSAGE_LIMITS.MAX_LENGTH;
    const minLength = rules.minLength ?? ValidationConstants.MESSAGE_LIMITS.MIN_LENGTH;

    if (contentLength > maxLength) {
      errors.push({
        code: 'CONTENT_TOO_LONG',
        message: `Content exceeds maximum length of ${maxLength} characters (current: ${contentLength})`,
        field: 'content',
        value: contentLength,
        suggestion: `Reduce content length by ${contentLength - maxLength} characters`,
        severity: ValidationSeverity.HIGH,
        category: ValidationCategory.CONTENT
      });
    }

    if (contentLength < minLength) {
      errors.push({
        code: 'CONTENT_TOO_SHORT',
        message: `Content is below minimum length of ${minLength} characters (current: ${contentLength})`,
        field: 'content',
        value: contentLength,
        suggestion: `Add at least ${minLength - contentLength} more characters`,
        severity: ValidationSeverity.MEDIUM,
        category: ValidationCategory.CONTENT
      });
    }

    // Required content validation
    if (rules.requireContent && contentLength === 0) {
      errors.push({
        code: 'CONTENT_REQUIRED',
        message: 'Content is required but is empty',
        field: 'content',
        value: content,
        severity: ValidationSeverity.HIGH,
        category: ValidationCategory.CONTENT
      });
    }

    // Security validation
    if (rules.security) {
      const securityResult = this.validateSecurity(content, rules.security);
      if (securityResult.errors) {
        errors.push(...(securityResult.errors as EnhancedValidationError[]));
      }
      if (securityResult.warnings) {
        warnings.push(...securityResult.warnings);
      }
    }

    // Warning for very long content
    const warningThreshold = maxLength * 0.8;
    if (contentLength > warningThreshold && contentLength <= maxLength) {
      warnings.push({
        code: 'CONTENT_APPROACHING_LIMIT',
        message: `Content is approaching the maximum length (${Math.round((contentLength / maxLength) * 100)}% of limit)`,
        field: 'content',
        value: contentLength,
        recommendation: 'Consider shortening the message to stay well within limits'
      });
    }

    return {
      valid: errors.length === 0,
      errors: errors.length > 0 ? errors : undefined,
      warnings: warnings.length > 0 ? warnings : undefined,
      metadata: {
        contentLength,
        contentType: typeof content,
        isArray: Array.isArray(content)
      }
    };
  }

  /**
   * Sanitize message content for security
   * @param content Content to sanitize
   * @param options Sanitization options
   * @returns Sanitized content
   */
  static sanitize(
    content: string,
    options: SanitizeOptions = {}
  ): string {
    let sanitized = content;

    // Remove HTML tags
    if (options.removeHtml !== false) {
      sanitized = sanitized.replace(/<[^>]*>/g, '');
    }

    // Remove script tags specifically
    if (options.removeScripts !== false) {
      sanitized = sanitized.replace(ValidationConstants.SECURITY_PATTERNS.SCRIPT_TAG, '');
      sanitized = sanitized.replace(ValidationConstants.SECURITY_PATTERNS.ON_EVENT, '');
    }

    // Remove or replace URLs
    if (options.removeUrls) {
      sanitized = sanitized.replace(/https?:\/\/[^\s]+/g, '');
    } else if (options.replaceUrls) {
      const replacement = options.urlReplacement ?? '[URL]';
      sanitized = sanitized.replace(/https?:\/\/[^\s]+/g, replacement);
    }

    // Escape special characters
    if (options.escapeSpecialChars) {
      sanitized = sanitized
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#x27;');
    }

    // Normalize whitespace
    if (options.normalizeWhitespace) {
      sanitized = sanitized.replace(/\s+/g, ' ');
    }

    // Trim whitespace
    if (options.trim !== false) {
      sanitized = sanitized.trim();
    }

    // Apply custom sanitization rules
    if (options.customRules && options.customRules.length > 0) {
      for (const rule of options.customRules) {
        try {
          const pattern = rule.pattern instanceof RegExp 
            ? rule.pattern 
            : new RegExp(rule.pattern, rule.ignoreCase ? 'gi' : 'g');
          
          if (rule.global !== false) {
            sanitized = sanitized.replace(pattern, rule.replacement);
          } else {
            sanitized = sanitized.replace(pattern, rule.replacement);
          }
        } catch (error) {
          // Continue with other rules if one fails
          console.warn(`Sanitization rule '${rule.name}' failed:`, error);
        }
      }
    }

    return sanitized;
  }

  /**
   * Check if content is safe (passes security validation)
   * @param content Content to check
   * @param strict Use strict security checks
   * @returns Whether content is safe
   */
  static isSafe(content: MessageContent, strict: boolean = true): boolean {
    const securityOptions: SecurityValidationOptions = {
      checkXSS: true,
      checkSQLInjection: strict,
      checkScriptTags: true,
      checkDangerousUrls: true,
      validateJson: strict
    };

    const result = this.validateSecurity(content, securityOptions);
    return result.valid;
  }

  /**
   * Get estimated token count for content
   * @param content Content to estimate
   * @param model Optional model for specific estimation
   * @returns Estimated token count
   */
  static estimateTokens(content: MessageContent, model?: string): number {
    const length = this.getContentLength(content);
    
    // Basic estimation: ~4 characters per token
    // Different models have different ratios, but this is a reasonable default
    const baseRatio = model?.toLowerCase().includes('gpt-4') ? 3.8 : 3.5;
    
    return Math.ceil(length / baseRatio);
  }

  // Private helper methods

  private static validateStructure(message: ValidatableMessage): ValidationResult {
    const errors: EnhancedValidationError[] = [];

    // Validate required content
    if (message.content === undefined || message.content === null) {
      errors.push({
        code: 'MISSING_CONTENT',
        message: 'Message content is required',
        field: 'content',
        severity: ValidationSeverity.CRITICAL,
        category: ValidationCategory.STRUCTURE
      });
    }

    // Validate role if present
    if (message.role && !['user', 'assistant', 'system', 'function'].includes(message.role)) {
      errors.push({
        code: 'INVALID_ROLE',
        message: `Invalid role: ${message.role}. Must be one of: user, assistant, system, function`,
        field: 'role',
        value: message.role,
        severity: ValidationSeverity.MEDIUM,
        category: ValidationCategory.STRUCTURE
      });
    }

    // Validate function call structure
    if (message.functionCall) {
      if (!message.functionCall.name || typeof message.functionCall.name !== 'string') {
        errors.push({
          code: 'INVALID_FUNCTION_NAME',
          message: 'Function call must have a valid name',
          field: 'functionCall.name',
          severity: ValidationSeverity.HIGH,
          category: ValidationCategory.STRUCTURE
        });
      }
      if (message.functionCall.arguments && typeof message.functionCall.arguments !== 'string') {
        errors.push({
          code: 'INVALID_FUNCTION_ARGUMENTS',
          message: 'Function arguments must be a string',
          field: 'functionCall.arguments',
          severity: ValidationSeverity.HIGH,
          category: ValidationCategory.STRUCTURE
        });
      }
    }

    return {
      valid: errors.length === 0,
      errors: errors.length > 0 ? errors : undefined
    };
  }

  private static validateMessageImages(
    images: Array<{ url?: string; width?: number; height?: number; size?: number; mimeType?: string }>,
    rules: ValidationRules
  ): ValidationResult {
    const errors: EnhancedValidationError[] = [];
    const warnings: ValidationWarning[] = [];
    const maxImages = rules.maxImages ?? ValidationConstants.MESSAGE_LIMITS.MAX_IMAGES;

    if (images.length > maxImages) {
      errors.push({
        code: 'TOO_MANY_IMAGES',
        message: `Message contains ${images.length} images, but maximum allowed is ${maxImages}`,
        field: 'images',
        value: images.length,
        severity: ValidationSeverity.HIGH,
        category: ValidationCategory.CONTENT
      });
    }

    // Validate each image
    for (let i = 0; i < images.length; i++) {
      const image = images[i];
      
      if (image.size && rules.maxFileSize && image.size > rules.maxFileSize) {
        errors.push({
          code: 'IMAGE_TOO_LARGE',
          message: `Image ${i + 1} size (${image.size} bytes) exceeds maximum (${rules.maxFileSize} bytes)`,
          field: `images[${i}].size`,
          value: image.size,
          severity: ValidationSeverity.HIGH,
          category: ValidationCategory.SIZE
        });
      }

      if (image.mimeType && rules.allowedFormats && !rules.allowedFormats.includes(image.mimeType)) {
        errors.push({
          code: 'UNSUPPORTED_IMAGE_FORMAT',
          message: `Image ${i + 1} format '${image.mimeType}' is not supported`,
          field: `images[${i}].mimeType`,
          value: image.mimeType,
          suggestion: `Use one of: ${rules.allowedFormats.join(', ')}`,
          severity: ValidationSeverity.HIGH,
          category: ValidationCategory.FORMAT
        });
      }
    }

    return {
      valid: errors.length === 0,
      errors: errors.length > 0 ? errors : undefined,
      warnings: warnings.length > 0 ? warnings : undefined
    };
  }

  private static validateModelConstraints(
    message: ValidatableMessage,
    constraints: ModelConstraints
  ): ValidationResult {
    const errors: EnhancedValidationError[] = [];
    const warnings: ValidationWarning[] = [];

    // Check vision support
    if (message.images && message.images.length > 0 && !constraints.supportsVision) {
      errors.push({
        code: 'MODEL_NO_VISION_SUPPORT',
        message: `Model '${constraints.model}' does not support images`,
        field: 'images',
        severity: ValidationSeverity.HIGH,
        category: ValidationCategory.MODEL
      });
    }

    // Check function calling support
    if ((message.functionCall || message.toolCalls) && !constraints.supportsFunctions) {
      errors.push({
        code: 'MODEL_NO_FUNCTION_SUPPORT',
        message: `Model '${constraints.model}' does not support function calling`,
        field: 'functionCall',
        severity: ValidationSeverity.HIGH,
        category: ValidationCategory.MODEL
      });
    }

    // Check token limits
    if (constraints.maxTokens) {
      const estimatedTokens = this.estimateTokens(message.content, constraints.model);
      if (estimatedTokens > constraints.maxTokens) {
        errors.push({
          code: 'EXCEEDS_TOKEN_LIMIT',
          message: `Estimated tokens (${estimatedTokens}) exceed model limit (${constraints.maxTokens})`,
          field: 'content',
          value: estimatedTokens,
          severity: ValidationSeverity.HIGH,
          category: ValidationCategory.MODEL
        });
      }
    }

    // Check image format support
    if (message.images && constraints.supportedImageFormats) {
      for (let i = 0; i < message.images.length; i++) {
        const image = message.images[i];
        if (image.mimeType && !constraints.supportedImageFormats.includes(image.mimeType)) {
          errors.push({
            code: 'MODEL_UNSUPPORTED_IMAGE_FORMAT',
            message: `Model '${constraints.model}' does not support image format '${image.mimeType}'`,
            field: `images[${i}].mimeType`,
            value: image.mimeType,
            suggestion: `Use one of: ${constraints.supportedImageFormats.join(', ')}`,
            severity: ValidationSeverity.HIGH,
            category: ValidationCategory.MODEL
          });
        }
      }
    }

    return {
      valid: errors.length === 0,
      errors: errors.length > 0 ? errors : undefined,
      warnings: warnings.length > 0 ? warnings : undefined
    };
  }

  private static validateSecurity(
    content: MessageContent,
    options: SecurityValidationOptions
  ): ValidationResult {
    const errors: EnhancedValidationError[] = [];
    const warnings: ValidationWarning[] = [];
    const contentStr = this.getContentAsString(content);

    // Check for XSS patterns
    if (options.checkXSS) {
      if (ValidationConstants.SECURITY_PATTERNS.SCRIPT_TAG.test(contentStr)) {
        errors.push({
          code: 'XSS_SCRIPT_TAG',
          message: 'Content contains potentially dangerous script tags',
          field: 'content',
          severity: ValidationSeverity.CRITICAL,
          category: ValidationCategory.SECURITY
        });
      }

      if (ValidationConstants.SECURITY_PATTERNS.ON_EVENT.test(contentStr)) {
        errors.push({
          code: 'XSS_EVENT_HANDLER',
          message: 'Content contains potentially dangerous event handlers',
          field: 'content',
          severity: ValidationSeverity.CRITICAL,
          category: ValidationCategory.SECURITY
        });
      }

      if (ValidationConstants.SECURITY_PATTERNS.JAVASCRIPT_URL.test(contentStr)) {
        errors.push({
          code: 'XSS_JAVASCRIPT_URL',
          message: 'Content contains javascript: URLs',
          field: 'content',
          severity: ValidationSeverity.HIGH,
          category: ValidationCategory.SECURITY
        });
      }
    }

    // Check for SQL injection patterns
    if (options.checkSQLInjection) {
      if (ValidationConstants.SECURITY_PATTERNS.SQL_INJECTION.test(contentStr)) {
        warnings.push({
          code: 'POTENTIAL_SQL_INJECTION',
          message: 'Content contains patterns that might be SQL injection attempts',
          field: 'content',
          recommendation: 'Review content for SQL keywords'
        });
      }
    }

    // Validate JSON if content appears to be JSON
    if (options.validateJson && this.looksLikeJson(contentStr)) {
      try {
        const parsed = JSON.parse(contentStr);
        if (options.maxJsonDepth) {
          const depth = this.getObjectDepth(parsed);
          if (depth > options.maxJsonDepth) {
            warnings.push({
              code: 'JSON_TOO_DEEP',
              message: `JSON nesting depth (${depth}) exceeds maximum (${options.maxJsonDepth})`,
              field: 'content',
              value: depth
            });
          }
        }
      } catch {
        warnings.push({
          code: 'INVALID_JSON',
          message: 'Content appears to be JSON but is malformed',
          field: 'content',
          recommendation: 'Verify JSON syntax if JSON format is intended'
        });
      }
    }

    return {
      valid: errors.length === 0,
      errors: errors.length > 0 ? errors : undefined,
      warnings: warnings.length > 0 ? warnings : undefined
    };
  }

  private static getContentLength(content: MessageContent): number {
    if (typeof content === 'string') {
      return content.length;
    }
    
    if (Array.isArray(content)) {
      return content.reduce((total, item) => {
        if (typeof item === 'object' && item !== null) {
          // For structured content, estimate length based on text content
          const text = item.text ?? item.content ?? JSON.stringify(item);
          return total + (typeof text === 'string' ? text.length : 0);
        }
        return total;
      }, 0);
    }
    
    return 0;
  }

  private static getContentAsString(content: MessageContent): string {
    if (typeof content === 'string') {
      return content;
    }
    
    if (Array.isArray(content)) {
      return content.map(item => {
        if (typeof item === 'object' && item !== null) {
          return item.text ?? item.content ?? JSON.stringify(item);
        }
        return String(item);
      }).join(' ');
    }
    
    return String(content);
  }

  private static looksLikeJson(content: string): boolean {
    const trimmed = content.trim();
    return (trimmed.startsWith('{') && trimmed.endsWith('}')) ||
           (trimmed.startsWith('[') && trimmed.endsWith(']'));
  }

  private static getObjectDepth(obj: unknown, currentDepth: number = 0): number {
    if (typeof obj !== 'object' || obj === null) {
      return currentDepth;
    }

    let maxDepth = currentDepth;
    
    if (Array.isArray(obj)) {
      for (const item of obj) {
        const depth = this.getObjectDepth(item, currentDepth + 1);
        maxDepth = Math.max(maxDepth, depth);
      }
    } else {
      for (const value of Object.values(obj)) {
        const depth = this.getObjectDepth(value, currentDepth + 1);
        maxDepth = Math.max(maxDepth, depth);
      }
    }

    return maxDepth;
  }
}