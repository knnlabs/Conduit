import {
  ContractApiClient,
  DEFAULT_RETRY_STRATEGIES,
  NetworkError,
  type RetryStrategy,
} from "@/lib/conduit-common";
import {
  createVideoSignalRClient,
  disconnectVideoSignalRClient,
} from "@/lib/client/videoSignalRClient";
import type { paths } from "@/generated/gateway-api";
import {
  gatewayEphemeralKeySchema,
  gatewayFunctionExecutionSchema,
  gatewayMediaUploadSchema,
  gatewayVideoTaskSchema,
  parseCriticalResponse,
} from "@/lib/api-transport/critical-response-validation";
import {
  GATEWAY_CONTRACT_ROUTES,
  materializeContractPath,
} from "@/lib/api-transport/contract-routes";
import { createGatewayError } from "./errors";
import { parseGatewaySse } from "./streaming";
import { HttpMethod } from "@/lib/conduit-common/http";
import type {
  ChatStreamEvent,
  DiscoveryResponse,
  FunctionExecutionResponse,
  GatewayClientConfig,
  ChatAttachment,
  MediaUploadOptions,
  MediaUploadResponse,
  MessageContent,
  ModelCapability,
  RequestOptions,
  VideoGenerationResponse,
  VideoProgressCallbacks,
  VideoTaskResponse,
} from "./types";

type JsonRecord = Record<string, unknown>;

function randomRequestId(): string {
  return (
    globalThis.crypto?.randomUUID?.() ??
    `req_${Date.now()}_${Math.random().toString(36).slice(2)}`
  );
}

function normalizeVideoTask(raw: JsonRecord): VideoTaskResponse {
  const value = <T>(...keys: string[]): T | undefined => {
    for (const key of keys) if (raw[key] !== undefined) return raw[key] as T;
    return undefined;
  };
  const estimatedCompletion = value<string>("estimated_completion_time");
  return {
    task_id: value<string>("task_id") ?? "",
    status: value<string>("status") ?? "pending",
    progress: value<number>("progress") ?? 0,
    message: value<string>("message"),
    error: value<string>("error"),
    result: value<VideoGenerationResponse>("result"),
    estimated_time_to_completion: estimatedCompletion
      ? Math.max(
          0,
          Math.floor(
            (new Date(estimatedCompletion).getTime() - Date.now()) / 1000,
          ),
        )
      : (value<number>(
          "estimated_time_to_completion",
        ) ?? 60),
    created_at: value<string>("created_at") ?? new Date().toISOString(),
    updated_at: value<string>("updated_at") ?? new Date().toISOString(),
    check_status_url: value<string>("check_status_url"),
  };
}

export class GatewayClient extends ContractApiClient<paths> {
  private readonly baseURL: string;
  private readonly apiKey: string;

  constructor(config: GatewayClientConfig) {
    const baseURL = config.baseURL.replace(/\/$/, "");
    super({
      baseUrl: baseURL,
      timeout: config.timeout ?? 60_000,
      retryStrategy: config.retryStrategy ?? {
        ...DEFAULT_RETRY_STRATEGIES.gateway,
        maxRetries:
          config.retries ?? DEFAULT_RETRY_STRATEGIES.gateway.maxRetries,
      },
    });
    this.baseURL = baseURL;
    this.apiKey = config.apiKey;
  }

  protected getAuthHeaders(): Record<string, string> {
    return { Authorization: `Bearer ${this.apiKey}` };
  }

  protected getDefaultRetryStrategy(): RetryStrategy {
    return DEFAULT_RETRY_STRATEGIES.gateway;
  }

  protected override handleErrorResponse(
    response: Response,
  ): Promise<Error> {
    return createGatewayError(response);
  }

  protected override normalizeContractError(error: unknown): unknown {
    if (
      error instanceof Error &&
      (error.name === "AbortError" || "statusCode" in error)
    ) {
      return error;
    }
    return new NetworkError(
      error instanceof Error
        ? error.message
        : "Gateway network request failed",
    );
  }

  private async gatewayRequest<T>(
    path: string,
    init: RequestInit = {},
    options: RequestOptions = {},
  ): Promise<T> {
    const isForm = init.body instanceof FormData;
    const body =
      typeof init.body === "string"
        ? (JSON.parse(init.body) as unknown)
        : init.body;
    const initHeaders = Object.fromEntries(new Headers(init.headers).entries());
    return super.request<T, unknown>(path, {
      method: (init.method ?? HttpMethod.GET) as HttpMethod,
      body,
      bodySerializer: isForm ? (value) => value as BodyInit : undefined,
      headers: { ...options.headers, ...initHeaders },
      signal: options.signal,
      timeout: options.timeout,
    });
  }

