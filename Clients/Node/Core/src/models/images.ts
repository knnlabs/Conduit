/**
 * Image generation models and interfaces for OpenAI-compatible API
 */

export interface ImageGenerationRequest {
  /**
   * A text description of the desired image(s). The maximum length is 1000 characters for dall-e-2 and 4000 characters for dall-e-3.
   */
  prompt: string;

  /**
   * The model to use for image generation.
   */
  model?: string;

  /**
   * The number of images to generate. Must be between 1 and 10. For dall-e-3, only n=1 is supported.
   */
  n?: number;

  /**
   * The quality of the image that will be generated. hd creates images with finer details and greater consistency across the image. This param is only supported for dall-e-3.
   */
  quality?: 'standard' | 'hd';

  /**
   * The format in which the generated images are returned. Must be one of url or b64_json.
   */
  response_format?: 'url' | 'b64_json';

  /**
   * The size of the generated images. Must be one of 256x256, 512x512, or 1024x1024 for dall-e-2. Must be one of 1024x1024, 1792x1024, or 1024x1792 for dall-e-3 models.
   */
  size?: '256x256' | '512x512' | '1024x1024' | '1792x1024' | '1024x1792';

  /**
   * The style of the generated images. Must be one of vivid or natural. Vivid causes the model to lean towards generating hyper-real and dramatic images. Natural causes the model to produce more natural, less hyper-real looking images. This param is only supported for dall-e-3.
   */
  style?: 'vivid' | 'natural';

  /**
   * A unique identifier representing your end-user, which can help OpenAI to monitor and detect abuse. Learn more.
   */
  user?: string;
}

export interface ImageData {
  /**
   * The base64-encoded JSON of the generated image, if response_format is b64_json.
   */
  b64_json?: string;

  /**
   * The URL of the generated image, if response_format is url (default).
   */
  url?: string;

  /**
   * The prompt that was used to generate the image, if there was any revision to the prompt.
   */
  revised_prompt?: string;
}

export interface ImageGenerationResponse {
  /**
   * The Unix timestamp (in seconds) when the image was created.
   */
  created: number;

  /**
   * The list of generated images.
   */
  data: ImageData[];
}

export interface ImageEditRequest {
  /**
   * The image to edit. Must be a valid PNG file, less than 4MB, and square. If mask is not provided, image must have transparency, which will be used as the mask.
   */
  image: File | Blob;

  /**
   * A text description of the desired image(s). The maximum length is 1000 characters.
   */
  prompt: string;

  /**
   * An additional image whose fully transparent areas (e.g. where alpha is zero) indicate where image should be edited. Must be a valid PNG file, less than 4MB, and have the same dimensions as image.
   */
  mask?: File | Blob;

  /**
   * The model to use for image editing. Only dall-e-2 is supported at this time.
   */
  model?: string;

  /**
   * The number of images to generate. Must be between 1 and 10.
   */
  n?: number;

  /**
   * The format in which the generated images are returned. Must be one of url or b64_json.
   */
  response_format?: 'url' | 'b64_json';

  /**
   * The size of the generated images. Must be one of 256x256, 512x512, or 1024x1024.
   */
  size?: '256x256' | '512x512' | '1024x1024';

  /**
   * A unique identifier representing your end-user, which can help OpenAI to monitor and detect abuse.
   */
  user?: string;
}

export interface ImageVariationRequest {
  /**
   * The image to use as the basis for the variation(s). Must be a valid PNG file, less than 4MB, and square.
   */
  image: File | Blob;

  /**
   * The model to use for image variation. Only dall-e-2 is supported at this time.
   */
  model?: string;

  /**
   * The number of images to generate. Must be between 1 and 10.
   */
  n?: number;

  /**
   * The format in which the generated images are returned. Must be one of url or b64_json.
   */
  response_format?: 'url' | 'b64_json';

  /**
   * The size of the generated images. Must be one of 256x256, 512x512, or 1024x1024.
   */
  size?: '256x256' | '512x512' | '1024x1024';

  /**
   * A unique identifier representing your end-user, which can help OpenAI to monitor and detect abuse.
   */
  user?: string;
}

export type ImageEditResponse = ImageGenerationResponse;
export type ImageVariationResponse = ImageGenerationResponse;

/**
 * Supported image generation models
 */
