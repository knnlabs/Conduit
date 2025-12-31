import { NextResponse } from 'next/server';

export interface ApiError {
  error: string;
  message?: string;
  details?: unknown;
  timestamp: string;
  requestId?: string;
}

/**
 * Standard API error response format
 */
export function apiError(
  error: string,
  status: number,
  details?: unknown
): NextResponse<ApiError> {
  const errorResponse: ApiError = {
    error,
    timestamp: new Date().toISOString(),
  };

  if (details) {
    if (details instanceof Error) {
      errorResponse.message = details.message;
    } else {
      errorResponse.details = details;
    }
  }

  return NextResponse.json(errorResponse, { status });
}

/**
 * Common error responses
 */
export const errors = {
  badRequest: (details?: unknown) => 
    apiError('Bad Request', 400, details),
    
  unauthorized: (details?: unknown) => 
    apiError('Unauthorized', 401, details),
    
  forbidden: (details?: unknown) => 
    apiError('Forbidden', 403, details),
    
  notFound: (resource?: string) => 
    apiError(resource ? `${resource} not found` : 'Not Found', 404),
    
  methodNotAllowed: (method: string) => 
    apiError(`Method ${method} not allowed`, 405),
    
  conflict: (details?: unknown) => 
    apiError('Conflict', 409, details),
    
  unprocessableEntity: (details?: unknown) => 
    apiError('Unprocessable Entity', 422, details),
    
  tooManyRequests: (retryAfter?: number) => {
    const response = apiError('Too Many Requests', 429);
    if (retryAfter) {
      response.headers.set('Retry-After', retryAfter.toString());
    }
    return response;
  },
    
  internalServerError: (details?: unknown) => 
    apiError('Internal Server Error', 500, details),
    
  serviceUnavailable: (details?: unknown) => 
    apiError('Service Unavailable', 503, details),
};