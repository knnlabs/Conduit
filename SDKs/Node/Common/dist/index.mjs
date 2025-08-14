// src/types/capabilities.ts
var ModelCapability = /* @__PURE__ */ ((ModelCapability2) => {
  ModelCapability2["CHAT"] = "chat";
  ModelCapability2["VISION"] = "vision";
  ModelCapability2["IMAGE_GENERATION"] = "image-generation";
  ModelCapability2["IMAGE_EDIT"] = "image-edit";
  ModelCapability2["IMAGE_VARIATION"] = "image-variation";
  ModelCapability2["AUDIO_TRANSCRIPTION"] = "audio-transcription";
  ModelCapability2["TEXT_TO_SPEECH"] = "text-to-speech";
  ModelCapability2["REALTIME_AUDIO"] = "realtime-audio";
  ModelCapability2["EMBEDDINGS"] = "embeddings";
  ModelCapability2["VIDEO_GENERATION"] = "video-generation";
  return ModelCapability2;
})(ModelCapability || {});
function getCapabilityDisplayName(capability) {
  switch (capability) {
    case "chat" /* CHAT */:
      return "Chat Completion";
    case "vision" /* VISION */:
      return "Vision (Image Understanding)";
    case "image-generation" /* IMAGE_GENERATION */:
      return "Image Generation";
    case "image-edit" /* IMAGE_EDIT */:
      return "Image Editing";
    case "image-variation" /* IMAGE_VARIATION */:
      return "Image Variation";
    case "audio-transcription" /* AUDIO_TRANSCRIPTION */:
      return "Audio Transcription";
    case "text-to-speech" /* TEXT_TO_SPEECH */:
      return "Text-to-Speech";
    case "realtime-audio" /* REALTIME_AUDIO */:
      return "Realtime Audio";
    case "embeddings" /* EMBEDDINGS */:
      return "Embeddings";
    case "video-generation" /* VIDEO_GENERATION */:
      return "Video Generation";
    default:
      return capability;
  }
}
function getCapabilityCategory(capability) {
  switch (capability) {
    case "chat" /* CHAT */:
    case "embeddings" /* EMBEDDINGS */:
      return "text";
    case "vision" /* VISION */:
    case "image-generation" /* IMAGE_GENERATION */:
    case "image-edit" /* IMAGE_EDIT */:
    case "image-variation" /* IMAGE_VARIATION */:
      return "vision";
    case "audio-transcription" /* AUDIO_TRANSCRIPTION */:
    case "text-to-speech" /* TEXT_TO_SPEECH */:
    case "realtime-audio" /* REALTIME_AUDIO */:
      return "audio";
    case "video-generation" /* VIDEO_GENERATION */:
      return "video";
    default:
      return "text";
  }
}

