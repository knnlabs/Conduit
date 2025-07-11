/**
 * Extended fetch options that include response type hints
 * This provides a cleaner way to handle different response types
 */
export interface ExtendedRequestInit extends RequestInit {
  /**
   * Hint for how to parse the response body
   * This is not a standard fetch option but helps our client handle responses correctly
   */
  responseType?: 'json' | 'text' | 'blob' | 'arraybuffer' | 'stream';
  
  /**
   * Custom timeout in milliseconds
   */
  timeout?: number;
  
  /**
   * Request metadata for logging/debugging
   */
  metadata?: {
    /** Operation name for debugging */
    operation?: string;
    /** Start time for performance tracking */
    startTime?: number;
    /** Request ID for tracing */
    requestId?: string;
  };
}

/**
 * Response parser that handles different response types based on content-type and hints
 */
export class ResponseParser {
  /**
   * Parses a fetch Response based on content type and response type hint
   */
  static async parse<T>(
    response: Response,
    responseType?: ExtendedRequestInit['responseType']
  ): Promise<T> {
    // Handle empty responses
    const contentLength = response.headers.get('content-length');
    if (contentLength === '0' || response.status === 204) {
      return undefined as T;
    }
    
    // Use explicit responseType if provided
    if (responseType) {
      switch (responseType) {
        case 'json':
          return await response.json() as T;
        case 'text':
          return await response.text() as T;
        case 'blob':
          return await response.blob() as T;
        case 'arraybuffer':
          return await response.arrayBuffer() as T;
        case 'stream':
          if (!response.body) {
            throw new Error('Response body is not a stream');
          }
          return response.body as T;
        default:
          // TypeScript exhaustiveness check
          const _exhaustive: never = responseType;
          throw new Error(`Unknown response type: ${_exhaustive}`);
      }
    }
    
    // Auto-detect based on content-type
    const contentType = response.headers.get('content-type') || '';
    
    if (contentType.includes('application/json')) {
      return await response.json() as T;
    }
    
    if (contentType.includes('text/') || contentType.includes('application/xml')) {
      return await response.text() as T;
    }
    
    if (contentType.includes('application/octet-stream') || 
        contentType.includes('image/') ||
        contentType.includes('audio/') ||
        contentType.includes('video/')) {
      return await response.blob() as T;
    }
    
    // Default to text for unknown content types
    return await response.text() as T;
  }
  
  /**
   * Creates a clean RequestInit object without custom properties
   */
  static cleanRequestInit(init: ExtendedRequestInit): RequestInit {
    const { responseType, timeout, metadata, ...standardInit } = init;
    return standardInit;
  }
}