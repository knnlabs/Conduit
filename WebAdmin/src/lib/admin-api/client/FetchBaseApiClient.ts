/**
 * Contract-backed Admin API transport.
 *
 * The shared ContractApiClient owns the openapi-fetch, timeout, callback, and
 * retry lifecycle. This subclass only supplies Admin authentication, error
 * mapping, media type policy, and the legacy service-facing overloads.
 */

import {
  ContractApiClient,
  type ContractOperation,
  type RetryStrategy,
  RetryStrategyType,
  handleApiError,
} from "@/lib/conduit-common";
import type { paths } from "../generated/admin-api";
import type {
  ApiClientConfig,
  RequestConfig,
} from "./types";
import { CLIENT_INFO, CONTENT_TYPES, HTTP_HEADERS } from "../constants";
import { HttpMethod } from "./HttpMethod";

function normalizeRetryStrategy(config: ApiClientConfig): RetryStrategy {
  if (config.retryDelay?.length) {
    return {
      type: RetryStrategyType.CUSTOM_DELAYS,
      delays: config.retryDelay,
    };
  }

  const retries = config.retries;
  if (typeof retries === "number") {
    return {
      type: RetryStrategyType.FIXED_DELAY,
      maxRetries: retries,
      delayMs: 1000,
    };
  }

  if (retries) {
    return {
      type: RetryStrategyType.FIXED_DELAY,
      maxRetries: retries.maxRetries,
      delayMs: retries.retryDelay,
      retryCondition: retries.retryCondition,
    };
  }

  return {
    type: RetryStrategyType.FIXED_DELAY,
    maxRetries: 3,
    delayMs: 1000,
  };
}

export abstract class FetchBaseApiClient extends ContractApiClient<paths> {
  protected readonly masterKey: string;

  constructor(config: ApiClientConfig) {
    super({
      baseUrl: config.baseUrl,
      timeout: config.timeout ?? 30000,
      defaultHeaders: {
        [HTTP_HEADERS.USER_AGENT]: CLIENT_INFO.USER_AGENT,
        ...config.defaultHeaders,
      },
      retryStrategy: normalizeRetryStrategy(config),
      onError: config.onError,
      onRequest: config.onRequest,
      onResponse: config.onResponse,
      logger: config.logger,
      cache: config.cache,
    });
    this.masterKey = config.masterKey;
  }

  protected getAuthHeaders(): Record<string, string> {
    return { "X-Master-Key": this.masterKey };
  }

  protected getDefaultRetryStrategy(): RetryStrategy {
    return {
      type: RetryStrategyType.FIXED_DELAY,
      maxRetries: 3,
      delayMs: 1000,
    };
  }

  protected override buildContractHeaders(
    additionalHeaders: Record<string, string> | undefined,
    method: HttpMethod,
  ): Record<string, string> {
    return {
      ...this.getAuthHeaders(),
      ...this.defaultHeaders,
      [HTTP_HEADERS.CONTENT_TYPE]:
        method === HttpMethod.PATCH
          ? CONTENT_TYPES.JSON_MERGE_PATCH
          : CONTENT_TYPES.JSON,
      ...additionalHeaders,
    };
  }

  protected override async handleErrorResponse(
    response: Response,
  ): Promise<Error> {
    const headers = Object.fromEntries(response.headers.entries());
    let data: unknown;
    try {
      data = response.headers.get("content-type")?.includes("application/json")
        ? await response.json()
        : await response.text();
    } catch {
      data = null;
    }

    return handleApiError({
      response: { status: response.status, data, headers },
      config: { url: response.url, method: "unknown" },
      isHttpError: false,
      message: `HTTP ${response.status}: ${response.statusText}`,
    });
  }

