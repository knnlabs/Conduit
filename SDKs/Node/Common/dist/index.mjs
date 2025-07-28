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

// src/types/providerType.ts
var ProviderType = /* @__PURE__ */ ((ProviderType2) => {
  ProviderType2[ProviderType2["OpenAI"] = 1] = "OpenAI";
  ProviderType2[ProviderType2["Anthropic"] = 2] = "Anthropic";
  ProviderType2[ProviderType2["AzureOpenAI"] = 3] = "AzureOpenAI";
  ProviderType2[ProviderType2["Gemini"] = 4] = "Gemini";
  ProviderType2[ProviderType2["VertexAI"] = 5] = "VertexAI";
  ProviderType2[ProviderType2["Cohere"] = 6] = "Cohere";
  ProviderType2[ProviderType2["Mistral"] = 7] = "Mistral";
  ProviderType2[ProviderType2["Groq"] = 8] = "Groq";
  ProviderType2[ProviderType2["Ollama"] = 9] = "Ollama";
  ProviderType2[ProviderType2["Replicate"] = 10] = "Replicate";
  ProviderType2[ProviderType2["Fireworks"] = 11] = "Fireworks";
  ProviderType2[ProviderType2["Bedrock"] = 12] = "Bedrock";
  ProviderType2[ProviderType2["HuggingFace"] = 13] = "HuggingFace";
  ProviderType2[ProviderType2["SageMaker"] = 14] = "SageMaker";
  ProviderType2[ProviderType2["OpenRouter"] = 15] = "OpenRouter";
  ProviderType2[ProviderType2["OpenAICompatible"] = 16] = "OpenAICompatible";
  ProviderType2[ProviderType2["MiniMax"] = 17] = "MiniMax";
  ProviderType2[ProviderType2["Ultravox"] = 18] = "Ultravox";
  ProviderType2[ProviderType2["ElevenLabs"] = 19] = "ElevenLabs";
  ProviderType2[ProviderType2["GoogleCloud"] = 20] = "GoogleCloud";
  ProviderType2[ProviderType2["Cerebras"] = 21] = "Cerebras";
  return ProviderType2;
})(ProviderType || {});
function isProviderType(value) {
  return typeof value === "number" && value >= 1 /* OpenAI */ && value <= 21 /* Cerebras */;
}
function getProviderDisplayName(provider) {
  const names = {
    [1 /* OpenAI */]: "OpenAI",
    [2 /* Anthropic */]: "Anthropic",
    [3 /* AzureOpenAI */]: "Azure OpenAI",
    [4 /* Gemini */]: "Google Gemini",
    [5 /* VertexAI */]: "Google Vertex AI",
    [6 /* Cohere */]: "Cohere",
    [7 /* Mistral */]: "Mistral AI",
    [8 /* Groq */]: "Groq",
    [9 /* Ollama */]: "Ollama",
    [10 /* Replicate */]: "Replicate",
    [11 /* Fireworks */]: "Fireworks AI",
    [12 /* Bedrock */]: "AWS Bedrock",
    [13 /* HuggingFace */]: "Hugging Face",
    [14 /* SageMaker */]: "AWS SageMaker",
    [15 /* OpenRouter */]: "OpenRouter",
    [16 /* OpenAICompatible */]: "OpenAI Compatible",
    [17 /* MiniMax */]: "MiniMax",
    [18 /* Ultravox */]: "Ultravox",
    [19 /* ElevenLabs */]: "ElevenLabs",
    [20 /* GoogleCloud */]: "Google Cloud",
    [21 /* Cerebras */]: "Cerebras"
  };
  return names[provider] || "Unknown Provider";
}

// src/types/models.ts
function hasModelFeatureSupport(obj) {
  return typeof obj === "object" && obj !== null && "capabilities" in obj && typeof obj.capabilities === "object";
}
function isBaseModel(obj) {
  return typeof obj === "object" && obj !== null && "id" in obj && "name" in obj && "providerId" in obj && "providerType" in obj;
}

