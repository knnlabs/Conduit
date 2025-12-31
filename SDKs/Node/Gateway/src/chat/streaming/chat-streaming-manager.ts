/**
 * Framework-agnostic chat streaming manager
 * Extracted and refactored from WebAdmin ChatStreamingLogic
 */

import { randomUUID } from 'crypto';
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
  ChatErrorType,
  StreamState,
  StreamingPerformanceMetrics,
  UsageData,
  MetricsEventData,
  MessageMetadata,
  StreamingRetryConfig,
  RetryInfo,
  StreamingCircuitBreakerConfig,
  CircuitBreakerEvent
} from './types';
import type { MessageContent } from '../../models/chat';
import { StreamingCircuitBreakerManager, isCircuitBreakerOpenError } from './streaming-circuit-breaker';
import type { CircuitBreakerStats } from '@knn_labs/conduit-common';

/**
 * ChatStreamingManager provides framework-agnostic chat streaming functionality
 */
export class ChatStreamingManager {
  private config: Required<StreamingConfig>;
  private state: StreamState & {
    totalReasoning: string;
  };
  private circuitBreakerManager: StreamingCircuitBreakerManager | null = null;

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
      totalReasoning: '',
      startTime: 0,
      metrics: {},
      abortController: null
    };
  }

  /**
   * Configure circuit breaker (can be called after construction)
   */
  configureCircuitBreaker(
    config: StreamingCircuitBreakerConfig,
    callbacks?: {
      onCircuitStateChange?: (event: CircuitBreakerEvent) => void;
      onCircuitOpen?: (stats: CircuitBreakerStats) => void;
    }
  ): void {
    this.circuitBreakerManager = new StreamingCircuitBreakerManager(config, callbacks);
  }

  /**
   * Get circuit breaker manager for external access
   */
  getCircuitBreakerManager(): StreamingCircuitBreakerManager | null {
    return this.circuitBreakerManager;
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
   * Stream a message with real-time callbacks and automatic retry for transient errors
   */
  async streamMessage(
    message: string,
    options: StreamMessageOptions,
    callbacks: StreamingCallbacks
  ): Promise<void> {
    const retryConfig: Required<StreamingRetryConfig> = {
      maxAttempts: options.retry?.maxAttempts ?? 3,
      baseDelayMs: options.retry?.baseDelayMs ?? 1000,
      maxDelayMs: options.retry?.maxDelayMs ?? 16000,
      shouldRetry: options.retry?.shouldRetry ?? (() => true),
    };

    // Initialize circuit breaker from options if provided and not already configured
    if (options.circuitBreaker && !this.circuitBreakerManager) {
      this.circuitBreakerManager = new StreamingCircuitBreakerManager(
        options.circuitBreaker,
        {
          onCircuitStateChange: callbacks.onCircuitStateChange,
          onCircuitOpen: callbacks.onCircuitOpen
        }
      );
    }

    // Handle model change for circuit breaker reset
    if (this.circuitBreakerManager) {
      this.circuitBreakerManager.handleModelChange(options.model);
    }

    // Check circuit breaker before attempting
    if (this.circuitBreakerManager) {
      try {
        this.circuitBreakerManager.checkOpen();
      } catch (error) {
        if (isCircuitBreakerOpenError(error)) {
          // Notify via callback
          callbacks.onCircuitOpen?.(error.stats);
          callbacks.onError?.(this.convertCircuitBreakerError(error));
          throw error;
        }
        throw error;
      }
    }

    let lastError: StreamingError | null = null;

    for (let attempt = 1; attempt <= retryConfig.maxAttempts; attempt++) {
      try {
        await this.streamMessageAttempt(message, options, callbacks);

        // Record success on circuit breaker
        this.circuitBreakerManager?.recordSuccess();
        return; // Success - exit retry loop
      } catch (error) {
        // Don't retry abort errors
        if (this.isAbortError(error)) {
          callbacks.onAbort?.();
          return;
        }

        const streamingError = this.enhanceError(error);
        lastError = streamingError;

        // Record failure on circuit breaker
        this.circuitBreakerManager?.recordFailure(streamingError);

        // Check if circuit breaker now blocks retries
        const circuitBlocksRetry = this.circuitBreakerManager?.shouldDisableRetry() ?? false;

        // Check if we should retry
        const shouldRetry = !circuitBlocksRetry && this.shouldRetryStreaming(streamingError, attempt, retryConfig);

        if (!shouldRetry || attempt >= retryConfig.maxAttempts) {
          // Don't call onError again - it was already called in streamMessageAttempt
          // unless it wasn't (for errors that happen before the inner try/catch)
          if (!streamingError.context?.includes('already reported')) {
            callbacks.onError?.(streamingError);
          }
          throw streamingError;
        }

        // Calculate delay with jitter
        const delayMs = this.getRetryDelayWithJitter(streamingError, attempt, retryConfig);

        // Notify UI of retry
        const retryInfo: RetryInfo = {
          error: streamingError,
          attempt,
          maxAttempts: retryConfig.maxAttempts,
          delayMs,
        };
        callbacks.onRetrying?.(retryInfo);

        this.log(`Retrying after error (attempt ${attempt}/${retryConfig.maxAttempts}), waiting ${delayMs}ms:`, streamingError.message);

        // Wait before retry
        await this.delay(delayMs);
      }
    }

    // Should not reach here, but handle edge case
    if (lastError) {
      callbacks.onError?.(lastError);
      throw lastError;
    }
  }

  /**
   * Execute a single streaming attempt (internal method)
   */
  private async streamMessageAttempt(
    message: string,
    options: StreamMessageOptions,
    callbacks: StreamingCallbacks
  ): Promise<void> {
    if (this.state.isStreaming) {
      throw new Error('Another streaming request is already in progress');
    }

    this.state.isStreaming = true;
    this.state.totalContent = '';
    this.state.totalReasoning = '';
    this.state.startTime = Date.now();
    this.state.metrics = {};

    let timeoutId: NodeJS.Timeout | undefined;

    try {
      // Create abort controller for this request
      const controller = new AbortController();
      this.state.abortController = controller;

      // Set timeout
      timeoutId = setTimeout(() => {
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
      timeoutId = undefined;

      if (!response.ok) {
        throw this.createStreamingError(`HTTP ${response.status}: ${response.statusText}`, response.status);
      }

      await this.handleStreamingResponse(response, callbacks);

    } catch (error) {
      if (this.isAbortError(error)) {
        throw error; // Re-throw abort errors for outer handler
      }

      const streamingError = this.enhanceError(error);
      // Mark that error was enhanced but don't call onError - let retry logic handle it
      throw streamingError;
    } finally {
      // Always clear timeout if it's still active
      if (timeoutId) {
        clearTimeout(timeoutId);
      }
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

    let completeCalled = false;
    try {
      for await (const event of parseSSEStream(reader)) {
        if (event.data === '[DONE]') {
          await this.handleStreamComplete(callbacks);
          completeCalled = true;
          break;
        }

        await this.processSSEEvent(event, callbacks);
      }
      // If we exit the loop without [DONE], still complete
      if (!completeCalled) {
        await this.handleStreamComplete(callbacks);
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
          choices?: Array<{ 
            delta?: { 
              content?: string;
              reasoning?: string;
              channel?: string;
              role?: string;
              [key: string]: unknown;
            } 
          }>;
          performance?: StreamingPerformanceMetrics;
          usage?: UsageData;
        };
        
        // Handle content chunks
        const delta = contentData?.choices?.[0]?.delta;
        if (delta) {
          let hasContent = false;
          
          // Handle regular content
          if (delta.content !== undefined && delta.content !== null && delta.content !== '') {
            this.state.totalContent += delta.content;
            callbacks.onContent?.(delta.content, this.state.totalContent);
            hasContent = true;
          }
          
          // Handle reasoning content (for models like gpt-oss-120b on Groq)
          // ACCUMULATE EVERYTHING IN ORDER FOR DISPLAY
          if (delta.reasoning !== undefined && delta.reasoning !== null && delta.reasoning !== '') {
            this.state.totalReasoning += delta.reasoning;
            // ADD TO TOTAL CONTENT TOO SO IT DISPLAYS IMMEDIATELY
            this.state.totalContent += delta.reasoning;
            callbacks.onContent?.(delta.reasoning, this.state.totalContent);
            hasContent = true;
          }
          
          // Create chunk for callback if there's any meaningful content
          if (hasContent || delta.role !== undefined || delta.channel !== undefined) {
            const chunk: ChatCompletionChunk = {
              id: randomUUID(),
              object: 'chat.completion.chunk',
              created: Math.floor(Date.now() / 1000),
              model: 'unknown',
              choices: [{ index: 0, delta, finish_reason: undefined }]
            };
            callbacks.onChunk?.(chunk);
          }
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

      case SSEEventType.Reasoning: {
        const reasoningData = event.data as { content?: string };
        const reasoning = reasoningData?.content;

        if (reasoning !== undefined && reasoning !== null && reasoning !== '') {
          this.state.totalReasoning += reasoning;
          callbacks.onReasoning?.(reasoning, this.state.totalReasoning);
          this.log('Reasoning content received:', reasoning.slice(0, 100));
        }
        break;
      }

      case SSEEventType.ToolExecuting: {
        const toolData = event.data as {
          tool_call_id?: string;
          function_name?: string;
          status: string;
          result?: unknown;
          cost?: number;
          error_message?: string;
          function_execution_id?: string;
        };

        this.log('Tool execution event:', toolData.function_name, toolData.status);
        callbacks.onToolExecuting?.(toolData);
        break;
      }

      case SSEEventType.ToolResult: {
        const toolResultData = event.data as {
          tool_call_id: string;
          result: unknown;
          error?: string;
        };

        this.log('Tool result received for:', toolResultData.tool_call_id);
        callbacks.onToolResult?.(toolResultData);
        break;
      }

      case SSEEventType.Error: {
        this.log('Received SSE error event:', event);
        const errorData = event.data as {
          error?: string;
          message?: string;
          statusCode?: number;
          code?: string;
          type?: string;
          retryAfter?: number;
          suggestions?: string[];
          technical?: string;
        };

        const message = errorData.error ?? errorData.message ?? 'Unknown streaming error';
        const error = this.createEnhancedStreamingError(
          `Stream error: ${message}`,
          errorData,
          this.state.totalContent
        );
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
    this.log('Final content length:', this.state.totalContent?.length || 0);
    this.log('Final reasoning length:', this.state.totalReasoning?.length || 0);
    this.log('Final content:', this.state.totalContent);
    this.log('Final reasoning:', this.state.totalReasoning);

    // totalContent already has everything (reasoning + content) in the right order
    callbacks.onComplete?.({
      content: this.state.totalContent,
      metadata: {
        ...metadata,
        hasReasoning: this.state.totalReasoning.length > 0,
        reasoning: this.state.totalReasoning || undefined
      }
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
    error.statusCode = status;
    error.context = context;
    error.retryable = this.isRetryableStatus(status);
    error.recoverable = this.isRetryableStatus(status);
    return error;
  }

  /**
   * Create enhanced StreamingError from SSE error data with detailed metadata
   */
  private createEnhancedStreamingError(
    message: string,
    errorData: {
      statusCode?: number;
      code?: string;
      type?: string;
      retryAfter?: number;
      suggestions?: string[];
      technical?: string;
    },
    partialContent: string
  ): StreamingError {
    const error = new Error(message) as StreamingError;
    const statusCode = errorData.statusCode;

    // Map error type
    error.errorType = this.mapErrorType(errorData.type, errorData.code, statusCode);
    error.statusCode = statusCode;
    error.status = statusCode;
    error.code = errorData.code;
    error.retryAfter = errorData.retryAfter;
    error.suggestions = errorData.suggestions ?? this.getDefaultSuggestions(error.errorType);
    error.technical = errorData.technical;
    error.recoverable = this.isRetryableStatus(statusCode);
    error.retryable = error.recoverable;
    error.partialContent = partialContent;

    return error;
  }

  /**
   * Convert circuit breaker error to streaming error format
   */
  private convertCircuitBreakerError(error: { message: string; stats: CircuitBreakerStats; timeUntilHalfOpen: number | null }): StreamingError {
    const streamingError = new Error(error.message) as StreamingError;
    streamingError.status = 503;
    streamingError.statusCode = 503;
    streamingError.code = 'CIRCUIT_BREAKER_OPEN';
    streamingError.retryable = false;
    streamingError.recoverable = false;
    streamingError.errorType = 'server_error';
    streamingError.suggestions = [
      `Service temporarily unavailable. Circuit will reset in ${Math.ceil((error.timeUntilHalfOpen ?? 0) / 1000)} seconds.`,
      'Multiple consecutive failures detected.',
      'Try switching to a different model or provider.'
    ];
    return streamingError;
  }

  /**
   * Map error type string to ChatErrorType
   */
  private mapErrorType(type?: string, code?: string, statusCode?: number): ChatErrorType {
    // Check explicit type first
    if (type === 'rate_limit' || code?.includes('rate_limit')) return 'rate_limit';
    if (type === 'model_not_found' || code?.includes('model_not_found')) return 'model_not_found';
    if (type === 'auth_error' || code?.includes('auth') || code?.includes('unauthorized')) return 'auth_error';
    if (type === 'network_error' || code?.includes('network')) return 'network_error';

    // Fallback to status code mapping
    if (statusCode === 401 || statusCode === 403) return 'auth_error';
    if (statusCode === 404) return 'model_not_found';
    if (statusCode === 429) return 'rate_limit';
    if (statusCode && statusCode >= 500) return 'server_error';
    if (!statusCode || statusCode === 0) return 'network_error';

    return 'server_error';
  }

  /**
   * Get default suggestions based on error type
   */
  private getDefaultSuggestions(errorType: ChatErrorType): string[] {
    switch (errorType) {
      case 'rate_limit':
        return ['Wait a moment before trying again', 'Consider upgrading your plan for higher limits'];
      case 'auth_error':
        return ['Check your API key configuration', 'Verify you are logged in'];
      case 'model_not_found':
        return ['Check the model name is correct', 'Verify the model is available'];
      case 'network_error':
        return ['Check your internet connection', 'Try refreshing the page'];
      case 'server_error':
        return ['Try again in a moment', 'Contact support if the problem persists'];
      default:
        return ['Try again in a moment'];
    }
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
   * Determine if a streaming error should be retried
   */
  private shouldRetryStreaming(
    error: StreamingError,
    attempt: number,
    config: Required<StreamingRetryConfig>
  ): boolean {
    // Never retry abort errors
    if (this.isAbortError(error)) {
      return false;
    }

    // Check if error has a retryable status code
    if (!this.isRetryableStatus(error.status)) {
      return false;
    }

    // Use custom shouldRetry function if provided
    if (config.shouldRetry) {
      return config.shouldRetry(error, attempt);
    }

    return true;
  }

  /**
   * Calculate retry delay with exponential backoff and jitter
   */
  private getRetryDelayWithJitter(
    error: StreamingError,
    attempt: number,
    config: Required<StreamingRetryConfig>
  ): number {
    // Respect Retry-After header for rate limits
    if (error.retryAfter) {
      return error.retryAfter * 1000;
    }

    // Exponential backoff: baseDelay * 2^(attempt-1)
    const exponentialDelay = config.baseDelayMs * Math.pow(2, attempt - 1);
    const cappedDelay = Math.min(exponentialDelay, config.maxDelayMs);

    // Add ±20% jitter to prevent thundering herd
    const jitterFactor = 0.2;
    const jitter = cappedDelay * jitterFactor * (Math.random() * 2 - 1);

    return Math.floor(cappedDelay + jitter);
  }

  /**
   * Delay for specified milliseconds
   */
  private delay(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
  }

  /**
   * Internal logging method
   */
  private log(...args: unknown[]): void {
    if (this.config.enableLogging) {
      console.warn('[ChatStreamingManager]', ...args);
    }
  }
}