/**
 * Metadata extraction utilities for media files
 */

import { MediaMetadata } from '@/app/types/media';
import { VideoData } from '@/app/videos/types';
import { GeneratedImage } from '@/app/images/types';

/**
 * Interface for metadata extractors
 */
export interface MetadataExtractor<T> {
  extract(media: T): Promise<MediaMetadata>;
}

/**
 * Extracts metadata from an image using the Image API
 */
export class ImageMetadataExtractor implements MetadataExtractor<GeneratedImage> {
  async extract(image: GeneratedImage): Promise<MediaMetadata> {
    const metadata: MediaMetadata = {};
    
    // Get image source
    const src = this.getImageSrc(image);
    if (!src) return metadata;

    try {
      // Extract dimensions using Image API
      const dimensions = await this.extractDimensions(src);
      if (dimensions) {
        metadata.width = dimensions.width;
        metadata.height = dimensions.height;
      }

      // Extract size and format
      if (image.b64_json) {
        metadata.sizeBytes = this.calculateBase64Size(image.b64_json);
        metadata.format = 'png'; // Base64 images are typically PNG
        metadata.mime_type = 'image/png';
      } else if (image.url) {
        const urlMetadata = await this.extractFromUrl(image.url);
        if (urlMetadata.sizeBytes) metadata.sizeBytes = urlMetadata.sizeBytes;
        if (urlMetadata.format) {
          metadata.format = urlMetadata.format;
          metadata.mime_type = `image/${urlMetadata.format}`;
        }
      }

      // Copy any existing metadata from the image
      if (image.width) metadata.width = image.width;
      if (image.height) metadata.height = image.height;
      if (image.sizeBytes) metadata.sizeBytes = image.sizeBytes;
      if (image.format) metadata.format = image.format;

    } catch (error) {
      console.warn('Failed to extract image metadata:', error);
    }

    return metadata;
  }

  private getImageSrc(image: GeneratedImage): string {
    if (image.url) {
      return image.url;
    } else if (image.b64_json) {
      return `data:image/png;base64,${image.b64_json}`;
    }
    return '';
  }

  private async extractDimensions(src: string): Promise<{ width: number; height: number } | null> {
    return new Promise((resolve) => {
      const img = new window.Image();
      img.onload = () => {
        resolve({ width: img.width, height: img.height });
      };
      img.onerror = () => {
        resolve(null);
      };
      img.src = src;
    });
  }

  private calculateBase64Size(b64Data: string): number {
    const base64 = b64Data.replace(/^data:[^;]+;base64,/, '');
    const padding = (base64.match(/=/g) ?? []).length;
    return Math.floor((base64.length * 3) / 4) - padding;
  }

  private async extractFromUrl(url: string): Promise<Partial<MediaMetadata>> {
    const metadata: Partial<MediaMetadata> = {};
    
    try {
      const response = await fetch(url, { method: 'HEAD' });
      
      const contentLength = response.headers.get('content-length');
      if (contentLength) {
        metadata.sizeBytes = parseInt(contentLength, 10);
      }
      
      const contentType = response.headers.get('content-type');
      if (contentType) {
        const format = contentType.split('/')[1];
        if (format) {
          metadata.format = format;
        }
      }
    } catch (error) {
      console.warn('Failed to fetch URL metadata:', error);
    }
    
    return metadata;
  }
}

/**
 * Extracts metadata from video data
 */
