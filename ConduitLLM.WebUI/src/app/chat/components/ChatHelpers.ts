import { 
  ImageAttachment, 
  ContentHelpers,
  type TextContent,
  type ImageContent,
  type MessageContent
} from '../types';

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