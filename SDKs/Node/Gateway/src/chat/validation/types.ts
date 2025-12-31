/**
 * Validation types and interfaces for chat validation utilities
 * Framework-agnostic validation definitions
 */

/**
 * Base validation result interface
 */
export interface ValidationResult {
  /** Whether validation passed */
  valid: boolean;
  /** Validation errors (if any) */
  errors?: ValidationError[];
  /** Validation warnings (if any) */
  warnings?: ValidationWarning[];
  /** Additional metadata about validation */
  metadata?: Record<string, unknown>;
}

/**
 * Validation error details
 */
export interface ValidationError {
  /** Error code for programmatic handling */
  code: string;
  /** Human-readable error message */
  message: string;
  /** Field that caused the error */
  field?: string;
  /** Path to the field (for nested objects) */
  path?: string;
  /** Value that caused the error */
  value?: unknown;
  /** Suggested fix or alternative */
  suggestion?: string;
}

/**
 * Validation warning details
 */
export interface ValidationWarning {
  /** Warning code */
  code: string;
  /** Human-readable warning message */
  message: string;
  /** Field that caused the warning */
  field?: string;
  /** Path to the field */
  path?: string;
  /** Value that caused the warning */
  value?: unknown;
  /** Recommendation for improvement */
  recommendation?: string;
}

/**
 * Message validation rules
 */
export interface ValidationRules {
  /** Maximum message content length in characters */
  maxLength?: number;
  /** Minimum message content length in characters */
  minLength?: number;
  /** Maximum number of images per message */
  maxImages?: number;
  /** Whether content is required */
  requireContent?: boolean;
  /** Allowed image formats */
  allowedFormats?: string[];
  /** Maximum file size in bytes */
  maxFileSize?: number;
  /** Model-specific constraints */
  modelConstraints?: ModelConstraints;
  /** Custom validation rules */
  customRules?: CustomValidationRule[];
  /** Security validation options */
  security?: SecurityValidationOptions;
}

/**
 * Model-specific validation constraints
 */
export interface ModelConstraints {
  /** Model identifier */
  model: string;
  /** Maximum context length in tokens */
  maxTokens?: number;
  /** Supported image formats for this model */
  supportedImageFormats?: string[];
  /** Maximum image dimensions */
  maxImageDimensions?: { width: number; height: number };
  /** Vision capability */
  supportsVision?: boolean;
  /** Function calling capability */
  supportsFunctions?: boolean;
  /** JSON mode capability */
  supportsJsonMode?: boolean;
}

/**
 * Security validation options
 */
export interface SecurityValidationOptions {
  /** Check for XSS patterns */
  checkXSS?: boolean;
  /** Check for SQL injection patterns */
  checkSQLInjection?: boolean;
  /** Check for script tags */
  checkScriptTags?: boolean;
  /** Check for dangerous URLs */
  checkDangerousUrls?: boolean;
  /** Validate JSON structures */
  validateJson?: boolean;
  /** Maximum nesting depth for JSON */
  maxJsonDepth?: number;
  /** Check for path traversal attempts */
  checkPathTraversal?: boolean;
}

/**
 * Custom validation rule interface
 */
export interface CustomValidationRule {
  /** Rule identifier */
  name: string;
  /** Rule description */
  description?: string;
  /** Validation function */
  validate: (value: unknown, context?: ValidationContext) => ValidationResult;
  /** Rule priority (higher runs first) */
  priority?: number;
}

/**
 * Validation context for custom rules
 */
export interface ValidationContext {
  /** Field being validated */
  field?: string;
  /** Path to field */
  path?: string;
  /** Parent object */
  parent?: Record<string, unknown>;
  /** Root object being validated */
  root?: Record<string, unknown>;
  /** Validation rules in effect */
  rules?: ValidationRules;
}

/**
 * Image validation constraints
 */
export interface ImageConstraints {
  /** Maximum file size in bytes */
  maxFileSize?: number;
  /** Minimum file size in bytes */
  minFileSize?: number;
  /** Maximum dimensions */
  maxDimensions?: ImageDimensions;
  /** Minimum dimensions */
  minDimensions?: ImageDimensions;
  /** Allowed MIME types */
  allowedFormats?: string[];
  /** Aspect ratio constraints */
  aspectRatio?: AspectRatioConstraints;
  /** Quality constraints */
  quality?: QualityConstraints;
  /** Security constraints */
  security?: ImageSecurityConstraints;
}

/**
 * Image dimensions interface
 */
export interface ImageDimensions {
  /** Image width in pixels */
  width: number;
  /** Image height in pixels */
  height: number;
}

/**
 * Aspect ratio constraints
 */
export interface AspectRatioConstraints {
  /** Minimum aspect ratio (width/height) */
  min?: number;
  /** Maximum aspect ratio (width/height) */
  max?: number;
  /** Exact aspect ratio required */
  exact?: number;
  /** Tolerance for exact ratio */
  tolerance?: number;
}

/**
 * Image quality constraints
 */
