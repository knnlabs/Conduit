import type { FetchBasedClient } from '../client/FetchBasedClient';
import type { RequestOptions } from '../client/types';
import type { 
  ImageGenerationRequest, 
  ImageGenerationResponse,
  ImageEditRequest,
  ImageEditResponse,
  ImageVariationRequest,
  ImageVariationResponse,
  AsyncImageGenerationRequest,
  AsyncImageGenerationResponse,
  TaskPollingOptions
} from '../models/images';
import { DEFAULT_POLLING_OPTIONS } from '../models/images';
import { validateImageGenerationRequest } from '../utils/validation';
import { API_ENDPOINTS, HTTP_METHODS, CONTENT_TYPES } from '../constants';

export class ImagesService {
  constructor(private readonly client: FetchBasedClient) {}

  /**
   * Creates an image given a text prompt.
   * @param request The image generation request
   * @param options Optional request options
   * @returns Promise resolving to image generation response
   */
  async generate(
    request: ImageGenerationRequest,
    options?: RequestOptions
  ): Promise<ImageGenerationResponse> {
    validateImageGenerationRequest(request);

    return this.client['request']<ImageGenerationResponse>(
      {
        method: HTTP_METHODS.POST,
        url: API_ENDPOINTS.V1.IMAGES.GENERATIONS,
        data: request,
      },
      options
    );
  }

  /**
   * Creates an edited or extended image given an original image and a prompt.
   * @param request The image edit request
   * @param options Optional request options
   * @returns Promise resolving to image edit response
   */
  async edit(
    request: ImageEditRequest,
    options?: RequestOptions
  ): Promise<ImageEditResponse> {
    const formData = new FormData();
    formData.append('image', request.image);
    formData.append('prompt', request.prompt);
    
    if (request.mask) {
      formData.append('mask', request.mask);
    }
    if (request.model) {
      formData.append('model', request.model);
    }
    if (request.n !== undefined) {
      formData.append('n', request.n.toString());
    }
    if (request.response_format) {
      formData.append('response_format', request.response_format);
    }
    if (request.size) {
      formData.append('size', request.size);
    }
    if (request.user) {
      formData.append('user', request.user);
    }

    return this.client['request']<ImageEditResponse>(
      {
        method: HTTP_METHODS.POST,
        url: API_ENDPOINTS.V1.IMAGES.EDITS,
        data: formData,
        headers: {
          'Content-Type': CONTENT_TYPES.FORM_DATA,
        },
      },
      options
    );
  }

  /**
   * Creates a variation of a given image.
   * @param request The image variation request
   * @param options Optional request options
   * @returns Promise resolving to image variation response
   */
  async createVariation(
    request: ImageVariationRequest,
    options?: RequestOptions
  ): Promise<ImageVariationResponse> {
    const formData = new FormData();
    formData.append('image', request.image);
    
    if (request.model) {
      formData.append('model', request.model);
    }
    if (request.n !== undefined) {
      formData.append('n', request.n.toString());
    }
    if (request.response_format) {
      formData.append('response_format', request.response_format);
    }
    if (request.size) {
      formData.append('size', request.size);
    }
    if (request.user) {
      formData.append('user', request.user);
    }

    return this.client['request']<ImageVariationResponse>(
      {
        method: HTTP_METHODS.POST,
        url: API_ENDPOINTS.V1.IMAGES.VARIATIONS,
        data: formData,
        headers: {
          'Content-Type': CONTENT_TYPES.FORM_DATA,
        },
      },
      options
    );
  }

  /**
   * Creates an image asynchronously given a text prompt.
   * @param request The async image generation request
   * @param options Optional request options
   * @returns Promise resolving to async task information
   */
  async generateAsync(
    request: AsyncImageGenerationRequest,
    options?: RequestOptions
  ): Promise<AsyncImageGenerationResponse> {
    validateImageGenerationRequest(request);

    // Validate async-specific fields
    if (request.timeout_seconds !== undefined && 
        (request.timeout_seconds < 1 || request.timeout_seconds > 3600)) {
      throw new Error('Timeout must be between 1 and 3600 seconds');
    }

    if (request.webhook_url) {
      try {
        const url = new URL(request.webhook_url);
        if (!['http:', 'https:'].includes(url.protocol)) {
          throw new Error('WebhookUrl must be a valid HTTP or HTTPS URL');
        }
      } catch {
        throw new Error('WebhookUrl must be a valid HTTP or HTTPS URL');
      }
    }

    return this.client['request']<AsyncImageGenerationResponse>(
      {
        method: HTTP_METHODS.POST,
        url: API_ENDPOINTS.V1.IMAGES.ASYNC_GENERATIONS,
        data: request,
      },
      options
    );
  }

  /**
   * Gets the status of an async image generation task.
   * @param taskId The task identifier
   * @param options Optional request options
   * @returns Promise resolving to the current task status
   */
  async getTaskStatus(
    taskId: string,
    options?: RequestOptions
  ): Promise<AsyncImageGenerationResponse> {
    if (!taskId?.trim()) {
      throw new Error('Task ID is required');
    }

    return this.client['request']<AsyncImageGenerationResponse>(
      {
        method: HTTP_METHODS.GET,
        url: API_ENDPOINTS.V1.IMAGES.TASK_STATUS(taskId),
      },
      options
    );
  }

  /**
   * Cancels a pending or running async image generation task.
   * @param taskId The task identifier
   * @param options Optional request options
   */
  async cancelTask(
    taskId: string,
    options?: RequestOptions
  ): Promise<void> {
    if (!taskId?.trim()) {
      throw new Error('Task ID is required');
    }

    await this.client['request']<void>(
      {
        method: HTTP_METHODS.DELETE,
        url: API_ENDPOINTS.V1.IMAGES.CANCEL_TASK(taskId),
      },
      options
    );
  }

  /**
   * Polls an async image generation task until completion or timeout.
   * @param taskId The task identifier
   * @param pollingOptions Polling configuration options
   * @param requestOptions Optional request options
   * @returns Promise resolving to the final generation result
   */
  async pollTaskUntilCompletion(
    taskId: string,
    pollingOptions?: TaskPollingOptions,
    requestOptions?: RequestOptions
  ): Promise<ImageGenerationResponse> {
    if (!taskId?.trim()) {
      throw new Error('Task ID is required');
    }

    const options = { ...DEFAULT_POLLING_OPTIONS, ...pollingOptions };
    const startTime = Date.now();
    let currentInterval = options.intervalMs;

    while (true) {
      // Check timeout
      if (Date.now() - startTime > options.timeoutMs) {
        throw new Error(`Task polling timed out after ${options.timeoutMs}ms`);
      }

      const status = await this.getTaskStatus(taskId, requestOptions);

      switch (status.status) {
        case 'completed':
          if (!status.result) {
            throw new Error('Task completed but no result was provided');
          }
          return status.result;

        case 'failed':
          throw new Error(`Task failed: ${status.error ?? 'Unknown error'}`);

        case 'cancelled':
          throw new Error('Task was cancelled');

        case 'timedout':
          throw new Error('Task timed out');

        case 'pending':
        case 'running':
          // Continue polling
          break;

        default:
          throw new Error(`Unknown task status: ${status.status}`);
      }

      // Wait before next poll
      await new Promise(resolve => setTimeout(resolve, currentInterval));

      // Apply exponential backoff if enabled
      if (options.useExponentialBackoff) {
        currentInterval = Math.min(currentInterval * 2, options.maxIntervalMs);
      }
    }
  }
}