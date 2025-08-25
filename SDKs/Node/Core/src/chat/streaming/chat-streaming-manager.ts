/**
 * Framework-agnostic chat streaming manager
 * Extracted and refactored from WebUI ChatStreamingLogic
 */

import { v4 as uuidv4 } from 'uuid';
import { parseSSEStream, buildMessageContent, SSEEventType, type SSEEvent, type ImageAttachment } from '../utils';
import type {
  StreamingConfig,
  SendMessageOptions,
  StreamMessageOptions,
  StreamingCallbacks,
  ChatCompletionRequest,
  ChatCompletionResponse,
  ChatCompletionChunk,
  StreamingError,
  StreamState,
  StreamingPerformanceMetrics,
  UsageData,
  MetricsEventData,
  MessageMetadata
} from './types';
import type { MessageContent } from '../../models/chat';

/**
 * ChatStreamingManager provides framework-agnostic chat streaming functionality
 */
export class ChatStreamingManager {
  private config: Required<StreamingConfig>;
  private state: StreamState;

  constructor(config: StreamingConfig) {
    this.config = {
      timeoutMs: 300000, // 5 minutes default
      trackPerformanceMetrics: true,
      showTokensPerSecond: true,
      useServerMetrics: true,
      enableLogging: false,
      ...config
    };

    this.state = {
      isStreaming: false,
      totalContent: '',
      startTime: 0,
      metrics: {},
      abortController: null
    };
  }

  /**
   * Send a non-streaming message
   */
  async sendMessage(
    message: string,
    options: SendMessageOptions,
    images?: ImageAttachment[]
  ): Promise<ChatCompletionResponse> {
    if (this.state.isStreaming) {
      throw new Error('Another streaming request is in progress');
    }

    const request = this.buildRequest(message, { ...options, stream: false }, images);
    
    try {
      this.state.startTime = Date.now();
      const response = await this.makeRequest(request);

      if (!response.ok) {
        throw this.createStreamingError(`HTTP ${response.status}: ${response.statusText}`, response.status);
      }

      const data = await response.json() as ChatCompletionResponse;
      return data;
    } catch (error) {
      throw this.enhanceError(error);
    }
  }

  /**
   * Stream a message with real-time callbacks
   */
  async streamMessage(
    message: string,
    options: StreamMessageOptions,
    callbacks: StreamingCallbacks
  ): Promise<void> {
    if (this.state.isStreaming) {
      throw new Error('Another streaming request is already in progress');
    }

    this.state.isStreaming = true;
    this.state.totalContent = '';
    this.state.startTime = Date.now();
    this.state.metrics = {};

    try {
      // Create abort controller for this request
      const controller = new AbortController();
      this.state.abortController = controller;

      // Set timeout
      const timeoutId = setTimeout(() => {
        controller.abort();
        this.log('Request timed out after', this.config.timeoutMs, 'ms');
      }, this.config.timeoutMs);

      callbacks.onStart?.();

      const request = this.buildRequest(message, options, options.images);
      
      this.log('Sending chat request:', {
        model: options.model,
        messageCount: request.messages.length,
        dynamicParameters: options.dynamicParameters,
      });

      const response = await this.makeRequest(request, controller.signal);
      
      // Clear timeout once we get a response
      clearTimeout(timeoutId);

      if (!response.ok) {
        throw this.createStreamingError(`HTTP ${response.status}: ${response.statusText}`, response.status);
      }

      await this.handleStreamingResponse(response, callbacks);

    } catch (error) {
      if (this.isAbortError(error)) {
        callbacks.onAbort?.();
        return; // Don't throw for user-initiated aborts
      }
      
      const streamingError = this.enhanceError(error);
      callbacks.onError?.(streamingError);
      throw streamingError;
    } finally {
      this.state.isStreaming = false;
      this.state.abortController = null;
    }
  }

  /**
   * Abort the current streaming request
   */
  abort(): void {
    if (this.state.abortController) {
      this.state.abortController.abort();
      this.state.abortController = null;
    }
    this.state.isStreaming = false;
  }

  /**
   * Check if currently streaming
   */
  isStreaming(): boolean {
    return this.state.isStreaming;
  }

