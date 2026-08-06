import { formatters } from '@/lib/utils/formatters';

// Download utilities
export {
  downloadMedia,
  createBlobFromBase64,
  createBlobFromUrl,
  triggerDownload,
  getMimeTypeFromFilename,
  validateUrl,
  getFileSizeFromUrl,
  getBase64Size
} from './download';

// File-size formatting comes straight from the canonical shared formatter.
export const formatFileSize = formatters.fileSize;
export type {
  MediaDownloadOptions,
  DownloadResult
} from './download';

// Metadata utilities (single source of truth in ../MediaMetadata)
export {
  ImageMetadataExtractor,
  VideoMetadataExtractor,
  MetadataCache,
  normalizeBackendVideoResponse
} from '../MediaMetadata';
export type {
  MetadataExtractor,
  BackendVideoResponse
} from '../MediaMetadata';

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