export interface QualityConstraints {
  /** Minimum resolution (pixels) */
  minResolution?: number;
  /** Maximum resolution (pixels) */
  maxResolution?: number;
  /** Color depth requirements */
  colorDepth?: number;
  /** Compression quality (0-100) */
  compressionQuality?: { min?: number; max?: number };
}

/**
 * Image security constraints
 */
export interface ImageSecurityConstraints {
  /** Check for embedded scripts */
  checkEmbeddedScripts?: boolean;
  /** Validate EXIF data */
  validateExif?: boolean;
  /** Check for malicious metadata */
  checkMaliciousMetadata?: boolean;
  /** Verify file signature (magic bytes) */
  verifyFileSignature?: boolean;
  /** Scan for hidden data */
  scanHiddenData?: boolean;
}

/**
 * Sanitization options for content cleaning
 */
export interface SanitizeOptions {
  /** Remove HTML tags */
  removeHtml?: boolean;
  /** Remove script tags specifically */
  removeScripts?: boolean;
  /** Escape special characters */
  escapeSpecialChars?: boolean;
  /** Remove URLs */
  removeUrls?: boolean;
  /** Replace URLs with placeholders */
  replaceUrls?: boolean;
  /** URL replacement text */
  urlReplacement?: string;
  /** Normalize whitespace */
  normalizeWhitespace?: boolean;
  /** Trim leading/trailing whitespace */
  trim?: boolean;
  /** Custom sanitization rules */
  customRules?: SanitizationRule[];
}

/**
 * Custom sanitization rule
 */
export interface SanitizationRule {
  /** Rule name */
  name: string;
  /** Pattern to match (RegExp or string) */
  pattern: RegExp | string;
  /** Replacement value */
  replacement: string;
  /** Global replacement flag */
  global?: boolean;
  /** Case insensitive flag */
  ignoreCase?: boolean;
}

/**
 * File validation interface for browser environments
 */
export interface FileValidationInfo {
  /** File name */
  name: string;
  /** File size in bytes */
  size: number;
  /** MIME type */
  type: string;
  /** File extension */
  extension: string;
  /** Last modified timestamp */
  lastModified: number;
}

/**
 * Validation constants for common limits and formats
 */
export const ValidationConstants = {
  /** Default message length limits */
  MESSAGE_LIMITS: {
    MIN_LENGTH: 0,
    MAX_LENGTH: 32000, // ~8k tokens
    MAX_IMAGES: 10,
  },
  
  /** Default file size limits */
  FILE_LIMITS: {
    MAX_IMAGE_SIZE: 20 * 1024 * 1024, // 20MB
    MIN_IMAGE_SIZE: 1024, // 1KB
    MAX_FILENAME_LENGTH: 255,
  },
  
  /** Supported image formats */
  IMAGE_FORMATS: {
    JPEG: ['image/jpeg', 'image/jpg'],
    PNG: ['image/png'],
    GIF: ['image/gif'],
    WEBP: ['image/webp'],
    BMP: ['image/bmp'],
    TIFF: ['image/tiff'],
  },
  
  /** Image dimension limits */
  IMAGE_DIMENSIONS: {
    MIN_WIDTH: 1,
    MIN_HEIGHT: 1,
    MAX_WIDTH: 8192,
    MAX_HEIGHT: 8192,
    MAX_RESOLUTION: 8192 * 8192,
  },
  
  /** Security patterns */
  SECURITY_PATTERNS: {
    SCRIPT_TAG: /<script[^>]*>.*?<\/script>/gi,
    ON_EVENT: /on\w+\s*=/gi,
    JAVASCRIPT_URL: /javascript:/gi,
    DATA_URL_SCRIPT: /data:text\/html/gi,
    SQL_INJECTION: /(union|select|insert|update|delete|drop|exec|script)/gi,
  },
  
  /** File signatures (magic bytes) for common formats */
  FILE_SIGNATURES: {
    JPEG: [0xFF, 0xD8, 0xFF],
    PNG: [0x89, 0x50, 0x4E, 0x47],
    GIF: [0x47, 0x49, 0x46],
    WEBP: [0x52, 0x49, 0x46, 0x46], // RIFF
    BMP: [0x42, 0x4D],
    TIFF_BE: [0x4D, 0x4D], // Big-endian
    TIFF_LE: [0x49, 0x49], // Little-endian
  }
} as const;

/**
 * Validation severity levels
 */
export enum ValidationSeverity {
  LOW = 'low',
  MEDIUM = 'medium',
  HIGH = 'high',
  CRITICAL = 'critical'
}

/**
 * Validation categories for organizing validation results
 */
export enum ValidationCategory {
  CONTENT = 'content',
  SECURITY = 'security',
  FORMAT = 'format',
  SIZE = 'size',
  STRUCTURE = 'structure',
  MODEL = 'model',
  CUSTOM = 'custom'
}

/**
 * Enhanced validation error with additional metadata
 */
export interface EnhancedValidationError extends ValidationError {
  /** Error severity level */
  severity?: ValidationSeverity;
  /** Error category */
  category?: ValidationCategory;
  /** Timestamp when error occurred */
  timestamp?: Date;
  /** Stack trace (for debugging) */
  stack?: string;
  /** Help URL for error resolution */
  helpUrl?: string;
}