'use strict';

var conduitCommon = require('@knn_labs/conduit-common');

// src/utils/errors.ts

// src/constants/endpoints.ts
var API_ENDPOINTS = {
  V1: {
    CHAT: {
      COMPLETIONS: "/v1/chat/completions"
    },
    IMAGES: {
      GENERATIONS: "/v1/images/generations",
      ASYNC_GENERATIONS: "/v1/images/generations/async",
      // Note: The following endpoints are not yet implemented in Core API
      EDITS: "/v1/images/edits",
      // Not implemented
      VARIATIONS: "/v1/images/variations",
      // Not implemented
      TASK_STATUS: (taskId) => `/v1/images/generations/${encodeURIComponent(taskId)}/status`,
      CANCEL_TASK: (taskId) => `/v1/images/generations/${encodeURIComponent(taskId)}`
    },
    VIDEOS: {
      // Note: Synchronous video generation endpoint does not exist
      ASYNC_GENERATIONS: "/v1/videos/generations/async",
      TASK_STATUS: (taskId) => `/v1/videos/generations/tasks/${encodeURIComponent(taskId)}`,
      CANCEL_TASK: (taskId) => `/v1/videos/generations/${encodeURIComponent(taskId)}`
    },
    AUDIO: {
      TRANSCRIPTIONS: "/v1/audio/transcriptions",
      TRANSLATIONS: "/v1/audio/translations",
      SPEECH: "/v1/audio/speech"
    },
    MODELS: {
      BASE: "/v1/models",
      BY_ID: (modelId) => `/v1/models/${encodeURIComponent(modelId)}`
    },
    EMBEDDINGS: {
      BASE: "/v1/embeddings"
    },
    TASKS: {
      BASE: "/v1/tasks",
      BY_ID: (taskId) => `/v1/tasks/${encodeURIComponent(taskId)}`,
      CANCEL: (taskId) => `/v1/tasks/${encodeURIComponent(taskId)}/cancel`,
      CLEANUP: "/v1/tasks/cleanup"
    },
    BATCH: {
      // Note: No generic /v1/batch endpoint exists. Use specific batch endpoints:
      SPEND_UPDATES: "/v1/batch/spend-updates",
      VIRTUAL_KEY_UPDATES: "/v1/batch/virtual-key-updates",
      WEBHOOK_SENDS: "/v1/batch/webhook-sends",
      OPERATIONS: {
        BY_ID: (operationId) => `/v1/batch/operations/${encodeURIComponent(operationId)}`,
        CANCEL: (operationId) => `/v1/batch/operations/${encodeURIComponent(operationId)}/cancel`
      }
    }
  },
  ROOT: {
    HEALTH: "/health",
    METRICS: "/metrics"
  }
};
var HTTP_HEADERS = {
  ...conduitCommon.HTTP_HEADERS};
var CONTENT_TYPES = {
  ...conduitCommon.CONTENT_TYPES};
var CLIENT_INFO = {
  // Could be imported from package.json
  USER_AGENT: "@conduit/core/0.1.0"
};

// src/constants/validation.ts
var IMAGE_RESPONSE_FORMATS = {
  URL: "url",
  BASE64_JSON: "b64_json"
};
var IMAGE_QUALITY = {
  STANDARD: "standard",
  HD: "hd"
};
var IMAGE_STYLE = {
  VIVID: "vivid",
  NATURAL: "natural"
};
var IMAGE_SIZES = {
  SMALL: "256x256",
  MEDIUM: "512x512",
  LARGE: "1024x1024",
  WIDE: "1792x1024",
  TALL: "1024x1792"
};
var ImageValidationHelpers = {
  /**
   * Check if response format is valid.
   */
  isValidResponseFormat: (format) => Object.values(IMAGE_RESPONSE_FORMATS).includes(format),
  /**
   * Check if quality is valid.
   */
  isValidQuality: (quality) => Object.values(IMAGE_QUALITY).includes(quality),
  /**
   * Check if style is valid.
   */
  isValidStyle: (style) => Object.values(IMAGE_STYLE).includes(style),
  /**
   * Check if size is valid.
   */
  isValidSize: (size) => Object.values(IMAGE_SIZES).includes(size),
  /**
   * Get all valid response formats.
   */
  getAllResponseFormats: () => Object.values(IMAGE_RESPONSE_FORMATS),
  /**
   * Get all valid qualities.
   */
  getAllQualities: () => Object.values(IMAGE_QUALITY),
  /**
   * Get all valid styles.
   */
  getAllStyles: () => Object.values(IMAGE_STYLE),
  /**
   * Get all valid sizes.
   */
  getAllSizes: () => Object.values(IMAGE_SIZES)
};

// src/client/FetchBasedClient.ts
var FetchBasedClient = class {
  constructor(config) {
    this.config = {
      apiKey: config.apiKey,
      baseURL: config.baseURL ?? "https://api.conduit.ai",
      timeout: config.timeout ?? 6e4,
      maxRetries: config.maxRetries ?? 3,
      headers: config.headers ?? {},
      debug: config.debug ?? false,
      signalR: config.signalR ?? {},
      retryDelay: config.retryDelay ?? [1e3, 2e3, 4e3, 8e3, 16e3],
      onError: config.onError,
      onRequest: config.onRequest,
      onResponse: config.onResponse
    };
    this.retryConfig = {
      maxRetries: this.config.maxRetries,
      initialDelay: 1e3,
      maxDelay: 3e4,
      factor: 2
    };
    this.retryDelays = this.config.retryDelay;
  }
  /**
   * Type-safe request method with proper request/response typing
   */
  async request(url, options = {}) {
    const fullUrl = this.buildUrl(url);
    const controller = new AbortController();
    const timeoutId = options.timeout ?? this.config.timeout ? setTimeout(() => controller.abort(), options.timeout ?? this.config.timeout) : void 0;
    try {
      const requestConfig = {
        method: options.method ?? conduitCommon.HttpMethod.GET,
        url: fullUrl,
        headers: this.buildHeaders(options.headers),
        data: options.body
      };
      if (this.config.onRequest) {
        await this.config.onRequest(requestConfig);
      }
      if (this.config.debug) {
        console.warn(`[Conduit] ${requestConfig.method} ${requestConfig.url}`);
      }
      const response = await this.executeWithRetry(
        fullUrl,
        {
          method: requestConfig.method,
          headers: requestConfig.headers,
          body: options.body ? JSON.stringify(options.body) : void 0,
          signal: options.signal ?? controller.signal,
          responseType: options.responseType,
          timeout: options.timeout ?? this.config.timeout
        },
        options
      );
      return response;
    } finally {
      if (timeoutId) {
        clearTimeout(timeoutId);
      }
    }
  }
  /**
   * Type-safe GET request
   */
  async get(url, options) {
    return this.request(url, { ...options, method: conduitCommon.HttpMethod.GET });
  }
  /**
   * Type-safe POST request
   */
  async post(url, data, options) {
    return this.request(url, {
      ...options,
      method: conduitCommon.HttpMethod.POST,
      body: data
    });
  }
  /**
   * Type-safe PUT request
   */
  async put(url, data, options) {
    return this.request(url, {
      ...options,
      method: conduitCommon.HttpMethod.PUT,
      body: data
    });
  }
  /**
   * Type-safe PATCH request
   */
  async patch(url, data, options) {
    return this.request(url, {
      ...options,
      method: conduitCommon.HttpMethod.PATCH,
      body: data
    });
  }
  /**
   * Type-safe DELETE request
   */
  async delete(url, options) {
    return this.request(url, { ...options, method: conduitCommon.HttpMethod.DELETE });
  }
  buildUrl(path) {
    if (path.startsWith("http://") || path.startsWith("https://")) {
      return path;
    }
    const baseUrl = this.config.baseURL.replace(/\/$/, "");
    const cleanPath = path.startsWith("/") ? path : `/${path}`;
    return `${baseUrl}${cleanPath}`;
  }
  buildHeaders(additionalHeaders) {
    return {
      [HTTP_HEADERS.AUTHORIZATION]: `Bearer ${this.config.apiKey}`,
      [HTTP_HEADERS.CONTENT_TYPE]: CONTENT_TYPES.JSON,
      [HTTP_HEADERS.USER_AGENT]: CLIENT_INFO.USER_AGENT,
      ...this.config.headers,
      ...additionalHeaders
    };
  }
  async executeWithRetry(url, init, options = {}, attempt = 1) {
    try {
      const response = await fetch(url, conduitCommon.ResponseParser.cleanRequestInit(init));
      if (this.config.onResponse) {
        const headers = {};
        response.headers.forEach((value, key) => {
          headers[key] = value;
        });
        const responseInfo = {
          status: response.status,
          statusText: response.statusText,
          headers,
          data: void 0,
          // Will be populated after parsing
          config: {
            method: init.method ?? "GET",
            url,
            headers: init.headers ?? {},
            data: void 0
          }
        };
        await this.config.onResponse(responseInfo);
      }
      if (this.config.debug) {
        console.warn(`[Conduit] Response: ${response.status} ${response.statusText}`);
      }
      if (!response.ok) {
        const error = await this.handleErrorResponse(response);
        throw error;
      }
      return await conduitCommon.ResponseParser.parse(response, init.responseType ?? options.responseType);
    } catch (error) {
      if (attempt > this.retryConfig.maxRetries) {
        throw this.handleError(error);
      }
      if (this.shouldRetry(error) && attempt <= this.retryConfig.maxRetries) {
        const delay = this.calculateDelay(attempt);
        if (this.config.debug) {
          console.warn(`[Conduit] Retrying request (attempt ${attempt + 1}) after ${delay}ms`);
        }
        await this.sleep(delay);
        return this.executeWithRetry(url, init, options, attempt + 1);
      }
      throw this.handleError(error);
    }
  }
  async handleErrorResponse(response) {
    let errorData;
    try {
      const contentType = response.headers.get("content-type");
      if (contentType?.includes("application/json")) {
        errorData = await response.json();
      }
    } catch {
    }
    const status = response.status;
    if (status === 401) {
      return new conduitCommon.AuthError(
        errorData?.error?.message ?? "Authentication failed",
        { code: errorData?.error?.code ?? "auth_error" }
      );
    } else if (status === 429) {
      const retryAfter = response.headers.get("retry-after");
      return new conduitCommon.RateLimitError(
        errorData?.error?.message ?? "Rate limit exceeded",
        retryAfter ? parseInt(retryAfter, 10) : void 0
      );
    } else if (status === 400) {
      return new conduitCommon.ConduitError(
        errorData?.error?.message ?? "Bad request",
        status,
        errorData?.error?.code ?? "bad_request"
      );
    } else if (errorData?.error) {
      return new conduitCommon.ConduitError(
        errorData.error.message,
        status,
        errorData.error.code ?? void 0
      );
    } else {
      return new conduitCommon.ConduitError(
        `Request failed with status ${status}`,
        status,
        "http_error"
      );
    }
  }
  shouldRetry(error) {
    if (error instanceof conduitCommon.ConduitError) {
      const status = error.statusCode;
      return status === 429 || status === 503 || status === 504;
    }
    if (error instanceof Error) {
      return error.name === "AbortError" || error.message.includes("network") || error.message.includes("fetch");
    }
    return false;
  }
  calculateDelay(attempt) {
    if (this.retryDelays && this.retryDelays.length > 0) {
      const index = Math.min(attempt - 1, this.retryDelays.length - 1);
      return this.retryDelays[index];
    }
    const delay = Math.min(
      this.retryConfig.initialDelay * Math.pow(this.retryConfig.factor, attempt - 1),
      this.retryConfig.maxDelay
    );
    return delay + Math.random() * 1e3;
  }
  sleep(ms) {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }
  handleError(error) {
    if (error instanceof Error) {
      if (error.name === "AbortError") {
        const networkError = new conduitCommon.NetworkError(
          "Request timeout",
          { code: conduitCommon.ERROR_CODES.CONNECTION_ABORTED }
        );
        if (this.config.onError) {
          this.config.onError(networkError);
        }
        return networkError;
      }
      if (this.config.onError) {
        this.config.onError(error);
      }
      return error;
    }
    const unknownError = new Error(String(error));
    if (this.config.onError) {
      this.config.onError(unknownError);
    }
    return unknownError;
  }
};

// src/utils/stream-response.ts
var TypedStreamingResponse = class {
  constructor(stream, abortController) {
    this.stream = stream;
    this.abortController = abortController;
  }
  async *[Symbol.asyncIterator]() {
    for await (const chunk of this.stream) {
      yield chunk;
    }
  }
  async toArray() {
    const chunks = [];
    for await (const chunk of this) {
      chunks.push(chunk);
    }
    return chunks;
  }
  async *map(fn) {
    for await (const chunk of this) {
      yield await fn(chunk);
    }
  }
  async *filter(predicate) {
    for await (const chunk of this) {
      if (await predicate(chunk)) {
        yield chunk;
      }
    }
  }
  async *take(n) {
    let count = 0;
    for await (const chunk of this) {
      if (count >= n) {
        break;
      }
      yield chunk;
      count++;
    }
  }
  async *skip(n) {
    let count = 0;
    for await (const chunk of this) {
      if (count >= n) {
        yield chunk;
      } else {
        count++;
      }
    }
  }
  cancel() {
    if (this.abortController) {
      this.abortController.abort();
    }
  }
};
function createStreamingResponse(stream, abortController) {
  return new TypedStreamingResponse(stream, abortController);
}

// src/utils/web-streaming.ts
function createWebStream(stream, options) {
  const abortController = new AbortController();
  if (options?.signal) {
    options.signal.addEventListener("abort", () => abortController.abort());
  }
  const generator = webStreamAsyncIterator(stream, options);
  return createStreamingResponse(generator, abortController);
}
async function* webStreamAsyncIterator(stream, options) {
  const reader = stream.getReader();
  const decoder = new TextDecoder();
  let buffer = "";
  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) {
        break;
      }
      buffer += decoder.decode(value, { stream: true });
      const lines = buffer.split("\n");
      buffer = lines.pop() ?? "";
      for (const line of lines) {
        if (line.trim() === "") continue;
        if (line.startsWith("data: ")) {
          const data = line.slice(6);
          if (data === "[DONE]") {
            return;
          }
          try {
            const parsed = JSON.parse(data);
            yield parsed;
          } catch (error) {
            if (options?.onError) {
              options.onError(new conduitCommon.StreamError("Failed to parse SSE message", { cause: error }));
            }
          }
        }
      }
    }
    if (buffer.trim() && buffer.startsWith("data: ")) {
      const data = buffer.slice(6);
      if (data !== "[DONE]") {
        try {
          const parsed = JSON.parse(data);
          yield parsed;
        } catch (error) {
          if (options?.onError) {
            options.onError(new conduitCommon.StreamError("Failed to parse final SSE message", { cause: error }));
          }
        }
      }
    }
  } finally {
    reader.releaseLock();
  }
}

// src/utils/enhanced-web-streaming.ts
function createEnhancedWebStream(stream, options) {
  const abortController = new AbortController();
  if (options?.signal) {
    options.signal.addEventListener("abort", () => abortController.abort());
  }
  const generator = enhancedWebStreamAsyncIterator(stream, options);
  return {
    async *[Symbol.asyncIterator]() {
      yield* generator;
    },
    async toArray() {
      const events = [];
      for await (const event of generator) {
        events.push(event);
      }
      return events;
    },
    cancel() {
      abortController.abort();
    }
  };
}
async function* enhancedWebStreamAsyncIterator(stream, options) {
  const reader = stream.getReader();
  const decoder = new TextDecoder();
  let buffer = "";
  let currentEventType;
  let currentData = "";
  let lineNumber = 0;
  const startTime = Date.now();
  const timeout = options?.timeout ?? 3e5;
  try {
    while (true) {
      if (Date.now() - startTime > timeout) {
        throw new conduitCommon.StreamError(`Stream timeout after ${timeout}ms`);
      }
      const { done, value } = await reader.read();
      if (done) {
        break;
      }
      if (value.length > 1048576) {
        throw new conduitCommon.StreamError(`Stream chunk too large: ${value.length} bytes`);
      }
      buffer += decoder.decode(value, { stream: true });
      const lines = buffer.split("\n");
      buffer = lines.pop() ?? "";
      for (const line of lines) {
        lineNumber++;
        const trimmedLine = line.trim();
        if (trimmedLine === "") {
          if (currentData) {
            const event = processEvent(currentEventType, currentData, options);
            if (event) {
              yield event;
            }
            currentEventType = void 0;
            currentData = "";
          }
          continue;
        }
        if (line.startsWith("event: ")) {
          const eventType = line.slice(7).trim();
          if (eventType.length > 50) {
            if (options?.onError) {
              options.onError(new conduitCommon.StreamError(`Invalid event type at line ${lineNumber}: too long`));
            }
            continue;
          }
          currentEventType = eventType;
        } else if (line.startsWith("data: ")) {
          const data = line.slice(6);
          if (currentData.length + data.length > 1048576) {
            if (options?.onError) {
              options.onError(new conduitCommon.StreamError(`Data too large at line ${lineNumber}`));
            }
            currentData = "";
            currentEventType = void 0;
            continue;
          }
          if (currentData) {
            currentData += `
${data}`;
          } else {
            currentData = data;
          }
        } else if (!line.startsWith(":")) {
          if (options?.onError) {
            options.onError(new conduitCommon.StreamError(`Malformed SSE line at ${lineNumber}: ${line}`));
          }
        }
      }
    }
    if (currentData) {
      const event = processEvent(currentEventType, currentData, options);
      if (event) {
        yield event;
      }
    }
  } finally {
    reader.releaseLock();
  }
}
function processEvent(eventType, data, options) {
  if (!data || data.length === 0) {
    if (options?.onError) {
      options.onError(new conduitCommon.StreamError("Empty event data"));
    }
    return null;
  }
  if (data === "[DONE]") {
    return {
      type: "done",
      data: "[DONE]"
    };
  }
  let type;
  switch (eventType) {
    case "metrics":
      type = "metrics";
      break;
    case "metrics-final":
      type = "metrics-final";
      break;
    case "error":
      type = "error";
      break;
    default:
      type = "content";
  }
  try {
    const parsed = JSON.parse(data);
    if (type === "content" && parsed && typeof parsed === "object" && !parsed.object) {
      if (options?.onError) {
        options.onError(new conduitCommon.StreamError("Invalid content event: missing object field"));
      }
      return null;
    }
    return {
      type,
      data: parsed
    };
  } catch (error) {
    if (options?.onError) {
      options.onError(new conduitCommon.StreamError(`Failed to parse SSE ${type} event: ${error instanceof Error ? error.message : "Unknown error"}`, { cause: error }));
    }
    if (type === "error") {
      return {
        type,
        data: { message: data, parse_error: true }
      };
    }
    return null;
  }
}