// src/errors/index.ts
var ConduitError = class _ConduitError extends Error {
  constructor(message, statusCode = 500, code = "INTERNAL_ERROR", context) {
    super(message);
    this.name = this.constructor.name;
    this.statusCode = statusCode;
    this.code = code;
    this.context = context;
    if (context) {
      this.details = context.details;
      this.endpoint = context.endpoint;
      this.method = context.method;
      this.type = context.type;
      this.param = context.param;
    }
    Object.setPrototypeOf(this, new.target.prototype);
    if (Error.captureStackTrace) {
      Error.captureStackTrace(this, this.constructor);
    }
  }
  toJSON() {
    return {
      name: this.name,
      message: this.message,
      statusCode: this.statusCode,
      code: this.code,
      context: this.context,
      details: this.details,
      endpoint: this.endpoint,
      method: this.method,
      type: this.type,
      param: this.param,
      timestamp: (/* @__PURE__ */ new Date()).toISOString()
    };
  }
  // Helper method for Next.js serialization
  toSerializable() {
    return {
      isConduitError: true,
      ...this.toJSON()
    };
  }
  // Static method to reconstruct from serialized error
  static fromSerializable(data) {
    if (!data || typeof data !== "object" || !("isConduitError" in data) || !data.isConduitError) {
      throw new Error("Invalid serialized ConduitError");
    }
    const errorData = data;
    const error = new _ConduitError(
      errorData.message,
      errorData.statusCode,
      errorData.code,
      errorData.context
    );
    if (errorData.details !== void 0) error.details = errorData.details;
    if (errorData.endpoint !== void 0) error.endpoint = errorData.endpoint;
    if (errorData.method !== void 0) error.method = errorData.method;
    if (errorData.type !== void 0) error.type = errorData.type;
    if (errorData.param !== void 0) error.param = errorData.param;
    return error;
  }
};
var AuthError = class extends ConduitError {
  constructor(message = "Authentication failed", context) {
    super(message, 401, "AUTH_ERROR", context);
  }
};
var AuthenticationError = class extends AuthError {
};
var AuthorizationError = class extends ConduitError {
  constructor(message = "Access forbidden", context) {
    super(message, 403, "AUTHORIZATION_ERROR", context);
  }
};
var ValidationError = class extends ConduitError {
  constructor(message = "Validation failed", context) {
    super(message, 400, "VALIDATION_ERROR", context);
    this.field = context?.field;
  }
};
var NotFoundError = class extends ConduitError {
  constructor(message = "Resource not found", context) {
    super(message, 404, "NOT_FOUND", context);
  }
};
var ConflictError = class extends ConduitError {
  constructor(message = "Resource conflict", context) {
    super(message, 409, "CONFLICT_ERROR", context);
  }
};
var InsufficientBalanceError = class extends ConduitError {
  constructor(message = "Insufficient balance to complete request", context) {
    super(message, 402, "INSUFFICIENT_BALANCE", context);
    this.balance = context?.balance;
    this.requiredAmount = context?.requiredAmount;
  }
};
var RateLimitError = class extends ConduitError {
  constructor(message = "Rate limit exceeded", retryAfter, context) {
    super(message, 429, "RATE_LIMIT_ERROR", { ...context, retryAfter });
    this.retryAfter = retryAfter;
  }
};
var ServerError = class extends ConduitError {
  constructor(message = "Internal server error", context) {
    super(message, 500, "SERVER_ERROR", context);
  }
};
var NetworkError = class extends ConduitError {
  constructor(message = "Network error", context) {
    super(message, 0, "NETWORK_ERROR", context);
  }
};
var TimeoutError = class extends ConduitError {
  constructor(message = "Request timeout", context) {
    super(message, 408, "TIMEOUT_ERROR", context);
  }
};
var NotImplementedError = class extends ConduitError {
  constructor(message, context) {
    super(message, 501, "NOT_IMPLEMENTED", context);
  }
};
var StreamError = class extends ConduitError {
  constructor(message = "Stream processing failed", context) {
    super(message, 500, "STREAM_ERROR", context);
  }
};
function isConduitError(error) {
  return error instanceof ConduitError;
}
function isAuthError(error) {
  return error instanceof AuthError || error instanceof AuthenticationError;
}
function isAuthorizationError(error) {
  return error instanceof AuthorizationError;
}
function isValidationError(error) {
  return error instanceof ValidationError;
}
function isNotFoundError(error) {
  return error instanceof NotFoundError;
}
function isConflictError(error) {
  return error instanceof ConflictError;
}
function isInsufficientBalanceError(error) {
  return error instanceof InsufficientBalanceError;
}
function isRateLimitError(error) {
  return error instanceof RateLimitError;
}
function isNetworkError(error) {
  return error instanceof NetworkError;
}
function isStreamError(error) {
  return error instanceof StreamError;
}
function isTimeoutError(error) {
  return error instanceof TimeoutError;
}
function isSerializedConduitError(data) {
  return typeof data === "object" && data !== null && "isConduitError" in data && data.isConduitError === true;
}
function isHttpError(error) {
  return typeof error === "object" && error !== null && "response" in error && typeof error.response === "object";
}
function isHttpNetworkError(error) {
  return typeof error === "object" && error !== null && "request" in error && !("response" in error);
}
function isErrorLike(error) {
  return typeof error === "object" && error !== null && "message" in error && typeof error.message === "string";
}
function serializeError(error) {
  if (isConduitError(error)) {
    return error.toSerializable();
  }
  if (error instanceof Error) {
    return {
      isError: true,
      name: error.name,
      message: error.message,
      stack: process.env.NODE_ENV === "development" ? error.stack : void 0
    };
  }
  return {
    isError: true,
    message: String(error)
  };
}
function deserializeError(data) {
  if (isSerializedConduitError(data)) {
    return ConduitError.fromSerializable(data);
  }
  if (typeof data === "object" && data !== null && "isError" in data) {
    const errorData = data;
    const error = new Error(errorData.message || "Unknown error");
    if (errorData.name) error.name = errorData.name;
    if (errorData.stack) error.stack = errorData.stack;
    return error;
  }
  return new Error("Unknown error");
}
function getErrorMessage(error) {
  if (isConduitError(error)) {
    return error.message;
  }
  if (error instanceof Error) {
    return error.message;
  }
  return "An unexpected error occurred";
}
function getErrorStatusCode(error) {
  if (isConduitError(error)) {
    return error.statusCode;
  }
  return 500;
}
function handleApiError(error, endpoint, method) {
  const context = {
    endpoint,
    method
  };
  if (isHttpError(error)) {
    const { status, data } = error.response;
    const errorData = data;
    const baseMessage = errorData?.error || errorData?.message || error.message;
    const endpointInfo = endpoint && method ? ` (${method.toUpperCase()} ${endpoint})` : "";
    const enhancedMessage = `${baseMessage}${endpointInfo}`;
    context.details = errorData?.details || data;
    switch (status) {
      case 400:
        throw new ValidationError(enhancedMessage, context);
      case 401:
        throw new AuthError(enhancedMessage, context);
      case 402:
        throw new InsufficientBalanceError(enhancedMessage, context);
      case 403:
        throw new AuthorizationError(enhancedMessage, context);
      case 404:
        throw new NotFoundError(enhancedMessage, context);
      case 409:
        throw new ConflictError(enhancedMessage, context);
      case 429: {
        const retryAfterHeader = error.response.headers["retry-after"];
        const retryAfter = typeof retryAfterHeader === "string" ? parseInt(retryAfterHeader, 10) : void 0;
        throw new RateLimitError(enhancedMessage, retryAfter, context);
      }
      case 500:
      case 502:
      case 503:
      case 504:
        throw new ServerError(enhancedMessage, context);
      default:
        throw new ConduitError(enhancedMessage, status, `HTTP_${status}`, context);
    }
  } else if (isHttpNetworkError(error)) {
    const endpointInfo = endpoint && method ? ` (${method.toUpperCase()} ${endpoint})` : "";
    context.code = error.code;
    if (error.code === "ECONNABORTED") {
      throw new TimeoutError(`Request timeout${endpointInfo}`, context);
    }
    throw new NetworkError(`Network error: No response received${endpointInfo}`, context);
  } else if (isErrorLike(error)) {
    context.originalError = error;
    throw new ConduitError(error.message, 500, "UNKNOWN_ERROR", context);
  } else {
    context.originalError = error;
    throw new ConduitError("Unknown error", 500, "UNKNOWN_ERROR", context);
  }
}
function createErrorFromResponse(response, statusCode) {
  const context = {
    type: response.error.type,
    param: response.error.param
  };
  return new ConduitError(
    response.error.message,
    statusCode || 500,
    response.error.code || "API_ERROR",
    context
  );
}

