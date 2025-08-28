/**
 * SSE Error Handler for OpenAI-compatible error responses
 * Extracted from WebUI and made framework-agnostic
 */

/**
 * OpenAI Error object structure
 */
export interface OpenAIError {
  message: string;
  type: string;
  code?: string;
  param?: string;
}

/**
 * OpenAI Error response structure
 */
export interface OpenAIErrorResponse {
  error: OpenAIError;
}

/**
 * Framework-agnostic application error
 */
export interface AppError {
  status: number;
  code: string;
  title: string;
  message: string;
  isRecoverable: boolean;
  suggestions: string[];
  severity: 'error' | 'warning' | 'info';
  iconName: string;
  retryAfter?: number;
  originalError?: OpenAIError;
}

/**
 * Check if SSE data contains an OpenAI error
 */
export function isOpenAIError(data: unknown): data is OpenAIErrorResponse {
  if (!data || typeof data !== 'object') {
    return false;
  }
  
  const obj = data as Record<string, unknown>;
  
  // Check for OpenAI error format
  if ('error' in obj && obj.error && typeof obj.error === 'object') {
    const error = obj.error as Record<string, unknown>;
    return 'message' in error && typeof error.message === 'string';
  }
  
  return false;
}

/**
 * Parse SSE error data into AppError
 */
export function parseSSEError(data: unknown): AppError | null {
  if (!isOpenAIError(data)) {
    return null;
  }
  
  const error = data.error;
  let status = 500;
  
  // Infer status from error code or type
  if (error.code === 'model_not_found') {
    status = 404;
  } else if (error.code === 'invalid_request' || error.code === 'invalid_parameter') {
    status = 400;
  } else if (error.code === 'authentication_error') {
    status = 401;
  } else if (error.code === 'insufficient_balance') {
    status = 402;
  } else if (error.code === 'permission_denied') {
    status = 403;
  } else if (error.code === 'rate_limit_exceeded') {
    status = 429;
  } else if (error.type === 'invalid_request_error') {
    status = 400;
  } else if (error.type === 'authentication_error') {
    status = 401;
  } else if (error.type === 'permission_error') {
    status = 403;
  } else if (error.type === 'not_found_error') {
    status = 404;
  } else if (error.type === 'rate_limit_error') {
    status = 429;
  }
  
  return createAppError(status, error);
}

/**
 * Create an AppError from status and OpenAI error
 */
function createAppError(status: number, error: OpenAIError): AppError {
  const title = getErrorTitle(status);
  const message = getErrorMessage(status, error);
  const suggestions = getErrorSuggestions(status, error);
  const isRecoverable = getIsRecoverable(status);
  const severity = getErrorSeverity(status);
  const iconName = getErrorIconName(status);
  
  return {
    status,
    code: error.code ?? 'unknown_error',
    title,
    message,
    isRecoverable,
    suggestions,
    severity,
    iconName,
    originalError: error,
  };
}

/**
 * Enhanced SSE event processor that detects errors
 */
export interface ProcessedSSEEvent {
  type: 'content' | 'error' | 'metrics' | 'done';
  data?: unknown;
  error?: AppError;
}

export function processSSEEvent(eventData: unknown): ProcessedSSEEvent {
  // Check for [DONE] marker
  if (eventData === '[DONE]') {
    return { type: 'done' };
  }
  
  // Check for OpenAI error
  const error = parseSSEError(eventData);
  if (error) {
    return { type: 'error', error };
  }
  
  // Check for metrics event (if data has metrics field)
  if (eventData && typeof eventData === 'object' && 'metrics' in eventData) {
    return { type: 'metrics', data: eventData };
  }
  
  // Regular content event
  return { type: 'content', data: eventData };
}

/**
 * Handle SSE connection errors
 */
export function handleSSEConnectionError(error: unknown): AppError {
  // Network errors
  if (error instanceof TypeError && error.message.includes('fetch')) {
    return {
      status: 503,
      code: 'network_error',
      title: 'Connection Failed',
      message: 'Unable to connect to the server. Please check your network connection.',
      isRecoverable: true,
      suggestions: [
        'Check your internet connection',
        'Try refreshing the page',
        'Check if the service is online',
      ],
      severity: 'error',
      iconName: 'ServerIcon',
    };
  }
  
  // Abort errors
  if (error instanceof DOMException && error.name === 'AbortError') {
    return {
      status: 499,
      code: 'request_cancelled',
      title: 'Request Cancelled',
      message: 'The request was cancelled.',
      isRecoverable: false,
      suggestions: [],
      severity: 'info',
      iconName: 'ExclamationCircleIcon',
    };
  }
  
  // Generic error
  return {
    status: 500,
    code: 'streaming_error',
    title: 'Streaming Error',
    message: error instanceof Error ? error.message : 'An error occurred during streaming',
    isRecoverable: true,
    suggestions: [
      'Try again',
      'Refresh the page if the issue persists',
    ],
    severity: 'error',
    iconName: 'ExclamationCircleIcon',
  };
}