// src/services/FetchChatService.ts
var FetchChatService = class extends FetchBasedClient {
  constructor(config) {
    super(config);
  }
  async create(request, options) {
    const processedRequest = this.convertLegacyFunctions(request);
    if (processedRequest.stream === true) {
      return this.createStream(processedRequest, options);
    }
    return this.createCompletion(processedRequest, options);
  }
  async createCompletion(request, options) {
    const response = await this.post(
      API_ENDPOINTS.V1.CHAT.COMPLETIONS,
      request,
      options
    );
    return response;
  }
  async createStream(request, options) {
    const response = await this.createStreamingRequest(request, options);
    const stream = response.body;
    if (!stream) {
      throw new Error("Response body is not a stream");
    }
    return createWebStream(
      stream,
      {
        signal: options?.signal,
        timeout: options?.timeout
      }
    );
  }
  async createStreamingRequest(request, options) {
    const url = `${this.config.baseURL}${API_ENDPOINTS.V1.CHAT.COMPLETIONS}`;
    const controller = new AbortController();
    const timeoutId = options?.timeout ?? this.config.timeout ? setTimeout(() => controller.abort(), options?.timeout ?? this.config.timeout) : void 0;
    try {
      const response = await fetch(url, {
        method: conduitCommon.HttpMethod.POST,
        headers: {
          "Authorization": `Bearer ${this.config.apiKey}`,
          "Content-Type": "application/json",
          "User-Agent": "@conduit/core/1.0.0",
          ...options?.headers
        },
        body: JSON.stringify(request),
        signal: options?.signal ?? controller.signal
      });
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      return response;
    } finally {
      if (timeoutId) {
        clearTimeout(timeoutId);
      }
    }
  }
  /**
   * Count tokens in messages (placeholder - actual implementation would use tiktoken)
   */
  countTokens(messages, _model = "gpt-4") {
    const text = messages.map(
      (m) => typeof m.content === "string" ? m.content : JSON.stringify(m.content)
    ).join(" ");
    return Math.ceil(text.length / 4);
  }
  /**
   * Validate that a request fits within model context limits
   */
  validateContextLength(request, maxTokens) {
    const tokens = this.countTokens(request.messages, request.model);
    const limit = maxTokens ?? 8192;
    return {
      valid: tokens <= limit,
      tokens,
      limit
    };
  }
  /**
   * Creates an enhanced streaming chat completion that preserves SSE event types.
   * This allows access to metrics events and other enhanced streaming features.
   * 
   * @param request - The chat completion request with stream: true
   * @param options - Optional request configuration
   * @returns A streaming response with enhanced events
   * 
   * @example
   * const stream = await chatService.createEnhancedStream({
   *   model: 'gpt-4',
   *   messages: [{ role: 'user', content: 'Hello!' }],
   *   stream: true
   * });
   * 
   * for await (const event of stream) {
   *   switch (event.type) {
   *     case 'content':
   *       console.log('Content:', event.data);
   *       break;
   *     case 'metrics':
   *       console.log('Metrics:', event.data);
   *       break;
   *   }
   * }
   */
  async createEnhancedStream(request, options) {
    const processedRequest = this.convertLegacyFunctions(request);
    const response = await this.createStreamingRequest(processedRequest, options);
    const stream = response.body;
    if (!stream) {
      throw new Error("Response body is not a stream");
    }
    return createEnhancedWebStream(
      stream,
      {
        signal: options?.signal,
        timeout: options?.timeout
      }
    );
  }
  /**
   * Converts legacy function parameters to the tools format
   * for backward compatibility
   */
  convertLegacyFunctions(request) {
    if (request.functions && !request.tools) {
      request.tools = request.functions.map((fn) => ({
        type: "function",
        function: fn
      }));
      delete request.functions;
    }
    if (request.function_call && !request.tool_choice) {
      if (typeof request.function_call === "string") {
        request.tool_choice = request.function_call;
      } else {
        request.tool_choice = {
          type: "function",
          function: request.function_call
        };
      }
      delete request.function_call;
    }
    return request;
  }
};

// src/services/AudioService.ts
var AudioService = class extends FetchBasedClient {
  constructor(client) {
    super(client.config);
  }
  /**
   * Transcribes audio to text using speech-to-text models.
   * Supports multiple audio formats and languages with customizable output formats.
   * 
   * @param request - The transcription request
   * @param options - Optional request options
   * @returns Promise resolving to transcription response
   * 
   * @example
   * ```typescript
   * // Basic transcription
   * const result = await audio.transcribe({
   *   file: AudioUtils.fromBuffer(audioBuffer, 'audio.mp3'),
   *   model: 'whisper-1'
   * });
   * console.log(result.text);
   * 
   * // With language and timestamps
   * const detailed = await audio.transcribe({
   *   file: AudioUtils.fromBuffer(audioBuffer, 'audio.mp3'),
   *   model: 'whisper-1',
   *   language: 'en',
   *   response_format: 'verbose_json',
   *   timestamp_granularities: ['word', 'segment']
   * });
   * ```
   */
  async transcribe(request, options) {
    this.validateTranscriptionRequest(request);
    const formData = this.createAudioFormData(request.file, {
      model: request.model,
      language: request.language,
      prompt: request.prompt,
      response_format: request.response_format,
      temperature: request.temperature,
      timestamp_granularities: request.timestamp_granularities
    });
    return this.request(
      "/v1/audio/transcriptions",
      {
        method: conduitCommon.HttpMethod.POST,
        body: formData,
        headers: {
          "Content-Type": "multipart/form-data"
        },
        ...options
      }
    );
  }
  /**
   * Translates audio to English text using speech-to-text models.
   * @param request The translation request
   * @param options Optional request options
   * @returns Promise resolving to translation response
   */
  async translate(request, options) {
    this.validateTranslationRequest(request);
    const formData = this.createAudioFormData(request.file, {
      model: request.model,
      prompt: request.prompt,
      response_format: request.response_format,
      temperature: request.temperature
    });
    return this.request(
      "/v1/audio/translations",
      {
        method: conduitCommon.HttpMethod.POST,
        body: formData,
        headers: {
          "Content-Type": "multipart/form-data"
        },
        ...options
      }
    );
  }
  /**
   * Generates speech from text using text-to-speech models.
   * Supports multiple voices and audio formats with adjustable speed.
   * 
   * @param request - The speech generation request
   * @param options - Optional request options
   * @returns Promise resolving to speech response with audio data
   * 
   * @example
   * ```typescript
   * // Generate speech with default settings
   * const speech = await audio.generateSpeech({
   *   model: 'tts-1',
   *   input: 'Welcome to our service!',
   *   voice: 'nova'
   * });
   * 
   * // High quality with specific format
   * const hdSpeech = await audio.generateSpeech({
   *   model: 'tts-1-hd',
   *   input: 'This is high quality audio.',
   *   voice: 'alloy',
   *   response_format: 'mp3',
   *   speed: 1.0
   * });
   * 
   * // Save to file
   * fs.writeFileSync('output.mp3', speech.audio);
   * ```
   */
  async generateSpeech(request, options) {
    this.validateSpeechRequest(request);
    const response = await this.request(
      "/v1/audio/speech",
      {
        method: conduitCommon.HttpMethod.POST,
        body: request,
        responseType: "arraybuffer",
        ...options
      }
    );
    const audioBuffer = Buffer.from(response);
    return {
      audio: audioBuffer,
      format: request.response_format ?? "mp3",
      metadata: {
        size: audioBuffer.length
      }
    };
  }
  /**
   * Processes audio through the hybrid pipeline (STT + LLM + TTS).
   * @param request The hybrid audio processing request
   * @param options Optional request options
   * @returns Promise resolving to hybrid audio response
   */
  async processHybrid(request, options) {
    this.validateHybridRequest(request);
    const formData = this.createAudioFormData(request.file, {
      models: request.models,
      voice: request.voice,
      system_prompt: request.system_prompt,
      context: request.context,
      language: request.language,
      temperature: request.temperature,
      voice_settings: request.voice_settings,
      session_id: request.session_id
    });
    const response = await this.request(
      "/v1/audio/hybrid/process",
      {
        method: conduitCommon.HttpMethod.POST,
        body: formData,
        headers: {
          "Content-Type": "multipart/form-data"
        },
        responseType: "arraybuffer",
        ...options
      }
    );
    const audioBuffer = Buffer.from(response);
    return {
      audio: audioBuffer,
      transcription: "",
      // Would be populated from response headers or separate endpoint
      llm_response: "",
      // Would be populated from response headers or separate endpoint
      stages: {
        transcription: { duration: 0 },
        llm: { duration: 0, tokens_used: 0, model_used: request.models.chat },
        speech: { duration: 0, audio_duration: 0, format: "mp3" }
      },
      usage: {
        llm_tokens: { prompt_tokens: 0, completion_tokens: 0, total_tokens: 0 },
        total_processing_time_ms: 0
      }
    };
  }
  /**
   * Creates a simple transcription request for quick speech-to-text conversion.
   * @param audioFile The audio file to transcribe
   * @param model Optional model to use (defaults to 'whisper-1')
   * @param language Optional language code
   * @returns Promise resolving to transcription text
   */
  async quickTranscribe(audioFile, model = "whisper-1", language) {
    const request = {
      file: audioFile,
      model,
      language,
      response_format: "text"
    };
    const response = await this.transcribe(request);
    return response.text;
  }
  /**
   * Creates a simple speech generation request for quick text-to-speech conversion.
   * @param text The text to convert to speech
   * @param voice Optional voice to use (defaults to 'alloy')
   * @param model Optional model to use (defaults to 'tts-1')
   * @returns Promise resolving to audio buffer
   */
  async quickSpeak(text, voice = "alloy", model = "tts-1") {
    const request = {
      model,
      input: text,
      voice,
      response_format: "mp3"
    };
    const response = await this.generateSpeech(request);
    return response.audio;
  }
  /**
   * Validates an audio transcription request.
   * @private
   */
  validateTranscriptionRequest(request) {
    if (!request.file) {
      throw new Error("Audio file is required for transcription");
    }
    if (!request.model) {
      throw new Error("Model is required for transcription");
    }
    if (request.temperature !== void 0 && (request.temperature < 0 || request.temperature > 1)) {
      throw new Error("Temperature must be between 0 and 1");
    }
    this.validateAudioFile(request.file);
  }
  /**
   * Validates an audio translation request.
   * @private
   */
  validateTranslationRequest(request) {
    if (!request.file) {
      throw new Error("Audio file is required for translation");
    }
    if (!request.model) {
      throw new Error("Model is required for translation");
    }
    if (request.temperature !== void 0 && (request.temperature < 0 || request.temperature > 1)) {
      throw new Error("Temperature must be between 0 and 1");
    }
    this.validateAudioFile(request.file);
  }
  /**
   * Validates a text-to-speech request.
   * @private
   */
  validateSpeechRequest(request) {
    if (!request.input || request.input.trim().length === 0) {
      throw new Error("Input text is required for speech generation");
    }
    if (request.input.length > 4096) {
      throw new Error("Input text must be 4096 characters or less");
    }
    if (!request.model) {
      throw new Error("Model is required for speech generation");
    }
    if (!request.voice) {
      throw new Error("Voice is required for speech generation");
    }
    if (request.speed !== void 0 && (request.speed < 0.25 || request.speed > 4)) {
      throw new Error("Speed must be between 0.25 and 4.0");
    }
  }
  /**
   * Validates a hybrid audio request.
   * @private
   */
  validateHybridRequest(request) {
    if (!request.file) {
      throw new Error("Audio file is required for hybrid processing");
    }
    if (!request.models) {
      throw new Error("Models configuration is required for hybrid processing");
    }
    if (!request.models.transcription) {
      throw new Error("Transcription model is required for hybrid processing");
    }
    if (!request.models.chat) {
      throw new Error("Chat model is required for hybrid processing");
    }
    if (!request.models.speech) {
      throw new Error("Speech model is required for hybrid processing");
    }
    if (!request.voice) {
      throw new Error("Voice is required for hybrid processing");
    }
    this.validateAudioFile(request.file);
  }
  /**
   * Validates an audio file.
   * @private
   */
  validateAudioFile(file) {
    if (!file.data) {
      throw new Error("Audio file data is required");
    }
    if (!file.filename) {
      throw new Error("Audio filename is required");
    }
    const extension = file.filename.toLowerCase().split(".").pop();
    const supportedExtensions = ["mp3", "wav", "flac", "ogg", "aac", "opus", "pcm", "m4a", "webm"];
    if (!extension || !supportedExtensions.includes(extension)) {
      throw new Error(`Unsupported audio format. Supported formats: ${supportedExtensions.join(", ")}`);
    }
    const maxSize = 25 * 1024 * 1024;
    let fileSize = 0;
    if (Buffer.isBuffer(file.data)) {
      fileSize = file.data.length;
    } else if (file.data instanceof Blob) {
      fileSize = file.data.size;
    } else if (typeof file.data === "string") {
      fileSize = Math.ceil(file.data.length * 0.75);
    }
    if (fileSize > maxSize) {
      throw new Error(`Audio file too large. Maximum size is ${maxSize / (1024 * 1024)}MB`);
    }
  }
  /**
   * Creates FormData for audio file uploads.
   * @private
   */
  createAudioFormData(file, additionalFields) {
    const formData = new FormData();
    let fileBlob;
    if (Buffer.isBuffer(file.data)) {
      fileBlob = new Blob([file.data], { type: file.contentType ?? "audio/mpeg" });
    } else if (file.data instanceof Blob) {
      fileBlob = file.data;
    } else if (typeof file.data === "string") {
      const binaryString = atob(file.data);
      const bytes = new Uint8Array(binaryString.length);
      for (let i = 0; i < binaryString.length; i++) {
        bytes[i] = binaryString.charCodeAt(i);
      }
      fileBlob = new Blob([bytes], { type: file.contentType ?? "audio/mpeg" });
    } else {
      throw new Error("Unsupported file data type");
    }
    formData.append("file", fileBlob, file.filename);
    Object.entries(additionalFields).forEach(([key, value]) => {
      if (value !== void 0) {
        if (typeof value === "object") {
          formData.append(key, JSON.stringify(value));
        } else {
          formData.append(key, String(value));
        }
      }
    });
    return formData;
  }
};
var AudioUtils = class {
  /**
   * Creates an AudioFile from a Buffer with specified filename.
   */
  static fromBuffer(data, filename, contentType) {
    return {
      data,
      filename,
      contentType
    };
  }
  /**
   * Creates an AudioFile from a Blob with specified filename.
   */
  static fromBlob(data, filename) {
    return {
      data,
      filename,
      contentType: data.type
    };
  }
  /**
   * Creates an AudioFile from a base64 string with specified filename.
   */
  static fromBase64(data, filename, contentType) {
    return {
      data,
      filename,
      contentType
    };
  }
  /**
   * Gets audio file metadata (basic validation).
   */
  static getBasicMetadata(file) {
    let size = 0;
    if (Buffer.isBuffer(file.data)) {
      size = file.data.length;
    } else if (file.data instanceof Blob) {
      size = file.data.size;
    } else if (typeof file.data === "string") {
      size = Math.ceil(file.data.length * 0.75);
    }
    const extension = file.filename.toLowerCase().split(".").pop() ?? "unknown";
    return {
      duration: 0,
      // Would need audio analysis library to determine
      size,
      format: extension,
      sample_rate: 0,
      // Would need audio analysis
      channels: 0
      // Would need audio analysis
    };
  }
  /**
   * Validates if the audio format is supported.
   */
  static isFormatSupported(format) {
    const supportedFormats = ["mp3", "wav", "flac", "ogg", "aac", "opus", "pcm", "m4a", "webm"];
    return supportedFormats.includes(format.toLowerCase());
  }
  /**
   * Gets the appropriate content type for an audio format.
   */
  static getContentType(format) {
    const contentTypes = {
      mp3: "audio/mpeg",
      wav: "audio/wav",
      flac: "audio/flac",
      ogg: "audio/ogg",
      aac: "audio/aac",
      opus: "audio/opus",
      pcm: "audio/pcm",
      m4a: "audio/mp4",
      webm: "audio/webm"
    };
    return contentTypes[format] || "audio/mpeg";
  }
};