export class VideoMetadataExtractor implements MetadataExtractor<VideoData> {
  async extract(video: VideoData): Promise<MediaMetadata> {
    const metadata: MediaMetadata = {};

    // Copy existing metadata from the video object
    if (video.metadata) {
      // Copy all standard metadata fields
      if (video.metadata.duration !== undefined) metadata.duration = video.metadata.duration;
      if (video.metadata.resolution) metadata.resolution = video.metadata.resolution;
      if (video.metadata.width !== undefined) metadata.width = video.metadata.width;
      if (video.metadata.height !== undefined) metadata.height = video.metadata.height;
      if (video.metadata.fps !== undefined) metadata.fps = video.metadata.fps;
      if (video.metadata.file_size_bytes !== undefined) metadata.file_size_bytes = video.metadata.file_size_bytes;
      if (video.metadata.format) metadata.format = video.metadata.format;
      if (video.metadata.codec) metadata.codec = video.metadata.codec;
      if (video.metadata.audio_codec) metadata.audio_codec = video.metadata.audio_codec;
      if (video.metadata.bitrate !== undefined) metadata.bitrate = video.metadata.bitrate;
    }

    // Try to extract additional metadata from URL if available
    if (video.url && !metadata.file_size_bytes) {
      try {
        const response = await fetch(video.url, { method: 'HEAD' });
        const contentLength = response.headers.get('content-length');
        if (contentLength) {
          metadata.file_size_bytes = parseInt(contentLength, 10);
        }
      } catch (error) {
        console.warn('Failed to fetch video URL metadata:', error);
      }
    }

    // Calculate size from base64 if available
    if (video.b64_json && !metadata.file_size_bytes) {
      const base64 = video.b64_json.replace(/^data:[^;]+;base64,/, '');
      const padding = (base64.match(/=/g) ?? []).length;
      metadata.file_size_bytes = Math.floor((base64.length * 3) / 4) - padding;
    }

    // Set default format if not present
    if (!metadata.format) {
      metadata.format = 'mp4';
      metadata.mime_type = 'video/mp4';
    }

    return metadata;
  }
}

/**
 * Normalizes backend video response to standard VideoData format
 */
export interface BackendVideoResponse {
  VideoUrl?: string;
  Duration?: number;
  Resolution?: string;
  FileSize?: number;
  Fps?: number;
  Codec?: string;
  AudioCodec?: string;
  Bitrate?: number;
}

export function normalizeBackendVideoResponse(response: BackendVideoResponse): VideoData {
  return {
    url: response.VideoUrl,
    metadata: {
      duration: response.Duration,
      resolution: response.Resolution,
      file_size_bytes: response.FileSize,
      fps: response.Fps,
      codec: response.Codec,
      audio_codec: response.AudioCodec,
      bitrate: response.Bitrate
    }
  };
}

/**
 * Factory function to get the appropriate metadata extractor
 */
export function getMetadataExtractor<T>(type: 'image' | 'video'): MetadataExtractor<T> {
  switch (type) {
    case 'image':
      return new ImageMetadataExtractor() as unknown as MetadataExtractor<T>;
    case 'video':
      return new VideoMetadataExtractor() as unknown as MetadataExtractor<T>;
    default:
      throw new Error(`Unknown media type: ${String(type)}`);
  }
}

/**
 * Caches extracted metadata to avoid repeated extraction
 */
export class MetadataCache {
  private cache = new Map<string, MediaMetadata>();

  /**
   * Get cache key for a media item
   */
  private getKey(media: VideoData | GeneratedImage): string {
    // Use URL or base64 data as the cache key
    return media.url ?? media.b64_json ?? '';
  }

  /**
   * Get cached metadata
   */
  get(media: VideoData | GeneratedImage): MediaMetadata | undefined {
    const key = this.getKey(media);
    return key ? this.cache.get(key) : undefined;
  }

  /**
   * Set cached metadata
   */
  set(media: VideoData | GeneratedImage, metadata: MediaMetadata): void {
    const key = this.getKey(media);
    if (key) {
      this.cache.set(key, metadata);
    }
  }

  /**
   * Check if metadata is cached
   */
  has(media: VideoData | GeneratedImage): boolean {
    const key = this.getKey(media);
    return key ? this.cache.has(key) : false;
  }

  /**
   * Clear the cache
   */
  clear(): void {
    this.cache.clear();
  }

  /**
   * Get cache size
   */
  get size(): number {
    return this.cache.size;
  }
}

/**
 * Hook-friendly metadata extraction with caching
 */
export async function extractMetadataWithCache<T extends VideoData | GeneratedImage>(
  media: T,
  type: 'image' | 'video',
  cache: MetadataCache
): Promise<MediaMetadata> {
  // Check cache first
  const cached = cache.get(media);
  if (cached) {
    return cached;
  }

  // Extract metadata
  const extractor = getMetadataExtractor<T>(type);
  const metadata = await extractor.extract(media);

  // Cache the result
  cache.set(media, metadata);

  return metadata;
}