// src/types/api.ts
var FilterOperator = /* @__PURE__ */ ((FilterOperator2) => {
  FilterOperator2["EQUALS"] = "eq";
  FilterOperator2["NOT_EQUALS"] = "ne";
  FilterOperator2["GREATER_THAN"] = "gt";
  FilterOperator2["GREATER_THAN_OR_EQUAL"] = "gte";
  FilterOperator2["LESS_THAN"] = "lt";
  FilterOperator2["LESS_THAN_OR_EQUAL"] = "lte";
  FilterOperator2["IN"] = "in";
  FilterOperator2["NOT_IN"] = "nin";
  FilterOperator2["CONTAINS"] = "contains";
  FilterOperator2["STARTS_WITH"] = "startsWith";
  FilterOperator2["ENDS_WITH"] = "endsWith";
  return FilterOperator2;
})(FilterOperator || {});

// src/constants.ts
var API_VERSION = "v1";
var API_PREFIX = "/api";
var PAGINATION = {
  DEFAULT_PAGE_SIZE: 20,
  MAX_PAGE_SIZE: 100,
  DEFAULT_PAGE: 1
};
var CACHE_TTL = {
  SHORT: 60,
  // 1 minute
  MEDIUM: 300,
  // 5 minutes
  LONG: 3600,
  // 1 hour
  VERY_LONG: 86400
  // 24 hours
};
var TASK_STATUS = {
  PENDING: "pending",
  PROCESSING: "processing",
  COMPLETED: "completed",
  FAILED: "failed",
  CANCELLED: "cancelled",
  TIMEOUT: "timeout"
};
var POLLING_CONFIG = {
  DEFAULT_INTERVAL: 1e3,
  // 1 second
  MAX_INTERVAL: 3e4,
  // 30 seconds
  DEFAULT_TIMEOUT: 3e5,
  // 5 minutes
  BACKOFF_FACTOR: 1.5
};
var BUDGET_DURATION = {
  TOTAL: "Total",
  DAILY: "Daily",
  WEEKLY: "Weekly",
  MONTHLY: "Monthly"
};
var FILTER_TYPE = {
  ALLOW: "whitelist",
  DENY: "blacklist"
};
var FILTER_MODE = {
  PERMISSIVE: "permissive",
  RESTRICTIVE: "restrictive"
};
var CHAT_ROLES = {
  SYSTEM: "system",
  USER: "user",
  ASSISTANT: "assistant",
  FUNCTION: "function",
  TOOL: "tool"
};
var IMAGE_RESPONSE_FORMATS = {
  URL: "url",
  B64_JSON: "b64_json"
};
var VIDEO_RESPONSE_FORMATS = {
  URL: "url",
  B64_JSON: "b64_json"
};
var DATE_FORMATS = {
  API_DATETIME: "YYYY-MM-DDTHH:mm:ss[Z]",
  API_DATE: "YYYY-MM-DD",
  DISPLAY_DATETIME: "MMM D, YYYY [at] h:mm A",
  DISPLAY_DATE: "MMM D, YYYY"
};
var STREAM_CONSTANTS = {
  DEFAULT_BUFFER_SIZE: 64 * 1024,
  // 64KB
  DEFAULT_TIMEOUT: 6e4,
  // 60 seconds
  CHUNK_DELIMITER: "\n\n",
  DATA_PREFIX: "data: ",
  EVENT_PREFIX: "event: ",
  DONE_MESSAGE: "[DONE]"
};
var CLIENT_INFO = {
  CORE_NAME: "@conduit/core",
  ADMIN_NAME: "@conduit/admin",
  VERSION: "0.2.0"
};
var HEALTH_STATUS = {
  HEALTHY: "healthy",
  DEGRADED: "degraded",
  UNHEALTHY: "unhealthy"
};
var PATTERNS = {
  API_KEY: /^sk-[a-zA-Z0-9]{32,}$/,
  EMAIL: /^[^\s@]+@[^\s@]+\.[^\s@]+$/,
  URL: /^https?:\/\/.+$/,
  ISO_DATE: /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{3})?Z?$/
};

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

