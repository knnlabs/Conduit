import { FetchBasedClient } from '../client/FetchBasedClient';
import type { AudioFile } from '../models/audio';

/**
 * Base class for audio service functionality providing common utilities.
 */
export abstract class AudioServiceBase extends FetchBasedClient {
  constructor(client: FetchBasedClient) {
    // @ts-expect-error Accessing protected property from another instance
    super(client.config);
  }

  /**
   * Validates an audio file.
   * @protected
   */
  protected validateAudioFile(file: AudioFile): void {
    if (!file.data) {
      throw new Error('Audio file data is required');
    }

    if (!file.filename) {
      throw new Error('Audio filename is required');
    }

    // Validate file extension
    const extension = file.filename.toLowerCase().split('.').pop();
    const supportedExtensions = ['mp3', 'wav', 'flac', 'ogg', 'aac', 'opus', 'pcm', 'm4a', 'webm'];
    
    if (!extension || !supportedExtensions.includes(extension)) {
      throw new Error(`Unsupported audio format. Supported formats: ${supportedExtensions.join(', ')}`);
    }

    // Validate file size (25MB limit for most providers)
    const maxSize = 25 * 1024 * 1024; // 25MB
    let fileSize = 0;

    if (Buffer.isBuffer(file.data)) {
      fileSize = file.data.length;
    } else if (file.data instanceof Blob) {
      fileSize = file.data.size;
    } else if (typeof file.data === 'string') {
      // Assume base64 string
      fileSize = Math.ceil(file.data.length * 0.75); // Approximate decoded size
    }

    if (fileSize > maxSize) {
      throw new Error(`Audio file too large. Maximum size is ${maxSize / (1024 * 1024)}MB`);
    }
  }

  /**
   * Creates FormData for audio file uploads.
   * @protected
   */
  protected createAudioFormData(file: AudioFile, additionalFields: Record<string, unknown>): FormData {
    const formData = new FormData();
    
    // Handle different file data types
    let fileBlob: Blob;
    
    if (Buffer.isBuffer(file.data)) {
      fileBlob = new Blob([file.data], { type: file.contentType ?? 'audio/mpeg' });
    } else if (file.data instanceof Blob) {
      fileBlob = file.data;
    } else if (typeof file.data === 'string') {
      // Assume base64 string
      const binaryString = atob(file.data);
      const bytes = new Uint8Array(binaryString.length);
      for (let i = 0; i < binaryString.length; i++) {
        bytes[i] = binaryString.charCodeAt(i);
      }
      fileBlob = new Blob([bytes], { type: file.contentType ?? 'audio/mpeg' });
    } else {
      throw new Error('Unsupported file data type');
    }

    formData.append('file', fileBlob, file.filename);

    // Add additional fields
    Object.entries(additionalFields).forEach(([key, value]) => {
      if (value !== undefined) {
        if (typeof value === 'object') {
          formData.append(key, JSON.stringify(value));
        } else {
          formData.append(key, String(value));
        }
      }
    });

    return formData;
  }
}