// src/services/HealthService.ts
var HealthService = class extends FetchBasedClient {
  constructor(client) {
    super(client.config);
  }
  async check(options) {
    return this.get("/health", options);
  }
  async waitForHealth(options) {
    const timeout = options?.timeout ?? 3e4;
    const pollingInterval = options?.pollingInterval ?? 1e3;
    const maxAttempts = Math.floor(timeout / pollingInterval);
    for (let i = 0; i < maxAttempts; i++) {
      try {
        const response = await this.check(options);
        if (response.status === "Healthy") {
          return response;
        }
      } catch {
      }
      if (i < maxAttempts - 1) {
        await new Promise((resolve) => setTimeout(resolve, pollingInterval));
      }
    }
    throw new Error(`Health check failed after ${maxAttempts} attempts`);
  }
};

// src/models/images.ts
var IMAGE_MODELS = {
  DALL_E_2: "dall-e-2",
  DALL_E_3: "dall-e-3",
  MINIMAX_IMAGE: "minimax-image"
};
var IMAGE_MODEL_CAPABILITIES = {
  [IMAGE_MODELS.DALL_E_2]: {
    maxPromptLength: 1e3,
    supportedSizes: ["256x256", "512x512", "1024x1024"],
    supportedQualities: ["standard"],
    supportedStyles: [],
    maxImages: 10,
    supportsEdit: true,
    supportsVariation: true
  },
  [IMAGE_MODELS.DALL_E_3]: {
    maxPromptLength: 4e3,
    supportedSizes: ["1024x1024", "1792x1024", "1024x1792"],
    supportedQualities: ["standard", "hd"],
    supportedStyles: ["vivid", "natural"],
    maxImages: 1,
    supportsEdit: false,
    supportsVariation: false
  },
  [IMAGE_MODELS.MINIMAX_IMAGE]: {
    maxPromptLength: 2e3,
    supportedSizes: ["1024x1024", "1792x1024", "1024x1792"],
    supportedQualities: ["standard", "hd"],
    supportedStyles: ["vivid", "natural"],
    maxImages: 4,
    supportsEdit: false,
    supportsVariation: false
  }
};
var IMAGE_DEFAULTS = {
  model: IMAGE_MODELS.DALL_E_3,
  n: 1,
  quality: "standard",
  response_format: "url",
  size: "1024x1024",
  style: "vivid"
};
var DEFAULT_POLLING_OPTIONS = {
  intervalMs: 1e3,
  timeoutMs: 3e5,
  // 5 minutes
  useExponentialBackoff: true,
  maxIntervalMs: 1e4
  // 10 seconds
};

// src/utils/validation.ts
function validateImageGenerationRequest(request) {
  if (!request.prompt) {
    throw new conduitCommon.ValidationError("Prompt is required", { field: "prompt" });
  }
  if (request.prompt.trim().length === 0) {
    throw new conduitCommon.ValidationError("Prompt cannot be empty", { field: "prompt" });
  }
  if (request.model && IMAGE_MODEL_CAPABILITIES[request.model]) {
    const capabilities = IMAGE_MODEL_CAPABILITIES[request.model];
    if (request.prompt.length > capabilities.maxPromptLength) {
      throw new conduitCommon.ValidationError(
        `Prompt exceeds maximum length of ${capabilities.maxPromptLength} characters for model ${request.model}`,
        { field: "prompt" }
      );
    }
    if (request.n !== void 0 && request.n > capabilities.maxImages) {
      throw new conduitCommon.ValidationError(
        `Number of images (${request.n}) exceeds maximum of ${capabilities.maxImages} for model ${request.model}`,
        { field: "n" }
      );
    }
    if (request.size && !capabilities.supportedSizes.includes(request.size)) {
      throw new conduitCommon.ValidationError(
        `Size '${request.size}' is not supported for model ${request.model}. Supported sizes: ${capabilities.supportedSizes.join(", ")}`,
        { field: "size" }
      );
    }
    if (request.quality && !capabilities.supportedQualities.includes(request.quality)) {
      throw new conduitCommon.ValidationError(
        `Quality '${request.quality}' is not supported for model ${request.model}. Supported qualities: ${capabilities.supportedQualities.join(", ")}`,
        { field: "quality" }
      );
    }
    if (request.style && capabilities.supportedStyles.length > 0 && !capabilities.supportedStyles.includes(request.style)) {
      throw new conduitCommon.ValidationError(
        `Style '${request.style}' is not supported for model ${request.model}. Supported styles: ${capabilities.supportedStyles.join(", ")}`,
        { field: "style" }
      );
    }
  }
  if (request.n !== void 0 && (request.n < 1 || request.n > 10)) {
    throw new conduitCommon.ValidationError("Number of images must be between 1 and 10", { field: "n" });
  }
  if (request.response_format && !ImageValidationHelpers.isValidResponseFormat(request.response_format)) {
    throw new conduitCommon.ValidationError(`response_format must be one of: ${ImageValidationHelpers.getAllResponseFormats().join(", ")}`, { field: "response_format" });
  }
  if (request.quality && !ImageValidationHelpers.isValidQuality(request.quality)) {
    throw new conduitCommon.ValidationError(`quality must be one of: ${ImageValidationHelpers.getAllQualities().join(", ")}`, { field: "quality" });
  }
  if (request.style && !ImageValidationHelpers.isValidStyle(request.style)) {
    throw new conduitCommon.ValidationError(`style must be one of: ${ImageValidationHelpers.getAllStyles().join(", ")}`, { field: "style" });
  }
  if (request.size && !ImageValidationHelpers.isValidSize(request.size)) {
    throw new conduitCommon.ValidationError(
      `size must be one of: ${ImageValidationHelpers.getAllSizes().join(", ")}`,
      { field: "size" }
    );
  }
}

// src/services/ImagesService.ts
var ImagesService = class extends FetchBasedClient {
  constructor(client) {
    super(client.config);
  }
  /**
   * Creates an image given a text prompt.
   * Supports various sizes, styles, and quality settings based on the model.
   * 
   * @param request - The image generation request
   * @param options - Optional request options
   * @returns Promise resolving to image generation response
   * 
   * @example
   * ```typescript
   * // Basic image generation
   * const result = await images.generate({
   *   prompt: 'A serene lake at sunset',
   *   n: 1
   * });
   * console.log(result.data[0].url);
   * 
   * // High quality with specific size
   * const hdResult = await images.generate({
   *   prompt: 'A futuristic city skyline',
   *   model: 'dall-e-3',
   *   size: '1792x1024',
   *   quality: 'hd',
   *   style: 'vivid'
   * });
   * 
   * // Get base64 encoded image
   * const base64Result = await images.generate({
   *   prompt: 'Abstract art',
   *   response_format: 'b64_json'
   * });
   * ```
   */
  async generate(request, options) {
    validateImageGenerationRequest(request);
    return this.post(
      API_ENDPOINTS.V1.IMAGES.GENERATIONS,
      request,
      options
    );
  }
  /**
   * Creates an edited or extended image given an original image and a prompt.
   * The mask specifies which areas should be edited. Transparent areas in the mask indicate where edits should be applied.
   * 
   * @param request - The image edit request
   * @param options - Optional request options
   * @returns Promise resolving to image edit response
   * 
   * @example
   * ```typescript
   * // Edit with a mask
   * const edited = await images.edit({
   *   image: originalImageFile,
   *   mask: maskFile,
   *   prompt: 'Replace the sky with a starry night',
   *   n: 1
   * });
   * 
   * // Edit using image transparency as mask
   * const result = await images.edit({
   *   image: transparentPngFile,
   *   prompt: 'Add a garden in the transparent area',
   *   size: '512x512'
   * });
   * ```
   */
  async edit(request, options) {
    const formData = new FormData();
    formData.append("image", request.image);
    formData.append("prompt", request.prompt);
    if (request.mask) {
      formData.append("mask", request.mask);
    }
    if (request.model) {
      formData.append("model", request.model);
    }
    if (request.n !== void 0) {
      formData.append("n", request.n.toString());
    }
    if (request.response_format) {
      formData.append("response_format", request.response_format);
    }
    if (request.size) {
      formData.append("size", request.size);
    }
    if (request.user) {
      formData.append("user", request.user);
    }
    return this.post(
      API_ENDPOINTS.V1.IMAGES.EDITS,
      formData,
      {
        ...options,
        headers: {
          ...options?.headers,
          "Content-Type": CONTENT_TYPES.FORM_DATA
        }
      }
    );
  }
  /**
   * Creates a variation of a given image.
   * Generates new images that maintain the same general composition but with variations.
   * 
   * @param request - The image variation request
   * @param options - Optional request options
   * @returns Promise resolving to image variation response
   * 
   * @example
   * ```typescript
   * // Create variations
   * const variations = await images.createVariation({
   *   image: originalImageFile,
   *   n: 3,
   *   size: '1024x1024'
   * });
   * 
   * // Get variations as base64
   * const base64Variations = await images.createVariation({
   *   image: imageFile,
   *   n: 2,
   *   response_format: 'b64_json'
   * });
   * ```
   */
  async createVariation(request, options) {
    const formData = new FormData();
    formData.append("image", request.image);
    if (request.model) {
      formData.append("model", request.model);
    }
    if (request.n !== void 0) {
      formData.append("n", request.n.toString());
    }
    if (request.response_format) {
      formData.append("response_format", request.response_format);
    }
    if (request.size) {
      formData.append("size", request.size);
    }
    if (request.user) {
      formData.append("user", request.user);
    }
    return this.post(
      API_ENDPOINTS.V1.IMAGES.VARIATIONS,
      formData,
      {
        ...options,
        headers: {
          ...options?.headers,
          "Content-Type": CONTENT_TYPES.FORM_DATA
        }
      }
    );
  }
  /**
   * Creates an image asynchronously given a text prompt.
   * @param request The async image generation request
   * @param options Optional request options
   * @returns Promise resolving to async task information
   */
  async generateAsync(request, options) {
    validateImageGenerationRequest(request);
    if (request.timeout_seconds !== void 0 && (request.timeout_seconds < 1 || request.timeout_seconds > 3600)) {
      throw new Error("Timeout must be between 1 and 3600 seconds");
    }
    if (request.webhook_url) {
      try {
        const url = new URL(request.webhook_url);
        if (!["http:", "https:"].includes(url.protocol)) {
          throw new Error("WebhookUrl must be a valid HTTP or HTTPS URL");
        }
      } catch {
        throw new Error("WebhookUrl must be a valid HTTP or HTTPS URL");
      }
    }
    return this.post(
      API_ENDPOINTS.V1.IMAGES.ASYNC_GENERATIONS,
      request,
      options
    );
  }
  /**
   * Gets the status of an async image generation task.
   * @param taskId The task identifier
   * @param options Optional request options
   * @returns Promise resolving to the current task status
   */
  async getTaskStatus(taskId, options) {
    if (!taskId?.trim()) {
      throw new Error("Task ID is required");
    }
    return this.get(
      API_ENDPOINTS.V1.IMAGES.TASK_STATUS(taskId),
      options
    );
  }
  /**
   * Cancels a pending or running async image generation task.
   * @param taskId The task identifier
   * @param options Optional request options
   */
  async cancelTask(taskId, options) {
    if (!taskId?.trim()) {
      throw new Error("Task ID is required");
    }
    await this.delete(
      API_ENDPOINTS.V1.IMAGES.CANCEL_TASK(taskId),
      options
    );
  }
  /**
   * Polls an async image generation task until completion or timeout.
   * Automatically handles retries with configurable intervals and backoff.
   * 
   * @param taskId - The task identifier
   * @param pollingOptions - Polling configuration options
   * @param requestOptions - Optional request options
   * @returns Promise resolving to the final generation result
   * 
   * @example
   * ```typescript
   * // Start async generation
   * const task = await images.generateAsync({
   *   prompt: 'Complex artistic scene',
   *   quality: 'hd',
   *   size: '1792x1024'
   * });
   * 
   * // Poll until complete with default settings
   * const result = await images.pollTaskUntilCompletion(task.task_id);
   * 
   * // Custom polling configuration
   * const customResult = await images.pollTaskUntilCompletion(
   *   task.task_id,
   *   {
   *     intervalMs: 2000,
   *     timeoutMs: 300000, // 5 minutes
   *     useExponentialBackoff: true
   *   }
   * );
   * ```
   */
  async pollTaskUntilCompletion(taskId, pollingOptions, requestOptions) {
    if (!taskId?.trim()) {
      throw new Error("Task ID is required");
    }
    const options = { ...DEFAULT_POLLING_OPTIONS, ...pollingOptions };
    const startTime = Date.now();
    let currentInterval = options.intervalMs;
    for (; ; ) {
      if (Date.now() - startTime > options.timeoutMs) {
        throw new Error(`Task polling timed out after ${options.timeoutMs}ms`);
      }
      const status = await this.getTaskStatus(taskId, requestOptions);
      switch (status.status) {
        case "completed":
          if (!status.result) {
            throw new Error("Task completed but no result was provided");
          }
          return status.result;
        case "failed":
          throw new Error(`Task failed: ${status.error ?? "Unknown error"}`);
        case "cancelled":
          throw new Error("Task was cancelled");
        case "timedout":
          throw new Error("Task timed out");
        case "pending":
        case "running":
          break;
        default:
          throw new Error(`Unknown task status: ${String(status.status)}`);
      }
      await new Promise((resolve) => setTimeout(resolve, currentInterval));
      if (options.useExponentialBackoff) {
        currentInterval = Math.min(currentInterval * 2, options.maxIntervalMs);
      }
    }
  }
};

// src/client/ClientAdapter.ts
function createClientAdapter(client) {
  const internalClient = client;
  return {
    request: (urlOrConfig, options) => internalClient.request(urlOrConfig, options),
    get: (url, options) => internalClient.get(url, options),
    post: (url, data, options) => internalClient.post(url, data, options),
    put: (url, data, options) => internalClient.put(url, data, options),
    patch: (url, data, options) => internalClient.patch(url, data, options),
    delete: (url, options) => internalClient.delete(url, options)
  };
}

// src/models/videos.ts
var VideoModels = {
  /** MiniMax video generation model */
  MINIMAX_VIDEO: "minimax-video",
  /** Default video model */
  DEFAULT: "minimax-video"
};
var VideoResolutions = {
  /** 720p resolution (1280x720) */
  HD: "1280x720",
  /** 1080p resolution (1920x1080) */
  FULL_HD: "1920x1080",
  /** Vertical 720p (720x1280) */
  VERTICAL_HD: "720x1280",
  /** Vertical 1080p (1080x1920) */
  VERTICAL_FULL_HD: "1080x1920",
  /** Square format (720x720) */
  SQUARE: "720x720"
};
var VideoResponseFormats = {
  /** Return video as URL (default) */
  URL: "url",
  /** Return video as base64-encoded JSON */
  BASE64_JSON: "b64_json"
};
var VideoDefaults = {
  /** Default duration in seconds */
  DURATION: 5,
  /** Default resolution */
  RESOLUTION: VideoResolutions.HD,
  /** Default frames per second */
  FPS: 30,
  /** Default response format */
  RESPONSE_FORMAT: VideoResponseFormats.URL,
  /** Default polling interval in milliseconds */
  POLLING_INTERVAL_MS: 2e3,
  /** Default polling timeout in milliseconds */
  POLLING_TIMEOUT_MS: 6e5,
  // 10 minutes
  /** Default maximum polling interval in milliseconds */
  MAX_POLLING_INTERVAL_MS: 3e4
  // 30 seconds
};
function getVideoModelCapabilities(model) {
  const modelLower = model.toLowerCase();
  switch (modelLower) {
    case "minimax-video":
    case "minimax-video-01":
      return {
        maxDuration: 6,
        supportedResolutions: [
          VideoResolutions.HD,
          VideoResolutions.FULL_HD,
          VideoResolutions.VERTICAL_HD,
          VideoResolutions.VERTICAL_FULL_HD,
          "720x480"
        ],
        supportedFps: [24, 30],
        supportsCustomStyles: true,
        supportsSeed: true,
        maxVideos: 1
      };
    default:
      return {
        maxDuration: 60,
        supportedResolutions: [
          VideoResolutions.HD,
          VideoResolutions.FULL_HD,
          VideoResolutions.SQUARE
        ],
        supportedFps: [24, 30, 60],
        supportsCustomStyles: true,
        supportsSeed: true,
        maxVideos: 10
      };
  }
}
function validateVideoGenerationRequest(request) {
  if (!request.prompt || request.prompt.trim().length === 0) {
    throw new Error("Prompt is required");
  }
  if (request.n !== void 0 && (request.n <= 0 || request.n > 10)) {
    throw new Error("Number of videos must be between 1 and 10");
  }
  if (request.duration !== void 0 && (request.duration <= 0 || request.duration > 60)) {
    throw new Error("Duration must be between 1 and 60 seconds");
  }
  if (request.fps !== void 0 && (request.fps <= 0 || request.fps > 120)) {
    throw new Error("FPS must be between 1 and 120");
  }
  if (request.response_format && request.response_format !== VideoResponseFormats.URL && request.response_format !== VideoResponseFormats.BASE64_JSON) {
    throw new Error(`Response format must be '${String(VideoResponseFormats.URL)}' or '${String(VideoResponseFormats.BASE64_JSON)}'`);
  }
}
function validateAsyncVideoGenerationRequest(request) {
  validateVideoGenerationRequest(request);
  if (request.timeout_seconds !== void 0 && (request.timeout_seconds <= 0 || request.timeout_seconds > 3600)) {
    throw new Error("Timeout must be between 1 and 3600 seconds");
  }
  if (request.webhook_url && !isValidUrl(request.webhook_url)) {
    throw new Error("WebhookUrl must be a valid HTTP or HTTPS URL");
  }
}
function isValidUrl(url) {
  try {
    const parsedUrl = new URL(url);
    return parsedUrl.protocol === "http:" || parsedUrl.protocol === "https:";
  } catch {
    return false;
  }
}

