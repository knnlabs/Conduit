/**
 * Service helper functions for common CRUD patterns
 * Reduces boilerplate from 30+ lines to ~5 lines per service
 */

import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';

/**
 * Narrowed options type for GET requests
 * Only includes fields that the get() method accepts as options
 */
interface GetRequestOptions {
  headers?: Record<string, string>;
  signal?: AbortSignal;
  timeout?: number;
  responseType?: 'json' | 'text' | 'blob' | 'arraybuffer';
}

/**
 * Helper interface for services that use FetchBaseApiClient
 */
interface ServiceWithClient {
  client: FetchBaseApiClient;
}

/**
 * Creates a type-safe GET by ID method
 *
 * @param endpoint - Function that takes an ID and returns the endpoint path
 * @returns Bound method that can be used in service classes
 *
 * @example
 * ```typescript
 * export class FetchVirtualKeyService {
 *   constructor(private readonly client: FetchBaseApiClient) {}
 *   getById = createGetById<VirtualKeyDto>(ENDPOINTS.VIRTUAL_KEYS.BY_ID);
 * }
 * ```
 */
export function createGetById<TResponse>(endpoint: (id: number) => string) {
  return function(this: ServiceWithClient, id: number, config?: GetRequestOptions): Promise<TResponse> {
    return this.client['get']<TResponse>(endpoint(id), config);
  };
}

/**
 * Creates a type-safe GET by string ID method
 *
 * @param endpoint - Function that takes a string ID and returns the endpoint path
 * @returns Bound method that can be used in service classes
 */
export function createGetByStringId<TResponse>(endpoint: (id: string) => string) {
  return function(this: ServiceWithClient, id: string, config?: GetRequestOptions): Promise<TResponse> {
    return this.client['get']<TResponse>(endpoint(id), config);
  };
}

/**
 * Creates a type-safe list/index method
 *
 * @param endpoint - The endpoint path for listing resources
 * @returns Bound method that can be used in service classes
 *
 * @example
 * ```typescript
 * export class FetchVirtualKeyService {
 *   constructor(private readonly client: FetchBaseApiClient) {}
 *   list = createListMethod<PaginatedResponse<VirtualKeyDto>>(ENDPOINTS.VIRTUAL_KEYS.LIST);
 * }
 * ```
 */
export function createListMethod<TResponse>(endpoint: string) {
  return function(this: ServiceWithClient, params?: Record<string, unknown>, config?: GetRequestOptions): Promise<TResponse> {
    // Use 3-arg signature when both params and config are provided
    if (params && config) {
      return this.client['get']<TResponse>(endpoint, params, config);
    }
    // Use 2-arg signature with just params or config
    return this.client['get']<TResponse>(endpoint, params ?? config);
  };
}

/**
 * Creates a type-safe POST/create method
 *
 * @param endpoint - The endpoint path for creating resources
 * @returns Bound method that can be used in service classes
 *
 * @example
 * ```typescript
 * export class FetchVirtualKeyService {
 *   constructor(private readonly client: FetchBaseApiClient) {}
 *   create = createCreateMethod<VirtualKeyDto, CreateVirtualKeyDto>(ENDPOINTS.VIRTUAL_KEYS.CREATE);
 * }
 * ```
 */
export function createCreateMethod<TResponse, TData = unknown>(endpoint: string) {
  return function(this: ServiceWithClient, data: TData, config?: RequestConfig): Promise<TResponse> {
    return this.client['post']<TResponse, TData>(endpoint, data, config);
  };
}

/**
 * Creates a type-safe PUT/update method that takes an ID
 *
 * @param endpoint - Function that takes an ID and returns the endpoint path
 * @returns Bound method that can be used in service classes
 *
 * @example
 * ```typescript
 * export class FetchVirtualKeyService {
 *   constructor(private readonly client: FetchBaseApiClient) {}
 *   update = createUpdateMethod<VirtualKeyDto, UpdateVirtualKeyDto>(ENDPOINTS.VIRTUAL_KEYS.BY_ID);
 * }
 * ```
 */
export function createUpdateMethod<TResponse, TData = unknown>(endpoint: (id: number) => string) {
  return function(this: ServiceWithClient, id: number, data: TData, config?: RequestConfig): Promise<TResponse> {
    return this.client['put']<TResponse, TData>(endpoint(id), data, config);
  };
}

/**
 * Creates a type-safe PUT/update method that takes a string ID
 *
 * @param endpoint - Function that takes a string ID and returns the endpoint path
 * @returns Bound method that can be used in service classes
 */
export function createUpdateByStringIdMethod<TResponse, TData = unknown>(endpoint: (id: string) => string) {
  return function(this: ServiceWithClient, id: string, data: TData, config?: RequestConfig): Promise<TResponse> {
    return this.client['put']<TResponse, TData>(endpoint(id), data, config);
  };
}

/**
 * Creates a type-safe PATCH method that takes an ID
 *
 * @param endpoint - Function that takes an ID and returns the endpoint path
 * @returns Bound method that can be used in service classes
 */
export function createPatchMethod<TResponse, TData = unknown>(endpoint: (id: number) => string) {
  return function(this: ServiceWithClient, id: number, data: TData, config?: RequestConfig): Promise<TResponse> {
    return this.client['patch']<TResponse, TData>(endpoint(id), data, config);
  };
}

/**
 * Creates a type-safe DELETE method that takes an ID
 *
 * @param endpoint - Function that takes an ID and returns the endpoint path
 * @returns Bound method that can be used in service classes
 *
 * @example
 * ```typescript
 * export class FetchVirtualKeyService {
 *   constructor(private readonly client: FetchBaseApiClient) {}
 *   delete = createDeleteMethod(ENDPOINTS.VIRTUAL_KEYS.BY_ID);
 * }
 * ```
 */
export function createDeleteMethod(endpoint: (id: number) => string) {
  return function(this: ServiceWithClient, id: number, config?: RequestConfig): Promise<void> {
    return this.client['delete']<void>(endpoint(id), config);
  };
}

/**
 * Creates a type-safe DELETE method that takes a string ID
 *
 * @param endpoint - Function that takes a string ID and returns the endpoint path
 * @returns Bound method that can be used in service classes
 */
export function createDeleteByStringIdMethod(endpoint: (id: string) => string) {
  return function(this: ServiceWithClient, id: string, config?: RequestConfig): Promise<void> {
    return this.client['delete']<void>(endpoint(id), config);
  };
}

/**
 * Creates a type-safe simple POST method (no data payload)
 * Used for actions like "mark as read", "activate", etc.
 *
 * @param endpoint - The endpoint path or function that generates it
 * @returns Bound method that can be used in service classes
 */
export function createActionMethod<TResponse = void>(endpoint: string | ((id: number) => string)) {
  return function(this: ServiceWithClient, idOrConfig?: number | RequestConfig, config?: RequestConfig): Promise<TResponse> {
    if (typeof endpoint === 'function') {
      // Endpoint is a function, first arg must be ID
      const id = idOrConfig as number;
      return this.client['post']<TResponse>(endpoint(id), undefined, config);
    } else {
      // Endpoint is a string, first arg might be config
      const actualConfig = typeof idOrConfig === 'object' ? idOrConfig : config;
      return this.client['post']<TResponse>(endpoint, undefined, actualConfig);
    }
  };
}