  protected async executeContractRead<TResponse>(
    resolvedPath: string,
    operation: ContractOperation<paths, TResponse>,
    config?: RequestConfig,
  ): Promise<TResponse> {
    return this.executeContractRequest(
      resolvedPath,
      HttpMethod.GET,
      operation,
      {
        headers: config?.headers,
        signal: config?.signal,
        timeout: config?.timeout,
      },
    );
  }

  protected async executeContractOperation<TResponse, TRequest = unknown>(
    resolvedPath: string,
    method: HttpMethod,
    operation: ContractOperation<paths, TResponse>,
    config?: RequestConfig,
    requestBody?: TRequest,
  ): Promise<TResponse> {
    return this.executeContractRequest(resolvedPath, method, operation, {
      headers: config?.headers,
      signal: config?.signal,
      timeout: config?.timeout,
      body: requestBody,
    });
  }

  protected override async get<TResponse = unknown>(
    url: string,
    optionsOrParams?:
      | {
          headers?: Record<string, string>;
          signal?: AbortSignal;
          timeout?: number;
          responseType?: "json" | "text" | "blob" | "arraybuffer";
        }
      | Record<string, unknown>,
    extraOptions?: {
      headers?: Record<string, string>;
      signal?: AbortSignal;
      timeout?: number;
      responseType?: "json" | "text" | "blob" | "arraybuffer";
    },
  ): Promise<TResponse> {
    if (extraOptions) {
      const urlWithParams = optionsOrParams
        ? this.buildUrlWithParams(
            url,
            optionsOrParams as Record<string, unknown>,
          )
        : url;
      return this.request<TResponse>(urlWithParams, {
        ...extraOptions,
        method: HttpMethod.GET,
      });
    }

    const isOptions =
      optionsOrParams &&
      ("headers" in optionsOrParams ||
        "signal" in optionsOrParams ||
        "timeout" in optionsOrParams ||
        "responseType" in optionsOrParams);
    if (isOptions) {
      return this.request<TResponse>(url, {
        ...(optionsOrParams as {
          headers?: Record<string, string>;
          signal?: AbortSignal;
          timeout?: number;
          responseType?: "json" | "text" | "blob" | "arraybuffer";
        }),
        method: HttpMethod.GET,
      });
    }

    const urlWithParams = optionsOrParams
      ? this.buildUrlWithParams(url, optionsOrParams)
      : url;
    return this.request<TResponse>(urlWithParams, { method: HttpMethod.GET });
  }

  private buildUrlWithParams(
    url: string,
    params: Record<string, unknown>,
  ): string {
    const searchParams = new URLSearchParams();
    const serialize = (value: unknown): string => {
      if (typeof value === "string") return value;
      if (
        typeof value === "number" ||
        typeof value === "boolean" ||
        typeof value === "bigint"
      ) {
        return value.toString();
      }
      if (typeof value === "symbol") return value.description ?? "";
      if (typeof value === "function") return value.name;
      return JSON.stringify(value) ?? "";
    };

    Object.entries(params).forEach(([key, value]) => {
      if (value === undefined || value === null) return;
      if (Array.isArray(value)) {
        value.forEach((item) => searchParams.append(key, serialize(item)));
      } else {
        searchParams.append(key, serialize(value));
      }
    });

    const queryString = searchParams.toString();
    return queryString ? `${url}?${queryString}` : url;
  }

  protected override getCacheKey(
    methodOrResource: string,
    urlOrId?: unknown,
    paramsOrId2?: Record<string, unknown> | string,
  ): string {
    if (typeof urlOrId === "string" && typeof paramsOrId2 === "string") {
      return `${methodOrResource}:${urlOrId}:${paramsOrId2}`;
    }
    if (
      typeof urlOrId === "string" &&
      paramsOrId2 &&
      typeof paramsOrId2 === "object"
    ) {
      return `${methodOrResource}:${urlOrId}:${JSON.stringify(paramsOrId2)}`;
    }
    const id = urlOrId ? JSON.stringify(urlOrId) : "";
    return `${methodOrResource}:${id}`;
  }
}
