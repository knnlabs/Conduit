import {
  normalizeVideoResponse,
  extractVideoData,
  extractVideoFromTaskResult,
  extractVideosFromTasks,
  validateVideoData,
  createVideoCacheKey
} from '../responseNormalizer';
import type { VideoData, VideoGenerationResult } from '@/app/videos/types';

describe('Response Normalizer', () => {
  describe('normalizeVideoResponse', () => {
    it('should normalize standard response format', () => {
      const response = {
        data: [
          { url: 'https://example.com/video.mp4' }
        ],
        usage: { total_tokens: 100 }
      };
      
      const result = normalizeVideoResponse(response);
      expect(result).toBeDefined();
      expect(result?.data).toHaveLength(1);
      expect(result?.data[0]).toEqual({ url: 'https://example.com/video.mp4' });
      expect(result?.usage).toEqual({ total_tokens: 100 });
    });

    it('should normalize backend response format', () => {
      const response = {
        VideoUrl: 'https://example.com/video.mp4',
        Duration: 120,
        Resolution: '1920x1080',
        FileSize: 1024000
      };
      
      const result = normalizeVideoResponse(response);
      expect(result).toBeDefined();
      expect(result?.data).toHaveLength(1);
      expect(result?.data[0]?.url).toBe('https://example.com/video.mp4');
      expect(result?.data[0]?.metadata?.duration).toBe(120);
    });

    it('should handle stringified JSON', () => {
      const response = JSON.stringify({
        data: [{ url: 'https://example.com/video.mp4' }]
      });
      
      const result = normalizeVideoResponse(response);
      expect(result).toBeDefined();
      expect(result?.data).toHaveLength(1);
    });

    it('should return null for unknown formats', () => {
      const response = { unknown: 'format' };
      const result = normalizeVideoResponse(response);
      expect(result).toBeNull();
    });
  });

  describe('extractVideoData', () => {
    it('should extract first video from result', () => {
      const result: VideoGenerationResult = {
        created: Date.now(),
        data: [
          { url: 'https://example.com/video1.mp4' },
          { url: 'https://example.com/video2.mp4' }
        ]
      };
      
      const video = extractVideoData(result);
      expect(video).toBeDefined();
      expect(video?.url).toBe('https://example.com/video1.mp4');
    });

    it('should return null for empty or invalid results', () => {
      expect(extractVideoData(null)).toBeNull();
      expect(extractVideoData(undefined)).toBeNull();
      expect(extractVideoData({ created: Date.now(), data: [] })).toBeNull();
      expect(extractVideoData({ created: Date.now(), data: null as unknown as VideoData[] })).toBeNull();
    });
  });

  describe('extractVideoFromTaskResult', () => {
    it('should extract video from standard result', () => {
      const result = {
        data: [{ url: 'https://example.com/video.mp4' }]
      };
      
      const video = extractVideoFromTaskResult(result);
      expect(video).toBeDefined();
      expect(video?.url).toBe('https://example.com/video.mp4');
    });

    it('should extract video from backend result', () => {
      const result = {
        VideoUrl: 'https://example.com/video.mp4',
        Duration: 120
      };
      
      const video = extractVideoFromTaskResult(result);
      expect(video).toBeDefined();
      expect(video?.url).toBe('https://example.com/video.mp4');
    });

    it('should handle stringified results', () => {
      const result = JSON.stringify({
        data: [{ url: 'https://example.com/video.mp4' }]
      });
      
      const video = extractVideoFromTaskResult(result);
      expect(video).toBeDefined();
      expect(video?.url).toBe('https://example.com/video.mp4');
    });

    it('should return null for invalid results', () => {
      expect(extractVideoFromTaskResult(null)).toBeNull();
      expect(extractVideoFromTaskResult(undefined)).toBeNull();
      expect(extractVideoFromTaskResult({})).toBeNull();
      expect(extractVideoFromTaskResult('invalid json')).toBeNull();
    });
  });

  describe('extractVideosFromTasks', () => {
    it('should extract videos from multiple tasks', () => {
      const tasks = [
        { result: { data: [{ url: 'video1.mp4' }] } },
        { result: { VideoUrl: 'video2.mp4' } },
        { result: null },
        { result: { unknown: 'format' } }
      ];
      
      const videos = extractVideosFromTasks(tasks);
      expect(videos).toHaveLength(4);
      expect(videos[0]?.url).toBe('video1.mp4');
      expect(videos[1]?.url).toBe('video2.mp4');
      expect(videos[2]).toBeNull();
      expect(videos[3]).toBeNull();
    });
  });

  describe('validateVideoData', () => {
    it('should validate correct video data', () => {
      expect(validateVideoData({ url: 'https://example.com/video.mp4' })).toBe(true);
      expect(validateVideoData({ b64_json: 'base64string' })).toBe(true);
      expect(validateVideoData({ 
        url: 'https://example.com/video.mp4',
        b64_json: 'base64string' 
      })).toBe(true);
    });

    it('should reject invalid video data', () => {
      expect(validateVideoData({} as VideoData)).toBe(false);
      expect(validateVideoData({ url: null } as unknown as VideoData)).toBe(false);
      expect(validateVideoData({ url: 123 } as unknown as VideoData)).toBe(false);
      expect(validateVideoData({ b64_json: 123 } as unknown as VideoData)).toBe(false);
    });
  });

  describe('createVideoCacheKey', () => {
    it('should create cache key from url', () => {
      const video: VideoData = { url: 'https://example.com/video.mp4' };
      expect(createVideoCacheKey(video)).toBe('https://example.com/video.mp4');
    });

    it('should create cache key from b64_json', () => {
      const video: VideoData = { b64_json: 'base64string' };
      expect(createVideoCacheKey(video)).toBe('base64string');
    });

    it('should prioritize url over b64_json', () => {
      const video: VideoData = { 
        url: 'https://example.com/video.mp4',
        b64_json: 'base64string' 
      };
      expect(createVideoCacheKey(video)).toBe('https://example.com/video.mp4');
    });

    it('should use fallback id', () => {
      const video: VideoData = {} as VideoData;
      expect(createVideoCacheKey(video, 'fallback-id')).toBe('fallback-id');
    });

    it('should return unknown if no valid key', () => {
      const video: VideoData = {} as VideoData;
      expect(createVideoCacheKey(video)).toBe('unknown');
    });
  });
});