/**
 * Chat utility functions for building message content
 * Extracted from WebUI for reuse in other applications
 */

import { ContentHelpers, type TextContent, type ImageContent, type MessageContent } from '../../models/chat';

/**
 * Image attachment interface for chat utilities
 */
export interface ImageAttachment {
  url: string;
  base64?: string;
  mimeType: string;
  size: number;
  name: string;
}

/**
 * Builds message content from text and optional images
 * @param text The text content
 * @param images Optional array of image attachments
 * @returns MessageContent suitable for chat API
 */
export function buildMessageContent(text: string, images?: ImageAttachment[]): MessageContent {
  if (!images || images.length === 0) {
    return text;
  }

  const content: Array<TextContent | ImageContent> = [];
  
  if (text) {
    content.push(ContentHelpers.text(text));
  }

  images.forEach(img => {
    if (img.base64) {
      content.push(ContentHelpers.imageBase64(img.base64, img.mimeType));
    } else if (img.url) {
      content.push(ContentHelpers.imageUrl(img.url));
    }
  });

  return content;
}