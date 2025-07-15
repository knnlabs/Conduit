import type { FetchBasedClient } from './FetchBasedClient';
import type { RequestOptions } from './types';
import type { HttpMethod } from './HttpMethod';

/**
 * Interface exposing the protected methods of FetchBasedClient
 * This is used by services that need to access the client's methods
 */
export interface IFetchBasedClientAdapter {
  request<TResponse = unknown>(
    urlOrConfig: string | { method: HttpMethod; url: string; body?: unknown },
    options?: RequestOptions & { method?: HttpMethod; body?: unknown }
  ): Promise<TResponse>;
  
  get<TResponse = unknown>(
    url: string,
    options?: RequestOptions
  ): Promise<TResponse>;
  
  post<TResponse = unknown, TRequest = unknown>(
    url: string,
    data?: TRequest,
    options?: RequestOptions
  ): Promise<TResponse>;
  
  put<TResponse = unknown, TRequest = unknown>(
    url: string,
    data?: TRequest,
    options?: RequestOptions
  ): Promise<TResponse>;
  
  patch<TResponse = unknown, TRequest = unknown>(
    url: string,
    data?: TRequest,
    options?: RequestOptions
  ): Promise<TResponse>;
  
  delete<TResponse = unknown>(
    url: string,
    options?: RequestOptions
  ): Promise<TResponse>;
}

/**
 * Type guard to check if a client has the required methods
 */
export function isClientAdapter(client: unknown): client is IFetchBasedClientAdapter {
  return (
    client !== null &&
    typeof client === 'object' &&
    'request' in client &&
    'get' in client &&
    'post' in client &&
    'put' in client &&
    'patch' in client &&
    'delete' in client
  );
}

/**
 * Internal interface to access protected methods of FetchBasedClient
 * This avoids using 'any' while maintaining type safety
 */
interface IFetchBasedClientInternal extends FetchBasedClient {
  request<TResponse = unknown>(
    urlOrConfig: string | { method: HttpMethod; url: string; body?: unknown },
    options?: RequestOptions & { method?: HttpMethod; body?: unknown }
  ): Promise<TResponse>;
  
  get<TResponse = unknown>(
    url: string,
    options?: RequestOptions
  ): Promise<TResponse>;
  
  post<TResponse = unknown, TRequest = unknown>(
    url: string,
    data?: TRequest,
    options?: RequestOptions
  ): Promise<TResponse>;
  
  put<TResponse = unknown, TRequest = unknown>(
    url: string,
    data?: TRequest,
    options?: RequestOptions
  ): Promise<TResponse>;
  
  patch<TResponse = unknown, TRequest = unknown>(
    url: string,
    data?: TRequest,
    options?: RequestOptions
  ): Promise<TResponse>;
  
  delete<TResponse = unknown>(
    url: string,
    options?: RequestOptions
  ): Promise<TResponse>;
}

/**
 * Adapter to safely access protected methods of FetchBasedClient
 */
export function createClientAdapter(client: FetchBasedClient): IFetchBasedClientAdapter {
  // Cast to internal interface to access protected methods with proper typing
  const internalClient = client as IFetchBasedClientInternal;
  
  return {
    request: <TResponse = unknown>(urlOrConfig: string | { method: HttpMethod; url: string; body?: unknown }, options?: RequestOptions & { method?: HttpMethod; body?: unknown }) => 
      internalClient.request<TResponse>(urlOrConfig, options),
    get: <TResponse = unknown>(url: string, options?: RequestOptions) => 
      internalClient.get<TResponse>(url, options),
    post: <TResponse = unknown, TRequest = unknown>(url: string, data?: TRequest, options?: RequestOptions) => 
      internalClient.post<TResponse, TRequest>(url, data, options),
    put: <TResponse = unknown, TRequest = unknown>(url: string, data?: TRequest, options?: RequestOptions) => 
      internalClient.put<TResponse, TRequest>(url, data, options),
    patch: <TResponse = unknown, TRequest = unknown>(url: string, data?: TRequest, options?: RequestOptions) => 
      internalClient.patch<TResponse, TRequest>(url, data, options),
    delete: <TResponse = unknown>(url: string, options?: RequestOptions) => 
      internalClient.delete<TResponse>(url, options),
  };
}