// src/services/VideosService.ts
var _VideosService = class _VideosService {
  constructor(client) {
    this.clientAdapter = createClientAdapter(client);
  }
  /**
   * @deprecated The synchronous video generation endpoint does not exist. Use generateAsync() instead.
   * This method has been removed to prevent runtime errors.
   */
  // Removed synchronous generate method - endpoint does not exist
  /**
   * Generates videos asynchronously from a text prompt
   */
  async generateAsync(request, options) {
    try {
      validateAsyncVideoGenerationRequest(request);
      const apiRequest = this.convertToAsyncApiRequest(request);
      const response = await this.clientAdapter.post(
        _VideosService.ASYNC_GENERATIONS_ENDPOINT,
        apiRequest,
        options
      );
      return response;
    } catch (error) {
      if (error instanceof conduitCommon.ConduitError) {
        throw error;
      }
      throw new conduitCommon.ConduitError(
        `Async video generation failed: ${error instanceof Error ? error.message : String(error)}`
      );
    }
  }
  /**
   * Gets the status of an async video generation task
   */
  async getTaskStatus(taskId, options) {
    try {
      if (!taskId || taskId.trim().length === 0) {
        throw new Error("Task ID is required");
      }
      const endpoint = `/v1/videos/generations/tasks/${encodeURIComponent(taskId)}`;
      const response = await this.clientAdapter.get(
        endpoint,
        options
      );
      return response;
    } catch (error) {
      if (error instanceof conduitCommon.ConduitError) {
        throw error;
      }
      throw new conduitCommon.ConduitError(
        `Failed to get task status: ${error instanceof Error ? error.message : String(error)}`
      );
    }
  }
  /**
   * Cancels a pending or running async video generation task
   */
  async cancelTask(taskId, options) {
    try {
      if (!taskId || taskId.trim().length === 0) {
        throw new Error("Task ID is required");
      }
      const endpoint = `/v1/videos/generations/${encodeURIComponent(taskId)}`;
      await this.clientAdapter.delete(
        endpoint,
        options
      );
    } catch (error) {
      if (error instanceof conduitCommon.ConduitError) {
        throw error;
      }
      throw new conduitCommon.ConduitError(
        `Failed to cancel task: ${error instanceof Error ? error.message : String(error)}`
      );
    }
  }
  /**
   * Polls an async video generation task until completion or timeout
   */
  async pollTaskUntilCompletion(taskId, pollingOptions, options) {
    const opts = {
      intervalMs: pollingOptions?.intervalMs ?? VideoDefaults.POLLING_INTERVAL_MS,
      timeoutMs: pollingOptions?.timeoutMs ?? VideoDefaults.POLLING_TIMEOUT_MS,
      useExponentialBackoff: pollingOptions?.useExponentialBackoff ?? true,
      maxIntervalMs: pollingOptions?.maxIntervalMs ?? VideoDefaults.MAX_POLLING_INTERVAL_MS
    };
    if (!taskId || taskId.trim().length === 0) {
      throw new Error("Task ID is required");
    }
    const startTime = Date.now();
    let currentInterval = opts.intervalMs;
    for (; ; ) {
      if (options?.signal?.aborted) {
        throw new conduitCommon.ConduitError("Operation was cancelled");
      }
      if (Date.now() - startTime > opts.timeoutMs) {
        throw new conduitCommon.ConduitError(
          `Task polling timed out after ${opts.timeoutMs}ms`
        );
      }
      const status = await this.getTaskStatus(taskId, options);
      switch (status.status) {
        case "Completed" /* Completed */:
          if (!status.result) {
            throw new conduitCommon.ConduitError(
              "Task completed but no result was provided"
            );
          }
          return status.result;
        case "Failed" /* Failed */:
          throw new conduitCommon.ConduitError(
            `Task failed: ${status.error ?? "Unknown error"}`
          );
        case "Cancelled" /* Cancelled */:
          throw new conduitCommon.ConduitError("Task was cancelled");
        case "TimedOut" /* TimedOut */:
          throw new conduitCommon.ConduitError("Task timed out");
        case "Pending" /* Pending */:
        case "Running" /* Running */:
          break;
        default:
          throw new conduitCommon.ConduitError(
            `Unknown task status: ${status.status}`
          );
      }
      await new Promise((resolve) => setTimeout(resolve, currentInterval));
      if (opts.useExponentialBackoff) {
        currentInterval = Math.min(currentInterval * 2, opts.maxIntervalMs);
      }
    }
  }
  /**
   * Gets the capabilities of a video model
   */
  getModelCapabilities(model) {
    return getVideoModelCapabilities(model);
  }
  /**
   * Converts a VideoGenerationRequest to the API request format
   */
  convertToApiRequest(request) {
    return {
      prompt: request.prompt,
      model: request.model ?? VideoModels.DEFAULT,
      duration: request.duration,
      size: request.size,
      fps: request.fps,
      style: request.style,
      response_format: request.response_format ?? VideoResponseFormats.URL,
      user: request.user,
      seed: request.seed,
      n: request.n ?? 1
    };
  }
  /**
   * Converts an AsyncVideoGenerationRequest to the API request format
   */
  convertToAsyncApiRequest(request) {
    const baseRequest = this.convertToApiRequest(request);
    return {
      ...baseRequest,
      webhook_url: request.webhook_url,
      webhook_metadata: request.webhook_metadata,
      webhook_headers: request.webhook_headers,
      timeout_seconds: request.timeout_seconds
    };
  }
};
// Note: /v1/videos/generations endpoint does not exist - only async generation is supported
_VideosService.ASYNC_GENERATIONS_ENDPOINT = "/v1/videos/generations/async";
var VideosService = _VideosService;

// src/services/DiscoveryService.ts
var DiscoveryService = class _DiscoveryService {
  constructor(client) {
    this.baseEndpoint = "/v1/discovery";
    this.clientAdapter = createClientAdapter(client);
  }
  /**
   * Gets all discovered models and their capabilities.
   */
  async getModels(options) {
    const response = await this.clientAdapter.get(
      `${this.baseEndpoint}/models`,
      options
    );
    return response;
  }
  /**
   * Gets models for a specific provider.
   */
  async getProviderModels(provider, options) {
    if (!provider?.trim()) {
      throw new Error("Provider name is required");
    }
    const response = await this.clientAdapter.get(
      `${this.baseEndpoint}/providers/${encodeURIComponent(provider)}/models`,
      options
    );
    return response;
  }
  /**
   * Tests if a model supports a specific capability.
   */
  async testModelCapability(model, capability, options) {
    if (!model?.trim()) {
      throw new Error("Model name is required");
    }
    const response = await this.clientAdapter.get(
      `${this.baseEndpoint}/models/${encodeURIComponent(model)}/capabilities/${capability}`,
      options
    );
    return response;
  }
  /**
   * Tests multiple model capabilities in a single request.
   */
  async testBulkCapabilities(request, options) {
    if (!request.tests || request.tests.length === 0) {
      throw new Error("At least one test is required");
    }
    const response = await this.clientAdapter.post(
      `${this.baseEndpoint}/bulk/capabilities`,
      request,
      options
    );
    return response;
  }
  /**
   * Gets discovery information for multiple models in a single request.
   */
  async getBulkModels(request, options) {
    if (!request.models || request.models.length === 0) {
      throw new Error("At least one model is required");
    }
    const response = await this.clientAdapter.post(
      `${this.baseEndpoint}/bulk/models`,
      request,
      options
    );
    return response;
  }
  /**
   * Refreshes the capability cache for all providers.
   * Requires admin/master key access.
   */
  async refreshCapabilities(options) {
    await this.clientAdapter.post(
      `${this.baseEndpoint}/refresh`,
      void 0,
      options
    );
  }
  /**
   * Static validation helper to test capabilities without making API calls.
   */
  static validateCapabilityTest(test) {
    if (!test.model?.trim()) {
      throw new Error("Model name is required");
    }
    if (!test.capability?.trim()) {
      throw new Error("Capability name is required");
    }
  }
  /**
   * Static validation helper for bulk requests.
   */
  static validateBulkCapabilityRequest(request) {
    if (!request.tests || request.tests.length === 0) {
      throw new Error("At least one test is required");
    }
    request.tests.forEach((test, index) => {
      try {
        _DiscoveryService.validateCapabilityTest(test);
      } catch (error) {
        throw new Error(`Invalid test at index ${index}: ${error instanceof Error ? error.message : String(error)}`);
      }
    });
  }
  /**
   * Static validation helper for bulk model discovery requests.
   */
  static validateBulkModelRequest(request) {
    if (!request.models || request.models.length === 0) {
      throw new Error("At least one model is required");
    }
    request.models.forEach((model, index) => {
      if (!model?.trim()) {
        throw new Error(`Invalid model at index ${index}: Model name is required`);
      }
    });
  }
};

// src/FetchConduitCoreClient.ts
var FetchConduitCoreClient = class extends FetchBasedClient {
  constructor(config) {
    super(config);
    this.chat = new FetchChatService(config);
    this.audio = new AudioService(this);
    this.health = new HealthService(this);
    this.images = new ImagesService(this);
    this.videos = new VideosService(this);
    this.discovery = new DiscoveryService(this);
  }
  /**
   * Type guard for checking if an error is a ConduitError
   */
  isConduitError(error) {
    return error instanceof conduitCommon.ConduitError;
  }
  /**
   * Type guard for checking if an error is an authentication error
   */
  isAuthError(error) {
    return this.isConduitError(error) && error.statusCode === 401;
  }
  /**
   * Type guard for checking if an error is a rate limit error
   */
  isRateLimitError(error) {
    return this.isConduitError(error) && error.statusCode === 429;
  }
  /**
   * Type guard for checking if an error is a validation error
   */
  isValidationError(error) {
    return this.isConduitError(error) && error.statusCode === 400;
  }
  /**
   * Type guard for checking if an error is a not found error
   */
  isNotFoundError(error) {
    return this.isConduitError(error) && error.statusCode === 404;
  }
  /**
   * Type guard for checking if an error is a server error
   */
  isServerError(error) {
    return this.isConduitError(error) && error.statusCode !== void 0 && error.statusCode >= 500;
  }
  /**
   * Type guard for checking if an error is a network error
   */
  isNetworkError(error) {
    return this.isConduitError(error) && (error.code === "ECONNABORTED" || error.code === "network_error");
  }
};

// src/models/chat.ts
var ContentHelpers = {
  /**
   * Creates a text content part
   */
  text(text) {
    return { type: "text", text };
  },
  /**
   * Creates an image content part from a URL
   */
  imageUrl(url, detail) {
    return {
      type: "image_url",
      image_url: { url, detail }
    };
  },
  /**
   * Creates an image content part from base64 data
   */
  imageBase64(base64Data, mimeType = "image/jpeg", detail) {
    return {
      type: "image_url",
      image_url: {
        url: `data:${mimeType};base64,${base64Data}`,
        detail
      }
    };
  },
  /**
   * Checks if content contains images
   */
  hasImages(content) {
    if (!Array.isArray(content)) return false;
    return content.some((part) => part.type === "image_url");
  },
  /**
   * Extracts text from multi-modal content
   */
  extractText(content) {
    if (typeof content === "string") return content;
    if (!content) return "";
    if (!Array.isArray(content)) return "";
    return content.filter((part) => part.type === "text").map((part) => part.text).join(" ");
  },
  /**
   * Extracts images from multi-modal content
   */
  extractImages(content) {
    if (!Array.isArray(content)) return [];
    return content.filter((part) => part.type === "image_url");
  }
};

// src/models/enhanced-streaming.ts
function isChatCompletionChunk(data) {
  return typeof data === "object" && data !== null && "object" in data && data.object === "chat.completion.chunk";
}
function isStreamingMetrics(data) {
  return typeof data === "object" && data !== null && ("current_tokens_per_second" in data || "tokens_generated" in data || "elapsed_ms" in data);
}
function isFinalMetrics(data) {
  return typeof data === "object" && data !== null && ("tokens_per_second" in data || "total_latency_ms" in data || "completion_tokens" in data);
}

// src/services/TasksService.ts
var TaskDefaults = {
  /** Default polling interval in milliseconds */
  POLLING_INTERVAL_MS: 2e3,
  /** Default polling timeout in milliseconds */
  POLLING_TIMEOUT_MS: 6e5,
  // 10 minutes
  /** Default maximum polling interval in milliseconds */
  MAX_POLLING_INTERVAL_MS: 3e4
  // 30 seconds
};
var TaskHelpers = {
  /**
   * Creates polling options with sensible defaults
   */
  createPollingOptions(options) {
    return {
      intervalMs: options?.intervalMs ?? TaskDefaults.POLLING_INTERVAL_MS,
      timeoutMs: options?.timeoutMs ?? TaskDefaults.POLLING_TIMEOUT_MS,
      useExponentialBackoff: options?.useExponentialBackoff ?? true,
      maxIntervalMs: options?.maxIntervalMs ?? TaskDefaults.MAX_POLLING_INTERVAL_MS
    };
  }
};

