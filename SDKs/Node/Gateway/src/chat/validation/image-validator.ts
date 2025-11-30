/**
 * Image validation utilities for chat image attachments
 * Framework-agnostic image file validation and security checks
 */

import {
  ValidationConstants,
  ValidationSeverity,
  ValidationCategory,
  type ValidationResult,
  type ValidationWarning,
  type ImageConstraints,
  type ImageDimensions,
  type AspectRatioConstraints,
  type QualityConstraints,
  type ImageSecurityConstraints,
  type FileValidationInfo,
  type EnhancedValidationError
} from './types';

/**
 * Browser File interface for validation
 */
interface BrowserFile {
  name: string;
  size: number;
  type: string;
  lastModified?: number;
  arrayBuffer?: () => Promise<ArrayBuffer>;
  slice?: (start?: number, end?: number) => Blob;
}

/**
 * Image metadata extracted from file
 */
export interface ImageMetadata {
  /** Image dimensions */
  dimensions?: ImageDimensions;
  /** File format */
  format: string;
  /** Color depth */
  colorDepth?: number;
  /** Compression quality (if applicable) */
  quality?: number;
  /** File signature verification */
  signatureValid: boolean;
  /** EXIF data present */
  hasExif: boolean;
  /** Estimated resolution */
  resolution?: number;
}

/**
 * Image validation result with metadata
 */
export interface ImageValidationResult extends ValidationResult {
  /** Extracted image metadata */
  imageMetadata?: ImageMetadata;
}

/**
 * Image validation utilities
 */
export class ImageValidator {
  /**
   * Validate an image file
   * @param file File or Blob to validate
   * @param constraints Validation constraints
   * @returns Validation result with image metadata
   */
  static async validateFile(
    file: BrowserFile | Blob,
    constraints: ImageConstraints = {}
  ): Promise<ImageValidationResult> {
    const errors: EnhancedValidationError[] = [];
    const warnings: ValidationWarning[] = [];

    // Convert to FileValidationInfo
    const fileInfo = this.extractFileInfo(file);

    // Basic file validation
    const basicResult = this.validateBasicFile(fileInfo, constraints);
    if (basicResult.errors) {
      errors.push(...(basicResult.errors as EnhancedValidationError[]));
    }
    if (basicResult.warnings) {
      warnings.push(...basicResult.warnings);
    }

    let imageMetadata: ImageMetadata | undefined;

    // Extended validation if file is accessible
    if (file.arrayBuffer) {
      try {
        const buffer = await file.arrayBuffer();
        const arrayBuffer = new Uint8Array(buffer);
        
        // Extract metadata
        imageMetadata = await this.extractImageMetadata(arrayBuffer, fileInfo.type);
        
        // Validate metadata against constraints
        const metadataResult = this.validateImageMetadata(imageMetadata, constraints);
        if (metadataResult.errors) {
          errors.push(...(metadataResult.errors as EnhancedValidationError[]));
        }
        if (metadataResult.warnings) {
          warnings.push(...metadataResult.warnings);
        }

        // Security validation
        if (constraints.security) {
          const securityResult = await this.validateImageSecurity(arrayBuffer, imageMetadata, constraints.security);
          if (securityResult.errors) {
            errors.push(...(securityResult.errors as EnhancedValidationError[]));
          }
          if (securityResult.warnings) {
            warnings.push(...securityResult.warnings);
          }
        }
      } catch (error) {
        errors.push({
          code: 'FILE_READ_ERROR',
          message: `Unable to read image file: ${error instanceof Error ? error.message : String(error)}`,
          field: 'file',
          severity: ValidationSeverity.HIGH,
          category: ValidationCategory.FORMAT
        });
      }
    }

    return {
      valid: errors.length === 0,
      errors: errors.length > 0 ? errors : undefined,
      warnings: warnings.length > 0 ? warnings : undefined,
      imageMetadata,
      metadata: {
        fileName: fileInfo.name,
        fileSize: fileInfo.size,
        mimeType: fileInfo.type,
        hasMetadata: Boolean(imageMetadata)
      }
    };
  }

