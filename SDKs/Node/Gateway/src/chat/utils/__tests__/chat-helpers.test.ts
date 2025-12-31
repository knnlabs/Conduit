/**
 * Unit tests for chat-helpers utilities
 */

import { buildMessageContent, type ImageAttachment } from '../chat-helpers';
import type { TextContent, ImageContent } from '../../../models/chat';

describe('chat-helpers', () => {
  describe('buildMessageContent', () => {
    it('should return text string when no images provided', () => {
      const text = 'Hello, world!';
      const result = buildMessageContent(text);
      
      expect(result).toBe(text);
    });

    it('should return text string when images array is empty', () => {
      const text = 'Hello, world!';
      const result = buildMessageContent(text, []);
      
      expect(result).toBe(text);
    });

    it('should build content array with text and base64 images', () => {
      const text = 'Look at this image:';
      const images: ImageAttachment[] = [
        {
          url: 'https://example.com/image.jpg',
          base64: 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==',
          mimeType: 'image/png',
          size: 1024,
          name: 'test.png'
        }
      ];

      const result = buildMessageContent(text, images);
      
      expect(Array.isArray(result)).toBe(true);
      expect(result).toHaveLength(2);
      
      const contentArray = result as Array<TextContent | ImageContent>;
      expect(contentArray[0]).toEqual({
        type: 'text',
        text: 'Look at this image:'
      });
      
      expect(contentArray[1]).toEqual({
        type: 'image_url',
        image_url: {
          url: 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg=='
        }
      });
    });

    it('should build content array with text and URL images', () => {
      const text = 'Check this out:';
      const images: ImageAttachment[] = [
        {
          url: 'https://example.com/image.jpg',
          mimeType: 'image/jpeg',
          size: 2048,
          name: 'example.jpg'
        }
      ];

      const result = buildMessageContent(text, images);
      
      expect(Array.isArray(result)).toBe(true);
      expect(result).toHaveLength(2);
      
      const contentArray = result as Array<TextContent | ImageContent>;
      expect(contentArray[0]).toEqual({
        type: 'text',
        text: 'Check this out:'
      });
      
      expect(contentArray[1]).toEqual({
        type: 'image_url',
        image_url: {
          url: 'https://example.com/image.jpg'
        }
      });
    });

    it('should handle multiple images', () => {
      const text = 'Multiple images:';
      const images: ImageAttachment[] = [
        {
          url: 'https://example.com/image1.jpg',
          mimeType: 'image/jpeg',
          size: 2048,
          name: 'image1.jpg'
        },
        {
          url: 'https://example.com/image2.png',
          base64: 'base64data',
          mimeType: 'image/png',
          size: 1024,
          name: 'image2.png'
        }
      ];

      const result = buildMessageContent(text, images);
      
      expect(Array.isArray(result)).toBe(true);
      expect(result).toHaveLength(3); // 1 text + 2 images
    });

    it('should handle empty text with images', () => {
      const text = '';
      const images: ImageAttachment[] = [
        {
          url: 'https://example.com/image.jpg',
          mimeType: 'image/jpeg',
          size: 2048,
          name: 'image.jpg'
        }
      ];

      const result = buildMessageContent(text, images);
      
      expect(Array.isArray(result)).toBe(true);
      expect(result).toHaveLength(1); // Only the image, no empty text
      
      const contentArray = result as Array<TextContent | ImageContent>;
      expect(contentArray[0]).toEqual({
        type: 'image_url',
        image_url: {
          url: 'https://example.com/image.jpg'
        }
      });
    });

    it('should prioritize base64 over URL when both are present', () => {
      const text = 'Image with both URL and base64:';
      const images: ImageAttachment[] = [
        {
          url: 'https://example.com/image.jpg',
          base64: 'base64data',
          mimeType: 'image/jpeg',
          size: 2048,
          name: 'image.jpg'
        }
      ];

      const result = buildMessageContent(text, images);
      
      const contentArray = result as Array<TextContent | ImageContent>;
      expect(contentArray[1]).toEqual({
        type: 'image_url',
        image_url: {
          url: 'data:image/jpeg;base64,base64data'
        }
      });
    });
  });
});