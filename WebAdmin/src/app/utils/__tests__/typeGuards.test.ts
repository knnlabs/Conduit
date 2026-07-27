import {
  isVideoData,
  isStandardVideoResponse,
  isBackendVideoResponse,
  safeJsonParse,
  identifyVideoResponse,
  isDefined,
  isNonEmptyString,
  isPositiveNumber
} from '@/app/components/media/utils/typeGuards';

describe('Type Guards', () => {
  describe('isVideoData', () => {
    it('should return true for valid VideoData with url', () => {
      const data = { url: 'https://example.com/video.mp4' };
      expect(isVideoData(data)).toBe(true);
    });

    it('should return true for valid VideoData with b64_json', () => {
      const data = { b64_json: 'base64string' };
      expect(isVideoData(data)).toBe(true);
    });

    it('should return true for VideoData with both url and b64_json', () => {
      const data = { 
        url: 'https://example.com/video.mp4',
        b64_json: 'base64string' 
      };
      expect(isVideoData(data)).toBe(true);
    });

    it('should return false for invalid data', () => {
      expect(isVideoData(null)).toBe(false);
      expect(isVideoData(undefined)).toBe(false);
      expect(isVideoData({})).toBe(false);
      expect(isVideoData({ invalid: 'data' })).toBe(false);
      expect(isVideoData('string')).toBe(false);
      expect(isVideoData(123)).toBe(false);
    });
  });

  describe('isStandardVideoResponse', () => {
    it('should return true for valid standard response', () => {
      const response = {
        data: [
          { url: 'https://example.com/video.mp4' }
        ]
      };
      expect(isStandardVideoResponse(response)).toBe(true);
    });

    it('should return true for empty data array', () => {
      const response = { data: [] };
      expect(isStandardVideoResponse(response)).toBe(true);
    });

    it('should return false for invalid responses', () => {
      expect(isStandardVideoResponse(null)).toBe(false);
      expect(isStandardVideoResponse(undefined)).toBe(false);
      expect(isStandardVideoResponse({})).toBe(false);
      expect(isStandardVideoResponse({ data: 'not array' })).toBe(false);
      expect(isStandardVideoResponse({ data: null })).toBe(false);
    });

    it('should return false if data items are invalid', () => {
      const response = {
        data: [{ invalid: 'item' }]
      };
      expect(isStandardVideoResponse(response)).toBe(false);
    });
  });

  describe('isBackendVideoResponse', () => {
    it('should return true for valid backend response', () => {
      const response = {
        VideoUrl: 'https://example.com/video.mp4',
        Duration: 120,
        Resolution: '1920x1080'
      };
      expect(isBackendVideoResponse(response)).toBe(true);
    });

    it('should return true with only VideoUrl', () => {
      const response = { VideoUrl: 'https://example.com/video.mp4' };
      expect(isBackendVideoResponse(response)).toBe(true);
    });

    it('should return false for invalid responses', () => {
      expect(isBackendVideoResponse(null)).toBe(false);
      expect(isBackendVideoResponse(undefined)).toBe(false);
      expect(isBackendVideoResponse({})).toBe(false);
      expect(isBackendVideoResponse({ url: 'wrong key' })).toBe(false);
      expect(isBackendVideoResponse({ VideoUrl: 123 })).toBe(false);
    });
  });

  describe('safeJsonParse', () => {
    it('should parse valid JSON strings', () => {
      const json = '{"key": "value"}';
      const result = safeJsonParse(json);
      expect(result).toEqual({ key: 'value' });
    });

    it('should return original value for invalid JSON', () => {
      const invalid = 'not json';
      expect(safeJsonParse(invalid)).toBe(invalid);
    });

    it('should return non-string values as-is', () => {
      const obj = { key: 'value' };
      expect(safeJsonParse(obj)).toBe(obj);
      expect(safeJsonParse(123)).toBe(123);
      expect(safeJsonParse(null)).toBe(null);
    });
  });

  describe('identifyVideoResponse', () => {
    it('should identify standard response', () => {
      const response = {
        data: [{ url: 'https://example.com/video.mp4' }]
      };
      const result = identifyVideoResponse(response);
      expect(result.type).toBe('standard');
      expect(result.value).toEqual(response);
    });

    it('should identify backend response', () => {
      const response = {
        VideoUrl: 'https://example.com/video.mp4'
      };
      const result = identifyVideoResponse(response);
      expect(result.type).toBe('backend');
      expect(result.value).toEqual(response);
    });

    it('should identify unknown response', () => {
      const response = { unknown: 'format' };
      const result = identifyVideoResponse(response);
      expect(result.type).toBe('unknown');
      expect(result.value).toEqual(response);
    });

    it('should parse stringified JSON', () => {
      const response = JSON.stringify({
        data: [{ url: 'https://example.com/video.mp4' }]
      });
      const result = identifyVideoResponse(response);
      expect(result.type).toBe('standard');
    });
  });

  describe('isDefined', () => {
    it('should return true for defined values', () => {
      expect(isDefined(0)).toBe(true);
      expect(isDefined('')).toBe(true);
      expect(isDefined(false)).toBe(true);
      expect(isDefined([])).toBe(true);
      expect(isDefined({})).toBe(true);
    });

    it('should return false for null and undefined', () => {
      expect(isDefined(null)).toBe(false);
      expect(isDefined(undefined)).toBe(false);
    });
  });

  describe('isNonEmptyString', () => {
    it('should return true for non-empty strings', () => {
      expect(isNonEmptyString('hello')).toBe(true);
      expect(isNonEmptyString(' hello ')).toBe(true);
    });

    it('should return false for empty strings and non-strings', () => {
      expect(isNonEmptyString('')).toBe(false);
      expect(isNonEmptyString(' ')).toBe(false); // whitespace-only counts as empty (trimmed)
      expect(isNonEmptyString(null)).toBe(false);
      expect(isNonEmptyString(undefined)).toBe(false);
      expect(isNonEmptyString(123)).toBe(false);
      expect(isNonEmptyString([])).toBe(false);
    });
  });

  describe('isPositiveNumber', () => {
    it('should return true for positive numbers', () => {
      expect(isPositiveNumber(1)).toBe(true);
      expect(isPositiveNumber(0.5)).toBe(true);
      expect(isPositiveNumber(1000)).toBe(true);
    });

    it('should return false for invalid numbers', () => {
      expect(isPositiveNumber(0)).toBe(false);
      expect(isPositiveNumber(-1)).toBe(false);
      expect(isPositiveNumber(Infinity)).toBe(false);
      expect(isPositiveNumber(NaN)).toBe(false);
      expect(isPositiveNumber('1')).toBe(false);
      expect(isPositiveNumber(null)).toBe(false);
    });
  });
});