  /**
   * Validate image dimensions
   * @param width Image width
   * @param height Image height
   * @param constraints Dimension constraints
   * @returns Validation result
   */
  static validateDimensions(
    width: number,
    height: number,
    constraints: Pick<ImageConstraints, 'maxDimensions' | 'minDimensions' | 'aspectRatio'> = {}
  ): ValidationResult {
    const errors: EnhancedValidationError[] = [];
    const warnings: ValidationWarning[] = [];

    // Validate dimensions are positive
    if (width <= 0 || height <= 0) {
      errors.push({
        code: 'INVALID_DIMENSIONS',
        message: `Image dimensions must be positive (got ${width}x${height})`,
        field: 'dimensions',
        value: { width, height },
        severity: ValidationSeverity.HIGH,
        category: ValidationCategory.FORMAT
      });
      return { valid: false, errors };
    }

    // Maximum dimensions
    if (constraints.maxDimensions) {
      const { width: maxWidth, height: maxHeight } = constraints.maxDimensions;
      if (width > maxWidth || height > maxHeight) {
        errors.push({
          code: 'DIMENSIONS_TOO_LARGE',
          message: `Image dimensions ${width}x${height} exceed maximum ${maxWidth}x${maxHeight}`,
          field: 'dimensions',
          value: { width, height },
          suggestion: `Resize image to ${maxWidth}x${maxHeight} or smaller`,
          severity: ValidationSeverity.HIGH,
          category: ValidationCategory.SIZE
        });
      }
    }

    // Minimum dimensions
    if (constraints.minDimensions) {
      const { width: minWidth, height: minHeight } = constraints.minDimensions;
      if (width < minWidth || height < minHeight) {
        errors.push({
          code: 'DIMENSIONS_TOO_SMALL',
          message: `Image dimensions ${width}x${height} are below minimum ${minWidth}x${minHeight}`,
          field: 'dimensions',
          value: { width, height },
          suggestion: `Use an image that is at least ${minWidth}x${minHeight}`,
          severity: ValidationSeverity.MEDIUM,
          category: ValidationCategory.SIZE
        });
      }
    }

    // Aspect ratio validation
    if (constraints.aspectRatio) {
      const aspectRatio = width / height;
      const aspectResult = this.validateAspectRatio(aspectRatio, constraints.aspectRatio);
      if (aspectResult.errors) {
        errors.push(...(aspectResult.errors as EnhancedValidationError[]));
      }
      if (aspectResult.warnings) {
        warnings.push(...aspectResult.warnings);
      }
    }

    // Resolution warnings
    const resolution = width * height;
    if (resolution > ValidationConstants.IMAGE_DIMENSIONS.MAX_RESOLUTION) {
      warnings.push({
        code: 'HIGH_RESOLUTION_WARNING',
        message: `Very high resolution image (${resolution.toLocaleString()} pixels) may cause performance issues`,
        field: 'dimensions',
        value: resolution,
        recommendation: 'Consider using a lower resolution image'
      });
    }

    return {
      valid: errors.length === 0,
      errors: errors.length > 0 ? errors : undefined,
      warnings: warnings.length > 0 ? warnings : undefined,
      metadata: {
        width,
        height,
        aspectRatio: width / height,
        resolution
      }
    };
  }

  /**
   * Check if image format is supported
   * @param mimeType MIME type to check
   * @param model Optional model to check format support for
   * @returns Whether format is supported
   */
  static isSupportedFormat(mimeType: string, model?: string): boolean {
    // Basic format support check
    const supportedFormats = Object.values(ValidationConstants.IMAGE_FORMATS).flat() as string[];
    const basicSupport = supportedFormats.includes(mimeType);

    if (!basicSupport) {
      return false;
    }

    // Model-specific checks (simplified - in real implementation, this would be more comprehensive)
    if (model) {
      const modelLower = model.toLowerCase();
      
      // Some models might not support certain formats
      if (modelLower.includes('gpt-4') && mimeType === 'image/gif') {
        return false; // Example: GPT-4 doesn't support animated GIFs
      }
      
      if (modelLower.includes('claude') && mimeType === 'image/bmp') {
        return false; // Example: Claude might not support BMP
      }
    }

    return true;
  }

