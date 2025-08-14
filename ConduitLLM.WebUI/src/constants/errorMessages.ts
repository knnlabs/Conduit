/**
 * User-friendly error messages for OpenAI-compatible error responses
 */

export interface ErrorMessageConfig {
  getTitle: () => string;
  getMessage: (error?: OpenAIError) => string;
  getSuggestions: (error?: OpenAIError) => string[];
  isRecoverable: boolean;
}

export interface OpenAIError {
  message: string;
  type: string;
  code?: string;
  param?: string;
}

export interface OpenAIErrorResponse {
  error: OpenAIError;
}

/**
 * Maps HTTP status codes to user-friendly error configurations
 */
export const ERROR_MESSAGES: Record<number, ErrorMessageConfig> = {
  [400]: {
    getTitle: () => 'Invalid Request',
    getMessage: (error) => {
      if (error?.code === 'missing_parameter' && error.param) {
        return `Required parameter '${error.param}' is missing`;
      }
      if (error?.code === 'invalid_parameter' && error.param) {
        return `Invalid value for parameter '${error.param}'`;
      }
      return error?.message ?? 'Your request contains invalid parameters. Please check your input and try again.';
    },
    getSuggestions: (error) => {
      const suggestions = [];
      if (error?.param) {
        suggestions.push(`Check the value of '${error.param}'`);
      }
      suggestions.push('Review the API documentation for parameter requirements');
      suggestions.push('Ensure all required fields are provided');
      return suggestions;
    },
    isRecoverable: false,
  },

  [401]: {
    getTitle: () => 'Authentication Failed',
    getMessage: (error) => 
      error?.message ?? 'Authentication failed. Please check your API key.',
    getSuggestions: () => [
      'Verify your API key is correct',
      'Check that your API key has not expired',
      'Ensure your API key has the necessary permissions',
    ],
    isRecoverable: false,
  },

  [402]: {
    getTitle: () => 'Insufficient Balance',
    getMessage: (error) => 
      error?.message ?? 'Your account balance is insufficient to complete this request.',
    getSuggestions: () => [
      'Add credits to your account',
      'Check your usage limits in account settings',
      'Contact billing support if you believe this is an error',
    ],
    isRecoverable: false,
  },

  [403]: {
    getTitle: () => 'Access Denied',
    getMessage: (error) => 
      error?.message ?? 'You do not have permission to access this resource.',
    getSuggestions: () => [
      'Check your account permissions',
      'Contact your administrator for access',
      'Verify you are using the correct API endpoint',
    ],
    isRecoverable: false,
  },

  [404]: {
    getTitle: () => 'Not Found',
    getMessage: (error) => {
      if (error?.code === 'model_not_found' && error.param) {
        return `The model "${error.param}" is not available. Please select a different model.`;
      }
      return error?.message ?? 'The requested resource was not found.';
    },
    getSuggestions: (error) => {
      if (error?.code === 'model_not_found') {
        return [
          'Check available models in the model selector',
          'Contact support if you need access to this model',
          'Try using an alternative model with similar capabilities',
        ];
      }
      return [
        'Verify the resource exists',
        'Check for typos in the resource identifier',
        'Ensure you have the correct permissions',
      ];
    },
    isRecoverable: false,
  },

  [408]: {
    getTitle: () => 'Request Timeout',
    getMessage: (error) => 
      error?.message ?? 'Your request took too long to process and timed out.',
    getSuggestions: () => [
      'Try with a shorter prompt or simpler request',
      'Break large requests into smaller chunks',
      'Check your network connection',
      'Try again during off-peak hours',
    ],
    isRecoverable: true,
  },

  [413]: {
    getTitle: () => 'Request Too Large',
    getMessage: (error) => 
      error?.message ?? 'Your request exceeds the maximum allowed size.',
    getSuggestions: () => [
      'Reduce the size of your input',
      'Split large requests into smaller chunks',
      'Remove unnecessary data from your request',
      'Consider using a streaming approach for large data',
    ],
    isRecoverable: false,
  },

  [429]: {
    getTitle: () => 'Rate Limit Exceeded',
    getMessage: (error) => {
      const retryAfter = extractRetryAfter(error);
      if (retryAfter) {
        return `Rate limit exceeded. Please wait ${retryAfter} seconds before trying again.`;
      }
      return error?.message ?? 'You have exceeded the rate limit. Please slow down your requests.';
    },
    getSuggestions: (error) => {
      const suggestions = [];
      const retryAfter = extractRetryAfter(error);
      if (retryAfter) {
        suggestions.push(`Wait ${retryAfter} seconds before retrying`);
      }
      suggestions.push('Consider upgrading your plan for higher limits');
      suggestions.push('Implement request batching to reduce API calls');
      suggestions.push('Add delays between consecutive requests');
      return suggestions;
    },
    isRecoverable: true,
  },

  [500]: {
    getTitle: () => 'Server Error',
    getMessage: (error) => 
      error?.message ?? 'An unexpected server error occurred. Our team has been notified.',
    getSuggestions: () => [
      'Try again in a few moments',
      'Check the service status page',
      'Contact support if the issue persists',
    ],
    isRecoverable: true,
  },

  [502]: {
    getTitle: () => 'Bad Gateway',
    getMessage: (error) => 
      error?.message ?? 'The server received an invalid response from an upstream server.',
    getSuggestions: () => [
      'Wait a few moments and try again',
      'Check the service status page',
      'Try a different endpoint if available',
    ],
    isRecoverable: true,
  },

  [503]: {
    getTitle: () => 'Service Unavailable',
    getMessage: (error) => 
      error?.message ?? 'The service is temporarily unavailable. Please try again later.',
    getSuggestions: () => [
      'Wait a few minutes before retrying',
      'Check the service status page for maintenance windows',
      'Try during off-peak hours',
      'Consider implementing automatic retry logic',
    ],
    isRecoverable: true,
  },

  [504]: {
    getTitle: () => 'Gateway Timeout',
    getMessage: (error) => 
      error?.message ?? 'The server did not receive a timely response from an upstream server.',
    getSuggestions: () => [
      'Try again with a simpler request',
      'Check your network connectivity',
      'Wait a few moments before retrying',
    ],
    isRecoverable: true,
  },
};

/**
 * Get the default error configuration for unknown status codes
 */
export function getDefaultErrorConfig(): ErrorMessageConfig {
  return {
    getTitle: () => 'Error',
    getMessage: (error) => error?.message ?? 'An unexpected error occurred.',
    getSuggestions: () => [
      'Try again in a few moments',
      'Contact support if the issue persists',
    ],
    isRecoverable: false,
  };
}

/**
 * Get error configuration for a specific status code
 */
export function getErrorConfig(statusCode: number): ErrorMessageConfig {
  return ERROR_MESSAGES[statusCode] ?? getDefaultErrorConfig();
}

/**
 * Extract retry-after value from error object or headers
 */
function extractRetryAfter(error?: OpenAIError): number | undefined {
  // Try to extract from error message if it contains a number
  if (error?.message) {
    const match = error.message.match(/\b(\d+)\s*seconds?\b/i);
    if (match) {
      return parseInt(match[1], 10);
    }
  }
  return undefined;
}

/**
 * Determine the appropriate icon name for an error
 */
export function getErrorIconName(statusCode: number): string {
  switch (statusCode) {
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

/**
 * Get the severity level for an error
 */
export function getErrorSeverity(statusCode: number): 'error' | 'warning' | 'info' {
  if (statusCode >= 500) {
    return 'error';
  }
  if (statusCode === 429 || statusCode === 408) {
    return 'warning';
  }
  return 'info';
}