  /**
   * Get current streaming state (read-only)
   */
  getState(): Readonly<StreamState> {
    return { ...this.state };
  }

  /**
   * Build the chat completion request
   */
  private buildRequest(
    message: string,
    options: SendMessageOptions | StreamMessageOptions,
    images?: ImageAttachment[]
  ): ChatCompletionRequest {
    // Build message content
    const content = buildMessageContent(message.trim(), images);
    
    // Build messages array
    const messages: Array<{ role: 'system' | 'user' | 'assistant'; content: MessageContent }> = [];
    
    // Add system prompt if provided
    if (options.systemPrompt) {
      messages.push({ role: 'system', content: options.systemPrompt });
    }

    // Add conversation history if provided (for streaming)
    if ('messages' in options && options.messages) {
      for (const msg of options.messages) {
        messages.push({
          role: msg.role,
          content: msg.images && msg.images.length > 0 
            ? buildMessageContent(msg.content, msg.images)
            : msg.content
        });
      }
    }

    // Add current message
    messages.push({ role: 'user', content });

    // Build request
    const request: ChatCompletionRequest = {
      messages,
      model: options.model,
      stream: options.stream ?? true,
    };

    // Add optional parameters
    if (options.temperature !== undefined) request.temperature = options.temperature;
    if (options.maxTokens !== undefined) request.max_tokens = options.maxTokens;
    if (options.topP !== undefined) request.top_p = options.topP;
    if (options.frequencyPenalty !== undefined) request.frequency_penalty = options.frequencyPenalty;
    if (options.presencePenalty !== undefined) request.presence_penalty = options.presencePenalty;
    if (options.seed !== undefined) request.seed = options.seed;
    if (options.stop && options.stop.length > 0) request.stop = options.stop;
    if (options.responseFormat === 'json_object') {
      request.response_format = { type: 'json_object' };
    }

    // Add dynamic parameters
    if (options.dynamicParameters) {
      Object.assign(request, options.dynamicParameters);
    }

    return request;
  }

