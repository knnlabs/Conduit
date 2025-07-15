import { FetchBasedClient } from '../client/FetchBasedClient';
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
import { API_ENDPOINTS, CONTENT_TYPES } from '../constants';

/**
 * Service for image generation, editing, and variation operations.
 * Provides OpenAI-compatible image API endpoints for DALL-E and other image models.
 * 
 * @example
 * ```typescript
 * // Initialize the service
 * const images = client.images;
 * 
 * // Generate an image
 * const result = await images.generate({
 *   prompt: 'A sunset over mountains',
 *   size: '1024x1024',
 *   quality: 'hd'
 * });
 * 
 * // Edit an image
 * const edited = await images.edit({
 *   image: imageFile,
 *   prompt: 'Add a rainbow to the sky',
 *   mask: maskFile
 * });
 * ```
 */
export class ImagesService extends FetchBasedClient {
  constructor(client: FetchBasedClient) {
    // Access the protected config property through inheritance
    // @ts-expect-error Accessing protected property from another instance
    super(client.config);
  }

  /**
   * Creates an image given a text prompt.
   * Supports various sizes, styles, and quality settings based on the model.
   * 
   * @param request - The image generation request
   * @param options - Optional request options
   * @returns Promise resolving to image generation response
   * 
   * @example
   * ```typescript
   * // Basic image generation
   * const result = await images.generate({
   *   prompt: 'A serene lake at sunset',
   *   n: 1
   * });
   * console.log(result.data[0].url);
   * 
   * // High quality with specific size
   * const hdResult = await images.generate({
   *   prompt: 'A futuristic city skyline',
   *   model: 'dall-e-3',
   *   size: '1792x1024',
   *   quality: 'hd',
   *   style: 'vivid'
   * });
   * 
   * // Get base64 encoded image
   * const base64Result = await images.generate({
   *   prompt: 'Abstract art',
   *   response_format: 'b64_json'
   * });
   * ```
   */
  async generate(
    request: ImageGenerationRequest,
    options?: RequestOptions
  ): Promise<ImageGenerationResponse> {
    validateImageGenerationRequest(request);

    return this.post<ImageGenerationResponse, ImageGenerationRequest>(
      API_ENDPOINTS.V1.IMAGES.GENERATIONS,
      request,
      options
    );
  }

  /**
   * Creates an edited or extended image given an original image and a prompt.
   * The mask specifies which areas should be edited. Transparent areas in the mask indicate where edits should be applied.
   * 
   * @param request - The image edit request
   * @param options - Optional request options
   * @returns Promise resolving to image edit response
   * 
   * @example
   * ```typescript
   * // Edit with a mask
   * const edited = await images.edit({
   *   image: originalImageFile,
   *   mask: maskFile,
   *   prompt: 'Replace the sky with a starry night',
   *   n: 1
   * });
   * 
   * // Edit using image transparency as mask
   * const result = await images.edit({
   *   image: transparentPngFile,
   *   prompt: 'Add a garden in the transparent area',
   *   size: '512x512'
   * });
   * ```
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

    return this.post<ImageEditResponse, FormData>(
      API_ENDPOINTS.V1.IMAGES.EDITS,
      formData,
      {
        ...options,
        headers: {
          ...options?.headers,
          'Content-Type': CONTENT_TYPES.FORM_DATA,
        },
      }
    );
  }

  /**
   * Creates a variation of a given image.
   * Generates new images that maintain the same general composition but with variations.
   * 
   * @param request - The image variation request
   * @param options - Optional request options
   * @returns Promise resolving to image variation response
   * 
   * @example
   * ```typescript
   * // Create variations
   * const variations = await images.createVariation({
   *   image: originalImageFile,
   *   n: 3,
   *   size: '1024x1024'
   * });
   * 
   * // Get variations as base64
   * const base64Variations = await images.createVariation({
   *   image: imageFile,
   *   n: 2,
   *   response_format: 'b64_json'
   * });
   * ```
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

    return this.post<ImageVariationResponse, FormData>(
      API_ENDPOINTS.V1.IMAGES.VARIATIONS,
      formData,
      {
        ...options,
        headers: {
          ...options?.headers,
          'Content-Type': CONTENT_TYPES.FORM_DATA,
        },
      }
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

    return this.post<AsyncImageGenerationResponse, AsyncImageGenerationRequest>(
      API_ENDPOINTS.V1.IMAGES.ASYNC_GENERATIONS,
      request,
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

    return this.get<AsyncImageGenerationResponse>(
      API_ENDPOINTS.V1.IMAGES.TASK_STATUS(taskId),
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

    await this.delete<void>(
      API_ENDPOINTS.V1.IMAGES.CANCEL_TASK(taskId),
      options
    );
  }

  /**
   * Polls an async image generation task until completion or timeout.
   * Automatically handles retries with configurable intervals and backoff.
   * 
   * @param taskId - The task identifier
   * @param pollingOptions - Polling configuration options
   * @param requestOptions - Optional request options
   * @returns Promise resolving to the final generation result
   * 
   * @example
   * ```typescript
   * // Start async generation
   * const task = await images.generateAsync({
   *   prompt: 'Complex artistic scene',
   *   quality: 'hd',
   *   size: '1792x1024'
   * });
   * 
   * // Poll until complete with default settings
   * const result = await images.pollTaskUntilCompletion(task.task_id);
   * 
   * // Custom polling configuration
   * const customResult = await images.pollTaskUntilCompletion(
   *   task.task_id,
   *   {
   *     intervalMs: 2000,
   *     timeoutMs: 300000, // 5 minutes
   *     useExponentialBackoff: true
   *   }
   * );
   * ```
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

    for (;;) {
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
          throw new Error(`Unknown task status: ${String(status.status)}`);
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