  readonly auth = {
    generateEphemeralKey: async (
      virtualKey: string,
      options?: { metadata?: JsonRecord },
    ) => {
      const response = await this.gatewayRequest<{
        ephemeral_key: string;
        expires_at: string;
        expires_in_seconds: number;
      }>(GATEWAY_CONTRACT_ROUTES.ephemeralKey, {
        method: "POST",
        headers: { Authorization: `Bearer ${virtualKey}` },
        body: JSON.stringify({
          metadata: { requestId: randomRequestId(), ...options?.metadata },
        }),
      });
      return parseCriticalResponse(
        gatewayEphemeralKeySchema,
        response,
        "Gateway ephemeral-key issuance",
      );
    },
  };

  readonly discovery = {
    getModels: () => this.gatewayRequest<DiscoveryResponse>(GATEWAY_CONTRACT_ROUTES.discoveryModels),
    getModelsByCapability: (capability: ModelCapability | string) => {
      if (!capability.trim()) throw new Error("Capability is required");
      return this.gatewayRequest<DiscoveryResponse>(
        `${GATEWAY_CONTRACT_ROUTES.discoveryModels}?capability=${encodeURIComponent(capability)}`,
      );
    },
    getFunctionParameters: async (id: number) => {
      if (id < 1) throw new Error("Function configuration ID must be positive");
      const response = await this.gatewayRequest<{
        example_request?: JsonRecord;
        parameter_schema?: JsonRecord;
      }>(materializeContractPath(GATEWAY_CONTRACT_ROUTES.functionParameters, {
        functionConfigurationId: id,
      }));
      return {
        exampleRequest: response.example_request,
        parameterSchema: response.parameter_schema,
      };
    },
  };

  readonly functions = {
    execute: async (body: JsonRecord, idempotencyKey?: string) => {
      const response = await this.gatewayRequest<FunctionExecutionResponse>(GATEWAY_CONTRACT_ROUTES.executeFunction, {
        method: "POST",
        headers: idempotencyKey ? { ["Idempotency-Key"]: idempotencyKey } : undefined,
        body: JSON.stringify(body),
      });
      return parseCriticalResponse(
        gatewayFunctionExecutionSchema,
        response,
        "Gateway function execution",
      ) as FunctionExecutionResponse;
    },
  };

  readonly images = {
    generate: (body: JsonRecord) =>
      this.gatewayRequest<{ data: Array<{ url?: string; b64_json?: string }> }>(
        GATEWAY_CONTRACT_ROUTES.imageGenerations,
        { method: "POST", body: JSON.stringify(body) },
        { timeout: 300_000 },
      ),
  };

  readonly chat = {
    create: async (
      body: JsonRecord,
      options: RequestOptions = {},
    ): Promise<AsyncIterable<ChatStreamEvent>> => {
      const controller = new AbortController();
      const timeout = setTimeout(
        () => controller.abort(),
        options.timeout ?? 300_000,
      );
      const abort = () => controller.abort();
      options.signal?.addEventListener("abort", abort, { once: true });
      try {
        const response = await fetch(`${this.baseURL}${GATEWAY_CONTRACT_ROUTES.chatCompletions}`, {
          method: "POST",
          headers: {
            Authorization: `Bearer ${this.apiKey}`,
            "Content-Type": "application/json",
            ...options.headers,
          },
          body: JSON.stringify(body),
          signal: controller.signal,
        });
        if (!response.ok) throw await createGatewayError(response);
        const stream = parseGatewaySse(response);
        return (async function* () {
          try {
            yield* stream;
          } finally {
            clearTimeout(timeout);
            options.signal?.removeEventListener("abort", abort);
          }
        })();
      } catch (error) {
        clearTimeout(timeout);
        options.signal?.removeEventListener("abort", abort);
        throw error;
      }
    },
  };