  /**
   * Get recommended image constraints for a specific model
   * @param model Model identifier
   * @returns Recommended constraints
   */
  static getModelImageConstraints(model: string): ImageConstraints {
    const modelLower = model.toLowerCase();

    if (modelLower.includes('gpt-4')) {
      return {
        maxFileSize: 20 * 1024 * 1024, // 20MB
        maxDimensions: { width: 2048, height: 2048 },
        allowedFormats: ['image/jpeg', 'image/png', 'image/webp'],
        aspectRatio: { min: 1/3, max: 3/1 },
        security: {
          verifyFileSignature: true,
          checkMaliciousMetadata: true
        }
      };
    }

    if (modelLower.includes('claude')) {
      return {
        maxFileSize: 5 * 1024 * 1024, // 5MB
        maxDimensions: { width: 1568, height: 1568 },
        allowedFormats: ['image/jpeg', 'image/png', 'image/gif', 'image/webp'],
        security: {
          verifyFileSignature: true,
          validateExif: true
        }
      };
    }

    // Default constraints
    return {
      maxFileSize: ValidationConstants.FILE_LIMITS.MAX_IMAGE_SIZE,
      maxDimensions: {
        width: ValidationConstants.IMAGE_DIMENSIONS.MAX_WIDTH,
        height: ValidationConstants.IMAGE_DIMENSIONS.MAX_HEIGHT
      },
      allowedFormats: Object.values(ValidationConstants.IMAGE_FORMATS).flat(),
      security: {
        verifyFileSignature: true
      }
    };
  }

  /**
   * Estimate processing cost for an image
   * @param dimensions Image dimensions
   * @param format Image format
   * @returns Processing cost estimate (relative scale)
   */
  static estimateProcessingCost(dimensions: ImageDimensions, format: string): number {
    const { width, height } = dimensions;
    const resolution = width * height;
    
    // Base cost based on resolution
    let cost = Math.sqrt(resolution / 1000000); // Normalize to megapixels
    
    // Format-specific multipliers
    const formatMultipliers = {
      'image/jpeg': 1.0,
      'image/png': 1.2, // PNG requires more processing
      'image/gif': 1.5, // GIF can be complex
      'image/webp': 0.8, // WebP is efficient
      'image/bmp': 1.3,  // BMP is uncompressed
      'image/tiff': 1.4  // TIFF can be complex
    };
    
    const multiplier = formatMultipliers[format as keyof typeof formatMultipliers] || 1.0;
    cost *= multiplier;
    
    return Math.round(cost * 100) / 100; // Round to 2 decimal places
  }

  // Private helper methods

  private static extractFileInfo(file: BrowserFile | Blob): FileValidationInfo {
    const browserFile = file as BrowserFile;
    
    return {
      name: browserFile.name || 'unknown',
      size: file.size,
      type: file.type,
      extension: this.getFileExtension(browserFile.name || ''),
      lastModified: browserFile.lastModified ?? Date.now()
    };
  }

