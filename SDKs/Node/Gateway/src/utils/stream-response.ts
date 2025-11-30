import type { BaseStreamChunk, StreamingResponse } from '../models/streaming';

/**
 * Creates a type-safe streaming response wrapper
 */
export class TypedStreamingResponse<T extends BaseStreamChunk> implements StreamingResponse<T> {
  private readonly stream: AsyncGenerator<T, void, unknown>;
  private abortController?: AbortController;

  constructor(stream: AsyncGenerator<T, void, unknown>, abortController?: AbortController) {
    this.stream = stream;
    this.abortController = abortController;
  }

  async *[Symbol.asyncIterator](): AsyncIterator<T> {
    for await (const chunk of this.stream) {
      yield chunk;
    }
  }

  async toArray(): Promise<T[]> {
    const chunks: T[] = [];
    for await (const chunk of this) {
      chunks.push(chunk);
    }
    return chunks;
  }

  async *map<U>(fn: (chunk: T) => U | Promise<U>): AsyncGenerator<U, void, unknown> {
    for await (const chunk of this) {
      yield await fn(chunk);
    }
  }

  async *filter(predicate: (chunk: T) => boolean | Promise<boolean>): AsyncGenerator<T, void, unknown> {
    for await (const chunk of this) {
      if (await predicate(chunk)) {
        yield chunk;
      }
    }
  }

  async *take(n: number): AsyncGenerator<T, void, unknown> {
    let count = 0;
    for await (const chunk of this) {
      if (count >= n) {
        break;
      }
      yield chunk;
      count++;
    }
  }

  async *skip(n: number): AsyncGenerator<T, void, unknown> {
    let count = 0;
    for await (const chunk of this) {
      if (count >= n) {
        yield chunk;
      } else {
        count++;
      }
    }
  }

  cancel(): void {
    if (this.abortController) {
      this.abortController.abort();
    }
  }
}

/**
 * Helper function to create a streaming response
 */
export function createStreamingResponse<T extends BaseStreamChunk>(
  stream: AsyncGenerator<T, void, unknown>,
  abortController?: AbortController
): StreamingResponse<T> {
  return new TypedStreamingResponse(stream, abortController);
}