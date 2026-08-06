import createClient, { type Client } from "openapi-fetch";

import { getRequestConstructor } from "@/lib/api-transport/request-constructor";

import { BaseApiClient, type BaseRequestOptions } from "./BaseApiClient";
import type { BaseApiClientConfig } from "./base-client-config";
import { HttpMethod } from "../http/types";
import { CONTENT_TYPES, HTTP_HEADERS } from "../http/constants";

export interface ContractResult<TResponse = unknown> {
  data?: TResponse;
  error?: unknown;
  response: Response;
}

export interface ContractOperationOptions {
  [key: string]: unknown;
  headers: Record<string, string>;
  signal: AbortSignal;
}

export type ContractOperation<TPaths extends object, TResponse> = (
  client: Client<TPaths>,
  options: ContractOperationOptions,
) => Promise<ContractResult<TResponse>>;

export interface ContractRequestOptions<TRequest = unknown>
  extends BaseRequestOptions {
  method?: HttpMethod;
  body?: TRequest;
  bodySerializer?: (body: TRequest) => BodyInit;
}

/**
 * Rebuild a response whose body has already been consumed by openapi-fetch.
 * Error mappers can therefore keep accepting the standard Response surface.
 */
export function recreateContractErrorResponse(
  result: ContractResult,
): Response {
  return {
    ...result.response,
    ok: false,
    status: result.response.status,
    statusText: result.response.statusText,
    headers: result.response.headers,
    url: result.response.url,
    text: async () =>
      result.error === undefined ? "" : JSON.stringify(result.error),
    json: async () => result.error,
  } as Response;
}

/**
 * Shared openapi-fetch transport for contract-backed API clients.
 *
 * Subclasses provide authentication, retry policy, and API-specific error
 * mapping while this class owns timeout bridging, callbacks, retries, and the
 * consumed-response compatibility shim.
 */
export abstract class ContractApiClient<
  TPaths extends object,
> extends BaseApiClient {
  protected readonly contractClient: Client<TPaths>;

  protected constructor(config: BaseApiClientConfig) {
    super(config);
    this.contractClient = createClient<TPaths>({
      baseUrl: this.baseUrl,
      Request: getRequestConstructor(),
    });
  }

  protected override async request<
    TResponse = unknown,
    TRequest = unknown,
  >(
    path: string,
    options: ContractRequestOptions<TRequest> = {},
  ): Promise<TResponse> {
    const method = options.method ?? HttpMethod.GET;
    const parseAs =
      options.responseType === "arraybuffer"
        ? "arrayBuffer"
        : (options.responseType ?? "json");
    const request = this.contractClient.request as unknown as (
      requestMethod: string,
      schemaPath: string,
      init: Record<string, unknown>,
    ) => Promise<ContractResult<TResponse>>;

    return this.executeContractRequest(
      path,
      method,
      (_client, operationOptions) =>
        request(method.toLowerCase(), path, {
          body: options.body,
          bodySerializer: options.bodySerializer,
          headers: operationOptions.headers,
          signal: operationOptions.signal,
          parseAs,
        }),
      options,
    );
  }

  protected async executeContractRequest<TResponse, TRequest = unknown>(
    resolvedPath: string,
    method: HttpMethod,
    operation: ContractOperation<TPaths, TResponse>,
    options: ContractRequestOptions<TRequest> = {},
  ): Promise<TResponse> {
    const fullUrl = this.buildUrl(resolvedPath);
    const controller = new AbortController();
    const timeoutId = setTimeout(
      () => controller.abort(),
      options.timeout ?? this.timeout,
    );
    const abortFromCaller = () => controller.abort(options.signal?.reason);
    options.signal?.addEventListener("abort", abortFromCaller, { once: true });

    try {
      if (options.signal?.aborted) abortFromCaller();

      const requestInfo = {
        method,
        url: fullUrl,
        headers: this.buildContractHeaders(
          options.headers,
          method,
          options.body,
        ),
        data: options.body,
      };
      if (this.onRequest) {
        await this.onRequest(requestInfo);
      }
      this.log("debug", `API Request: ${method} ${fullUrl}`);

      return await this.executeContractWithRetry(
        fullUrl,
        method,
        operation,
        {
          headers: requestInfo.headers,
          signal: controller.signal,
        },
      );
    } finally {
      clearTimeout(timeoutId);
      options.signal?.removeEventListener("abort", abortFromCaller);
    }
  }

  protected buildContractHeaders(
    additionalHeaders: Record<string, string> | undefined,
    method: HttpMethod,
    body: unknown,
  ): Record<string, string> {
    const headers = this.buildHeaders(additionalHeaders);
    if (body instanceof FormData) {
      delete headers[HTTP_HEADERS.CONTENT_TYPE];
    } else if (method === HttpMethod.GET || method === HttpMethod.HEAD) {
      delete headers[HTTP_HEADERS.CONTENT_TYPE];
    } else {
      headers[HTTP_HEADERS.CONTENT_TYPE] = CONTENT_TYPES.JSON;
    }
    return headers;
  }

  protected normalizeContractError(error: unknown): unknown {
    return error;
  }

  private async executeContractWithRetry<TResponse>(
    fullUrl: string,
    method: HttpMethod,
    operation: ContractOperation<TPaths, TResponse>,
    operationOptions: ContractOperationOptions,
    attempt = 1,
  ): Promise<TResponse> {
    try {
      const result = await operation(this.contractClient, operationOptions);
      const { response } = result;
      this.log(
        "debug",
        `API Response: ${response.status} ${response.statusText}`,
      );

      const headers = Object.fromEntries(response.headers.entries());
      if (this.onResponse) {
        await this.onResponse({
          status: response.status,
          statusText: response.statusText,
          headers,
          data: result.data,
          config: {
            url: fullUrl,
            method,
            headers: operationOptions.headers,
          },
        });
      }

      if (result.error !== undefined || !response.ok) {
        throw await this.handleErrorResponse(
          recreateContractErrorResponse(result),
        );
      }

      if (
        response.status === 204 ||
        response.headers.get("content-length") === "0"
      ) {
        return undefined as TResponse;
      }
      return result.data as TResponse;
    } catch (error) {
      if (this.shouldRetry(error, attempt)) {
        const delay = this.getRetryDelay(error, attempt);
        this.log(
          "debug",
          `Retrying request (attempt ${attempt + 1}) after ${delay}ms`,
        );
        await this.sleep(delay);
        return this.executeContractWithRetry(
          fullUrl,
          method,
          operation,
          operationOptions,
          attempt + 1,
        );
      }

      const finalError = this.normalizeContractError(error);
      if (this.onError && finalError instanceof Error) {
        this.onError(finalError);
      }
      throw finalError;
    }
  }
}