// src/services/BatchOperationsService.ts
var BatchOperationsService = class {
  constructor(client) {
    this.clientAdapter = createClientAdapter(client);
  }
  /**
   * Performs a batch spend update operation
   * 
   * @param request - The batch spend update request containing up to 10,000 spend updates
   * @returns Promise<BatchOperationStartResponse> The batch operation start response
   * @throws {ConduitCoreError} When the API request fails or request validation fails
   * 
   * @example
   * ```typescript
   * const spendUpdates = [
   *   { virtualKeyId: 1, amount: 10.50, model: 'gpt-4', provider: 'openai' },
   *   { virtualKeyId: 2, amount: 5.25, model: 'claude-3', provider: 'anthropic' }
   * ];
   * 
   * const startResponse = await coreClient.batchOperations.batchSpendUpdate({
   *   spendUpdates
   * });
   * 
   * console.log(`Started batch operation: ${startResponse.operationId}`);
   * console.log(`Track progress with task ID: ${startResponse.taskId}`);
   * ```
   */
  async batchSpendUpdate(request) {
    if (request.spendUpdates.length > 1e4) {
      throw new Error("Batch spend updates cannot exceed 10,000 items");
    }
    if (request.spendUpdates.length === 0) {
      throw new Error("Cannot create empty batch spend update request");
    }
    return this.clientAdapter.post(
      "/v1/batch/spend-updates",
      request
    );
  }
  /**
   * Performs a batch virtual key update operation (requires admin permissions)
   * 
   * @param request - The batch virtual key update request containing up to 1,000 virtual key updates
   * @returns Promise<BatchOperationStartResponse> The batch operation start response
   * @throws {ConduitCoreError} When the API request fails or request validation fails
   * 
   * @example
   * ```typescript
   * const virtualKeyUpdates = [
   *   { virtualKeyId: 1, maxBudget: 1000, isEnabled: true },
   *   { virtualKeyId: 2, allowedModels: ['gpt-4', 'gpt-3.5-turbo'] }
   * ];
   * 
   * const startResponse = await coreClient.batchOperations.batchVirtualKeyUpdate({
   *   virtualKeyUpdates
   * });
   * 
   * console.log(`Started virtual key batch operation: ${startResponse.operationId}`);
   * ```
   */
  async batchVirtualKeyUpdate(request) {
    if (request.virtualKeyUpdates.length > 1e3) {
      throw new Error("Batch virtual key updates cannot exceed 1,000 items");
    }
    if (request.virtualKeyUpdates.length === 0) {
      throw new Error("Cannot create empty batch virtual key update request");
    }
    return this.clientAdapter.post(
      "/v1/batch/virtual-key-updates",
      request
    );
  }
  /**
   * Performs a batch webhook send operation
   * 
   * @param request - The batch webhook send request containing up to 5,000 webhook sends
   * @returns Promise<BatchOperationStartResponse> The batch operation start response
   * @throws {ConduitCoreError} When the API request fails or request validation fails
   * 
   * @example
   * ```typescript
   * const webhookSends = [
   *   {
   *     url: 'https://example.com/webhook',
   *     eventType: 'spend_update',
   *     payload: { userId: 123, amount: 10.50 },
   *     headers: { 'X-Custom-Header': 'value' }
   *   }
   * ];
   * 
   * const startResponse = await coreClient.batchOperations.batchWebhookSend({
   *   webhookSends
   * });
   * 
   * console.log(`Started webhook batch operation: ${startResponse.operationId}`);
   * ```
   */
  async batchWebhookSend(request) {
    if (request.webhookSends.length > 5e3) {
      throw new Error("Batch webhook sends cannot exceed 5,000 items");
    }
    if (request.webhookSends.length === 0) {
      throw new Error("Cannot create empty batch webhook send request");
    }
    return this.clientAdapter.post(
      "/v1/batch/webhook-sends",
      request
    );
  }
  /**
   * Gets the status of a batch operation
   * 
   * @param operationId - The unique identifier of the batch operation
   * @returns Promise<BatchOperationStatusResponse> The batch operation status response
   * @throws {ConduitCoreError} When the API request fails
   * 
   * @example
   * ```typescript
   * const status = await coreClient.batchOperations.getOperationStatus('batch-123');
   * 
   * console.log(`Operation status: ${status.status}`);
   * console.log(`Progress: ${status.metadata.processedItems}/${status.metadata.totalItems}`);
   * console.log(`Success rate: ${((status.metadata.processedItems - status.metadata.failedItems) / status.metadata.processedItems * 100).toFixed(2)}%`);
   * 
   * if (status.status === BatchOperationStatusEnum.Completed) {
   *   console.log('Batch operation completed!');
   * } else if (status.status === BatchOperationStatusEnum.Failed) {
   *   console.log('Batch operation failed:', status.errors);
   * }
   * ```
   */
  async getOperationStatus(operationId) {
    return this.clientAdapter.get(
      `/v1/batch/operations/${operationId}`
    );
  }
  /**
   * Cancels a running batch operation
   * 
   * @param operationId - The unique identifier of the batch operation to cancel
   * @returns Promise<BatchOperationStatusResponse> The updated batch operation status response
   * @throws {ConduitCoreError} When the API request fails
   * 
   * @example
   * ```typescript
   * const canceledStatus = await coreClient.batchOperations.cancelOperation('batch-123');
   * console.log(`Operation canceled. Final status: ${canceledStatus.status}`);
   * ```
   */
  async cancelOperation(operationId) {
    return this.clientAdapter.post(
      `/v1/batch/operations/${operationId}/cancel`,
      {}
    );
  }
  /**
   * Polls a batch operation until completion or timeout
   * 
   * @param operationId - The unique identifier of the batch operation
   * @param options - Polling options (interval and timeout)
   * @returns Promise<BatchOperationStatusResponse> The final batch operation status response
   * @throws {Error} When the operation doesn't complete within the timeout
   * 
   * @example
   * ```typescript
   * // Poll every 3 seconds for up to 5 minutes
   * const finalStatus = await coreClient.batchOperations.pollOperation('batch-123', {
   *   pollingInterval: 3000,
   *   timeout: 300000
   * });
   * 
   * if (finalStatus.status === BatchOperationStatusEnum.Completed) {
   *   console.log('Operation completed successfully!');
   *   console.log(`Processed ${finalStatus.metadata.processedItems} items`);
   * }
   * ```
   */
  async pollOperation(operationId, options = {}) {
    const pollingInterval = options.pollingInterval ?? 5e3;
    const timeout = options.timeout ?? 6e5;
    const startTime = Date.now();
    let lastStatus;
    while (Date.now() - startTime < timeout) {
      lastStatus = await this.getOperationStatus(operationId);
      if (lastStatus.status === "Completed" /* Completed */ || lastStatus.status === "Failed" /* Failed */ || lastStatus.status === "Cancelled" /* Cancelled */ || lastStatus.status === "PartiallyCompleted" /* PartiallyCompleted */) {
        return lastStatus;
      }
      await new Promise((resolve) => setTimeout(resolve, pollingInterval));
    }
    throw new Error(
      `Batch operation ${operationId} did not complete within ${timeout}ms. Last status: ${lastStatus?.status ?? "unknown"}`
    );
  }
  /**
   * Validates a batch spend update request
   * 
   * @param spendUpdates - Array of spend updates to validate
   * @param options - Validation options
   * @returns BatchValidationResult Validation result with errors and warnings
   * 
   * @example
   * ```typescript
   * const spendUpdates = [
   *   { virtualKeyId: 1, amount: 10.50, model: 'gpt-4', provider: 'openai' },
   *   { virtualKeyId: 0, amount: -5, model: '', provider: 'invalid' } // Invalid
   * ];
   * 
   * const validation = BatchOperationsService.validateSpendUpdateRequest(spendUpdates);
   * if (!validation.isValid) {
   *   console.log('Validation errors:', validation.errors);
   * }
   * ```
   */
  static validateSpendUpdateRequest(spendUpdates, options = {}) {
    const errors = [];
    const warnings = [];
    const validateItems = options.validateItems !== false;
    if (spendUpdates.length > 1e4) {
      errors.push("Cannot process more than 10,000 spend updates in a single batch");
    }
    if (spendUpdates.length === 0) {
      errors.push("Cannot create empty batch spend update request");
    }
    if (validateItems) {
      spendUpdates.forEach((update, index) => {
        if (!update.virtualKeyId || update.virtualKeyId <= 0) {
          errors.push(`Invalid virtualKeyId at index ${index}: ${update.virtualKeyId}`);
        }
        if (!update.amount || update.amount <= 0 || update.amount > 1e6) {
          errors.push(`Invalid amount at index ${index}: ${update.amount}. Must be between 0.0001 and 1,000,000`);
        }
        if (!update.model || update.model.trim() === "") {
          errors.push(`Model cannot be empty at index ${index}`);
        }
        if (!update.provider || update.provider.trim() === "") {
          errors.push(`Provider cannot be empty at index ${index}`);
        }
        if (update.amount && update.amount < 0.01) {
          warnings.push(`Small amount at index ${index}: ${update.amount}. Consider using larger amounts for better efficiency`);
        }
      });
    }
    return {
      isValid: errors.length === 0,
      errors,
      itemCount: spendUpdates.length,
      warnings: warnings.length > 0 ? warnings : void 0
    };
  }
  /**
   * Validates a batch virtual key update request
   * 
   * @param virtualKeyUpdates - Array of virtual key updates to validate
   * @param options - Validation options
   * @returns BatchValidationResult Validation result with errors and warnings
   */
  static validateVirtualKeyUpdateRequest(virtualKeyUpdates, options = {}) {
    const errors = [];
    const warnings = [];
    const validateItems = options.validateItems !== false;
    const validateDates = options.validateDates !== false;
    if (virtualKeyUpdates.length > 1e3) {
      errors.push("Cannot process more than 1,000 virtual key updates in a single batch");
    }
    if (virtualKeyUpdates.length === 0) {
      errors.push("Cannot create empty batch virtual key update request");
    }
    if (validateItems) {
      virtualKeyUpdates.forEach((update, index) => {
        if (!update.virtualKeyId || update.virtualKeyId <= 0) {
          errors.push(`Invalid virtualKeyId at index ${index}: ${update.virtualKeyId}`);
        }
        if (update.maxBudget !== void 0 && update.maxBudget < 0) {
          errors.push(`Invalid maxBudget at index ${index}: ${update.maxBudget}. Cannot be negative`);
        }
        if (validateDates && update.expiresAt) {
          const expiryDate = new Date(update.expiresAt);
          if (isNaN(expiryDate.getTime())) {
            errors.push(`Invalid expiresAt date format at index ${index}: ${update.expiresAt}`);
          } else if (expiryDate < /* @__PURE__ */ new Date()) {
            errors.push(`Invalid expiresAt at index ${index}: ${update.expiresAt}. Cannot be in the past`);
          }
        }
        if (update.allowedModels && update.allowedModels.length === 0) {
          warnings.push(`Empty allowedModels array at index ${index}. This will remove all model restrictions`);
        }
      });
    }
    return {
      isValid: errors.length === 0,
      errors,
      itemCount: virtualKeyUpdates.length,
      warnings: warnings.length > 0 ? warnings : void 0
    };
  }
  /**
   * Validates a batch webhook send request
   * 
   * @param webhookSends - Array of webhook sends to validate
   * @param options - Validation options
   * @returns BatchValidationResult Validation result with errors and warnings
   */
  static validateWebhookSendRequest(webhookSends, options = {}) {
    const errors = [];
    const warnings = [];
    const validateItems = options.validateItems !== false;
    const validateUrls = options.validateUrls !== false;
    if (webhookSends.length > 5e3) {
      errors.push("Cannot process more than 5,000 webhook sends in a single batch");
    }
    if (webhookSends.length === 0) {
      errors.push("Cannot create empty batch webhook send request");
    }
    if (validateItems) {
      webhookSends.forEach((webhook, index) => {
        if (!webhook.url || webhook.url.trim() === "") {
          errors.push(`URL cannot be empty at index ${index}`);
        } else if (validateUrls) {
          try {
            const url = new URL(webhook.url);
            if (url.protocol !== "http:" && url.protocol !== "https:") {
              errors.push(`Invalid URL protocol at index ${index}: ${webhook.url}. Must be http or https`);
            }
          } catch {
            errors.push(`Invalid URL format at index ${index}: ${webhook.url}`);
          }
        }
        if (!webhook.eventType || webhook.eventType.trim() === "") {
          errors.push(`EventType cannot be empty at index ${index}`);
        }
        if (!webhook.payload || Object.keys(webhook.payload).length === 0) {
          errors.push(`Payload cannot be empty at index ${index}`);
        }
        if (webhook.headers && Object.keys(webhook.headers).length > 50) {
          warnings.push(`Large number of headers at index ${index}. Consider reducing for better performance`);
        }
      });
    }
    return {
      isValid: errors.length === 0,
      errors,
      itemCount: webhookSends.length,
      warnings: warnings.length > 0 ? warnings : void 0
    };
  }
  /**
   * Creates a validated batch spend update request
   * 
   * @param spendUpdates - Array of spend updates
   * @returns BatchSpendUpdateRequest Validated request object
   * @throws {Error} When validation fails
   */
  static createSpendUpdateRequest(spendUpdates) {
    const validation = this.validateSpendUpdateRequest(spendUpdates);
    if (!validation.isValid) {
      throw new Error(`Batch spend update validation failed: ${validation.errors.join(", ")}`);
    }
    return { spendUpdates };
  }
  /**
   * Creates a validated batch virtual key update request
   * 
   * @param virtualKeyUpdates - Array of virtual key updates
   * @returns BatchVirtualKeyUpdateRequest Validated request object
   * @throws {Error} When validation fails
   */
  static createVirtualKeyUpdateRequest(virtualKeyUpdates) {
    const validation = this.validateVirtualKeyUpdateRequest(virtualKeyUpdates);
    if (!validation.isValid) {
      throw new Error(`Batch virtual key update validation failed: ${validation.errors.join(", ")}`);
    }
    return { virtualKeyUpdates };
  }
  /**
   * Creates a validated batch webhook send request
   * 
   * @param webhookSends - Array of webhook sends
   * @returns BatchWebhookSendRequest Validated request object
   * @throws {Error} When validation fails
   */
  static createWebhookSendRequest(webhookSends) {
    const validation = this.validateWebhookSendRequest(webhookSends);
    if (!validation.isValid) {
      throw new Error(`Batch webhook send validation failed: ${validation.errors.join(", ")}`);
    }
    return { webhookSends };
  }
};

// src/services/MetricsService.ts
var MetricsService = class {
  constructor(client) {
    this.clientAdapter = createClientAdapter(client);
  }
  /**
   * Gets the current comprehensive metrics snapshot
   * 
   * @returns Promise<MetricsSnapshot> A complete snapshot of current system metrics
   */
  async getCurrentMetrics() {
    const response = await this.clientAdapter.get(
      "/metrics"
    );
    return response;
  }
  /**
   * Gets current database connection pool metrics
   * 
   * @returns Promise<DatabaseMetrics> Database connection pool metrics
   */
  async getDatabasePoolMetrics() {
    const response = await this.clientAdapter.get(
      "/metrics/database/pool"
    );
    return response;
  }
  /**
   * Gets the raw Prometheus metrics format
   * 
   * @returns Promise<string> Prometheus-formatted metrics as a string
   */
  async getPrometheusMetrics() {
    const response = await this.clientAdapter.get(
      "/metrics",
      {
        headers: {
          "Accept": "text/plain"
        }
      }
    );
    return response;
  }
  /**
   * Gets historical metrics data for a specified time range
   * 
   * @param request - The historical metrics request parameters
   * @returns Promise<HistoricalMetricsResponse> Historical metrics data
   */
  async getHistoricalMetrics(request) {
    const response = await this.clientAdapter.post(
      "/metrics/historical",
      request
    );
    return response;
  }
  /**
   * Gets historical metrics for a specific time range with simplified parameters
   * 
   * @param startTime - Start time for the metrics query
   * @param endTime - End time for the metrics query
   * @param metricNames - Optional list of specific metrics to retrieve
   * @param interval - Optional interval for data aggregation (default: "5m")
   * @returns Promise<HistoricalMetricsResponse> Historical metrics data
   */
  async getHistoricalMetricsSimple(startTime, endTime, metricNames = [], interval = "5m") {
    const request = {
      startTime,
      endTime,
      metricNames,
      interval
    };
    return this.getHistoricalMetrics(request);
  }
  /**
   * Gets current HTTP performance metrics
   * 
   * @returns Promise<HttpMetrics> HTTP performance metrics
   */
  async getHttpMetrics() {
    const snapshot = await this.getCurrentMetrics();
    return snapshot.http;
  }
  /**
   * Gets current business metrics
   * 
   * @returns Promise<BusinessMetrics> Business metrics including costs and usage
   */
  async getBusinessMetrics() {
    const snapshot = await this.getCurrentMetrics();
    return snapshot.business;
  }
  /**
   * Gets current system resource metrics
   * 
   * @returns Promise<SystemMetrics> System resource metrics
   */
  async getSystemMetrics() {
    const snapshot = await this.getCurrentMetrics();
    return snapshot.system;
  }
  /**
   * Gets current infrastructure component metrics
   * 
   * @returns Promise<InfrastructureMetrics> Infrastructure metrics including database, Redis, and messaging
   */
  async getInfrastructureMetrics() {
    const snapshot = await this.getCurrentMetrics();
    return snapshot.infrastructure;
  }
  /**
   * Gets current provider health status for all providers
   * 
   * @returns Promise<ProviderHealthStatus[]> List of provider health statuses
   */
  async getProviderHealth() {
    const snapshot = await this.getCurrentMetrics();
    return snapshot.providerHealth;
  }
  /**
   * Gets health status for a specific provider
   * 
   * @param providerName - The name of the provider
   * @returns Promise<ProviderHealthStatus | null> Provider health status, or null if not found
   */
  async getProviderHealthByName(providerName) {
    if (!providerName?.trim()) {
      throw new Error("Provider name cannot be null or empty");
    }
    const allProviders = await this.getProviderHealth();
    return allProviders.find((p) => p.providerName.toLowerCase() === providerName.toLowerCase()) ?? null;
  }
  /**
   * Gets the top performing models by request volume
   * 
   * @param count - Number of top models to return (default: 10)
   * @returns Promise<ModelUsageStats[]> List of top performing models ordered by request volume
   */
  async getTopModelsByRequestVolume(count = 10) {
    if (count <= 0) {
      throw new Error("Count must be greater than 0");
    }
    const metrics = await this.getBusinessMetrics();
    return metrics.modelUsage.sort((a, b) => b.requestsPerMinute - a.requestsPerMinute).slice(0, count);
  }
  /**
   * Gets the top spending virtual keys
   * 
   * @param count - Number of top virtual keys to return (default: 10)
   * @returns Promise<VirtualKeyStats[]> List of top spending virtual keys ordered by current spend
   */
  async getTopSpendingVirtualKeys(count = 10) {
    if (count <= 0) {
      throw new Error("Count must be greater than 0");
    }
    const metrics = await this.getBusinessMetrics();
    return metrics.virtualKeyStats.sort((a, b) => b.currentSpend - a.currentSpend).slice(0, count);
  }
  /**
   * Gets providers that are currently unhealthy
   * 
   * @returns Promise<ProviderHealthStatus[]> List of unhealthy providers
   */
  async getUnhealthyProviders() {
    const allProviders = await this.getProviderHealth();
    return allProviders.filter((p) => !p.isHealthy);
  }
  /**
   * Calculates the overall system health percentage
   * 
   * @returns Promise<number> System health percentage (0-100)
   */
  async getSystemHealthPercentage() {
    const providers = await this.getProviderHealth();
    if (providers.length === 0) return 100;
    const healthyCount = providers.filter((p) => p.isHealthy).length;
    return healthyCount / providers.length * 100;
  }
  /**
   * Gets the current cost burn rate in USD per hour
   * 
   * @returns Promise<number> Current cost burn rate in USD per hour
   */
  async getCurrentCostBurnRate() {
    const metrics = await this.getBusinessMetrics();
    return metrics.cost.costPerMinute * 60;
  }
  /**
   * Checks if the system is currently healthy based on configurable thresholds
   * 
   * @param options - Health check criteria
   * @returns Promise<boolean> True if the system is healthy based on the specified thresholds
   */
  async isSystemHealthy(options = {}) {
    const {
      maxErrorRate = 5,
      maxResponseTime = 2e3,
      minProviderHealthPercentage = 80
    } = options;
    try {
      const snapshot = await this.getCurrentMetrics();
      if (snapshot.http.errorRate > maxErrorRate) {
        return false;
      }
      if (snapshot.http.responseTimes.p95 > maxResponseTime) {
        return false;
      }
      const providerHealth = await this.getSystemHealthPercentage();
      if (providerHealth < minProviderHealthPercentage) {
        return false;
      }
      return true;
    } catch {
      return false;
    }
  }
  /**
   * Gets a summary of key performance indicators
   * 
   * @returns Promise<KPISummary> A summary object with key performance indicators
   */
  async getKPISummary() {
    const snapshot = await this.getCurrentMetrics();
    const systemHealth = await this.getSystemHealthPercentage();
    const costBurnRate = await this.getCurrentCostBurnRate();
    return {
      timestamp: snapshot.timestamp,
      systemHealth: {
        overallHealthPercentage: systemHealth,
        errorRate: snapshot.http.errorRate,
        responseTimeP95: snapshot.http.responseTimes.p95,
        activeConnections: snapshot.infrastructure.database.activeConnections,
        databaseUtilization: snapshot.infrastructure.database.poolUtilization
      },
      performance: {
        requestsPerSecond: snapshot.http.requestsPerSecond,
        activeRequests: snapshot.http.activeRequests,
        averageResponseTime: snapshot.http.responseTimes.average,
        cacheHitRate: snapshot.infrastructure.redis.hitRate
      },
      business: {
        activeVirtualKeys: snapshot.business.activeVirtualKeys,
        requestsPerMinute: snapshot.business.totalRequestsPerMinute,
        costBurnRatePerHour: costBurnRate,
        averageCostPerRequest: snapshot.business.cost.averageCostPerRequest
      },
      infrastructure: {
        cpuUsage: snapshot.system.cpuUsagePercent,
        memoryUsage: snapshot.system.memoryUsageMB,
        uptime: snapshot.system.uptime,
        signalRConnections: snapshot.infrastructure.signalR.activeConnections
      }
    };
  }
  /**
   * Gets metrics for the last N minutes
   * 
   * @param minutes - Number of minutes to look back
   * @param interval - Data aggregation interval (default: "1m")
   * @returns Promise<HistoricalMetricsResponse> Historical metrics for the specified period
   */
  async getMetricsForLastMinutes(minutes, interval = "1m") {
    const endTime = /* @__PURE__ */ new Date();
    const startTime = new Date(endTime.getTime() - minutes * 60 * 1e3);
    return this.getHistoricalMetricsSimple(startTime, endTime, [], interval);
  }
  /**
   * Gets metrics for the last N hours
   * 
   * @param hours - Number of hours to look back
   * @param interval - Data aggregation interval (default: "5m")
   * @returns Promise<HistoricalMetricsResponse> Historical metrics for the specified period
   */
  async getMetricsForLastHours(hours, interval = "5m") {
    const endTime = /* @__PURE__ */ new Date();
    const startTime = new Date(endTime.getTime() - hours * 60 * 60 * 1e3);
    return this.getHistoricalMetricsSimple(startTime, endTime, [], interval);
  }
  /**
   * Gets metrics for today
   * 
   * @param interval - Data aggregation interval (default: "15m")
   * @returns Promise<HistoricalMetricsResponse> Historical metrics for today
   */
  async getMetricsForToday(interval = "15m") {
    const endTime = /* @__PURE__ */ new Date();
    const startTime = /* @__PURE__ */ new Date();
    startTime.setHours(0, 0, 0, 0);
    return this.getHistoricalMetricsSimple(startTime, endTime, [], interval);
  }
};

