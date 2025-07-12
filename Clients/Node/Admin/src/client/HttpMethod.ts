/**
 * HTTP methods enum for type-safe API requests
 */
export enum HttpMethod {
  GET = 'GET',
  POST = 'POST',
  PUT = 'PUT',
  DELETE = 'DELETE',
  PATCH = 'PATCH',
  HEAD = 'HEAD',
  OPTIONS = 'OPTIONS'
}

/**
 * Type guard to check if a string is a valid HTTP method
 */
export function isHttpMethod(method: string): method is HttpMethod {
  return Object.values(HttpMethod).includes(method as HttpMethod);
}

/**
 * Request options with proper typing
 */
export interface RequestOptions<TRequest = unknown> {
  headers?: Record<string, string>;
  signal?: AbortSignal;
  timeout?: number;
  body?: TRequest;
  params?: Record<string, string | number | boolean>;
  responseType?: 'json' | 'text' | 'blob' | 'arraybuffer';
}

/**
 * Type-safe response interface
 */
export interface ApiResponse<T = unknown> {
  data: T;
  status: number;
  statusText: string;
  headers: Record<string, string>;
}