  private static validateBasicFile(
    fileInfo: FileValidationInfo,
    constraints: ImageConstraints
  ): ValidationResult {
    const errors: EnhancedValidationError[] = [];
    const warnings: ValidationWarning[] = [];

    // File size validation
    if (constraints.maxFileSize && fileInfo.size > constraints.maxFileSize) {
      errors.push({
        code: 'FILE_TOO_LARGE',
        message: `File size ${this.formatFileSize(fileInfo.size)} exceeds maximum ${this.formatFileSize(constraints.maxFileSize)}`,
        field: 'size',
        value: fileInfo.size,
        suggestion: `Compress or resize the image to reduce file size`,
        severity: ValidationSeverity.HIGH,
        category: ValidationCategory.SIZE
      });
    }

    if (constraints.minFileSize && fileInfo.size < constraints.minFileSize) {
      errors.push({
        code: 'FILE_TOO_SMALL',
        message: `File size ${this.formatFileSize(fileInfo.size)} is below minimum ${this.formatFileSize(constraints.minFileSize)}`,
        field: 'size',
        value: fileInfo.size,
        severity: ValidationSeverity.MEDIUM,
        category: ValidationCategory.SIZE
      });
    }

    // Format validation
    if (constraints.allowedFormats && !constraints.allowedFormats.includes(fileInfo.type)) {
      errors.push({
        code: 'UNSUPPORTED_FORMAT',
        message: `Image format '${fileInfo.type}' is not supported`,
        field: 'type',
        value: fileInfo.type,
        suggestion: `Use one of: ${constraints.allowedFormats.join(', ')}`,
        severity: ValidationSeverity.HIGH,
        category: ValidationCategory.FORMAT
      });
    }

    // File extension vs MIME type mismatch
    const expectedExtensions = this.getExpectedExtensions(fileInfo.type);
    if (expectedExtensions.length > 0 && !expectedExtensions.includes(fileInfo.extension.toLowerCase())) {
      warnings.push({
        code: 'EXTENSION_MISMATCH',
        message: `File extension '${fileInfo.extension}' doesn't match MIME type '${fileInfo.type}'`,
        field: 'extension',
        value: fileInfo.extension,
        recommendation: `Expected extensions: ${expectedExtensions.join(', ')}`
      });
    }

    // File name length
    if (fileInfo.name.length > ValidationConstants.FILE_LIMITS.MAX_FILENAME_LENGTH) {
      warnings.push({
        code: 'FILENAME_TOO_LONG',
        message: `File name is very long (${fileInfo.name.length} characters)`,
        field: 'name',
        value: fileInfo.name.length,
        recommendation: 'Consider using a shorter file name'
      });
    }

    return {
      valid: errors.length === 0,
      errors: errors.length > 0 ? errors : undefined,
      warnings: warnings.length > 0 ? warnings : undefined
    };
  }

  private static async extractImageMetadata(
    buffer: Uint8Array,
    mimeType: string
  ): Promise<ImageMetadata> {
    const metadata: ImageMetadata = {
      format: mimeType,
      signatureValid: this.verifyFileSignature(buffer, mimeType),
      hasExif: this.hasExifData(buffer, mimeType)
    };

    // Extract dimensions based on format
    try {
      const dimensions = await this.extractDimensions(buffer, mimeType);
      if (dimensions) {
        metadata.dimensions = dimensions;
        metadata.resolution = dimensions.width * dimensions.height;
      }
    } catch {
      // Dimensions extraction failed - metadata will indicate this
    }

    return metadata;
  }

  private static validateImageMetadata(
    metadata: ImageMetadata,
    constraints: ImageConstraints
  ): ValidationResult {
    const errors: EnhancedValidationError[] = [];
    const warnings: ValidationWarning[] = [];

    // Validate file signature
    if (!metadata.signatureValid) {
      errors.push({
        code: 'INVALID_FILE_SIGNATURE',
        message: 'File signature does not match the declared format',
        field: 'signature',
        severity: ValidationSeverity.HIGH,
        category: ValidationCategory.SECURITY
      });
    }

    // Validate dimensions if available
    if (metadata.dimensions) {
      const dimensionResult = this.validateDimensions(
        metadata.dimensions.width,
        metadata.dimensions.height,
        constraints
      );
      if (dimensionResult.errors) {
        errors.push(...(dimensionResult.errors as EnhancedValidationError[]));
      }
      if (dimensionResult.warnings) {
        warnings.push(...dimensionResult.warnings);
      }
    }

    // Quality constraints
    if (constraints.quality && metadata.resolution) {
      const qualityResult = this.validateQuality(metadata, constraints.quality);
      if (qualityResult.errors) {
        errors.push(...(qualityResult.errors as EnhancedValidationError[]));
      }
      if (qualityResult.warnings) {
        warnings.push(...qualityResult.warnings);
      }
    }

    return {
      valid: errors.length === 0,
      errors: errors.length > 0 ? errors : undefined,
      warnings: warnings.length > 0 ? warnings : undefined
    };
  }

