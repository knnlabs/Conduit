import type {
  AudioFile,
  AudioFormat,
  AudioMetadata,
} from '../models/audio';

/**
 * Audio utility functions for working with audio files.
 * Provides helper methods for creating AudioFile objects from various sources.
 * 
 * @example
 * ```typescript
 * // From Buffer (Node.js)
 * const audioFile = AudioUtils.fromBuffer(buffer, 'audio.mp3', 'audio/mpeg');
 * 
 * // From Blob (Browser)
 * const audioFile = AudioUtils.fromBlob(blob, 'recording.wav');
 * 
 * // From Base64
 * const audioFile = AudioUtils.fromBase64(base64String, 'speech.mp3');
 * ```
 */
export class AudioUtils {
  /**
   * Creates an AudioFile from a Buffer with specified filename.
   */
  static fromBuffer(data: Buffer, filename: string, contentType?: string): AudioFile {
    return {
      data,
      filename,
      contentType,
    };
  }

  /**
   * Creates an AudioFile from a Blob with specified filename.
   */
  static fromBlob(data: Blob, filename: string): AudioFile {
    return {
      data,
      filename,
      contentType: data.type,
    };
  }

  /**
   * Creates an AudioFile from a base64 string with specified filename.
   */
  static fromBase64(data: string, filename: string, contentType?: string): AudioFile {
    return {
      data,
      filename,
      contentType,
    };
  }

  /**
   * Gets audio file metadata (basic validation).
   */
  static getBasicMetadata(file: AudioFile): AudioMetadata {
    let size = 0;
    
    if (Buffer.isBuffer(file.data)) {
      size = file.data.length;
    } else if (file.data instanceof Blob) {
      size = file.data.size;
    } else if (typeof file.data === 'string') {
      size = Math.ceil(file.data.length * 0.75);
    }

    const extension = file.filename.toLowerCase().split('.').pop() ?? 'unknown';
    
    return {
      duration: 0, // Would need audio analysis library to determine
      size,
      format: extension as AudioFormat,
      sample_rate: 0, // Would need audio analysis
      channels: 0, // Would need audio analysis
    };
  }

  /**
   * Validates if the audio format is supported.
   */
  static isFormatSupported(format: string): boolean {
    const supportedFormats = ['mp3', 'wav', 'flac', 'ogg', 'aac', 'opus', 'pcm', 'm4a', 'webm'];
    return supportedFormats.includes(format.toLowerCase());
  }

  /**
   * Gets the appropriate content type for an audio format.
   */
  static getContentType(format: AudioFormat): string {
    const contentTypes: Record<AudioFormat, string> = {
      mp3: 'audio/mpeg',
      wav: 'audio/wav',
      flac: 'audio/flac',
      ogg: 'audio/ogg',
      aac: 'audio/aac',
      opus: 'audio/opus',
      pcm: 'audio/pcm',
      m4a: 'audio/mp4',
      webm: 'audio/webm',
    };
    
    return contentTypes[format] || 'audio/mpeg';
  }
}