// src/http/types.ts
var HttpMethod = /* @__PURE__ */ ((HttpMethod2) => {
  HttpMethod2["GET"] = "GET";
  HttpMethod2["POST"] = "POST";
  HttpMethod2["PUT"] = "PUT";
  HttpMethod2["DELETE"] = "DELETE";
  HttpMethod2["PATCH"] = "PATCH";
  HttpMethod2["HEAD"] = "HEAD";
  HttpMethod2["OPTIONS"] = "OPTIONS";
  return HttpMethod2;
})(HttpMethod || {});
function isHttpMethod(method) {
  return Object.values(HttpMethod).includes(method);
}

// src/http/parser.ts
var ResponseParser = class {
  /**
   * Parses a fetch Response based on content type and response type hint
   */
  static async parse(response, responseType) {
    const contentLength = response.headers.get("content-length");
    if (contentLength === "0" || response.status === 204) {
      return void 0;
    }
    if (responseType) {
      switch (responseType) {
        case "json":
          return await response.json();
        case "text":
          return await response.text();
        case "blob":
          return await response.blob();
        case "arraybuffer":
          return await response.arrayBuffer();
        case "stream":
          if (!response.body) {
            throw new Error("Response body is not a stream");
          }
          return response.body;
        default: {
          const _exhaustive = responseType;
          throw new Error(`Unknown response type: ${String(_exhaustive)}`);
        }
      }
    }
    const contentType = response.headers.get("content-type") || "";
    if (contentType.includes("application/json")) {
      return await response.json();
    }
    if (contentType.includes("text/") || contentType.includes("application/xml")) {
      return await response.text();
    }
    if (contentType.includes("application/octet-stream") || contentType.includes("image/") || contentType.includes("audio/") || contentType.includes("video/")) {
      return await response.blob();
    }
    return await response.text();
  }
  /**
   * Creates a clean RequestInit object without custom properties
   */
  static cleanRequestInit(init) {
    const { responseType, timeout, metadata, ...standardInit } = init;
    return standardInit;
  }
};