  private static async validateImageSecurity(
    buffer: Uint8Array,
    metadata: ImageMetadata,
    security: ImageSecurityConstraints
  ): Promise<ValidationResult> {
    const errors: EnhancedValidationError[] = [];
    const warnings: ValidationWarning[] = [];

    // Check for embedded scripts
    if (security.checkEmbeddedScripts) {
      if (this.hasEmbeddedScripts(buffer)) {
        errors.push({
          code: 'EMBEDDED_SCRIPT_DETECTED',
          message: 'Image contains embedded script data',
          field: 'content',
          severity: ValidationSeverity.CRITICAL,
          category: ValidationCategory.SECURITY
        });
      }
    }

    // Validate EXIF data
    if (security.validateExif && metadata.hasExif) {
      if (this.hasSuspiciousExif(buffer)) {
        warnings.push({
          code: 'SUSPICIOUS_EXIF',
          message: 'Image EXIF data contains unusual or potentially malicious content',
          field: 'exif',
          recommendation: 'Consider stripping EXIF data before use'
        });
      }
    }

    // Check for malicious metadata
    if (security.checkMaliciousMetadata) {
      if (this.hasMaliciousMetadata(buffer, metadata.format)) {
        errors.push({
          code: 'MALICIOUS_METADATA',
          message: 'Image metadata contains potentially harmful content',
          field: 'metadata',
          severity: ValidationSeverity.HIGH,
          category: ValidationCategory.SECURITY
        });
      }
    }

    // Scan for hidden data
    if (security.scanHiddenData) {
      if (this.hasHiddenData(buffer)) {
        warnings.push({
          code: 'HIDDEN_DATA_DETECTED',
          message: 'Image may contain hidden or steganographic data',
          field: 'content',
          recommendation: 'Verify the image source and content'
        });
      }
    }

    return {
      valid: errors.length === 0,
      errors: errors.length > 0 ? errors : undefined,
      warnings: warnings.length > 0 ? warnings : undefined
    };
  }

  private static validateAspectRatio(
    ratio: number,
    constraints: AspectRatioConstraints
  ): ValidationResult {
    const errors: EnhancedValidationError[] = [];

    if (constraints.exact !== undefined) {
      const tolerance = constraints.tolerance ?? 0.01;
      if (Math.abs(ratio - constraints.exact) > tolerance) {
        errors.push({
          code: 'INVALID_ASPECT_RATIO',
          message: `Aspect ratio ${ratio.toFixed(2)} does not match required ratio ${constraints.exact.toFixed(2)}`,
          field: 'aspectRatio',
          value: ratio,
          severity: ValidationSeverity.MEDIUM,
          category: ValidationCategory.FORMAT
        });
      }
    } else {
      if (constraints.min !== undefined && ratio < constraints.min) {
        errors.push({
          code: 'ASPECT_RATIO_TOO_LOW',
          message: `Aspect ratio ${ratio.toFixed(2)} is below minimum ${constraints.min.toFixed(2)}`,
          field: 'aspectRatio',
          value: ratio,
          severity: ValidationSeverity.MEDIUM,
          category: ValidationCategory.FORMAT
        });
      }

      if (constraints.max !== undefined && ratio > constraints.max) {
        errors.push({
          code: 'ASPECT_RATIO_TOO_HIGH',
          message: `Aspect ratio ${ratio.toFixed(2)} exceeds maximum ${constraints.max.toFixed(2)}`,
          field: 'aspectRatio',
          value: ratio,
          severity: ValidationSeverity.MEDIUM,
          category: ValidationCategory.FORMAT
        });
      }
    }

    return {
      valid: errors.length === 0,
      errors: errors.length > 0 ? errors : undefined
    };
  }