  readonly videos = {
    getTaskStatus: async (taskId: string, options: RequestOptions = {}) => {
      const raw = await this.gatewayRequest<JsonRecord>(
        materializeContractPath(GATEWAY_CONTRACT_ROUTES.videoTaskStatus, { taskId }),
        {},
        options,
      );
      return normalizeVideoTask(parseCriticalResponse(
        gatewayVideoTaskSchema,
        raw,
        "Gateway video task status",
      ));
    },
    cancelTask: (taskId: string, options: RequestOptions = {}) =>
      this.gatewayRequest<void>(
        materializeContractPath(GATEWAY_CONTRACT_ROUTES.cancelVideoTask, { taskId }),
        { method: "DELETE" },
        options,
      ),
    generateWithProgress: async (
      body: JsonRecord,
      callbacks: VideoProgressCallbacks = {},
      options: RequestOptions = {},
    ): Promise<{
      taskId: string;
      result: Promise<VideoGenerationResponse>;
    }> => {
      const raw = await this.gatewayRequest<JsonRecord>(
        GATEWAY_CONTRACT_ROUTES.createVideoTask,
        {
          method: "POST",
          body: JSON.stringify({ response_format: "url", n: 1, ...body }),
        },
        options,
      );
      const initial = normalizeVideoTask(parseCriticalResponse(
        gatewayVideoTaskSchema,
        raw,
        "Gateway video task creation",
      ));
      const taskId = initial.task_id;
      callbacks.onStarted?.(taskId, initial.estimated_time_to_completion);
      const signalRClient = createVideoSignalRClient(taskId);

      const result = new Promise<VideoGenerationResponse>((resolve, reject) => {
        let settled = false;
        const finish = (callback: () => void) => {
          if (settled) return;
          settled = true;
          void disconnectVideoSignalRClient(taskId, signalRClient);
          callback();
        };

        void signalRClient
          .connect(taskId, undefined, {
            onProgress: (update) =>
              callbacks.onProgress?.({
                percentage: update.progress ?? 0,
                status: update.status,
                message: update.message,
              }),
            onCompleted: () => {
              void this.videos
                .getTaskStatus(taskId, options)
                .then((status) => {
                  const completed = status.result;
                  if (completed)
                    finish(() => {
                      callbacks.onCompleted?.(completed);
                      resolve(completed);
                    });
                })
                .catch(() => undefined);
            },
            onFailed: (message) =>
              finish(() => {
                callbacks.onFailed?.(message, false);
                reject(new Error(message));
              }),
          })
          .catch(() => {
            void disconnectVideoSignalRClient(taskId, signalRClient);
          });

        const started = Date.now();
        const poll = async (delay = 1_000): Promise<void> => {
          if (settled) return;
          if (options.signal?.aborted)
            return finish(() =>
              reject(new DOMException("Aborted", "AbortError")),
            );
          if (Date.now() - started > 600_000)
            return finish(() =>
              reject(new Error("Video generation timed out")),
            );
          try {
            const status = await this.videos.getTaskStatus(taskId, options);
            callbacks.onProgress?.({
              percentage: status.progress,
              status: status.status,
              message: status.message,
            });
            const normalized = status.status.toLowerCase();
            if (normalized === "completed" && status.result) {
              const completed = status.result;
              return finish(() => {
                callbacks.onCompleted?.(completed);
                resolve(completed);
              });
            }
            if (
              ["failed", "cancelled", "timedout", "timed_out"].includes(
                normalized,
              )
            ) {
              const message = status.error ?? `Video task ${normalized}`;
              return finish(() => {
                callbacks.onFailed?.(message, normalized.includes("timed"));
                reject(new Error(message));
              });
            }
          } catch (error) {
            if (options.signal?.aborted) {
              const rejection =
                error instanceof Error ? error : new Error(String(error));
              return finish(() => reject(rejection));
            }
          }
          setTimeout(() => void poll(Math.min(delay * 2, 10_000)), delay);
        };
        void poll();
      });

      return { taskId, result };
    },
  };

  readonly media = {
    validateFileSize: (
      file: File | Blob,
      mediaType?: string,
    ): { valid: boolean; message?: string } => {
      const limits: Record<string, number> = {
        Image: 100 * 1024 * 1024,
        Video: 500 * 1024 * 1024,
        Audio: 200 * 1024 * 1024,
      };
      let inferred = mediaType;
      if (!inferred) {
        if (file.type.startsWith("video/")) inferred = "Video";
        else if (file.type.startsWith("audio/")) inferred = "Audio";
        else inferred = "Image";
      }
      const limit = limits[inferred] ?? limits.Image;
      return file.size <= limit
        ? { valid: true }
        : {
            valid: false,
            message: `File size exceeds maximum allowed size of ${limit / 1024 / 1024}MB for ${inferred}`,
          };
    },
    upload: (file: File | Blob, options: MediaUploadOptions = {}) =>
      this.uploadMedia(file, options),
  };