// Helper functions for error messages and metadata
function getErrorTitle(status: number): string {
  switch (status) {
    case 400: return 'Invalid Request';
    case 401: return 'Authentication Failed';
    case 402: return 'Insufficient Balance';
    case 403: return 'Access Denied';
    case 404: return 'Not Found';
    case 408: return 'Request Timeout';
    case 413: return 'Request Too Large';
    case 429: return 'Rate Limit Exceeded';
    case 500: return 'Server Error';
    case 502: return 'Bad Gateway';
    case 503: return 'Service Unavailable';
    case 504: return 'Gateway Timeout';
    default: return 'Error';
  }
}

function getErrorMessage(status: number, error?: OpenAIError): string {
  if (error?.message) {
    // For specific cases, enhance the message
    if (error.code === 'model_not_found' && error.param) {
      return `The model "${error.param}" is not available. Please select a different model.`;
    }
    return error.message;
  }
  
  // Default messages by status code
  switch (status) {
    case 400: return 'Your request contains invalid parameters. Please check your input and try again.';
    case 401: return 'Authentication failed. Please check your API key.';
    case 402: return 'Your account balance is insufficient to complete this request.';
    case 403: return 'You do not have permission to access this resource.';
    case 404: return 'The requested resource was not found.';
    case 408: return 'Your request took too long to process and timed out.';
    case 413: return 'Your request exceeds the maximum allowed size.';
    case 429: return 'You have exceeded the rate limit. Please slow down your requests.';
    case 500: return 'An unexpected server error occurred. Our team has been notified.';
    case 502: return 'The server received an invalid response from an upstream server.';
    case 503: return 'The service is temporarily unavailable. Please try again later.';
    case 504: return 'The server did not receive a timely response from an upstream server.';
    default: return 'An unexpected error occurred.';
  }
}

function getErrorSuggestions(status: number, error?: OpenAIError): string[] {
  if (error?.code === 'model_not_found') {
    return [
      'Check available models in the model selector',
      'Contact support if you need access to this model',
      'Try using an alternative model with similar capabilities',
    ];
  }
  
  switch (status) {
    case 400: return [
      'Review the API documentation for parameter requirements',
      'Ensure all required fields are provided',
      'Check parameter values are in the correct format'
    ];
    case 401: return [
      'Verify your API key is correct',
      'Check that your API key has not expired',
      'Ensure your API key has the necessary permissions',
    ];
    case 402: return [
      'Add credits to your account',
      'Check your usage limits in account settings',
      'Contact billing support if you believe this is an error',
    ];
    case 403: return [
      'Check your account permissions',
      'Contact your administrator for access',
      'Verify you are using the correct API endpoint',
    ];
    case 404: return [
      'Verify the resource exists',
      'Check for typos in the resource identifier',
      'Ensure you have the correct permissions',
    ];
    case 408: case 504: return [
      'Try with a shorter prompt or simpler request',
      'Break large requests into smaller chunks',
      'Check your network connection',
      'Try again during off-peak hours',
    ];
    case 413: return [
      'Reduce the size of your input',
      'Split large requests into smaller chunks',
      'Remove unnecessary data from your request',
      'Consider using a streaming approach for large data',
    ];
    case 429: return [
      'Wait a few moments before retrying',
      'Consider upgrading your plan for higher limits',
      'Implement request batching to reduce API calls',
      'Add delays between consecutive requests',
    ];
    case 500: case 502: case 503: return [
      'Try again in a few moments',
      'Check the service status page',
      'Contact support if the issue persists',
    ];
    default: return [
      'Try again in a few moments',
      'Contact support if the issue persists',
    ];
  }
}

function getIsRecoverable(status: number): boolean {
  return [408, 429, 500, 502, 503, 504].includes(status);
}

function getErrorSeverity(status: number): 'error' | 'warning' | 'info' {
  if (status >= 500) {
    return 'error';
  }
  if (status === 429 || status === 408) {
    return 'warning';
  }
  return 'info';
}

function getErrorIconName(status: number): string {
  switch (status) {
    case 401:
    case 403:
      return 'LockClosedIcon';
    case 402:
      return 'CreditCardIcon';
    case 404:
      return 'MagnifyingGlassIcon';
    case 408:
    case 504:
      return 'ClockIcon';
    case 413:
      return 'DocumentTextIcon';
    case 429:
      return 'ExclamationTriangleIcon';
    case 500:
    case 502:
    case 503:
      return 'ServerIcon';
    default:
      return 'ExclamationCircleIcon';
  }
}