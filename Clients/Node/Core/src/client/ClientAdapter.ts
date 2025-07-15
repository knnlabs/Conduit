import type { FetchBasedClient } from './FetchBasedClient';
import type { RequestOptions } from './types';
import { HttpMethod } from './HttpMethod';

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
 * Adapter to safely access protected methods of FetchBasedClient
 */
export function createClientAdapter(client: FetchBasedClient): IFetchBasedClientAdapter {
  // Using bracket notation to access protected methods
  // This is a necessary workaround for the current architecture
  return {
    request: (urlOrConfig, options) => (client as any).request(urlOrConfig, options),
    get: (url, options) => (client as any).get(url, options),
    post: (url, data, options) => (client as any).post(url, data, options),
    put: (url, data, options) => (client as any).put(url, data, options),
    patch: (url, data, options) => (client as any).patch(url, data, options),
    delete: (url, options) => (client as any).delete(url, options),
  };
}