  private async uploadMedia(
    file: File | Blob,
    options: MediaUploadOptions,
  ): Promise<MediaUploadResponse> {
    if (!file || file.size === 0)
      throw new Error("File is required and cannot be empty");
    const form = new FormData();
    form.append("file", file, file instanceof File ? file.name : "upload.bin");
    if (options.mediaType) form.append("mediaType", options.mediaType);

    if (typeof XMLHttpRequest === "undefined" || !options.onProgress) {
      const result = await this.gatewayRequest<MediaUploadResponse>(
        GATEWAY_CONTRACT_ROUTES.mediaUpload,
        { method: "POST", body: form },
        options,
      );
      options.onProgress?.(file.size, file.size);
      return parseCriticalResponse(
        gatewayMediaUploadSchema,
        result,
        "Gateway media upload",
      );
    }

    return new Promise<MediaUploadResponse>((resolve, reject) => {
      const xhr = new XMLHttpRequest();
      xhr.open("POST", `${this.baseURL}${GATEWAY_CONTRACT_ROUTES.mediaUpload}`);
      xhr.setRequestHeader("Authorization", `Bearer ${this.apiKey}`);
      Object.entries(options.headers ?? {}).forEach(([key, value]) =>
        xhr.setRequestHeader(key, value),
      );
      xhr.upload.onprogress = (event) =>
        options.onProgress?.(event.loaded, event.total);
      xhr.onerror = () => reject(new NetworkError("Media upload failed"));
      xhr.onabort = () => reject(new DOMException("Aborted", "AbortError"));
      xhr.onload = () => {
        if (xhr.status >= 200 && xhr.status < 300) {
          let response: unknown;
          try {
            response = JSON.parse(xhr.responseText) as unknown;
          } catch {
            reject(new Error("Invalid media upload response"));
            return;
          }
          resolve(parseCriticalResponse(
            gatewayMediaUploadSchema,
            response,
            "Gateway media upload",
          ));
        } else reject(new Error(`Media upload failed (${xhr.status})`));
      };
      options.signal?.addEventListener("abort", () => xhr.abort(), {
        once: true,
      });
      xhr.send(form);
    });
  }
}

export const ConduitGatewayClient = GatewayClient;

function inferAttachmentKind(attachment: ChatAttachment): NonNullable<ChatAttachment["kind"]> {
  if (attachment.kind) return attachment.kind;
  if (attachment.mimeType === "application/pdf") return "pdf";
  if (attachment.mimeType.startsWith("audio/")) return "audio";
  if (attachment.mimeType.startsWith("video/")) return "video";
  return "image";
}

function normalizeAudioFormat(candidate?: string): string {
  if (candidate === "wave") return "wav";
  if (candidate === "mpeg") return "mp3";
  if (candidate === "x-aiff" || candidate === "aif") return "aiff";
  return candidate ?? "wav";
}

export function buildMessageContent(
  text: string,
  attachments?: ChatAttachment[],
): MessageContent {
  if (!attachments?.length) return text;
  return [
    ...(text ? [{ type: "text" as const, text }] : []),
    ...attachments.map((attachment) => {
      const kind = inferAttachmentKind(attachment);
      const value = attachment.base64
        ? `data:${attachment.mimeType};base64,${attachment.base64}`
        : attachment.url;

      if (kind === "pdf") {
        return {
          type: "file" as const,
          file: { filename: attachment.name, file_data: value },
        };
      }
      if (kind === "audio") {
        if (!attachment.base64) {
          throw new Error("Audio attachments must contain base64 data.");
        }
        const extension = attachment.name.split(".").pop()?.toLowerCase();
        const mimeSubtype = attachment.mimeType.split("/")[1]?.toLowerCase();
        let candidate = mimeSubtype;
        if (extension !== undefined && extension !== attachment.name.toLowerCase()) {
          candidate = extension;
        }
        const format = normalizeAudioFormat(candidate);
        return {
          type: "input_audio" as const,
          input_audio: { data: attachment.base64, format },
        };
      }
      if (kind === "video") {
        return { type: "video_url" as const, video_url: { url: value } };
      }
      return {
        type: "image_url" as const,
        image_url: { url: value, detail: attachment.detail ?? "auto" },
      };
    }),
  ];
}
