import { BaseService } from './BaseService';
import type { FetchBasedClient } from '../client/FetchBasedClient';
import type { RequestOptions } from '../client/types';

/**
 * Response from media upload
 */
export interface MediaUploadResponse {
  success: boolean;
  storageKey: string;
  url: string;
  directUrl: string;
  contentType: string;
  mediaType: string;
  fileName: string;
  sizeBytes: number;
}

/**
 * Options for media upload
 */
export interface MediaUploadOptions extends RequestOptions {
  mediaType?: 'Image' | 'Video' | 'Audio';
  onProgress?: (loaded: number, total: number) => void;
}

/**
 * Service for media upload operations
 */
export class MediaService extends BaseService {
  constructor(client: FetchBasedClient) {
    super(client);
  }

  /**
   * Uploads a media file to the storage service
   * 
   * @param file - The file to upload
   * @param options - Optional upload options
   * @returns The upload response with storage URL
   * 
   * @example
   * ```typescript
   * // Upload an image file
   * const fileInput = document.getElementById('file-input') as HTMLInputElement;
   * const file = fileInput.files[0];
   * 
   * const result = await client.media.upload(file, {
   *   mediaType: 'Image',
   *   onProgress: (loaded, total) => {
   *     console.warn(`Upload progress: ${(loaded / total * 100).toFixed(2)}%`);
   *   }
   * });
   * 
   * console.warn('File uploaded:', result.url);
   * ```
   */
  async upload(
    file: File | Blob,
    options?: MediaUploadOptions
  ): Promise<MediaUploadResponse> {
    // Validate file
    if (!file) {
      throw new Error('File is required');
    }

    if (file.size === 0) {
      throw new Error('File is empty');
    }

    // Validate file type if it's a File
    if (file instanceof File) {
      const extension = this.getFileExtension(file.name);
      const isValidExtension = this.isValidFileExtension(extension);
      
      if (!isValidExtension) {
        throw new Error(`Unsupported file extension: ${extension}`);
      }
    }

    // Create FormData
    const formData = new FormData();
    
    // Add file - use the original filename if available
    if (file instanceof File) {
      formData.append('file', file, file.name);
    } else {
      // For Blob, we need to provide a filename
      const extension = this.getExtensionFromMimeType(file.type);
      const filename = `upload${extension}`;
      formData.append('file', file, filename);
    }

    // Add media type if specified
    if (options?.mediaType) {
      formData.append('mediaType', options.mediaType);
    }

    // Use direct fetch for FormData upload
    const response = await this.uploadFormDataDirectly(
      '/v1/media/upload',
      formData,
      options
    );

    return response;
  }

  /**
   * Upload FormData directly using fetch to avoid JSON serialization
   */
  private async uploadFormDataDirectly(
    endpoint: string,
    formData: FormData,
    options?: MediaUploadOptions
  ): Promise<MediaUploadResponse> {
    // Get the client configuration
    // @ts-expect-error Accessing protected config
    const config = this.client.config;
    const baseUrl = config.baseURL ?? 'http://localhost:5000';
    
    // Build full URL
    const fullUrl = endpoint.startsWith('http') ? endpoint : `${baseUrl}${endpoint}`;
    
    // Build headers with auth
    const headers = new Headers();
    
    // Always use Authorization Bearer header for all keys (including ephemeral)
    if (config.apiKey) {
      headers.set('Authorization', `Bearer ${config.apiKey}`);
    }
    // Don't set Content-Type - let browser set it for multipart/form-data
    
    // Add custom headers from options
    if (options?.headers) {
      Object.entries(options.headers).forEach(([key, value]) => {
        if (key.toLowerCase() !== 'content-type') {
          headers.set(key, value as string);
        }
      });
    }
    
    try {
      const response = await fetch(fullUrl, {
        method: 'POST',
        headers,
        body: formData,
        signal: options?.signal,
      });

      if (!response.ok) {
        const errorText = await response.text();
        let errorMessage: string;
        try {
          const errorJson = JSON.parse(errorText);
          errorMessage = errorJson.error ?? errorJson.message ?? `Upload failed with status ${response.status}`;
        } catch {
          errorMessage = `Upload failed with status ${response.status}`;
        }
        throw new Error(errorMessage);
      }

      const data = await response.json() as MediaUploadResponse;
      console.warn(`Media uploaded: ${data.url}`);
      return data;
    } catch (error) {
      if (error instanceof Error) {
        throw error;
      }
      throw new Error('Upload failed with unknown error');
    }
  }

  /**
   * Get file extension from filename
   */
  private getFileExtension(filename: string): string {
    const lastDot = filename.lastIndexOf('.');
    if (lastDot === -1) return '';
    return filename.substring(lastDot).toLowerCase();
  }

  /**
   * Get file extension from MIME type
   */
  private getExtensionFromMimeType(mimeType: string): string {
    const mimeToExtension: Record<string, string> = {
      'image/jpeg': '.jpg',
      'image/png': '.png',
      'image/gif': '.gif',
      'image/webp': '.webp',
      'image/bmp': '.bmp',
      'image/svg+xml': '.svg',
      'video/mp4': '.mp4',
      'video/webm': '.webm',
      'video/quicktime': '.mov',
      'video/x-msvideo': '.avi',
      'audio/mpeg': '.mp3',
      'audio/wav': '.wav',
      'audio/ogg': '.ogg',
      'audio/mp4': '.m4a'
    };

    return mimeToExtension[mimeType] ?? '.bin';
  }

  /**
   * Check if file extension is valid
   */
  private isValidFileExtension(extension: string): boolean {
    const allowedExtensions = [
      // Images
      '.jpg', '.jpeg', '.png', '.gif', '.webp', '.bmp', '.svg',
      // Videos
      '.mp4', '.webm', '.mov', '.avi', '.mkv', '.flv', '.wmv', '.m4v',
      // Audio
      '.mp3', '.wav', '.ogg', '.m4a', '.flac', '.aac'
    ];

    return allowedExtensions.includes(extension);
  }

  /**
   * Validate file size based on media type
   */
  validateFileSize(file: File | Blob, mediaType?: string): { valid: boolean; message?: string } {
    const maxSizes: Record<string, number> = {
      'Image': 104857600, // 100MB
      'Video': 524288000, // 500MB
      'Audio': 209715200  // 200MB
    };

    // Try to determine media type from MIME type if not provided
    let determinedType = mediaType;
    if (!determinedType && file.type) {
      if (file.type.startsWith('image/')) determinedType = 'Image';
      else if (file.type.startsWith('video/')) determinedType = 'Video';
      else if (file.type.startsWith('audio/')) determinedType = 'Audio';
    }

    const maxSize = maxSizes[determinedType ?? 'Image'] ?? maxSizes.Image;
    
    if (file.size > maxSize) {
      const maxSizeMB = maxSize / (1024 * 1024);
      return {
        valid: false,
        message: `File size exceeds maximum allowed size of ${maxSizeMB}MB for ${determinedType ?? 'file'}`
      };
    }

    return { valid: true };
  }
}