// src/models/discovery.ts
var ModelCapability = /* @__PURE__ */ ((ModelCapability3) => {
  ModelCapability3["Chat"] = "Chat";
  ModelCapability3["ChatStream"] = "ChatStream";
  ModelCapability3["Embeddings"] = "Embeddings";
  ModelCapability3["ImageGeneration"] = "ImageGeneration";
  ModelCapability3["Vision"] = "Vision";
  ModelCapability3["VideoGeneration"] = "VideoGeneration";
  ModelCapability3["VideoUnderstanding"] = "VideoUnderstanding";
  ModelCapability3["FunctionCalling"] = "FunctionCalling";
  ModelCapability3["ToolUse"] = "ToolUse";
  ModelCapability3["JsonMode"] = "JsonMode";
  return ModelCapability3;
})(ModelCapability || {});

// src/services/ProviderModelsService.ts
var ProviderModelsService = class {
  constructor(client) {
    this.baseEndpoint = "/api/provider-models";
    this.clientAdapter = createClientAdapter(client);
  }
  /**
   * Gets available models for a specified provider.
   * @param providerName - Name of the provider
   * @param forceRefresh - Whether to bypass cache and force refresh
   * @returns List of available model IDs
   */
  async getProviderModels(providerName, forceRefresh = false, options) {
    if (!providerName?.trim()) {
      throw new Error("Provider name is required");
    }
    const queryParams = forceRefresh ? "?forceRefresh=true" : "";
    const response = await this.clientAdapter.get(
      `${this.baseEndpoint}/${encodeURIComponent(providerName)}${queryParams}`,
      options
    );
    return response;
  }
  /**
   * Static validation helper to validate provider name.
   */
  static validateProviderName(providerName) {
    if (!providerName?.trim()) {
      throw new Error("Provider name is required");
    }
  }
};
var BaseSignalRConnection = class extends conduitCommon.BaseSignalRConnection {
  constructor(baseUrl, virtualKey) {
    const authConfig = {
      authToken: virtualKey,
      authType: "virtual"
    };
    const config = {
      baseUrl,
      auth: authConfig,
      options: {
        reconnectionDelay: [0, 2e3, 1e4, 3e4]
      },
      userAgent: "Conduit-Core-Node-Client/0.2.0"
    };
    super(config);
    this.virtualKey = virtualKey;
  }
  /**
   * Starts the SignalR connection.
   */
  async start() {
    await this.getConnection();
  }
  /**
   * Stops the SignalR connection.
   */
  async stop() {
    await this.disconnect();
  }
  /**
   * Waits for the connection to be established.
   */
  async waitForConnection(timeoutMs = 3e4) {
    try {
      await Promise.race([
        this.waitForReady(),
        new Promise(
          (_, reject) => setTimeout(() => reject(new Error("Connection timeout")), timeoutMs)
        )
      ]);
      return true;
    } catch {
      return false;
    }
  }
};
var SignalREndpoints = {
  TaskHub: "/hubs/tasks",
  VideoGenerationHub: "/hubs/video-generation",
  ImageGenerationHub: "/hubs/image-generation",
  NavigationStateHub: "/hubs/navigation-state"
};

// src/signalr/TaskHubClient.ts
var TaskHubClient = class extends BaseSignalRConnection {
  /**
   * Gets the hub path for task notifications.
   */
  get hubPath() {
    return SignalREndpoints.TaskHub;
  }
  /**
   * Configures the hub-specific event handlers.
   */
  configureHubHandlers(connection) {
    connection.on("TaskStarted", async (taskId, taskType, metadata) => {
      console.warn(`Task started: ${taskId}, Type: ${taskType}`);
      if (this.onTaskStarted) {
        await this.onTaskStarted({ eventType: "TaskStarted", taskId, taskType, metadata });
      }
    });
    connection.on("TaskProgress", async (taskId, progress, message) => {
      console.warn(`Task progress: ${taskId}, Progress: ${progress}%`);
      if (this.onTaskProgress) {
        await this.onTaskProgress({ eventType: "TaskProgress", taskId, progress, message });
      }
    });
    connection.on("TaskCompleted", async (taskId, result) => {
      console.warn(`Task completed: ${taskId}`);
      if (this.onTaskCompleted) {
        await this.onTaskCompleted({ eventType: "TaskCompleted", taskId, result });
      }
    });
    connection.on("TaskFailed", async (taskId, error, isRetryable) => {
      console.error(`Task failed: ${taskId}, Error: ${error}, Retryable: ${isRetryable}`);
      if (this.onTaskFailed) {
        await this.onTaskFailed({ eventType: "TaskFailed", taskId, error, isRetryable });
      }
    });
    connection.on("TaskCancelled", async (taskId, reason) => {
      console.warn(`Task cancelled: ${taskId}, Reason: ${reason}`);
      if (this.onTaskCancelled) {
        await this.onTaskCancelled({ eventType: "TaskCancelled", taskId, reason });
      }
    });
    connection.on("TaskTimedOut", async (taskId, timeoutSeconds) => {
      console.error(`Task timed out: ${taskId}, Timeout: ${timeoutSeconds}s`);
      if (this.onTaskTimedOut) {
        await this.onTaskTimedOut({ eventType: "TaskTimedOut", taskId, timeoutSeconds });
      }
    });
  }
  /**
   * Subscribe to notifications for a specific task.
   */
  async subscribeToTask(taskId) {
    if (!taskId?.trim()) {
      throw new Error("Task ID cannot be null or empty");
    }
    await this.invoke("SubscribeToTask", taskId);
    console.warn(`Subscribed to task: ${taskId}`);
  }
  /**
   * Unsubscribe from notifications for a specific task.
   */
  async unsubscribeFromTask(taskId) {
    if (!taskId?.trim()) {
      throw new Error("Task ID cannot be null or empty");
    }
    await this.invoke("UnsubscribeFromTask", taskId);
    console.warn(`Unsubscribed from task: ${taskId}`);
  }
  /**
   * Subscribe to notifications for all tasks of a specific type.
   */
  async subscribeToTaskType(taskType) {
    if (!taskType?.trim()) {
      throw new Error("Task type cannot be null or empty");
    }
    await this.invoke("SubscribeToTaskType", taskType);
    console.warn(`Subscribed to task type: ${taskType}`);
  }
  /**
   * Unsubscribe from notifications for a task type.
   */
  async unsubscribeFromTaskType(taskType) {
    if (!taskType?.trim()) {
      throw new Error("Task type cannot be null or empty");
    }
    await this.invoke("UnsubscribeFromTaskType", taskType);
    console.warn(`Unsubscribed from task type: ${taskType}`);
  }
  /**
   * Subscribe to multiple tasks at once.
   */
  async subscribeToTasks(taskIds) {
    await Promise.all(taskIds.map((taskId) => this.subscribeToTask(taskId)));
  }
  /**
   * Unsubscribe from multiple tasks at once.
   */
  async unsubscribeFromTasks(taskIds) {
    await Promise.all(taskIds.map((taskId) => this.unsubscribeFromTask(taskId)));
  }
  /**
   * Subscribe to multiple task types at once.
   */
  async subscribeToTaskTypes(taskTypes) {
    await Promise.all(taskTypes.map((taskType) => this.subscribeToTaskType(taskType)));
  }
  /**
   * Unsubscribe from multiple task types at once.
   */
  async unsubscribeFromTaskTypes(taskTypes) {
    await Promise.all(taskTypes.map((taskType) => this.unsubscribeFromTaskType(taskType)));
  }
};

// src/signalr/VideoGenerationHubClient.ts
var VideoGenerationHubClient = class extends BaseSignalRConnection {
  /**
   * Gets the hub path for video generation notifications.
   */
  get hubPath() {
    return SignalREndpoints.VideoGenerationHub;
  }
  /**
   * Configures the hub-specific event handlers.
   */
  configureHubHandlers(connection) {
    connection.on("VideoGenerationStarted", async (taskId, prompt, estimatedSeconds) => {
      console.warn(`Video generation started: ${taskId}, Estimated: ${estimatedSeconds}s`);
      if (this.onVideoGenerationStarted) {
        await this.onVideoGenerationStarted({ eventType: "VideoGenerationStarted", taskId, prompt, estimatedSeconds });
      }
    });
    connection.on("VideoGenerationProgress", async (taskId, progress, currentFrame, totalFrames, message) => {
      console.warn(`Video generation progress: ${taskId}, Progress: ${progress}%`);
      if (this.onVideoGenerationProgress) {
        await this.onVideoGenerationProgress({
          eventType: "VideoGenerationProgress",
          taskId,
          progress,
          currentFrame,
          totalFrames,
          message
        });
      }
    });
    connection.on("VideoGenerationCompleted", async (taskId, videoUrl, duration, metadata) => {
      console.warn(`Video generation completed: ${taskId}, Duration: ${duration}s`);
      if (this.onVideoGenerationCompleted) {
        await this.onVideoGenerationCompleted({ eventType: "VideoGenerationCompleted", taskId, videoUrl, duration, metadata });
      }
    });
    connection.on("VideoGenerationFailed", async (taskId, error, isRetryable) => {
      console.error(`Video generation failed: ${taskId}, Error: ${error}`);
      if (this.onVideoGenerationFailed) {
        await this.onVideoGenerationFailed({ eventType: "VideoGenerationFailed", taskId, error, isRetryable, errorCode: void 0 });
      }
    });
  }
  /**
   * Subscribe to notifications for a specific video generation task.
   */
  async subscribeToTask(taskId) {
    if (!taskId?.trim()) {
      throw new Error("Task ID cannot be null or empty");
    }
    await this.invoke("SubscribeToTask", taskId);
    console.warn(`Subscribed to video generation task: ${taskId}`);
  }
  /**
   * Unsubscribe from notifications for a specific video generation task.
   */
  async unsubscribeFromTask(taskId) {
    if (!taskId?.trim()) {
      throw new Error("Task ID cannot be null or empty");
    }
    await this.invoke("UnsubscribeFromTask", taskId);
    console.warn(`Unsubscribed from video generation task: ${taskId}`);
  }
  /**
   * Subscribe to multiple tasks at once.
   */
  async subscribeToTasks(taskIds) {
    await Promise.all(taskIds.map((taskId) => this.subscribeToTask(taskId)));
  }
  /**
   * Unsubscribe from multiple tasks at once.
   */
  async unsubscribeFromTasks(taskIds) {
    await Promise.all(taskIds.map((taskId) => this.unsubscribeFromTask(taskId)));
  }
};

// src/signalr/ImageGenerationHubClient.ts
var ImageGenerationHubClient = class extends BaseSignalRConnection {
  /**
   * Gets the hub path for image generation notifications.
   */
  get hubPath() {
    return SignalREndpoints.ImageGenerationHub;
  }
  /**
   * Configures the hub-specific event handlers.
   */
  configureHubHandlers(connection) {
    connection.on("ImageGenerationStarted", async (taskId, prompt, model) => {
      console.warn(`Image generation started: ${taskId}, Model: ${model}`);
      if (this.onImageGenerationStarted) {
        await this.onImageGenerationStarted({ eventType: "ImageGenerationStarted", taskId, prompt, model });
      }
    });
    connection.on("ImageGenerationProgress", async (taskId, progress, stage) => {
      console.warn(`Image generation progress: ${taskId}, Progress: ${progress}%, Stage: ${stage}`);
      if (this.onImageGenerationProgress) {
        await this.onImageGenerationProgress({ eventType: "ImageGenerationProgress", taskId, progress, stage });
      }
    });
    connection.on("ImageGenerationCompleted", async (taskId, imageUrl, metadata) => {
      console.warn(`Image generation completed: ${taskId}`);
      if (this.onImageGenerationCompleted) {
        await this.onImageGenerationCompleted({ eventType: "ImageGenerationCompleted", taskId, imageUrl, metadata });
      }
    });
    connection.on("ImageGenerationFailed", async (taskId, error, isRetryable) => {
      console.error(`Image generation failed: ${taskId}, Error: ${error}`);
      if (this.onImageGenerationFailed) {
        await this.onImageGenerationFailed({ eventType: "ImageGenerationFailed", taskId, error, isRetryable, errorCode: void 0 });
      }
    });
  }
  /**
   * Subscribe to notifications for a specific image generation task.
   */
  async subscribeToTask(taskId) {
    if (!taskId?.trim()) {
      throw new Error("Task ID cannot be null or empty");
    }
    await this.invoke("SubscribeToTask", taskId);
    console.warn(`Subscribed to image generation task: ${taskId}`);
  }
  /**
   * Unsubscribe from notifications for a specific image generation task.
   */
  async unsubscribeFromTask(taskId) {
    if (!taskId?.trim()) {
      throw new Error("Task ID cannot be null or empty");
    }
    await this.invoke("UnsubscribeFromTask", taskId);
    console.warn(`Unsubscribed from image generation task: ${taskId}`);
  }
  /**
   * Subscribe to multiple tasks at once.
   */
  async subscribeToTasks(taskIds) {
    await Promise.all(taskIds.map((taskId) => this.subscribeToTask(taskId)));
  }
  /**
   * Unsubscribe from multiple tasks at once.
   */
  async unsubscribeFromTasks(taskIds) {
    await Promise.all(taskIds.map((taskId) => this.unsubscribeFromTask(taskId)));
  }
};