// src/utils/validation.ts
function isValidEmail(email) {
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  return emailRegex.test(email);
}
function isValidUrl(url) {
  try {
    new URL(url);
    return true;
  } catch {
    return false;
  }
}
function isValidApiKey(apiKey) {
  const apiKeyRegex = /^sk-[a-zA-Z0-9]{32,}$/;
  return apiKeyRegex.test(apiKey);
}
function isValidIsoDate(date) {
  const isoDateRegex = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{3})?Z?$/;
  if (!isoDateRegex.test(date)) {
    return false;
  }
  const parsed = new Date(date);
  return !isNaN(parsed.getTime());
}
function isValidUuid(uuid) {
  const uuidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
  return uuidRegex.test(uuid);
}
function assertDefined(value, name) {
  if (value === null || value === void 0) {
    throw new ValidationError(`${name} is required`);
  }
  return value;
}
function assertNotEmpty(value, name) {
  const defined = assertDefined(value, name);
  if (defined.trim().length === 0) {
    throw new ValidationError(`${name} cannot be empty`);
  }
  return defined;
}
function assertInRange(value, min, max, name) {
  if (value < min || value > max) {
    throw new ValidationError(`${name} must be between ${min} and ${max}`);
  }
  return value;
}
function assertOneOf(value, allowed, name) {
  if (!allowed.includes(value)) {
    throw new ValidationError(`${name} must be one of: ${allowed.join(", ")}`);
  }
  return value;
}
function assertArrayLength(array, min, max, name) {
  if (array.length < min || array.length > max) {
    throw new ValidationError(`${name} must have between ${min} and ${max} items`);
  }
  return array;
}
function assertHasProperties(obj, required, name) {
  const missing = required.filter((prop) => !(prop in obj));
  if (missing.length > 0) {
    throw new ValidationError(`${name} is missing required properties: ${missing.join(", ")}`);
  }
  return obj;
}
function sanitizeString(str, maxLength) {
  let sanitized = str.replace(/[\x00-\x1F\x7F]/g, "").trim();
  if (maxLength && sanitized.length > maxLength) {
    sanitized = sanitized.substring(0, maxLength);
  }
  return sanitized;
}
function isNonEmptyString(value) {
  return typeof value === "string" && value.trim().length > 0;
}
function isPositiveNumber(value) {
  return typeof value === "number" && value > 0 && isFinite(value);
}
function isEnumValue(value, enumObject) {
  return Object.values(enumObject).includes(value);
}
function isValidJson(str) {
  try {
    JSON.parse(str);
    return true;
  } catch {
    return false;
  }
}
function isValidBase64(str) {
  const base64Regex = /^[A-Za-z0-9+/]*(={0,2})$/;
  if (!base64Regex.test(str)) {
    return false;
  }
  return str.length % 4 === 0;
}
function createValidator(validators) {
  return (value) => {
    for (const validator of validators) {
      const result = validator(value);
      if (typeof result === "string") {
        throw new ValidationError(result);
      }
      if (!result) {
        throw new ValidationError("Validation failed");
      }
    }
  };
}