// src/http/constants.ts
var HTTP_HEADERS = {
  CONTENT_TYPE: "Content-Type",
  AUTHORIZATION: "Authorization",
  X_API_KEY: "X-API-Key",
  USER_AGENT: "User-Agent",
  X_CORRELATION_ID: "X-Correlation-Id",
  RETRY_AFTER: "Retry-After",
  ACCEPT: "Accept",
  CACHE_CONTROL: "Cache-Control"
};
var CONTENT_TYPES = {
  JSON: "application/json",
  FORM_DATA: "multipart/form-data",
  FORM_URLENCODED: "application/x-www-form-urlencoded",
  TEXT_PLAIN: "text/plain",
  TEXT_STREAM: "text/event-stream"
};
var HTTP_STATUS = {
  // 2xx Success
  OK: 200,
  CREATED: 201,
  NO_CONTENT: 204,
  // 4xx Client Errors
  BAD_REQUEST: 400,
  UNAUTHORIZED: 401,
  FORBIDDEN: 403,
  NOT_FOUND: 404,
  CONFLICT: 409,
  TOO_MANY_REQUESTS: 429,
  RATE_LIMITED: 429,
  // Alias for Core SDK compatibility
  // 5xx Server Errors
  INTERNAL_SERVER_ERROR: 500,
  INTERNAL_ERROR: 500,
  // Alias for Admin SDK compatibility
  BAD_GATEWAY: 502,
  SERVICE_UNAVAILABLE: 503,
  GATEWAY_TIMEOUT: 504
};
var ERROR_CODES = {
  CONNECTION_ABORTED: "ECONNABORTED",
  TIMEOUT: "ETIMEDOUT",
  CONNECTION_RESET: "ECONNRESET",
  NETWORK_UNREACHABLE: "ENETUNREACH",
  CONNECTION_REFUSED: "ECONNREFUSED",
  HOST_NOT_FOUND: "ENOTFOUND"
};
var TIMEOUTS = {
  DEFAULT_REQUEST: 6e4,
  // 60 seconds
  SHORT_REQUEST: 1e4,
  // 10 seconds
  LONG_REQUEST: 3e5,
  // 5 minutes
  STREAMING: 0
  // No timeout for streaming
};
var RETRY_CONFIG = {
  DEFAULT_MAX_RETRIES: 3,
  INITIAL_DELAY: 1e3,
  // 1 second
  MAX_DELAY: 3e4,
  // 30 seconds
  BACKOFF_FACTOR: 2
};

// src/signalr/types.ts
var HubConnectionState = /* @__PURE__ */ ((HubConnectionState3) => {
  HubConnectionState3["Disconnected"] = "Disconnected";
  HubConnectionState3["Connecting"] = "Connecting";
  HubConnectionState3["Connected"] = "Connected";
  HubConnectionState3["Disconnecting"] = "Disconnecting";
  HubConnectionState3["Reconnecting"] = "Reconnecting";
  return HubConnectionState3;
})(HubConnectionState || {});
var SignalRLogLevel = /* @__PURE__ */ ((SignalRLogLevel2) => {
  SignalRLogLevel2[SignalRLogLevel2["Trace"] = 0] = "Trace";
  SignalRLogLevel2[SignalRLogLevel2["Debug"] = 1] = "Debug";
  SignalRLogLevel2[SignalRLogLevel2["Information"] = 2] = "Information";
  SignalRLogLevel2[SignalRLogLevel2["Warning"] = 3] = "Warning";
  SignalRLogLevel2[SignalRLogLevel2["Error"] = 4] = "Error";
  SignalRLogLevel2[SignalRLogLevel2["Critical"] = 5] = "Critical";
  SignalRLogLevel2[SignalRLogLevel2["None"] = 6] = "None";
  return SignalRLogLevel2;
})(SignalRLogLevel || {});
var HttpTransportType = /* @__PURE__ */ ((HttpTransportType3) => {
  HttpTransportType3[HttpTransportType3["None"] = 0] = "None";
  HttpTransportType3[HttpTransportType3["WebSockets"] = 1] = "WebSockets";
  HttpTransportType3[HttpTransportType3["ServerSentEvents"] = 2] = "ServerSentEvents";
  HttpTransportType3[HttpTransportType3["LongPolling"] = 4] = "LongPolling";
  return HttpTransportType3;
})(HttpTransportType || {});
var DefaultTransports = 1 /* WebSockets */ | 2 /* ServerSentEvents */ | 4 /* LongPolling */;