// src/services/SignalRService.ts
var SignalRService = class {
  constructor(baseUrl, virtualKey) {
    this.connections = /* @__PURE__ */ new Map();
    this.disposed = false;
    this.baseUrl = baseUrl;
    this.virtualKey = virtualKey;
  }
  /**
   * Gets or creates a TaskHubClient for task progress notifications.
   */
  getTaskHubClient() {
    return this.getOrCreateConnection(
      "TaskHubClient",
      () => new TaskHubClient(this.baseUrl, this.virtualKey)
    );
  }
  /**
   * Gets or creates a VideoGenerationHubClient for video generation notifications.
   */
  getVideoGenerationHubClient() {
    return this.getOrCreateConnection(
      "VideoGenerationHubClient",
      () => new VideoGenerationHubClient(this.baseUrl, this.virtualKey)
    );
  }
  /**
   * Gets or creates an ImageGenerationHubClient for image generation notifications.
   */
  getImageGenerationHubClient() {
    return this.getOrCreateConnection(
      "ImageGenerationHubClient",
      () => new ImageGenerationHubClient(this.baseUrl, this.virtualKey)
    );
  }
  /**
   * Gets or creates a connection of the specified type.
   */
  getOrCreateConnection(key, factory) {
    const existing = this.connections.get(key);
    if (existing) {
      return existing;
    }
    const newConnection = factory();
    this.connections.set(key, newConnection);
    return newConnection;
  }
  /**
   * Starts all active hub connections.
   */
  async startAllConnections() {
    const startPromises = Array.from(this.connections.values()).map(
      (connection) => connection.start()
    );
    await Promise.all(startPromises);
  }
  /**
   * Stops all active hub connections.
   */
  async stopAllConnections() {
    const stopPromises = Array.from(this.connections.values()).map(
      (connection) => connection.stop()
    );
    await Promise.all(stopPromises);
  }
  /**
   * Waits for all connections to be established.
   */
  async waitForAllConnections(timeoutMs = 3e4) {
    const waitPromises = Array.from(this.connections.values()).map(
      (connection) => connection.waitForConnection(timeoutMs)
    );
    try {
      const results = await Promise.all(waitPromises);
      return results.every((result) => result === true);
    } catch {
      return false;
    }
  }
  /**
   * Gets the connection status for all hub connections.
   */
  getConnectionStatus() {
    const status = {};
    for (const [key, connection] of this.connections) {
      status[key] = connection.state;
    }
    return status;
  }
  /**
   * Checks if all connections are established.
   */
  areAllConnectionsEstablished() {
    return Array.from(this.connections.values()).every(
      (connection) => connection.isConnected
    );
  }
  /**
   * Checks if SignalR service is connected.
   */
  isConnected() {
    return this.areAllConnectionsEstablished();
  }
  /**
   * Subscribes to a task across all relevant hubs.
   */
  async subscribeToTask(taskId, taskType) {
    const taskHubClient = this.getTaskHubClient();
    await taskHubClient.subscribeToTask(taskId);
    if (taskType?.toLowerCase().includes("video")) {
      const videoHubClient = this.getVideoGenerationHubClient();
      await videoHubClient.subscribeToTask(taskId);
    } else if (taskType?.toLowerCase().includes("image")) {
      const imageHubClient = this.getImageGenerationHubClient();
      await imageHubClient.subscribeToTask(taskId);
    }
    console.warn(`Subscribed to task ${taskId} with type ${taskType}`);
  }
  /**
   * Unsubscribes from a task across all relevant hubs.
   */
  async unsubscribeFromTask(taskId, taskType) {
    const taskHubClient = this.getTaskHubClient();
    await taskHubClient.unsubscribeFromTask(taskId);
    if (taskType?.toLowerCase().includes("video")) {
      const videoHubClient = this.getVideoGenerationHubClient();
      await videoHubClient.unsubscribeFromTask(taskId);
    } else if (taskType?.toLowerCase().includes("image")) {
      const imageHubClient = this.getImageGenerationHubClient();
      await imageHubClient.unsubscribeFromTask(taskId);
    }
    console.warn(`Unsubscribed from task ${taskId} with type ${taskType}`);
  }
  /**
   * Disposes all SignalR connections.
   */
  async dispose() {
    if (!this.disposed) {
      const disposePromises = Array.from(this.connections.values()).map(
        (connection) => connection.dispose()
      );
      await Promise.all(disposePromises);
      this.connections.clear();
      this.disposed = true;
      console.warn("Disposed SignalRService and all connections");
    }
  }
};
var CoreModelCapability = conduitCommon.ModelCapability;
function modelSupportsCapability(modelId, capability) {
  if (modelId in IMAGE_MODEL_CAPABILITIES) {
    const imageCapabilities = IMAGE_MODEL_CAPABILITIES[modelId];
    switch (capability) {
      case conduitCommon.ModelCapability.IMAGE_GENERATION:
        return true;
      // All models in IMAGE_MODEL_CAPABILITIES support generation
      case conduitCommon.ModelCapability.IMAGE_EDIT:
        return imageCapabilities.supportsEdit;
      case conduitCommon.ModelCapability.IMAGE_VARIATION:
        return imageCapabilities.supportsVariation;
      case conduitCommon.ModelCapability.VISION:
      case conduitCommon.ModelCapability.CHAT:
        return false;
      // Image generation models don't support chat/vision
      default:
        return false;
    }
  }
  const lowerModelId = modelId.toLowerCase();
  switch (capability) {
    case CoreModelCapability.CHAT:
      return !lowerModelId.includes("dall-e") && !lowerModelId.includes("image") && !lowerModelId.includes("stable-diffusion");
    case CoreModelCapability.VISION:
      return lowerModelId.includes("vision") || lowerModelId.includes("gpt-4") || lowerModelId.includes("claude-3");
    case CoreModelCapability.IMAGE_GENERATION:
      return lowerModelId.includes("dall-e") || lowerModelId.includes("image") || lowerModelId.includes("stable-diffusion") || lowerModelId.includes("minimax-image");
    default:
      return false;
  }
}
function getModelCapabilities(modelId) {
  const capabilities = [];
  Object.values(conduitCommon.ModelCapability).forEach((capability) => {
    if (modelSupportsCapability(modelId, capability)) {
      capabilities.push(capability);
    }
  });
  return capabilities;
}
function validateModelCompatibility(modelId, requestType) {
  const errors = [];
  const suggestions = [];
  const capabilityMap = {
    "chat": conduitCommon.ModelCapability.CHAT,
    "image-generation": conduitCommon.ModelCapability.IMAGE_GENERATION,
    "image-edit": conduitCommon.ModelCapability.IMAGE_EDIT,
    "image-variation": conduitCommon.ModelCapability.IMAGE_VARIATION
  };
  const requiredCapability = capabilityMap[requestType];
  if (!modelSupportsCapability(modelId, requiredCapability)) {
    errors.push(`Model '${modelId}' does not support ${requestType}`);
    switch (requestType) {
      case "image-generation":
        suggestions.push("Try using models like: dall-e-3, dall-e-2, or minimax-image");
        break;
      case "image-edit":
      case "image-variation":
        suggestions.push("Try using dall-e-2 for image editing and variations");
        break;
      case "chat":
        suggestions.push("Try using models like: gpt-4, gpt-3.5-turbo, or claude-3");
        break;
    }
  }
  return {
    isValid: errors.length === 0,
    errors,
    suggestions: suggestions.length > 0 ? suggestions : void 0
  };
}
function getRecommendedModels(capability, preferences) {
  const { prioritizeQuality, prioritizeSpeed, prioritizeCost } = preferences ?? {};
  switch (capability) {
    case conduitCommon.ModelCapability.CHAT:
      if (prioritizeQuality) {
        return ["gpt-4", "claude-3-sonnet", "gpt-3.5-turbo"];
      }
      if (prioritizeSpeed) {
        return ["gpt-3.5-turbo", "gpt-4", "claude-3-haiku"];
      }
      if (prioritizeCost) {
        return ["gpt-3.5-turbo", "claude-3-haiku", "gpt-4"];
      }
      return ["gpt-4", "gpt-3.5-turbo", "claude-3-sonnet"];
    case conduitCommon.ModelCapability.VISION:
      if (prioritizeQuality) {
        return ["gpt-4-vision-preview", "claude-3-sonnet", "gpt-4"];
      }
      return ["gpt-4-vision-preview", "claude-3-sonnet"];
    case conduitCommon.ModelCapability.IMAGE_GENERATION:
      if (prioritizeQuality) {
        return ["dall-e-3", "minimax-image", "dall-e-2"];
      }
      if (prioritizeSpeed) {
        return ["dall-e-2", "minimax-image", "dall-e-3"];
      }
      if (prioritizeCost) {
        return ["dall-e-2", "minimax-image", "dall-e-3"];
      }
      return ["dall-e-3", "dall-e-2", "minimax-image"];
    case conduitCommon.ModelCapability.IMAGE_EDIT:
    case conduitCommon.ModelCapability.IMAGE_VARIATION:
      return ["dall-e-2"];
    // Currently only DALL-E 2 supports these
    default:
      return [];
  }
}
function getCapabilityDisplayName(capability) {
  return conduitCommon.getCapabilityDisplayName(capability);
}
function areModelsEquivalent(modelA, modelB, capability) {
  if (!modelSupportsCapability(modelA, capability) || !modelSupportsCapability(modelB, capability)) {
    return false;
  }
  if (capability === CoreModelCapability.IMAGE_GENERATION) {
    const capabilitiesA = IMAGE_MODEL_CAPABILITIES[modelA];
    const capabilitiesB = IMAGE_MODEL_CAPABILITIES[modelB];
    if (capabilitiesA && capabilitiesB) {
      return capabilitiesA.maxImages === capabilitiesB.maxImages && JSON.stringify(capabilitiesA.supportedSizes) === JSON.stringify(capabilitiesB.supportedSizes);
    }
  }
  const normalizeModel = (model) => model.toLowerCase().replace(/[^a-z0-9]/g, "");
  return normalizeModel(modelA) === normalizeModel(modelB);
}

// src/models/embeddings.ts
var EmbeddingModels = {
  /**
   * OpenAI text-embedding-ada-002
   * Dimensions: 1536
   */
  ADA_002: "text-embedding-ada-002",
  /**
   * OpenAI text-embedding-3-small
   * Dimensions: 1536 (can be reduced)
   */
  EMBEDDING_3_SMALL: "text-embedding-3-small",
  /**
   * OpenAI text-embedding-3-large
   * Dimensions: 3072 (can be reduced)
   */
  EMBEDDING_3_LARGE: "text-embedding-3-large",
  /**
   * Default embedding model
   */
  DEFAULT: "text-embedding-3-small"
};
var EmbeddingEncodingFormats = {
  /**
   * Return embeddings as array of floats (default)
   */
  FLOAT: "float",
  /**
   * Return embeddings as base64-encoded string
   */
  BASE64: "base64"
};
function validateEmbeddingRequest(request) {
  if (!request.input) {
    throw new Error("Input is required");
  }
  if (typeof request.input === "string") {
    if (!request.input.trim()) {
      throw new Error("Input text cannot be empty");
    }
  } else if (Array.isArray(request.input)) {
    if (request.input.length === 0) {
      throw new Error("At least one input text is required");
    }
    if (request.input.some((text) => !text?.trim())) {
      throw new Error("Input texts cannot be null or empty");
    }
  } else {
    throw new Error("Input must be a string or array of strings");
  }
  if (!request.model) {
    throw new Error("Model is required");
  }
  if (request.encoding_format && request.encoding_format !== EmbeddingEncodingFormats.FLOAT && request.encoding_format !== EmbeddingEncodingFormats.BASE64) {
    throw new Error(`Encoding format must be '${String(EmbeddingEncodingFormats.FLOAT)}' or '${String(EmbeddingEncodingFormats.BASE64)}'`);
  }
  if (request.dimensions !== void 0 && request.dimensions <= 0) {
    throw new Error("Dimensions must be a positive integer");
  }
}
function convertEmbeddingToFloatArray(embedding) {
  if (Array.isArray(embedding)) {
    return embedding;
  }
  if (typeof embedding === "string") {
    const buffer = Buffer.from(embedding, "base64");
    const floats = new Float32Array(buffer.buffer, buffer.byteOffset, buffer.length / Float32Array.BYTES_PER_ELEMENT);
    return Array.from(floats);
  }
  throw new Error(`Unexpected embedding type: ${typeof embedding}`);
}
function calculateCosineSimilarity(embedding1, embedding2) {
  if (embedding1.length !== embedding2.length) {
    throw new Error("Embeddings must have the same dimensions");
  }
  let dotProduct = 0;
  let magnitude1 = 0;
  let magnitude2 = 0;
  for (let i = 0; i < embedding1.length; i++) {
    dotProduct += embedding1[i] * embedding2[i];
    magnitude1 += embedding1[i] * embedding1[i];
    magnitude2 += embedding2[i] * embedding2[i];
  }
  magnitude1 = Math.sqrt(magnitude1);
  magnitude2 = Math.sqrt(magnitude2);
  if (magnitude1 === 0 || magnitude2 === 0) {
    return 0;
  }
  return dotProduct / (magnitude1 * magnitude2);
}

// src/services/EmbeddingsService.ts
var EmbeddingsService = class {
  constructor(client) {
    this.clientAdapter = createClientAdapter(client);
  }
  /**
   * Creates embeddings for the given input text(s)
   * 
   * @param request - The embedding request
   * @param options - Request options
   * @returns Promise<EmbeddingResponse> The embedding response containing the generated embeddings
   * @throws {ConduitError} When the API request fails or validation fails
   * 
   * @example
   * ```typescript
   * const response = await client.embeddings.createEmbedding({
   *   input: "Hello, world!",
   *   model: "text-embedding-3-small"
   * });
   * 
   * console.log(`Generated ${response.data.length} embeddings`);
   * console.log(`Used ${response.usage.total_tokens} tokens`);
   * ```
   */
  async createEmbedding(request, options) {
    try {
      validateEmbeddingRequest(request);
      const response = await this.clientAdapter.post(
        API_ENDPOINTS.V1.EMBEDDINGS.BASE,
        request,
        options
      );
      return response;
    } catch (error) {
      if (error instanceof conduitCommon.ConduitError) {
        throw error;
      }
      throw new conduitCommon.ConduitError(
        `Embedding creation failed: ${error instanceof Error ? error.message : String(error)}`
      );
    }
  }
  /**
   * Creates embeddings for a single text input
   * 
   * @param text - The input text
   * @param model - The model to use (defaults to text-embedding-3-small)
   * @param options - Additional options
   * @returns Promise<number[]> The embedding vector for the input text
   * 
   * @example
   * ```typescript
   * const embedding = await client.embeddings.createSingleEmbedding(
   *   "Hello, world!",
   *   "text-embedding-3-small"
   * );
   * 
   * console.log(`Embedding dimension: ${embedding.length}`);
   * ```
   */
  async createSingleEmbedding(text, model, options) {
    const request = {
      input: text,
      model: model ?? EmbeddingModels.DEFAULT,
      dimensions: options?.dimensions,
      encoding_format: options?.encoding_format,
      user: options?.user
    };
    const response = await this.createEmbedding(request, { signal: options?.signal });
    if (response.data.length === 0) {
      throw new conduitCommon.ConduitError("No embeddings returned");
    }
    return convertEmbeddingToFloatArray(response.data[0].embedding);
  }
  /**
   * Creates embeddings for multiple text inputs
   * 
   * @param texts - The input texts
   * @param model - The model to use (defaults to text-embedding-3-small)
   * @param options - Additional options
   * @returns Promise<number[][]> A list of embedding vectors for each input text
   * 
   * @example
   * ```typescript
   * const embeddings = await client.embeddings.createBatchEmbeddings(
   *   ["Hello", "World", "AI"],
   *   "text-embedding-3-small"
   * );
   * 
   * embeddings.forEach((embedding, i) => {
   *   console.log(`Text ${i}: ${embedding.length} dimensions`);
   * });
   * ```
   */
  async createBatchEmbeddings(texts, model, options) {
    if (texts.length === 0) {
      throw new Error("At least one text input is required");
    }
    const request = {
      input: texts,
      model: model ?? EmbeddingModels.DEFAULT,
      dimensions: options?.dimensions,
      encoding_format: options?.encoding_format,
      user: options?.user
    };
    const response = await this.createEmbedding(request, { signal: options?.signal });
    return response.data.sort((a, b) => a.index - b.index).map((data) => convertEmbeddingToFloatArray(data.embedding));
  }
  /**
   * Finds the most similar texts from a list of candidates to a query text
   * 
   * @param query - The query text
   * @param candidates - The list of candidate texts
   * @param options - Search options
   * @returns Promise<Array<{ text: string; similarity: number; index: number }>> 
   *          Sorted list of candidates with similarity scores
   * 
   * @example
   * ```typescript
   * const candidates = [
   *   "The cat sat on the mat",
   *   "Dogs are loyal animals",
   *   "The feline rested on the rug",
   *   "Birds can fly"
   * ];
   * 
   * const results = await client.embeddings.findSimilar(
   *   "A cat is sleeping",
   *   candidates,
   *   { topK: 2 }
   * );
   * 
   * results.forEach(result => {
   *   console.log(`"${result.text}" - Similarity: ${result.similarity.toFixed(3)}`);
   * });
   * ```
   */
  async findSimilar(query, candidates, options) {
    if (candidates.length === 0) {
      throw new Error("At least one candidate is required");
    }
    const allTexts = [query, ...candidates];
    const embeddings = await this.createBatchEmbeddings(
      allTexts,
      options?.model,
      {
        dimensions: options?.dimensions,
        signal: options?.signal
      }
    );
    const queryEmbedding = embeddings[0];
    const candidateEmbeddings = embeddings.slice(1);
    const results = candidates.map((text, index) => ({
      text,
      similarity: calculateCosineSimilarity(queryEmbedding, candidateEmbeddings[index]),
      index
    }));
    results.sort((a, b) => b.similarity - a.similarity);
    const topK = options?.topK ?? candidates.length;
    return results.slice(0, topK);
  }
  /**
   * Calculates the similarity between two texts
   * 
   * @param text1 - The first text
   * @param text2 - The second text
   * @param model - The model to use for embeddings
   * @param options - Additional options
   * @returns Promise<number> The cosine similarity between -1 and 1
   * 
   * @example
   * ```typescript
   * const similarity = await client.embeddings.calculateSimilarity(
   *   "The weather is nice today",
   *   "It's a beautiful day outside"
   * );
   * 
   * console.log(`Similarity: ${(similarity * 100).toFixed(1)}%`);
   * ```
   */
  async calculateSimilarity(text1, text2, model, options) {
    const embeddings = await this.createBatchEmbeddings(
      [text1, text2],
      model,
      options
    );
    return calculateCosineSimilarity(embeddings[0], embeddings[1]);
  }
  /**
   * Groups texts by similarity using embeddings
   * 
   * @param texts - The texts to group
   * @param threshold - Similarity threshold for grouping (0-1)
   * @param model - The model to use for embeddings
   * @param options - Additional options
   * @returns Promise<string[][]> Groups of similar texts
   * 
   * @example
   * ```typescript
   * const texts = [
   *   "Python programming",
   *   "JavaScript coding",
   *   "Cooking recipes",
   *   "Software development",
   *   "Baking cakes"
   * ];
   * 
   * const groups = await client.embeddings.groupBySimilarity(
   *   texts,
   *   0.7 // 70% similarity threshold
   * );
   * 
   * groups.forEach((group, i) => {
   *   console.log(`Group ${i + 1}: ${group.join(", ")}`);
   * });
   * ```
   */
  async groupBySimilarity(texts, threshold = 0.7, model, options) {
    if (texts.length === 0) {
      return [];
    }
    if (threshold < 0 || threshold > 1) {
      throw new Error("Threshold must be between 0 and 1");
    }
    const embeddings = await this.createBatchEmbeddings(texts, model, options);
    const groups = [];
    const assigned = /* @__PURE__ */ new Set();
    for (let i = 0; i < texts.length; i++) {
      if (assigned.has(i)) continue;
      const group = [i];
      assigned.add(i);
      for (let j = i + 1; j < texts.length; j++) {
        if (assigned.has(j)) continue;
        const similarity = calculateCosineSimilarity(embeddings[i], embeddings[j]);
        if (similarity >= threshold) {
          group.push(j);
          assigned.add(j);
        }
      }
      groups.push(group);
    }
    return groups.map((group) => group.map((index) => texts[index]));
  }
};
var EmbeddingHelpers = {
  /**
   * Normalizes an embedding vector to unit length
   */
  normalize(embedding) {
    const magnitude = Math.sqrt(embedding.reduce((sum, val) => sum + val * val, 0));
    if (magnitude === 0) return embedding;
    return embedding.map((val) => val / magnitude);
  },
  /**
   * Calculates euclidean distance between two embeddings
   */
  euclideanDistance(embedding1, embedding2) {
    if (embedding1.length !== embedding2.length) {
      throw new Error("Embeddings must have the same dimensions");
    }
    let sum = 0;
    for (let i = 0; i < embedding1.length; i++) {
      const diff = embedding1[i] - embedding2[i];
      sum += diff * diff;
    }
    return Math.sqrt(sum);
  },
  /**
   * Calculates the centroid of multiple embeddings
   */
  centroid(embeddings) {
    if (embeddings.length === 0) {
      throw new Error("At least one embedding is required");
    }
    const dimensions = embeddings[0].length;
    const result = new Array(dimensions).fill(0);
    for (const embedding of embeddings) {
      for (let i = 0; i < dimensions; i++) {
        result[i] += embedding[i];
      }
    }
    return result.map((val) => val / embeddings.length);
  }
};