  private static validateQuality(
    metadata: ImageMetadata,
    constraints: QualityConstraints
  ): ValidationResult {
    const errors: EnhancedValidationError[] = [];
    const warnings: ValidationWarning[] = [];

    if (constraints.minResolution && metadata.resolution && metadata.resolution < constraints.minResolution) {
      errors.push({
        code: 'RESOLUTION_TOO_LOW',
        message: `Image resolution ${metadata.resolution?.toLocaleString()} is below minimum ${constraints.minResolution.toLocaleString()}`,
        field: 'resolution',
        value: metadata.resolution,
        severity: ValidationSeverity.MEDIUM,
        category: ValidationCategory.SIZE
      });
    }

    if (constraints.maxResolution && metadata.resolution && metadata.resolution > constraints.maxResolution) {
      warnings.push({
        code: 'RESOLUTION_TOO_HIGH',
        message: `Image resolution ${metadata.resolution?.toLocaleString()} exceeds recommended maximum ${constraints.maxResolution.toLocaleString()}`,
        field: 'resolution',
        value: metadata.resolution,
        recommendation: 'Consider reducing image resolution for better performance'
      });
    }

    return {
      valid: errors.length === 0,
      errors: errors.length > 0 ? errors : undefined,
      warnings: warnings.length > 0 ? warnings : undefined
    };
  }

  // File format specific methods

  private static verifyFileSignature(buffer: Uint8Array, mimeType: string): boolean {
    if (buffer.length < 4) return false;

    const signatures = ValidationConstants.FILE_SIGNATURES;
    
    switch (mimeType) {
      case 'image/jpeg':
        return this.matchesSignature(buffer, signatures.JPEG);
      case 'image/png':
        return this.matchesSignature(buffer, signatures.PNG);
      case 'image/gif':
        return this.matchesSignature(buffer, signatures.GIF);
      case 'image/webp':
        return this.matchesSignature(buffer, signatures.WEBP);
      case 'image/bmp':
        return this.matchesSignature(buffer, signatures.BMP);
      case 'image/tiff':
        return this.matchesSignature(buffer, signatures.TIFF_BE) || 
               this.matchesSignature(buffer, signatures.TIFF_LE);
      default:
        return true; // Unknown format, can't verify
    }
  }

  private static matchesSignature(buffer: Uint8Array, signature: readonly number[]): boolean {
    if (buffer.length < signature.length) return false;
    
    for (let i = 0; i < signature.length; i++) {
      if (buffer[i] !== signature[i]) return false;
    }
    
    return true;
  }

  private static async extractDimensions(buffer: Uint8Array, mimeType: string): Promise<ImageDimensions | null> {
    // Simplified dimension extraction - in a real implementation, 
    // this would use proper image parsing libraries
    try {
      switch (mimeType) {
        case 'image/jpeg':
          return this.extractJPEGDimensions(buffer);
        case 'image/png':
          return this.extractPNGDimensions(buffer);
        case 'image/gif':
          return this.extractGIFDimensions(buffer);
        case 'image/webp':
          return this.extractWebPDimensions(buffer);
        default:
          return null;
      }
    } catch {
      return null;
    }
  }

  // Simplified dimension extraction methods
  private static extractPNGDimensions(buffer: Uint8Array): ImageDimensions | null {
    if (buffer.length < 24) return null;
    
    // PNG width and height are at bytes 16-23 (big-endian)
    const width = (buffer[16] << 24) | (buffer[17] << 16) | (buffer[18] << 8) | buffer[19];
    const height = (buffer[20] << 24) | (buffer[21] << 16) | (buffer[22] << 8) | buffer[23];
    
    return { width, height };
  }

