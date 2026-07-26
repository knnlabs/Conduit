/**
 * Media download utilities for handling various media download scenarios
 */

/**
 * Options for downloading media
 */
export interface MediaDownloadOptions {
  url?: string;
  b64_json?: string;
  filename: string;
  mimeType?: string;
}

/**
 * Result of a download operation
 */
export interface DownloadResult {
  success: boolean;
  error?: Error;
}

/**
 * Creates a Blob from base64 encoded data
 * @param b64Data - Base64 encoded string
 * @param mimeType - MIME type of the data
 * @returns Blob object
 */
export function createBlobFromBase64(b64Data: string, mimeType: string = 'application/octet-stream'): Blob {
  // Remove data URL prefix if present
  const base64 = b64Data.replace(/^data:[^;]+;base64,/, '');
  
  // Decode base64 string
  const byteCharacters = atob(base64);
  const byteNumbers = new Array(byteCharacters.length);
  
  for (let i = 0; i < byteCharacters.length; i++) {
    byteNumbers[i] = byteCharacters.charCodeAt(i);
  }
  
  const byteArray = new Uint8Array(byteNumbers);
  return new Blob([byteArray], { type: mimeType });
}

/**
 * Creates a Blob from a URL by fetching its content
 * @param url - URL to fetch
 * @returns Promise that resolves to a Blob
 */
export async function createBlobFromUrl(url: string): Promise<Blob> {
  const response = await fetch(url);
  
  if (!response.ok) {
    throw new Error(`Failed to fetch media: ${response.status} ${response.statusText}`);
  }
  
  return response.blob();
}

/**
 * Triggers a download in the browser
 * @param blob - Blob to download
 * @param filename - Name for the downloaded file
 */
export function triggerDownload(blob: Blob, filename: string): void {
  const blobUrl = URL.createObjectURL(blob);
  
  try {
    const link = document.createElement('a');
    link.href = blobUrl;
    link.download = filename;
    link.style.display = 'none';
    
    // Add to DOM, click, and remove
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  } finally {
    // Clean up the blob URL after a short delay to ensure download starts
    setTimeout(() => {
      URL.revokeObjectURL(blobUrl);
    }, 100);
  }
}

/**
 * Downloads media from either a URL or base64 data
 * @param options - Download options
 * @returns Promise that resolves when download is complete
 */
export async function downloadMedia(options: MediaDownloadOptions): Promise<DownloadResult> {
  const { url, b64_json, filename, mimeType = 'application/octet-stream' } = options;
  
  try {
    // Validate input
    if (!url && !b64_json) {
      throw new Error('Either url or b64_json must be provided');
    }
    
    if (!filename) {
      throw new Error('Filename is required');
    }
    
    let blob: Blob;
    
    if (url) {
      // Download from URL
      blob = await createBlobFromUrl(url);
    } else if (b64_json) {
      // Create blob from base64
      blob = createBlobFromBase64(b64_json, mimeType);
    } else {
      throw new Error('No media data available');
    }
    
    // Trigger download
    triggerDownload(blob, filename);
    
    return { success: true };
  } catch (error) {
    console.error('Download failed:', error);
    return { 
      success: false, 
      error: error instanceof Error ? error : new Error(String(error))
    };
  }
}

/**
 * Determines the appropriate MIME type based on file extension
 * @param filename - Name of the file
 * @returns MIME type string
 */
export function getMimeTypeFromFilename(filename: string): string {
  const extension = filename.split('.').pop()?.toLowerCase();
  
  const mimeTypes: Record<string, string> = {
    // Images
    'png': 'image/png',
    'jpg': 'image/jpeg',
    'jpeg': 'image/jpeg',
    'gif': 'image/gif',
    'webp': 'image/webp',
    'svg': 'image/svg+xml',
    
    // Videos
    'mp4': 'video/mp4',
    'webm': 'video/webm',
    'ogg': 'video/ogg',
    'mov': 'video/quicktime',
    'avi': 'video/x-msvideo',
    
    // Audio
    'mp3': 'audio/mpeg',
    'wav': 'audio/wav',
    'oga': 'audio/ogg',
    
    // Documents
    'pdf': 'application/pdf',
    'json': 'application/json',
    'xml': 'application/xml',
    'txt': 'text/plain',
  };
  
  return mimeTypes[extension ?? ''] ?? 'application/octet-stream';
}

/**
 * Validates if a URL is accessible
 * @param url - URL to validate
 * @returns Promise that resolves to boolean
 */
export async function validateUrl(url: string): Promise<boolean> {
  try {
    const response = await fetch(url, { method: 'HEAD' });
    return response.ok;
  } catch {
    return false;
  }
}

/**
 * Gets file size from URL headers
 * @param url - URL to check
 * @returns Promise that resolves to file size in bytes or null if not available
 */
export async function getFileSizeFromUrl(url: string): Promise<number | null> {
  try {
    const response = await fetch(url, { method: 'HEAD' });
    const contentLength = response.headers.get('content-length');
    return contentLength ? parseInt(contentLength, 10) : null;
  } catch {
    return null;
  }
}

/**
 * Calculates size of base64 encoded data
 * @param b64Data - Base64 encoded string
 * @returns Size in bytes
 */
export function getBase64Size(b64Data: string): number {
  // Remove data URL prefix if present
  const base64 = b64Data.replace(/^data:[^;]+;base64,/, '');
  
  // Calculate size
  const padding = (base64.match(/=/g) ?? []).length;
  return Math.floor((base64.length * 3) / 4) - padding;
}