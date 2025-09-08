import {
  downloadMedia,
  createBlobFromBase64,
  createBlobFromUrl,
  triggerDownload,
  formatFileSize,
  getMimeTypeFromFilename,
  validateUrl,
  getFileSizeFromUrl,
  getBase64Size
} from '../download';

// Mock fetch globally
global.fetch = jest.fn();

// Mock URL.createObjectURL and URL.revokeObjectURL
global.URL.createObjectURL = jest.fn(() => 'blob:mock-url');
global.URL.revokeObjectURL = jest.fn();

// Mock document.createElement and click
const mockClick = jest.fn();
const mockRemove = jest.fn();
const mockAppendChild = jest.fn();
const mockRemoveChild = jest.fn();

jest.spyOn(document, 'createElement').mockImplementation((tagName: string) => {
  if (tagName === 'a') {
    return {
      click: mockClick,
      remove: mockRemove,
      href: '',
      download: '',
      style: {}
    } as unknown as HTMLAnchorElement;
  }
  return document.createElement(tagName);
});

jest.spyOn(document.body, 'appendChild').mockImplementation(mockAppendChild);
jest.spyOn(document.body, 'removeChild').mockImplementation(mockRemoveChild);

describe('download utilities', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('formatFileSize', () => {
    it('should format bytes correctly', () => {
      expect(formatFileSize(0)).toBe('0 B');
      expect(formatFileSize(512)).toBe('512 B');
      expect(formatFileSize(1024)).toBe('1.0 KB');
      expect(formatFileSize(1536)).toBe('1.5 KB');
      expect(formatFileSize(1048576)).toBe('1.0 MB');
      expect(formatFileSize(5242880)).toBe('5.0 MB');
      expect(formatFileSize(1073741824)).toBe('1.0 GB');
    });

    it('should handle negative numbers', () => {
      expect(formatFileSize(-1024)).toBe('-1.0 KB');
    });
  });

  describe('getMimeTypeFromFilename', () => {
    it('should return correct MIME types for common extensions', () => {
      expect(getMimeTypeFromFilename('image.jpg')).toBe('image/jpeg');
      expect(getMimeTypeFromFilename('image.jpeg')).toBe('image/jpeg');
      expect(getMimeTypeFromFilename('image.png')).toBe('image/png');
      expect(getMimeTypeFromFilename('image.gif')).toBe('image/gif');
      expect(getMimeTypeFromFilename('image.webp')).toBe('image/webp');
      expect(getMimeTypeFromFilename('video.mp4')).toBe('video/mp4');
      expect(getMimeTypeFromFilename('video.webm')).toBe('video/webm');
      expect(getMimeTypeFromFilename('video.avi')).toBe('video/avi');
    });

    it('should be case insensitive', () => {
      expect(getMimeTypeFromFilename('IMAGE.JPG')).toBe('image/jpeg');
      expect(getMimeTypeFromFilename('Video.MP4')).toBe('video/mp4');
    });

    it('should return default for unknown extensions', () => {
      expect(getMimeTypeFromFilename('file.unknown')).toBe('application/octet-stream');
      expect(getMimeTypeFromFilename('file')).toBe('application/octet-stream');
    });
  });

  describe('validateUrl', () => {
    it('should validate correct URLs', () => {
      expect(validateUrl('https://example.com/image.jpg')).toBe(true);
      expect(validateUrl('http://example.com/image.jpg')).toBe(true);
      expect(validateUrl('https://cdn.example.com/path/to/image.png')).toBe(true);
    });

    it('should reject invalid URLs', () => {
      expect(validateUrl('not-a-url')).toBe(false);
      expect(validateUrl('ftp://example.com')).toBe(false);
      expect(validateUrl('')).toBe(false);
      expect(validateUrl('javascript:alert(1)')).toBe(false);
    });
  });

  describe('getBase64Size', () => {
    it('should calculate base64 string size', () => {
      const base64 = 'SGVsbG8gV29ybGQ='; // "Hello World"
      expect(getBase64Size(base64)).toBe(11); // Actual decoded size
    });

    it('should handle empty strings', () => {
      expect(getBase64Size('')).toBe(0);
    });

    it('should handle strings with data URL prefix', () => {
      const dataUrl = 'data:image/png;base64,SGVsbG8gV29ybGQ=';
      expect(getBase64Size(dataUrl)).toBe(11);
    });
  });

  describe('createBlobFromBase64', () => {
    it('should create blob from base64 string', () => {
      const base64 = 'SGVsbG8gV29ybGQ='; // "Hello World"
      const blob = createBlobFromBase64(base64, 'text/plain');
      
      expect(blob).toBeInstanceOf(Blob);
      expect(blob.type).toBe('text/plain');
      expect(blob.size).toBeGreaterThan(0);
    });

    it('should handle base64 with data URL prefix', () => {
      const dataUrl = 'data:text/plain;base64,SGVsbG8gV29ybGQ=';
      const blob = createBlobFromBase64(dataUrl, 'text/plain');
      
      expect(blob).toBeInstanceOf(Blob);
      expect(blob.type).toBe('text/plain');
    });
  });

  describe('createBlobFromUrl', () => {
    it('should create blob from URL', async () => {
      const mockBlob = new Blob(['test data'], { type: 'text/plain' });
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        blob: jest.fn().mockResolvedValue(mockBlob)
      });

      const result = await createBlobFromUrl('https://example.com/file.txt');
      
      expect(result).toBe(mockBlob);
      expect(global.fetch).toHaveBeenCalledWith('https://example.com/file.txt');
    });

    it('should throw error for failed fetch', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 404
      });

      await expect(createBlobFromUrl('https://example.com/404.txt'))
        .rejects.toThrow('Failed to fetch');
    });
  });

  describe('triggerDownload', () => {
    it('should trigger download with blob and filename', () => {
      const blob = new Blob(['test'], { type: 'text/plain' });
      triggerDownload(blob, 'test.txt');
      
      expect(global.URL.createObjectURL).toHaveBeenCalledWith(blob);
      expect(mockClick).toHaveBeenCalled();
      expect(global.URL.revokeObjectURL).toHaveBeenCalledWith('blob:mock-url');
    });
  });

  describe('downloadMedia', () => {
    it('should download from URL', async () => {
      const mockBlob = new Blob(['test'], { type: 'image/jpeg' });
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        blob: jest.fn().mockResolvedValue(mockBlob)
      });

      const result = await downloadMedia({
        url: 'https://example.com/image.jpg',
        filename: 'downloaded.jpg'
      });

      expect(result.success).toBe(true);
      expect(result.filename).toBe('downloaded.jpg');
      expect(result.mimeType).toBe('image/jpeg');
      expect(result.size).toBe(mockBlob.size);
    });

    it('should download from base64', async () => {
      const result = await downloadMedia({
        b64_json: 'SGVsbG8gV29ybGQ=',
        filename: 'test.txt',
        mimeType: 'text/plain'
      });

      expect(result.success).toBe(true);
      expect(result.filename).toBe('test.txt');
      expect(result.mimeType).toBe('text/plain');
    });

    it('should generate filename if not provided', async () => {
      const result = await downloadMedia({
        b64_json: 'SGVsbG8gV29ybGQ=',
        mimeType: 'text/plain'
      });

      expect(result.success).toBe(true);
      expect(result.filename).toMatch(/^download-\d+\.txt$/);
    });

    it('should handle errors gracefully', async () => {
      (global.fetch as jest.Mock).mockRejectedValue(new Error('Network error'));

      const result = await downloadMedia({
        url: 'https://example.com/error.jpg'
      });

      expect(result.success).toBe(false);
      expect(result.error).toBe('Network error');
    });

    it('should reject if neither URL nor base64 provided', async () => {
      const result = await downloadMedia({
        filename: 'test.jpg'
      });

      expect(result.success).toBe(false);
      expect(result.error).toBe('No URL or base64 data provided');
    });
  });

  describe('getFileSizeFromUrl', () => {
    it('should get file size from URL headers', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        headers: {
          get: jest.fn((header) => {
            if (header === 'content-length') return '1024';
            return null;
          })
        }
      });

      const size = await getFileSizeFromUrl('https://example.com/file.jpg');
      expect(size).toBe(1024);
    });

    it('should return 0 if content-length not available', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        headers: {
          get: jest.fn(() => null)
        }
      });

      const size = await getFileSizeFromUrl('https://example.com/file.jpg');
      expect(size).toBe(0);
    });

    it('should return 0 on fetch error', async () => {
      (global.fetch as jest.Mock).mockRejectedValue(new Error('Network error'));

      const size = await getFileSizeFromUrl('https://example.com/file.jpg');
      expect(size).toBe(0);
    });
  });
});