  private static extractJPEGDimensions(buffer: Uint8Array): ImageDimensions | null {
    // Simplified JPEG parsing - real implementation would be more robust
    let offset = 2; // Skip SOI marker
    
    while (offset < buffer.length - 4) {
      if (buffer[offset] === 0xFF) {
        const marker = buffer[offset + 1];
        
        // SOF0 or SOF2 markers
        if (marker === 0xC0 || marker === 0xC2) {
          const height = (buffer[offset + 5] << 8) | buffer[offset + 6];
          const width = (buffer[offset + 7] << 8) | buffer[offset + 8];
          return { width, height };
        }
        
        // Skip to next segment
        const segmentLength = (buffer[offset + 2] << 8) | buffer[offset + 3];
        offset += 2 + segmentLength;
      } else {
        offset++;
      }
    }
    
    return null;
  }

  private static extractGIFDimensions(buffer: Uint8Array): ImageDimensions | null {
    if (buffer.length < 10) return null;
    
    // GIF dimensions are at bytes 6-9 (little-endian)
    const width = buffer[6] | (buffer[7] << 8);
    const height = buffer[8] | (buffer[9] << 8);
    
    return { width, height };
  }

  private static extractWebPDimensions(buffer: Uint8Array): ImageDimensions | null {
    if (buffer.length < 30) return null;
    
    // Simplified WebP parsing
    if (buffer[12] === 0x56 && buffer[13] === 0x50 && buffer[14] === 0x38) { // VP8
      // WebP dimensions extraction would be more complex in reality
      return null; // Simplified for this example
    }
    
    return null;
  }

  // Security check methods (simplified implementations)
  private static hasExifData(buffer: Uint8Array, mimeType: string): boolean {
    if (mimeType === 'image/jpeg') {
      // Look for EXIF marker
      for (let i = 0; i < buffer.length - 4; i++) {
        if (buffer[i] === 0xFF && buffer[i + 1] === 0xE1) {
          return true;
        }
      }
    }
    return false;
  }

  private static hasEmbeddedScripts(buffer: Uint8Array): boolean {
    // Convert to string and check for script patterns
    const text = new TextDecoder('utf-8', { fatal: false }).decode(buffer);
    return ValidationConstants.SECURITY_PATTERNS.SCRIPT_TAG.test(text);
  }

  private static hasSuspiciousExif(_buffer: Uint8Array): boolean {
    // Simplified EXIF validation - real implementation would parse EXIF structure
    return false; // Placeholder
  }

  private static hasMaliciousMetadata(_buffer: Uint8Array, _format: string): boolean {
    // Simplified metadata scanning - real implementation would be more thorough
    return false; // Placeholder
  }

  private static hasHiddenData(_buffer: Uint8Array): boolean {
    // Simplified steganography detection - real implementation would use statistical analysis
    return false; // Placeholder
  }

  // Utility methods
  private static getFileExtension(filename: string): string {
    const lastDot = filename.lastIndexOf('.');
    return lastDot === -1 ? '' : filename.substring(lastDot + 1);
  }

  private static getExpectedExtensions(mimeType: string): string[] {
    const extensionMap: Record<string, string[]> = {
      'image/jpeg': ['jpg', 'jpeg'],
      'image/png': ['png'],
      'image/gif': ['gif'],
      'image/webp': ['webp'],
      'image/bmp': ['bmp'],
      'image/tiff': ['tiff', 'tif']
    };
    
    return extensionMap[mimeType] || [];
  }

  private static formatFileSize(bytes: number): string {
    const sizes = ['B', 'KB', 'MB', 'GB'];
    if (bytes === 0) return '0 B';
    
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    const size = bytes / Math.pow(1024, i);
    
    return `${Math.round(size * 100) / 100} ${sizes[i]}`;
  }
}