// src/signalr/BaseSignalRConnection.ts
import * as signalR from "@microsoft/signalr";
var BaseSignalRConnection = class {
  constructor(config) {
    this.disposed = false;
    this.config = {
      ...config,
      baseUrl: config.baseUrl.replace(/\/$/, "")
    };
    this.connectionReadyPromise = new Promise((resolve, reject) => {
      this.connectionReadyResolve = resolve;
      this.connectionReadyReject = reject;
    });
  }
  /**
   * Gets whether the connection is established and ready for use.
   */
  get isConnected() {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }
  /**
   * Gets the current connection state.
   */
  get state() {
    if (!this.connection) {
      return "Disconnected" /* Disconnected */;
    }
    switch (this.connection.state) {
      case signalR.HubConnectionState.Connected:
        return "Connected" /* Connected */;
      case signalR.HubConnectionState.Connecting:
        return "Connecting" /* Connecting */;
      case signalR.HubConnectionState.Disconnected:
        return "Disconnected" /* Disconnected */;
      case signalR.HubConnectionState.Disconnecting:
        return "Disconnecting" /* Disconnecting */;
      case signalR.HubConnectionState.Reconnecting:
        return "Reconnecting" /* Reconnecting */;
      default:
        return "Disconnected" /* Disconnected */;
    }
  }
  /**
   * Establishes the SignalR connection.
   */
  async getConnection() {
    if (this.connection) {
      return this.connection;
    }
    const hubUrl = `${this.config.baseUrl}${this.hubPath}`;
    const connectionOptions = {
      accessTokenFactory: this.config.options?.accessTokenFactory || (() => this.config.auth.authToken),
      transport: this.mapTransportType(this.config.options?.transport || DefaultTransports),
      headers: this.buildHeaders(),
      withCredentials: false
    };
    const builder = new signalR.HubConnectionBuilder().withUrl(hubUrl, connectionOptions).withAutomaticReconnect(this.config.options?.reconnectionDelay || [0, 2e3, 1e4, 3e4]);
    if (this.config.options?.serverTimeout) {
      builder.withServerTimeout(this.config.options.serverTimeout);
    }
    if (this.config.options?.keepAliveInterval) {
      builder.withKeepAliveInterval(this.config.options.keepAliveInterval);
    }
    const logLevel = this.mapLogLevel(this.config.options?.logLevel || 2 /* Information */);
    builder.configureLogging(logLevel);
    this.connection = builder.build();
    this.connection.onclose(async (error) => {
      if (this.onDisconnected) {
        await this.onDisconnected(error);
      }
    });
    this.connection.onreconnecting(async (error) => {
      if (this.onReconnecting) {
        await this.onReconnecting(error);
      }
    });
    this.connection.onreconnected(async (connectionId) => {
      if (this.onReconnected) {
        await this.onReconnected(connectionId);
      }
    });
    this.configureHubHandlers(this.connection);
    try {
      await this.connection.start();
      if (this.connectionReadyResolve) {
        this.connectionReadyResolve();
      }
      if (this.onConnected) {
        await this.onConnected();
      }
    } catch (error) {
      if (this.connectionReadyReject) {
        this.connectionReadyReject(error);
      }
      throw error;
    }
    return this.connection;
  }
  /**
   * Maps transport type enum to SignalR transport.
   */
  mapTransportType(transport) {
    let result = signalR.HttpTransportType.None;
    if (transport & 1 /* WebSockets */) {
      result |= signalR.HttpTransportType.WebSockets;
    }
    if (transport & 2 /* ServerSentEvents */) {
      result |= signalR.HttpTransportType.ServerSentEvents;
    }
    if (transport & 4 /* LongPolling */) {
      result |= signalR.HttpTransportType.LongPolling;
    }
    return result;
  }
  /**
   * Maps log level enum to SignalR log level.
   */
  mapLogLevel(level) {
    switch (level) {
      case 0 /* Trace */:
        return signalR.LogLevel.Trace;
      case 1 /* Debug */:
        return signalR.LogLevel.Debug;
      case 2 /* Information */:
        return signalR.LogLevel.Information;
      case 3 /* Warning */:
        return signalR.LogLevel.Warning;
      case 4 /* Error */:
        return signalR.LogLevel.Error;
      case 5 /* Critical */:
        return signalR.LogLevel.Critical;
      case 6 /* None */:
        return signalR.LogLevel.None;
      default:
        return signalR.LogLevel.Information;
    }
  }
  /**
   * Builds headers for the connection based on configuration.
   */
  buildHeaders() {
    const headers = {
      "User-Agent": this.config.userAgent || "Conduit-Node-Client/1.0.0",
      ...this.config.options?.headers
    };
    if (this.config.auth.authType === "master" && this.config.auth.additionalHeaders) {
      Object.assign(headers, this.config.auth.additionalHeaders);
    }
    return headers;
  }
  /**
   * Waits for the connection to be ready.
   */
  async waitForReady() {
    return this.connectionReadyPromise;
  }
  /**
   * Invokes a method on the hub with proper error handling.
   */
  async invoke(methodName, ...args) {
    if (this.disposed) {
      throw new Error("Connection has been disposed");
    }
    const connection = await this.getConnection();
    try {
      return await connection.invoke(methodName, ...args);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : String(error);
      throw new Error(`SignalR invoke error for ${methodName}: ${errorMessage}`);
    }
  }
  /**
   * Sends a message to the hub without expecting a response.
   */
  async send(methodName, ...args) {
    if (this.disposed) {
      throw new Error("Connection has been disposed");
    }
    const connection = await this.getConnection();
    try {
      await connection.send(methodName, ...args);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : String(error);
      throw new Error(`SignalR send error for ${methodName}: ${errorMessage}`);
    }
  }
  /**
   * Disconnects the SignalR connection.
   */
  async disconnect() {
    if (this.connection && this.connection.state !== signalR.HubConnectionState.Disconnected) {
      await this.connection.stop();
      this.connection = void 0;
      this.connectionReadyPromise = new Promise((resolve, reject) => {
        this.connectionReadyResolve = resolve;
        this.connectionReadyReject = reject;
      });
    }
  }
  /**
   * Disposes of the connection and cleans up resources.
   */
  async dispose() {
    this.disposed = true;
    await this.disconnect();
    this.connectionReadyResolve = void 0;
    this.connectionReadyReject = void 0;
  }
};

