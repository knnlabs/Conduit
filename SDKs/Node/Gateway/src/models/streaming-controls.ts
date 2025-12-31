import type { StreamingResponse, BaseStreamChunk } from './streaming';
import type { ChatCompletionChunk } from './chat';

export interface StreamControlOptions {
  onProgress?: (tokens: number, tps: number) => void;
  onPause?: () => void;
  onResume?: () => void;
  onCancel?: () => void;
}

export interface ControllableStream<T extends BaseStreamChunk> extends StreamingResponse<T> {
  pause(): void;
  resume(): void;
  cancel(): void;
  isPaused(): boolean;
  isCancelled(): boolean;
  getStats(): StreamStats;
}

export interface StreamStats {
  tokensReceived: number;
  startTime: number;
  endTime?: number;
  averageTPS: number;
  currentTPS: number;
  pausedDuration: number;
}

export class ControllableChatStream implements ControllableStream<ChatCompletionChunk> {
  private paused = false;
  private cancelled = false;
  private tokensReceived = 0;
  private startTime = Date.now();
  private endTime?: number;
  private pauseStartTime?: number;
  private totalPausedDuration = 0;
  private lastTokenTime = Date.now();
  private tpsWindow: number[] = [];
  private readonly baseStream: StreamingResponse<ChatCompletionChunk>;
  private readonly options: StreamControlOptions;
  
  constructor(
    baseStream: StreamingResponse<ChatCompletionChunk>,
    options: StreamControlOptions = {}
  ) {
    this.baseStream = baseStream;
    this.options = options;
  }

  async *[Symbol.asyncIterator](): AsyncIterator<ChatCompletionChunk> {
    try {
      for await (const chunk of this.baseStream) {
        // Check if cancelled
        if (this.cancelled) {
          this.options.onCancel?.();
          break;
        }

        // Wait while paused
        while (this.paused && !this.cancelled) {
          await new Promise(resolve => setTimeout(resolve, 100));
        }

        // Process chunk
        if (!this.cancelled) {
          this.updateStats(chunk);
          yield chunk;
        }
      }
    } finally {
      this.endTime = Date.now();
    }
  }

  pause(): void {
    if (!this.paused && !this.cancelled) {
      this.paused = true;
      this.pauseStartTime = Date.now();
      this.options.onPause?.();
    }
  }

  resume(): void {
    if (this.paused && !this.cancelled) {
      this.paused = false;
      if (this.pauseStartTime) {
        this.totalPausedDuration += Date.now() - this.pauseStartTime;
        this.pauseStartTime = undefined;
      }
      this.options.onResume?.();
    }
  }

  cancel(): void {
    if (!this.cancelled) {
      this.cancelled = true;
      this.endTime = Date.now();
      // Clean up the base stream if it has a cancel method
      if ('cancel' in this.baseStream && typeof this.baseStream.cancel === 'function') {
        const cancellableStream = this.baseStream as { cancel: () => void };
        cancellableStream.cancel();
      }
    }
  }

  isPaused(): boolean {
    return this.paused;
  }

  isCancelled(): boolean {
    return this.cancelled;
  }

  getStats(): StreamStats {
    const now = Date.now();
    const duration = (this.endTime ?? now) - this.startTime - this.totalPausedDuration;
    const averageTPS = duration > 0 ? (this.tokensReceived / duration) * 1000 : 0;
    
    return {
      tokensReceived: this.tokensReceived,
      startTime: this.startTime,
      endTime: this.endTime,
      averageTPS,
      currentTPS: this.calculateCurrentTPS(),
      pausedDuration: this.totalPausedDuration
    };
  }

  private updateStats(chunk: ChatCompletionChunk): void {
    // Count tokens (approximate - would need proper tokenizer for accuracy)
    const content = chunk.choices?.[0]?.delta?.content ?? '';
    const tokenCount = Math.ceil(content.length / 4); // Rough approximation
    this.tokensReceived += tokenCount;

    // Update TPS tracking
    const now = Date.now();
    const timeSinceLastToken = now - this.lastTokenTime;
    if (timeSinceLastToken > 0 && tokenCount > 0) {
      const instantTPS = (tokenCount / timeSinceLastToken) * 1000;
      this.tpsWindow.push(instantTPS);
      if (this.tpsWindow.length > 10) {
        this.tpsWindow.shift();
      }
    }
    this.lastTokenTime = now;

    // Notify progress
    this.options.onProgress?.(this.tokensReceived, this.calculateCurrentTPS());
  }

  private calculateCurrentTPS(): number {
    if (this.tpsWindow.length === 0) return 0;
    const sum = this.tpsWindow.reduce((a, b) => a + b, 0);
    return sum / this.tpsWindow.length;
  }

  // Implement StreamingResponse methods
  async toArray(): Promise<ChatCompletionChunk[]> {
    const chunks: ChatCompletionChunk[] = [];
    for await (const chunk of this) {
      chunks.push(chunk);
    }
    return chunks;
  }

  async *map<U>(fn: (chunk: ChatCompletionChunk) => U | Promise<U>): AsyncGenerator<U, void, unknown> {
    for await (const chunk of this) {
      yield await fn(chunk);
    }
  }

  async *filter(predicate: (chunk: ChatCompletionChunk) => boolean | Promise<boolean>): AsyncGenerator<ChatCompletionChunk, void, unknown> {
    for await (const chunk of this) {
      if (await predicate(chunk)) {
        yield chunk;
      }
    }
  }

  async *take(n: number): AsyncGenerator<ChatCompletionChunk, void, unknown> {
    let count = 0;
    for await (const chunk of this) {
      if (count >= n) break;
      yield chunk;
      count++;
    }
  }

  async *skip(n: number): AsyncGenerator<ChatCompletionChunk, void, unknown> {
    let count = 0;
    for await (const chunk of this) {
      if (count >= n) {
        yield chunk;
      }
      count++;
    }
  }
}