// src/services/NotificationsService.ts
var NotificationsService = class {
  constructor(signalRService) {
    this.subscriptions = /* @__PURE__ */ new Map();
    this.connectionStateCallbacks = /* @__PURE__ */ new Set();
    // Store callbacks for each subscription
    this.videoCallbacks = /* @__PURE__ */ new Map();
    this.imageCallbacks = /* @__PURE__ */ new Map();
    this.spendUpdateCallbacks = /* @__PURE__ */ new Map();
    this.spendLimitCallbacks = /* @__PURE__ */ new Map();
    this.taskCallbacks = /* @__PURE__ */ new Map();
    this.signalRService = signalRService;
  }
  /**
   * Subscribe to video generation progress events
   */
  onVideoProgress(callback, options) {
    if (!this.videoHubClient) {
      this.videoHubClient = this.signalRService.getVideoGenerationHubClient();
      this.videoHubClient.onVideoGenerationProgress = (event) => {
        for (const [subId, cb] of this.videoCallbacks) {
          const subscription2 = this.subscriptions.get(subId);
          if (!subscription2) continue;
          const opts = options?.filter?.taskIds;
          if (opts && !opts.includes(event.taskId)) continue;
          const notificationEvent = {
            taskId: event.taskId,
            progress: event.progress,
            status: "processing",
            message: event.message
          };
          cb(notificationEvent);
        }
        return Promise.resolve();
      };
      this.videoHubClient.onVideoGenerationCompleted = (event) => {
        for (const [subId, cb] of this.videoCallbacks) {
          const subscription2 = this.subscriptions.get(subId);
          if (!subscription2) continue;
          const opts = options?.filter?.taskIds;
          if (opts && !opts.includes(event.taskId)) continue;
          const notificationEvent = {
            taskId: event.taskId,
            progress: 100,
            status: "completed",
            metadata: event.metadata
          };
          cb(notificationEvent);
        }
        return Promise.resolve();
      };
      this.videoHubClient.onVideoGenerationFailed = (event) => {
        for (const [subId, cb] of this.videoCallbacks) {
          const subscription2 = this.subscriptions.get(subId);
          if (!subscription2) continue;
          const opts = options?.filter?.taskIds;
          if (opts && !opts.includes(event.taskId)) continue;
          const notificationEvent = {
            taskId: event.taskId,
            progress: 0,
            status: "failed",
            message: event.error
          };
          cb(notificationEvent);
        }
        return Promise.resolve();
      };
    }
    const subscriptionId = this.generateSubscriptionId();
    this.videoCallbacks.set(subscriptionId, callback);
    const subscription = {
      id: subscriptionId,
      eventType: "videoProgress",
      unsubscribe: () => this.unsubscribe(subscriptionId)
    };
    this.subscriptions.set(subscriptionId, subscription);
    if (options?.onConnectionStateChange) {
      this.connectionStateCallbacks.add(options.onConnectionStateChange);
    }
    return subscription;
  }
  /**
   * Subscribe to image generation progress events
   */
  onImageProgress(callback, options) {
    if (!this.imageHubClient) {
      this.imageHubClient = this.signalRService.getImageGenerationHubClient();
      this.imageHubClient.onImageGenerationProgress = (event) => {
        for (const [subId, cb] of this.imageCallbacks) {
          const subscription2 = this.subscriptions.get(subId);
          if (!subscription2) continue;
          const opts = options?.filter?.taskIds;
          if (opts && !opts.includes(event.taskId)) continue;
          const notificationEvent = {
            taskId: event.taskId,
            progress: event.progress,
            status: "processing"
          };
          cb(notificationEvent);
        }
        return Promise.resolve();
      };
      this.imageHubClient.onImageGenerationCompleted = (event) => {
        for (const [subId, cb] of this.imageCallbacks) {
          const subscription2 = this.subscriptions.get(subId);
          if (!subscription2) continue;
          const opts = options?.filter?.taskIds;
          if (opts && !opts.includes(event.taskId)) continue;
          const notificationEvent = {
            taskId: event.taskId,
            progress: 100,
            status: "completed",
            images: event.imageUrl ? [{ url: event.imageUrl }] : void 0
          };
          cb(notificationEvent);
        }
        return Promise.resolve();
      };
      this.imageHubClient.onImageGenerationFailed = (event) => {
        for (const [subId, cb] of this.imageCallbacks) {
          const subscription2 = this.subscriptions.get(subId);
          if (!subscription2) continue;
          const opts = options?.filter?.taskIds;
          if (opts && !opts.includes(event.taskId)) continue;
          const notificationEvent = {
            taskId: event.taskId,
            progress: 0,
            status: "failed",
            message: event.error
          };
          cb(notificationEvent);
        }
        return Promise.resolve();
      };
    }
    const subscriptionId = this.generateSubscriptionId();
    this.imageCallbacks.set(subscriptionId, callback);
    const subscription = {
      id: subscriptionId,
      eventType: "imageProgress",
      unsubscribe: () => this.unsubscribe(subscriptionId)
    };
    this.subscriptions.set(subscriptionId, subscription);
    if (options?.onConnectionStateChange) {
      this.connectionStateCallbacks.add(options.onConnectionStateChange);
    }
    return subscription;
  }
  /**
   * Subscribe to spend update events
   */
  onSpendUpdate(callback, options) {
    this.taskHubClient ?? (this.taskHubClient = this.signalRService.getTaskHubClient());
    const subscriptionId = this.generateSubscriptionId();
    this.spendUpdateCallbacks.set(subscriptionId, callback);
    const subscription = {
      id: subscriptionId,
      eventType: "spendUpdate",
      unsubscribe: () => this.unsubscribe(subscriptionId)
    };
    this.subscriptions.set(subscriptionId, subscription);
    if (options?.onConnectionStateChange) {
      this.connectionStateCallbacks.add(options.onConnectionStateChange);
    }
    return subscription;
  }
  /**
   * Subscribe to spend limit alert events
   */
  onSpendLimitAlert(callback, options) {
    this.taskHubClient ?? (this.taskHubClient = this.signalRService.getTaskHubClient());
    const subscriptionId = this.generateSubscriptionId();
    this.spendLimitCallbacks.set(subscriptionId, callback);
    const subscription = {
      id: subscriptionId,
      eventType: "spendLimitAlert",
      unsubscribe: () => this.unsubscribe(subscriptionId)
    };
    this.subscriptions.set(subscriptionId, subscription);
    if (options?.onConnectionStateChange) {
      this.connectionStateCallbacks.add(options.onConnectionStateChange);
    }
    return subscription;
  }
  /**
   * Subscribe to updates for a specific task
   */
  async subscribeToTask(taskId, taskType, callback, options) {
    var _a, _b, _c;
    const subscriptionId = this.generateSubscriptionId();
    await this.signalRService.subscribeToTask(taskId, taskType);
    this.taskCallbacks.set(subscriptionId, { taskId, callback });
    if (taskType === "video") {
      this.videoHubClient ?? (this.videoHubClient = this.signalRService.getVideoGenerationHubClient());
    } else if (taskType === "image") {
      this.imageHubClient ?? (this.imageHubClient = this.signalRService.getImageGenerationHubClient());
    } else {
      this.taskHubClient ?? (this.taskHubClient = this.signalRService.getTaskHubClient());
      (_a = this.taskHubClient).onTaskProgress ?? (_a.onTaskProgress = (event) => {
        for (const [subId, taskInfo] of this.taskCallbacks) {
          if (taskInfo.taskId === event.taskId) {
            const subscription2 = this.subscriptions.get(subId);
            if (!subscription2) continue;
            const notificationEvent = {
              taskId: event.taskId,
              taskType,
              status: "processing",
              progress: event.progress,
              metadata: {}
            };
            taskInfo.callback(notificationEvent);
          }
        }
        return Promise.resolve();
      });
      (_b = this.taskHubClient).onTaskCompleted ?? (_b.onTaskCompleted = (event) => {
        for (const [subId, taskInfo] of this.taskCallbacks) {
          if (taskInfo.taskId === event.taskId) {
            const subscription2 = this.subscriptions.get(subId);
            if (!subscription2) continue;
            const notificationEvent = {
              taskId: event.taskId,
              taskType,
              status: "completed",
              result: event.result
            };
            taskInfo.callback(notificationEvent);
          }
        }
        return Promise.resolve();
      });
      (_c = this.taskHubClient).onTaskFailed ?? (_c.onTaskFailed = (event) => {
        for (const [subId, taskInfo] of this.taskCallbacks) {
          if (taskInfo.taskId === event.taskId) {
            const subscription2 = this.subscriptions.get(subId);
            if (!subscription2) continue;
            const notificationEvent = {
              taskId: event.taskId,
              taskType,
              status: "failed",
              error: event.error
            };
            taskInfo.callback(notificationEvent);
          }
        }
        return Promise.resolve();
      });
    }
    const subscription = {
      id: subscriptionId,
      eventType: "taskUpdate",
      unsubscribe: () => this.unsubscribe(subscriptionId)
    };
    this.subscriptions.set(subscriptionId, subscription);
    if (options?.onConnectionStateChange) {
      this.connectionStateCallbacks.add(options.onConnectionStateChange);
    }
    return subscription;
  }
  /**
   * Unsubscribe from a specific task
   */
  async unsubscribeFromTask(taskId) {
    const toRemove = [];
    for (const [id, taskInfo] of this.taskCallbacks) {
      if (taskInfo.taskId === taskId) {
        toRemove.push(id);
      }
    }
    toRemove.forEach((id) => this.unsubscribe(id));
    await this.signalRService.unsubscribeFromTask(taskId);
  }
  /**
   * Unsubscribe from all notifications
   */
  unsubscribeAll() {
    const subscriptionIds = Array.from(this.subscriptions.keys());
    subscriptionIds.forEach((id) => this.unsubscribe(id));
    this.connectionStateCallbacks.clear();
  }
  /**
   * Get all active subscriptions
   */
  getActiveSubscriptions() {
    return Array.from(this.subscriptions.values());
  }
  /**
   * Connect to SignalR hubs
   */
  async connect() {
    await this.signalRService.startAllConnections();
  }
  /**
   * Disconnect from SignalR hubs
   */
  async disconnect() {
    await this.signalRService.stopAllConnections();
  }
  /**
   * Check if connected to SignalR hubs
   */
  isConnected() {
    const states = this.signalRService.getConnectionStatus();
    return Object.values(states).some((state) => state === conduitCommon.HubConnectionState.Connected);
  }
  unsubscribe(subscriptionId) {
    this.subscriptions.delete(subscriptionId);
    this.videoCallbacks.delete(subscriptionId);
    this.imageCallbacks.delete(subscriptionId);
    this.spendUpdateCallbacks.delete(subscriptionId);
    this.spendLimitCallbacks.delete(subscriptionId);
    this.taskCallbacks.delete(subscriptionId);
  }
  generateSubscriptionId() {
    return `sub_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
  }
};

// src/models/metadata.ts
function isValidMetadata(value) {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
function parseMetadata(metadata) {
  if (!metadata) {
    return void 0;
  }
  if (typeof metadata === "object") {
    return metadata;
  }
  if (typeof metadata === "string") {
    try {
      const parsed = JSON.parse(metadata);
      if (isValidMetadata(parsed)) {
        return parsed;
      }
    } catch {
    }
  }
  return void 0;
}
function stringifyMetadata(metadata) {
  if (!metadata || Object.keys(metadata).length === 0) {
    return void 0;
  }
  try {
    return JSON.stringify(metadata);
  } catch {
    return void 0;
  }
}

Object.defineProperty(exports, "AuthenticationError", {
  enumerable: true,
  get: function () { return conduitCommon.AuthenticationError; }
});
Object.defineProperty(exports, "ConduitError", {
  enumerable: true,
  get: function () { return conduitCommon.ConduitError; }
});
Object.defineProperty(exports, "DefaultTransports", {
  enumerable: true,
  get: function () { return conduitCommon.DefaultTransports; }
});
Object.defineProperty(exports, "HttpMethod", {
  enumerable: true,
  get: function () { return conduitCommon.HttpMethod; }
});
Object.defineProperty(exports, "NetworkError", {
  enumerable: true,
  get: function () { return conduitCommon.NetworkError; }
});
Object.defineProperty(exports, "RateLimitError", {
  enumerable: true,
  get: function () { return conduitCommon.RateLimitError; }
});
Object.defineProperty(exports, "StreamError", {
  enumerable: true,
  get: function () { return conduitCommon.StreamError; }
});
exports.AudioService = AudioService;
exports.AudioUtils = AudioUtils;
exports.BatchOperationsService = BatchOperationsService;
exports.ConduitCoreClient = FetchConduitCoreClient;
exports.ContentHelpers = ContentHelpers;
exports.CoreModelCapability = CoreModelCapability;
exports.DiscoveryService = DiscoveryService;
exports.EmbeddingEncodingFormats = EmbeddingEncodingFormats;
exports.EmbeddingHelpers = EmbeddingHelpers;
exports.EmbeddingModels = EmbeddingModels;
exports.EmbeddingsService = EmbeddingsService;
exports.FetchConduitCoreClient = FetchConduitCoreClient;
exports.IMAGE_DEFAULTS = IMAGE_DEFAULTS;
exports.IMAGE_MODELS = IMAGE_MODELS;
exports.IMAGE_MODEL_CAPABILITIES = IMAGE_MODEL_CAPABILITIES;
exports.ImageGenerationHubClient = ImageGenerationHubClient;
exports.ImagesService = ImagesService;
exports.MetricsService = MetricsService;
exports.ModelCapability = ModelCapability;
exports.NotificationsService = NotificationsService;
exports.ProviderModelsService = ProviderModelsService;
exports.SignalREndpoints = SignalREndpoints;
exports.SignalRService = SignalRService;
exports.TaskDefaults = TaskDefaults;
exports.TaskHelpers = TaskHelpers;
exports.TaskHubClient = TaskHubClient;
exports.VideoDefaults = VideoDefaults;
exports.VideoGenerationHubClient = VideoGenerationHubClient;
exports.VideoModels = VideoModels;
exports.VideoResolutions = VideoResolutions;
exports.VideoResponseFormats = VideoResponseFormats;
exports.VideosService = VideosService;
exports.areModelsEquivalent = areModelsEquivalent;
exports.calculateCosineSimilarity = calculateCosineSimilarity;
exports.convertEmbeddingToFloatArray = convertEmbeddingToFloatArray;
exports.getCapabilityDisplayName = getCapabilityDisplayName;
exports.getModelCapabilities = getModelCapabilities;
exports.getRecommendedModels = getRecommendedModels;
exports.getVideoModelCapabilities = getVideoModelCapabilities;
exports.isChatCompletionChunk = isChatCompletionChunk;
exports.isFinalMetrics = isFinalMetrics;
exports.isStreamingMetrics = isStreamingMetrics;
exports.isValidMetadata = isValidMetadata;
exports.modelSupportsCapability = modelSupportsCapability;
exports.parseMetadata = parseMetadata;
exports.stringifyMetadata = stringifyMetadata;
exports.validateAsyncVideoGenerationRequest = validateAsyncVideoGenerationRequest;
exports.validateEmbeddingRequest = validateEmbeddingRequest;
exports.validateModelCompatibility = validateModelCompatibility;
exports.validateVideoGenerationRequest = validateVideoGenerationRequest;
//# sourceMappingURL=index.js.map
//# sourceMappingURL=index.js.map