// src/client/types.ts
var HttpError = class extends Error {
  constructor(message, code) {
    super(message);
    this.name = "HttpError";
    this.code = code;
  }
};
export {
  AuthError,
  AuthenticationError,
  AuthorizationError,
  BaseSignalRConnection,
  CONTENT_TYPES,
  ConduitError,
  ConflictError,
  DefaultTransports,
  ERROR_CODES,
  HTTP_HEADERS,
  HTTP_STATUS,
  HttpError,
  HttpMethod,
  HttpTransportType,
  HubConnectionState,
  InsufficientBalanceError,
  ModelCapability,
  NetworkError,
  NotFoundError,
  NotImplementedError,
  RETRY_CONFIG,
  RateLimitError,
  ResponseParser,
  ServerError,
  SignalRLogLevel,
  StreamError,
  TIMEOUTS,
  TimeoutError,
  ValidationError,
  createErrorFromResponse,
  deserializeError,
  getCapabilityCategory,
  getCapabilityDisplayName,
  getErrorMessage,
  getErrorStatusCode,
  handleApiError,
  isAuthError,
  isAuthorizationError,
  isConduitError,
  isConflictError,
  isErrorLike,
  isHttpError,
  isHttpMethod,
  isHttpNetworkError,
  isInsufficientBalanceError,
  isNetworkError,
  isNotFoundError,
  isRateLimitError,
  isSerializedConduitError,
  isStreamError,
  isTimeoutError,
  isValidationError,
  serializeError
};
//# sourceMappingURL=index.mjs.map