// MediaGallery components
export { 
  MediaGallery, 
  createMediaGallery,
  MediaCard, 
  MediaContent, 
  MediaPlaceholder
} from './MediaGallery';
export type { 
  MediaGalleryProps,
  MediaCardProps, 
  MediaContentProps, 
  MediaPlaceholderProps 
} from './MediaGallery';

// MediaPromptInput components
export { MediaPromptInput, useMediaPrompt } from './MediaPromptInput';
export type { MediaPromptInputProps } from './MediaPromptInput';

// MediaDownloader components
export { MediaDownloader } from './MediaDownloader';
export type { MediaDownloaderProps } from './MediaDownloader';

// MediaMetadata components
export {
  ImageMetadataExtractor,
  VideoMetadataExtractor,
  MetadataCache,
  normalizeBackendVideoResponse
} from './MediaMetadata';
export type {
  MetadataExtractor,
  BackendVideoResponse
} from './MediaMetadata';

// Utility exports
export {
  // Download utilities
  downloadMedia,
  createBlobFromBase64,
  createBlobFromUrl,
  triggerDownload,
  formatFileSize,
  getMimeTypeFromFilename,
  validateUrl,
  getFileSizeFromUrl,
  getBase64Size,
  // Type guards
  isVideoData,
  isStandardVideoResponse,
  isBackendVideoResponse,
  safeJsonParse,
  identifyVideoResponse,
  isDefined,
  isNonEmptyString,
  isPositiveNumber,
  // Response normalizer
  normalizeVideoResponse,
  extractVideoData,
  extractVideoFromTaskResult,
  extractVideosFromTasks,
  validateVideoData,
  createVideoCacheKey
} from './utils';

export type {
  MediaDownloadOptions,
  DownloadResult,
  StandardVideoResponse,
  VideoResponse
} from './utils';