export const IMAGE_MODELS = {
  DALL_E_2: 'dall-e-2',
  DALL_E_3: 'dall-e-3',
  MINIMAX_IMAGE: 'minimax-image',
} as const;

export type ImageModel = typeof IMAGE_MODELS[keyof typeof IMAGE_MODELS];

/**
 * Model-specific capabilities and constraints
 */
export const IMAGE_MODEL_CAPABILITIES = {
  [IMAGE_MODELS.DALL_E_2]: {
    maxPromptLength: 1000,
    supportedSizes: ['256x256', '512x512', '1024x1024'] as const,
    supportedQualities: ['standard'] as const,
    supportedStyles: [] as const,
    maxImages: 10,
    supportsEdit: true,
    supportsVariation: true,
  },
  [IMAGE_MODELS.DALL_E_3]: {
    maxPromptLength: 4000,
    supportedSizes: ['1024x1024', '1792x1024', '1024x1792'] as const,
    supportedQualities: ['standard', 'hd'] as const,
    supportedStyles: ['vivid', 'natural'] as const,
    maxImages: 1,
    supportsEdit: false,
    supportsVariation: false,
  },
  [IMAGE_MODELS.MINIMAX_IMAGE]: {
    maxPromptLength: 2000,
    supportedSizes: ['1024x1024', '1792x1024', '1024x1792'] as const,
    supportedQualities: ['standard', 'hd'] as const,
    supportedStyles: ['vivid', 'natural'] as const,
    maxImages: 4,
    supportsEdit: false,
    supportsVariation: false,
  },
} as const;

/**
 * Default values for image generation requests
 */
export const IMAGE_DEFAULTS = {
  model: IMAGE_MODELS.DALL_E_3,
  n: 1,
  quality: 'standard' as const,
  response_format: 'url' as const,
  size: '1024x1024' as const,
  style: 'vivid' as const,
} as const;

/**
 * Async image generation request extending the basic generation request
 */
export interface AsyncImageGenerationRequest extends ImageGenerationRequest {
  /**
   * The webhook URL to receive the result when generation is complete
   */
  webhook_url?: string;

  /**
   * Additional metadata to include with the webhook callback
   */
  webhook_metadata?: Record<string, unknown>;

  /**
   * The timeout for the generation task in seconds (1-3600)
   */
  timeout_seconds?: number;
}

/**
 * Task status for async operations
 */
export type TaskStatus = 
  | 'pending' 
  | 'running' 
  | 'completed' 
  | 'failed' 
  | 'cancelled' 
  | 'timedout';

/**
 * Response from an async image generation request
 */
export interface AsyncImageGenerationResponse {
  /**
   * The unique task identifier
   */
  task_id: string;

  /**
   * The current status of the task
   */
  status: TaskStatus;

  /**
   * The progress percentage (0-100)
   */
  progress: number;

  /**
   * An optional progress message
   */
  message?: string;

  /**
   * The estimated time to completion in seconds
   */
  estimated_time_to_completion?: number;

  /**
   * When the task was created
   */
  created_at: string;

  /**
   * When the task was last updated
   */
  updated_at: string;

  /**
   * The generation result, available when status is 'completed'
   */
  result?: ImageGenerationResponse;

  /**
   * Error information if the task failed
   */
  error?: string;
}

/**
 * Options for polling task status
 */
export interface TaskPollingOptions {
  /**
   * The polling interval in milliseconds (default: 1000)
   */
  intervalMs?: number;

  /**
   * The maximum polling timeout in milliseconds (default: 300000 - 5 minutes)
   */
  timeoutMs?: number;

  /**
   * Whether to use exponential backoff for polling intervals (default: true)
   */
  useExponentialBackoff?: boolean;

  /**
   * The maximum interval between polls in milliseconds when using exponential backoff (default: 10000)
   */
  maxIntervalMs?: number;
}

/**
 * Default polling options
 */
export const DEFAULT_POLLING_OPTIONS: Required<TaskPollingOptions> = {
  intervalMs: 1000,
  timeoutMs: 300000, // 5 minutes
  useExponentialBackoff: true,
  maxIntervalMs: 10000, // 10 seconds
} as const;

/**
 * Image generation streaming chunk
 */
export interface ImageGenerationChunk {
  id: string;
  object: 'image.generation.chunk';
  created: number;
  data: Partial<ImageData>[];
  progress?: number;
  message?: string;
}