import type { BaseClient } from '../client/BaseClient';
import type { RequestOptions } from '../client/types';
import type { 
  ImageGenerationRequest, 
  ImageGenerationResponse,
  ImageEditRequest,
  ImageEditResponse,
  ImageVariationRequest,
  ImageVariationResponse
} from '../models/images';
import { validateImageGenerationRequest } from '../utils/validation';

export class ImagesService {
  constructor(private readonly client: BaseClient) {}

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
        method: 'POST',
        url: '/v1/images/generations',
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
        method: 'POST',
        url: '/v1/images/edits',
        data: formData,
        headers: {
          'Content-Type': 'multipart/form-data',
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
        method: 'POST',
        url: '/v1/images/variations',
        data: formData,
        headers: {
          'Content-Type': 'multipart/form-data',
        },
      },
      options
    );
  }
}