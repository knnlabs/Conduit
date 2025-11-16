// Download utilities
export {
  downloadMedia,
  createBlobFromBase64,
  createBlobFromUrl,
  triggerDownload,
  formatFileSize,
  getMimeTypeFromFilename,
  validateUrl,
  getFileSizeFromUrl,
  getBase64Size
} from './download';
export type {
  MediaDownloadOptions,
  DownloadResult
} from './download';

// Metadata utilities
export {
  ImageMetadataExtractor,
  VideoMetadataExtractor,
  MetadataCache,
  normalizeBackendVideoResponse
} from './metadata';
export type {
  MetadataExtractor,
  BackendVideoResponse
} from './metadata';

// Type guards
export {
  isVideoData,
  isStandardVideoResponse,
  isBackendVideoResponse,
  safeJsonParse,
  identifyVideoResponse,
  isDefined,
  isNonEmptyString,
  isPositiveNumber
} from './typeGuards';
export type {
  StandardVideoResponse,
  VideoResponse
} from './typeGuards';

// Response normalizer
export {
  normalizeVideoResponse,
  extractVideoData,
  extractVideoFromTaskResult,
  extractVideosFromTasks,
  validateVideoData,
  createVideoCacheKey
} from './responseNormalizer';