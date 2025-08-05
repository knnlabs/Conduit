/**
 * Utility for enhancing error objects with HTTP status codes and appropriate names
 */

/**
 * Creates an enhanced Error object from an error message string
 * Extracts HTTP status codes and sets appropriate error names for better classification
 */
export function createEnhancedError(errorMessage: string): Error {
  const errorInstance = new Error(errorMessage);
  
  // Extract HTTP status code if present in error message
  const statusMatch = errorMessage.match(/status:\s*(\d+)|HTTP\s*(\d+)/i);
  if (statusMatch) {
    const statusCode = parseInt(statusMatch[1] || statusMatch[2], 10);
    (errorInstance as Error & { status?: number }).status = statusCode;
    
    // Set appropriate error name based on status code
    if (statusCode === 402) {
      errorInstance.name = 'InsufficientBalanceError';
    } else if (statusCode === 401) {
      errorInstance.name = 'AuthenticationError';
    } else if (statusCode === 403) {
      errorInstance.name = 'PermissionError';
    } else if (statusCode === 404) {
      errorInstance.name = 'NotFoundError';
    } else if (statusCode === 429) {
      errorInstance.name = 'RateLimitError';
    } else if (statusCode >= 500) {
      errorInstance.name = 'ServerError';
    } else if (statusCode >= 400) {
      errorInstance.name = 'ClientError';
    }
  }
  
  return errorInstance;
}