  /**
   * Make the HTTP request
   */
  private async makeRequest(request: ChatCompletionRequest, signal?: AbortSignal): Promise<Response> {
    return fetch(this.config.apiEndpoint, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(request),
      signal,
    });
  }

  /**
   * Handle streaming response
   */
  private async handleStreamingResponse(response: Response, callbacks: StreamingCallbacks): Promise<void> {
    const reader = response.body?.getReader();
    if (!reader) {
      throw new Error('No response body reader available');
    }

    try {
      for await (const event of parseSSEStream(reader)) {
        if (event.data === '[DONE]') {
          await this.handleStreamComplete(callbacks);
          break;
        }

        await this.processSSEEvent(event, callbacks);
      }
    } finally {
      reader.releaseLock();
    }
  }

  /**
   * Process individual SSE events
   */
  private async processSSEEvent(event: SSEEvent, callbacks: StreamingCallbacks): Promise<void> {
    switch (event.event) {
      case SSEEventType.Content: {
        const contentData = event.data as { 
          choices?: Array<{ delta?: { content?: string } }>;
          performance?: StreamingPerformanceMetrics;
          usage?: UsageData;
        };
        
        // Handle content chunks
        const delta = contentData?.choices?.[0]?.delta;
        if (delta?.content) {
          this.state.totalContent += delta.content;
          callbacks.onContent?.(delta.content, this.state.totalContent);
          
          // Create chunk for callback
          const chunk: ChatCompletionChunk = {
            id: uuidv4(),
            object: 'chat.completion.chunk',
            created: Math.floor(Date.now() / 1000),
            model: 'unknown',
            choices: [{ index: 0, delta, finish_reason: undefined }]
          };
          callbacks.onChunk?.(chunk);
        }

        // Handle inline performance metrics
        if (contentData?.performance && this.config.useServerMetrics) {
          this.updateMetrics(contentData.performance, callbacks);
        }

        // Handle usage data
        if (contentData?.usage) {
          Object.assign(this.state.metrics, contentData.usage);
        }
        break;
      }

      case SSEEventType.Metrics: {
        if (this.config.useServerMetrics) {
          const metricsData = event.data as MetricsEventData;
          this.log('Metrics event received:', metricsData);
          this.updateMetrics(metricsData, callbacks);
        }
        break;
      }

      case SSEEventType.MetricsFinal: {
        if (this.config.useServerMetrics) {
          const finalMetricsData = event.data as MetricsEventData;
          this.log('Final metrics event received:', finalMetricsData);
          Object.assign(this.state.metrics, finalMetricsData);
          callbacks.onMetrics?.(finalMetricsData);
        }
        break;
      }

      case SSEEventType.Error: {
        this.log('Received SSE error event:', event);
        const errorData = event.data as { 
          error?: string; 
          message?: string; 
          statusCode?: number; 
        };
        
        const message = errorData.error ?? errorData.message ?? 'Unknown streaming error';
        const error = this.createStreamingError(`Stream error: ${message}`, errorData.statusCode);
        callbacks.onError?.(error);
        throw error;
      }
    }
  }

  /**
   * Handle stream completion
   */
  private async handleStreamComplete(callbacks: StreamingCallbacks): Promise<void> {
    const endTime = Date.now();
    const duration = (endTime - this.state.startTime) / 1000;

    // Calculate final metrics
    const totalTokens = this.state.metrics.total_tokens ?? this.state.metrics.completion_tokens ?? 0;
    const tokensPerSecond = (this.state.metrics as StreamingPerformanceMetrics).completion_tokens_per_second 
      ?? (this.state.metrics as StreamingPerformanceMetrics).tokens_per_second 
      ?? (totalTokens > 0 && duration > 0 ? totalTokens / duration : 0);
    const latencyMs = (this.state.metrics as MetricsEventData).total_latency_ms ?? duration * 1000;

    const metadata: MessageMetadata = this.config.trackPerformanceMetrics ? {
      tokensUsed: totalTokens,
      tokensPerSecond,
      latency: latencyMs,
      provider: this.state.metrics.provider,
      model: this.state.metrics.model,
      promptTokens: this.state.metrics.prompt_tokens,
      completionTokens: this.state.metrics.completion_tokens,
      timeToFirstToken: (this.state.metrics as StreamingPerformanceMetrics).time_to_first_token_ms,
      streaming: true,
    } : { streaming: true };

    this.log('Final metrics collected:', this.state.metrics);

    callbacks.onComplete?.({
      content: this.state.totalContent,
      metadata
    });
  }

  /**
   * Update metrics and trigger callbacks
   */
  private updateMetrics(
    metrics: StreamingPerformanceMetrics | MetricsEventData, 
    callbacks: StreamingCallbacks
  ): void {
    Object.assign(this.state.metrics, metrics);
    callbacks.onMetrics?.(metrics);

    // Handle tokens per second
    const tokensPerSecond = ('completion_tokens_per_second' in metrics) 
      ? metrics.completion_tokens_per_second 
      : metrics.tokens_per_second;
      
    if (tokensPerSecond !== undefined && this.config.showTokensPerSecond) {
      callbacks.onTokensPerSecond?.(tokensPerSecond);
    }
  }

  /**
   * Create a StreamingError with enhanced information
   */
  private createStreamingError(message: string, status?: number, context?: string): StreamingError {
    const error = new Error(message) as StreamingError;
    error.status = status;
    error.context = context;
    error.retryable = this.isRetryableStatus(status);
    return error;
  }

  /**
   * Enhance any error with streaming context
   */
  private enhanceError(error: unknown, context = 'streaming'): StreamingError {
    if (error instanceof Error) {
      const streamingError = error as StreamingError;
      streamingError.context = context;
      streamingError.retryable = this.isRetryableStatus(streamingError.status);
      return streamingError;
    }
    
    return this.createStreamingError(String(error), undefined, context);
  }

  /**
   * Check if an error is due to abort
   */
  private isAbortError(error: unknown): boolean {
    return error instanceof Error && (
      error.name === 'AbortError' || 
      error.message.includes('aborted')
    );
  }

  /**
   * Check if a status code indicates a retryable error
   */
  private isRetryableStatus(status?: number): boolean {
    if (!status) return false;
    return [408, 429, 500, 502, 503, 504].includes(status);
  }

  /**
   * Internal logging method
   */
  private log(...args: unknown[]): void {
    if (this.config.enableLogging) {
      console.log('[ChatStreamingManager]', ...args);
    }
  }
}