// src/utils/datetime.ts
function toIsoString(date) {
  const dateObj = date instanceof Date ? date : new Date(date);
  return dateObj.toISOString();
}
function parseIsoDate(dateStr) {
  const date = new Date(dateStr);
  if (isNaN(date.getTime())) {
    throw new Error(`Invalid date string: ${dateStr}`);
  }
  return date;
}
function getCurrentTimestamp() {
  return (/* @__PURE__ */ new Date()).toISOString();
}
function getTimeDifference(start, end = /* @__PURE__ */ new Date()) {
  const startTime = start instanceof Date ? start.getTime() : new Date(start).getTime();
  const endTime = end instanceof Date ? end.getTime() : new Date(end).getTime();
  return endTime - startTime;
}
function formatDuration(ms) {
  if (ms < 1e3) {
    return `${ms}ms`;
  }
  const seconds = Math.floor(ms / 1e3);
  const minutes = Math.floor(seconds / 60);
  const hours = Math.floor(minutes / 60);
  const days = Math.floor(hours / 24);
  if (days > 0) {
    return `${days}d ${hours % 24}h`;
  }
  if (hours > 0) {
    return `${hours}h ${minutes % 60}m`;
  }
  if (minutes > 0) {
    return `${minutes}m ${seconds % 60}s`;
  }
  return `${seconds}s`;
}
function addTime(date, amount, unit) {
  const dateObj = date instanceof Date ? new Date(date) : new Date(date);
  switch (unit) {
    case "seconds":
      dateObj.setSeconds(dateObj.getSeconds() + amount);
      break;
    case "minutes":
      dateObj.setMinutes(dateObj.getMinutes() + amount);
      break;
    case "hours":
      dateObj.setHours(dateObj.getHours() + amount);
      break;
    case "days":
      dateObj.setDate(dateObj.getDate() + amount);
      break;
  }
  return dateObj;
}
function isDateInRange(date, start, end) {
  const dateTime = date instanceof Date ? date.getTime() : new Date(date).getTime();
  const startTime = start instanceof Date ? start.getTime() : new Date(start).getTime();
  const endTime = end instanceof Date ? end.getTime() : new Date(end).getTime();
  return dateTime >= startTime && dateTime <= endTime;
}
function getStartOf(date, period) {
  const dateObj = date instanceof Date ? new Date(date) : new Date(date);
  switch (period) {
    case "day":
      dateObj.setHours(0, 0, 0, 0);
      break;
    case "week":
      const day = dateObj.getDay();
      const diff = dateObj.getDate() - day;
      dateObj.setDate(diff);
      dateObj.setHours(0, 0, 0, 0);
      break;
    case "month":
      dateObj.setDate(1);
      dateObj.setHours(0, 0, 0, 0);
      break;
    case "year":
      dateObj.setMonth(0, 1);
      dateObj.setHours(0, 0, 0, 0);
      break;
  }
  return dateObj;
}
function getEndOf(date, period) {
  const dateObj = date instanceof Date ? new Date(date) : new Date(date);
  switch (period) {
    case "day":
      dateObj.setHours(23, 59, 59, 999);
      break;
    case "week":
      const day = dateObj.getDay();
      const diff = dateObj.getDate() - day + 6;
      dateObj.setDate(diff);
      dateObj.setHours(23, 59, 59, 999);
      break;
    case "month":
      dateObj.setMonth(dateObj.getMonth() + 1, 0);
      dateObj.setHours(23, 59, 59, 999);
      break;
    case "year":
      dateObj.setMonth(11, 31);
      dateObj.setHours(23, 59, 59, 999);
      break;
  }
  return dateObj;
}
function formatApiDate(date) {
  const dateObj = date instanceof Date ? date : new Date(date);
  const year = dateObj.getFullYear();
  const month = String(dateObj.getMonth() + 1).padStart(2, "0");
  const day = String(dateObj.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}
function fromUnixTimestamp(timestamp) {
  const isSeconds = timestamp < 1e10;
  return new Date(isSeconds ? timestamp * 1e3 : timestamp);
}
function toUnixTimestamp(date) {
  const dateObj = date instanceof Date ? date : new Date(date);
  return Math.floor(dateObj.getTime() / 1e3);
}

// src/utils/formatting.ts
function formatCurrency(amount, currency = "USD", locale = "en-US") {
  return new Intl.NumberFormat(locale, {
    style: "currency",
    currency
  }).format(amount);
}
function formatNumber(value, decimals = 0, locale = "en-US") {
  return new Intl.NumberFormat(locale, {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals
  }).format(value);
}
function formatBytes(bytes, decimals = 2) {
  if (bytes === 0) return "0 Bytes";
  const k = 1024;
  const dm = decimals < 0 ? 0 : decimals;
  const sizes = ["Bytes", "KB", "MB", "GB", "TB", "PB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(dm))} ${sizes[i]}`;
}
function formatPercentage(value, decimals = 2) {
  return `${(value * 100).toFixed(decimals)}%`;
}
function truncateString(str, maxLength, suffix = "...") {
  if (str.length <= maxLength) return str;
  return str.slice(0, maxLength - suffix.length) + suffix;
}
function capitalize(str) {
  return str.charAt(0).toUpperCase() + str.slice(1);
}
function toTitleCase(str) {
  return str.replace(/\w\S*/g, (txt) => {
    return txt.charAt(0).toUpperCase() + txt.substr(1).toLowerCase();
  });
}
function toKebabCase(str) {
  return str.replace(/([a-z])([A-Z])/g, "$1-$2").replace(/[\s_]+/g, "-").toLowerCase();
}
function toSnakeCase(str) {
  return str.replace(/([a-z])([A-Z])/g, "$1_$2").replace(/[\s-]+/g, "_").toLowerCase();
}
function toCamelCase(str) {
  return str.replace(/(?:^\w|[A-Z]|\b\w)/g, (word, index) => {
    return index === 0 ? word.toLowerCase() : word.toUpperCase();
  }).replace(/[\s-_]+/g, "");
}
function padZero(value, length) {
  return String(value).padStart(length, "0");
}
function formatDurationHMS(seconds) {
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor(seconds % 3600 / 60);
  const secs = Math.floor(seconds % 60);
  const parts = [];
  if (hours > 0) parts.push(padZero(hours, 2));
  parts.push(padZero(minutes, 2));
  parts.push(padZero(secs, 2));
  return parts.join(":");
}
function pluralize(count, singular, plural) {
  if (count === 1) return singular;
  return plural || `${singular}s`;
}
function formatList(items, conjunction = "and") {
  if (items.length === 0) return "";
  if (items.length === 1) return items[0];
  if (items.length === 2) return `${items[0]} ${conjunction} ${items[1]}`;
  const lastItem = items[items.length - 1];
  const otherItems = items.slice(0, -1);
  return `${otherItems.join(", ")}, ${conjunction} ${lastItem}`;
}
function maskSensitive(value, showFirst = 4, showLast = 4, maskChar = "*") {
  if (value.length <= showFirst + showLast) {
    return value;
  }
  const first = value.slice(0, showFirst);
  const last = value.slice(-showLast);
  const maskLength = Math.max(value.length - showFirst - showLast, 4);
  const mask = maskChar.repeat(maskLength);
  return `${first}${mask}${last}`;
}
function formatFilePath(path, maxLength = 50) {
  if (path.length <= maxLength) return path;
  const parts = path.split("/");
  if (parts.length <= 2) return truncateString(path, maxLength);
  const fileName = parts[parts.length - 1];
  const firstDir = parts[0] || parts[1];
  if (fileName.length + firstDir.length + 6 > maxLength) {
    return truncateString(path, maxLength);
  }
  return `${firstDir}/.../${fileName}`;
}

// src/utils/index.ts
function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
async function retry(fn, options = {}) {
  const {
    maxRetries = 3,
    initialDelay = 1e3,
    maxDelay = 3e4,
    backoffFactor = 2,
    shouldRetry = () => true
  } = options;
  let lastError;
  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    try {
      return await fn();
    } catch (error) {
      lastError = error;
      if (attempt === maxRetries || !shouldRetry(lastError, attempt)) {
        throw lastError;
      }
      const delayMs = Math.min(initialDelay * Math.pow(backoffFactor, attempt), maxDelay);
      await delay(delayMs);
    }
  }
  throw lastError;
}
function debounce(fn, wait) {
  let timeout;
  return (...args) => {
    if (timeout) clearTimeout(timeout);
    timeout = setTimeout(() => fn(...args), wait);
  };
}
function throttle(fn, limit) {
  let inThrottle = false;
  return (...args) => {
    if (!inThrottle) {
      fn(...args);
      inThrottle = true;
      setTimeout(() => inThrottle = false, limit);
    }
  };
}
function deepClone(obj) {
  if (obj === null || typeof obj !== "object") return obj;
  if (obj instanceof Date) return new Date(obj.getTime());
  if (obj instanceof Array) return obj.map((item) => deepClone(item));
  if (obj instanceof Set) return new Set([...obj].map((item) => deepClone(item)));
  if (obj instanceof Map) {
    return new Map([...obj].map(([k, v]) => [deepClone(k), deepClone(v)]));
  }
  const clonedObj = Object.create(Object.getPrototypeOf(obj));
  for (const key in obj) {
    if (obj.hasOwnProperty(key)) {
      clonedObj[key] = deepClone(obj[key]);
    }
  }
  return clonedObj;
}
function deepMerge(target, ...sources) {
  if (!sources.length) return target;
  const source = sources.shift();
  if (!source) return target;
  for (const key in source) {
    if (source.hasOwnProperty(key)) {
      const sourceValue = source[key];
      const targetValue = target[key];
      if (isObject(sourceValue) && isObject(targetValue)) {
        target[key] = deepMerge(targetValue, sourceValue);
      } else {
        target[key] = sourceValue;
      }
    }
  }
  return deepMerge(target, ...sources);
}
function isObject(value) {
  return value !== null && typeof value === "object" && value.constructor === Object;
}
function groupBy(array, keyFn) {
  return array.reduce((result, item) => {
    const key = keyFn(item);
    if (!result[key]) {
      result[key] = [];
    }
    result[key].push(item);
    return result;
  }, {});
}
function chunk(array, size) {
  const chunks = [];
  for (let i = 0; i < array.length; i += size) {
    chunks.push(array.slice(i, i + size));
  }
  return chunks;
}
function pick(obj, keys) {
  const result = {};
  for (const key of keys) {
    if (key in obj) {
      result[key] = obj[key];
    }
  }
  return result;
}
function omit(obj, keys) {
  const result = { ...obj };
  for (const key of keys) {
    delete result[key];
  }
  return result;
}
function withTimeout(promise, timeoutMs, timeoutError) {
  return Promise.race([
    promise,
    new Promise(
      (_, reject) => setTimeout(
        () => reject(timeoutError || new Error(`Timeout after ${timeoutMs}ms`)),
        timeoutMs
      )
    )
  ]);
}
function memoize(fn, keyFn) {
  const cache = /* @__PURE__ */ new Map();
  return (...args) => {
    const key = keyFn ? keyFn(...args) : JSON.stringify(args);
    if (cache.has(key)) {
      return cache.get(key);
    }
    const result = fn(...args);
    cache.set(key, result);
    return result;
  };
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

// src/client/BaseApiClient.ts
var BaseApiClient = class {
  constructor(config) {
    this.config = {
      baseURL: config.baseURL.replace(/\/$/, ""),
      // Remove trailing slash
      timeout: config.timeout ?? 3e4,
      retries: config.retries ?? 3,
      headers: config.headers ?? {},
      debug: config.debug ?? false,
      retryDelay: config.retryDelay ?? [1e3, 2e3, 4e3, 8e3, 16e3],
      validateStatus: config.validateStatus ?? ((status) => status >= 200 && status < 300),
      logger: config.logger,
      cache: config.cache,
      onError: config.onError,
      onRequest: config.onRequest,
      onResponse: config.onResponse
    };
    this.logger = config.logger;
    this.cache = config.cache;
    this.retryConfig = this.normalizeRetryConfig(config.retries);
  }
  /**
   * Get base URL for services that need direct access
   */
  getBaseURL() {
    return this.config.baseURL;
  }
  /**
   * Get timeout for services that need direct access
   */
  getTimeout() {
    return this.config.timeout;
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
        method: options.method ?? "GET" /* GET */,
        url: fullUrl,
        headers: this.buildHeaders(options.headers),
        data: options.body
      };
      if (this.config.onRequest) {
        await this.config.onRequest(requestConfig);
      }
      if (this.config.debug) {
        this.log("debug", `[Conduit] ${requestConfig.method} ${requestConfig.url}`);
      }
      const response = await this.executeWithRetry(
        fullUrl,
        {
          method: requestConfig.method,
          headers: requestConfig.headers,
          body: options.body ? JSON.stringify(options.body) : void 0,
          signal: options.signal ?? controller.signal
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
   * Type-safe GET request with support for query parameters
   */
  async get(url, paramsOrOptions, options) {
    if (options) {
      const urlWithParams = this.buildUrlWithParams(url, paramsOrOptions);
      return this.request(urlWithParams, { ...options, method: "GET" /* GET */ });
    }
    const isOptions = paramsOrOptions && ("headers" in paramsOrOptions || "signal" in paramsOrOptions || "timeout" in paramsOrOptions || "responseType" in paramsOrOptions);
    if (isOptions) {
      return this.request(url, {
        ...paramsOrOptions,
        method: "GET" /* GET */
      });
    } else if (paramsOrOptions) {
      const urlWithParams = this.buildUrlWithParams(url, paramsOrOptions);
      return this.request(urlWithParams, { method: "GET" /* GET */ });
    } else {
      return this.request(url, { method: "GET" /* GET */ });
    }
  }
  /**
   * Type-safe POST request
   */
  async post(url, data, options) {
    return this.request(url, {
      ...options,
      method: "POST" /* POST */,
      body: data
    });
  }
  /**
   * Type-safe PUT request
   */
  async put(url, data, options) {
    return this.request(url, {
      ...options,
      method: "PUT" /* PUT */,
      body: data
    });
  }
  /**
   * Type-safe PATCH request
   */
  async patch(url, data, options) {
    return this.request(url, {
      ...options,
      method: "PATCH" /* PATCH */,
      body: data
    });
  }
  /**
   * Type-safe DELETE request
   */
  async delete(url, options) {
    return this.request(url, { ...options, method: "DELETE" /* DELETE */ });
  }
  /**
   * Build full URL from path
   */
  buildUrl(path) {
    if (path.startsWith("http://") || path.startsWith("https://")) {
      return path;
    }
    const cleanPath = path.startsWith("/") ? path : `/${path}`;
    return `${this.config.baseURL}${cleanPath}`;
  }
  /**
   * Build headers with authentication and defaults
   */
  buildHeaders(additionalHeaders) {
    return {
      "Content-Type": "application/json",
      "User-Agent": "@knn_labs/conduit-sdk",
      ...this.config.headers,
      ...this.getAuthHeaders(),
      // SDK-specific auth headers
      ...additionalHeaders
    };
  }
  /**
   * Execute request with retry logic
   */
  async executeWithRetry(url, init, options, attempt = 1) {
    try {
      const response = await fetch(url, init);
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
        this.log("debug", `[Conduit] Response: ${response.status} ${response.statusText}`);
      }
      if (!this.config.validateStatus(response.status)) {
        const error = await this.handleErrorResponse(response);
        throw error;
      }
      return await this.parseResponse(response, options.responseType);
    } catch (error) {
      if (attempt <= this.retryConfig.maxRetries && this.shouldRetry(error)) {
        const delay2 = this.calculateDelay(attempt);
        if (this.config.debug) {
          this.log("debug", `[Conduit] Retrying request (attempt ${attempt + 1}) after ${delay2}ms`);
        }
        await this.sleep(delay2);
        return this.executeWithRetry(url, init, options, attempt + 1);
      }
      const handledError = this.handleError(error);
      if (this.config.onError) {
        this.config.onError(handledError);
      }
      throw handledError;
    }
  }
  /**
   * Parse response based on content type
   */
  async parseResponse(response, responseType) {
    const contentLength = response.headers.get("content-length");
    if (contentLength === "0" || response.status === 204) {
      return void 0;
    }
    const contentType = response.headers.get("content-type") ?? "";
    if (responseType === "blob" || contentType.includes("image/") || contentType.includes("application/octet-stream")) {
      return await response.blob();
    } else if (responseType === "arraybuffer") {
      return await response.arrayBuffer();
    } else if (responseType === "text" || contentType.includes("text/")) {
      return await response.text();
    } else {
      return await response.json();
    }
  }
  /**
   * Determine if error should trigger retry
   */
  shouldRetry(error) {
    if (this.retryConfig.retryCondition) {
      return this.retryConfig.retryCondition(error);
    }
    if (error instanceof Error) {
      if (error.name === "AbortError" || error.message.includes("network") || error.message.includes("fetch")) {
        return true;
      }
    }
    return false;
  }
  /**
   * Calculate retry delay
   */
  calculateDelay(attempt) {
    if (this.config.retryDelay && this.config.retryDelay.length > 0) {
      const index = Math.min(attempt - 1, this.config.retryDelay.length - 1);
      return this.config.retryDelay[index];
    }
    const initialDelay = this.retryConfig.initialDelay ?? 1e3;
    const maxDelay = this.retryConfig.maxDelay ?? 3e4;
    const factor = this.retryConfig.factor ?? 2;
    const delay2 = Math.min(
      initialDelay * Math.pow(factor, attempt - 1),
      maxDelay
    );
    return delay2 + Math.random() * 1e3;
  }
  /**
   * Sleep for specified milliseconds
   */
  sleep(ms) {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }
  /**
   * Handle and transform errors
   */
  handleError(error) {
    if (error instanceof Error) {
      return error;
    }
    return new Error(String(error));
  }
  /**
   * Normalize retry configuration
   */
  normalizeRetryConfig(retries) {
    if (typeof retries === "number") {
      return {
        maxRetries: retries,
        initialDelay: 1e3,
        maxDelay: 3e4,
        factor: 2
      };
    }
    return retries ?? { maxRetries: 3, initialDelay: 1e3, maxDelay: 3e4, factor: 2 };
  }
  /**
   * Log message using logger if available
   */
  log(level, message, ...args) {
    if (this.logger?.[level]) {
      this.logger[level](message, ...args);
    } else if (this.config.debug && level === "debug") {
      console.warn(message, ...args);
    }
  }
  /**
   * Build URL with query parameters
   */
  buildUrlWithParams(url, params) {
    const searchParams = new URLSearchParams();
    Object.entries(params).forEach(([key, value]) => {
      if (value !== void 0 && value !== null) {
        if (Array.isArray(value)) {
          value.forEach((v) => searchParams.append(key, String(v)));
        } else {
          searchParams.append(key, String(value));
        }
      }
    });
    const queryString = searchParams.toString();
    return queryString ? `${url}?${queryString}` : url;
  }
  /**
   * Get cache key for a request
   */
  getCacheKey(resource, id, params) {
    const parts = [resource];
    if (id !== void 0) {
      parts.push(JSON.stringify(id));
    }
    if (params) {
      parts.push(JSON.stringify(params));
    }
    return parts.join(":");
  }
  /**
   * Get from cache
   */
  async getFromCache(key) {
    if (!this.cache) return null;
    try {
      const cached = await this.cache.get(key);
      if (cached) {
        this.log("debug", `Cache hit for key: ${key}`);
        return cached;
      }
    } catch (error) {
      this.log("error", "Cache get error:", error);
    }
    return null;
  }
  /**
   * Set cache value
   */
  async setCache(key, value, ttl) {
    if (!this.cache) return;
    try {
      await this.cache.set(key, value, ttl);
      this.log("debug", `Cache set for key: ${key}`);
    } catch (error) {
      this.log("error", "Cache set error:", error);
    }
  }
  /**
   * Execute function with caching
   */
  async withCache(cacheKey, fn, ttl) {
    const cached = await this.getFromCache(cacheKey);
    if (cached !== null) {
      return cached;
    }
    const result = await fn();
    await this.setCache(cacheKey, result, ttl);
    return result;
  }
};
export {
  API_PREFIX,
  API_VERSION,
  AuthError,
  AuthenticationError,
  AuthorizationError,
  BUDGET_DURATION,
  BaseApiClient,
  BaseSignalRConnection,
  CACHE_TTL,
  CHAT_ROLES,
  CLIENT_INFO,
  CONTENT_TYPES,
  ConduitError,
  ConflictError,
  DATE_FORMATS,
  DefaultTransports,
  ERROR_CODES,
  FILTER_MODE,
  FILTER_TYPE,
  FilterOperator,
  HEALTH_STATUS,
  HTTP_HEADERS,
  HTTP_STATUS,
  HttpError,
  HttpMethod,
  HttpTransportType,
  HubConnectionState,
  IMAGE_RESPONSE_FORMATS,
  ModelCapability,
  NetworkError,
  NotFoundError,
  NotImplementedError,
  PAGINATION,
  PATTERNS,
  POLLING_CONFIG,
  ProviderType,
  RETRY_CONFIG,
  RateLimitError,
  ResponseParser,
  STREAM_CONSTANTS,
  ServerError,
  SignalRLogLevel,
  StreamError,
  TASK_STATUS,
  TIMEOUTS,
  TimeoutError,
  VIDEO_RESPONSE_FORMATS,
  ValidationError,
  addTime,
  assertArrayLength,
  assertDefined,
  assertHasProperties,
  assertInRange,
  assertNotEmpty,
  assertOneOf,
  capitalize,
  chunk,
  createErrorFromResponse,
  createValidator,
  debounce,
  deepClone,
  deepMerge,
  delay,
  deserializeError,
  formatApiDate,
  formatBytes,
  formatCurrency,
  formatDuration,
  formatDurationHMS,
  formatFilePath,
  formatList,
  formatNumber,
  formatPercentage,
  fromUnixTimestamp,
  getCapabilityCategory,
  getCapabilityDisplayName,
  getCurrentTimestamp,
  getEndOf,
  getErrorMessage,
  getErrorStatusCode,
  getProviderDisplayName,
  getStartOf,
  getTimeDifference,
  groupBy,
  handleApiError,
  hasModelFeatureSupport,
  isAuthError,
  isAuthorizationError,
  isBaseModel,
  isConduitError,
  isConflictError,
  isDateInRange,
  isEnumValue,
  isErrorLike,
  isHttpError,
  isHttpMethod,
  isHttpNetworkError,
  isNetworkError,
  isNonEmptyString,
  isNotFoundError,
  isObject,
  isPositiveNumber,
  isProviderType,
  isRateLimitError,
  isSerializedConduitError,
  isStreamError,
  isTimeoutError,
  isValidApiKey,
  isValidBase64,
  isValidEmail,
  isValidIsoDate,
  isValidJson,
  isValidUrl,
  isValidUuid,
  isValidationError,
  maskSensitive,
  memoize,
  omit,
  padZero,
  parseIsoDate,
  pick,
  pluralize,
  retry,
  sanitizeString,
  serializeError,
  throttle,
  toCamelCase,
  toIsoString,
  toKebabCase,
  toSnakeCase,
  toTitleCase,
  toUnixTimestamp,
  truncateString,
  withTimeout
};
//# sourceMappingURL=index.mjs.map