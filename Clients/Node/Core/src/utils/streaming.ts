import type { SSEMessage, StreamEvent } from '../models/streaming';
import type { ChatCompletionChunk } from '../models/chat';
import { StreamError } from './errors';
import { STREAM_CONSTANTS, StreamingHelpers } from '../constants';

export function parseSSEMessage(line: string): SSEMessage | null {
  if (!line || StreamingHelpers.isCommentLine(line)) {
    return null;
  }

  const message: SSEMessage = { data: '' };
  const colonIndex = line.indexOf(':');

  if (colonIndex === -1) {
    message.data = line;
  } else {
    const field = line.substring(0, colonIndex);
    const value = line.substring(colonIndex + 1).trim();

    switch (field) {
      case 'data':
        message.data = value;
        break;
      case 'event':
        message.event = value;
        break;
      case 'id':
        message.id = value;
        break;
      case 'retry':
        message.retry = parseInt(value, 10);
        break;
    }
  }

  return message;
}

export function parseSSEStream(text: string): SSEMessage[] {
  const lines = text.split('\n');
  const messages: SSEMessage[] = [];
  let currentMessage: Partial<SSEMessage> = {};

  for (const line of lines) {
    if (line.trim() === '') {
      if (currentMessage.data !== undefined) {
        messages.push(currentMessage as SSEMessage);
        currentMessage = {};
      }
      continue;
    }

    const parsed = parseSSEMessage(line);
    if (parsed) {
      Object.assign(currentMessage, parsed);
    }
  }

  if (currentMessage.data !== undefined) {
    messages.push(currentMessage as SSEMessage);
  }

  return messages;
}

export function parseStreamEvent(data: string): StreamEvent | null {
  if (StreamingHelpers.isDoneMarker(data)) {
    return '[DONE]';
  }

  try {
    return JSON.parse(data) as ChatCompletionChunk;
  } catch (error) {
    throw new StreamError(`Failed to parse stream event: ${data}`);
  }
}

export async function* streamAsyncIterator(
  stream: NodeJS.ReadableStream
): AsyncGenerator<ChatCompletionChunk, void, unknown> {
  let buffer = '';

  for await (const chunk of stream) {
    buffer += chunk.toString();
    const lines = buffer.split('\n');
    
    buffer = lines.pop() || '';

    for (const line of lines) {
      const trimmedLine = line.trim();
      if (trimmedLine === '' || StreamingHelpers.isCommentLine(trimmedLine)) {
        continue;
      }

      if (StreamingHelpers.isDataLine(trimmedLine)) {
        const data = StreamingHelpers.extractData(trimmedLine);
        
        if (StreamingHelpers.isDoneMarker(data)) {
          return;
        }

        try {
          const event = JSON.parse(data) as ChatCompletionChunk;
          yield event;
        } catch (error) {
          console.error('Failed to parse SSE data:', data);
          throw new StreamError(`Failed to parse stream event: ${data}`);
        }
      }
    }
  }

  if (buffer.trim()) {
    console.warn('Unprocessed data in buffer:', buffer);
  }
}