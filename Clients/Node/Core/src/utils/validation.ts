import type { ChatCompletionRequest, TextContent, ImageContent } from '../models/chat';
import { ValidationError } from './errors';
import { IMAGE_MODEL_CAPABILITIES, type ImageGenerationRequest } from '../models/images';
import { ChatRoleHelpers, ImageValidationHelpers } from '../constants';

/**
 * Validates multi-modal content array
 */
function validateMultiModalContent(content: Array<TextContent | ImageContent>, messageIndex: number): void {
  if (content.length === 0) {
    throw new ValidationError(
      `Message at index ${messageIndex} has empty content array`,
      { field: 'messages' }
    );
  }

  let imageCount = 0;

  for (let j = 0; j < content.length; j++) {
    const part = content[j];
    
    if (!part || typeof part !== 'object') {
      throw new ValidationError(
        `Content part at index ${j} in message ${messageIndex} must be an object`,
        { field: 'messages' }
      );
    }

    if (!part.type) {
      throw new ValidationError(
        `Content part at index ${j} in message ${messageIndex} must have a type`,
        { field: 'messages' }
      );
    }

    if (part.type === 'text') {
      const textPart = part;
      if (typeof textPart.text !== 'string') {
        throw new ValidationError(
          `Text content at index ${j} in message ${messageIndex} must have a string 'text' property`,
          { field: 'messages' }
        );
      }
    } else if (part.type === 'image_url') {
      const imagePart = part;
      if (!imagePart.image_url || typeof imagePart.image_url !== 'object') {
        throw new ValidationError(
          `Image content at index ${j} in message ${messageIndex} must have an 'image_url' object`,
          { field: 'messages' }
        );
      }
      
      if (!imagePart.image_url.url || typeof imagePart.image_url.url !== 'string') {
        throw new ValidationError(
          `Image URL at index ${j} in message ${messageIndex} must have a string 'url' property`,
          { field: 'messages' }
        );
      }

      // Validate image URL format (either http(s) URL or base64 data URL)
      const url = imagePart.image_url.url;
      if (!url.startsWith('http://') && !url.startsWith('https://') && !url.startsWith('data:image/')) {
        throw new ValidationError(
          `Image URL at index ${j} in message ${messageIndex} must be an HTTP(S) URL or base64 data URL`,
          { field: 'messages' }
        );
      }

      // Validate detail level if provided
      if (imagePart.image_url.detail !== undefined) {
        const validDetails = ['low', 'high', 'auto'];
        if (!validDetails.includes(imagePart.image_url.detail)) {
          throw new ValidationError(
            `Image detail at index ${j} in message ${messageIndex} must be one of: ${validDetails.join(', ')}`,
            { field: 'messages' }
          );
        }
      }

      imageCount++;
    } else {
      throw new ValidationError(
        `Unknown content type '${(part as any).type}' at index ${j} in message ${messageIndex}. Must be 'text' or 'image_url'`,
        { field: 'messages' }
      );
    }
  }

  // Warn if there are many images (but don't fail validation)
  if (imageCount > 10) {
    console.warn(`Message at index ${messageIndex} contains ${imageCount} images. This may impact performance and token usage.`);
  }
}

export function validateChatCompletionRequest(request: ChatCompletionRequest): void {
  if (!request.model) {
    throw new ValidationError('Model is required', { field: 'model' });
  }

  if (!request.messages || !Array.isArray(request.messages)) {
    throw new ValidationError('Messages must be an array', { field: 'messages' });
  }

  if (request.messages.length === 0) {
    throw new ValidationError('Messages array cannot be empty', { field: 'messages' });
  }

  for (let i = 0; i < request.messages.length; i++) {
    const message = request.messages[i];
    
    if (!message.role) {
      throw new ValidationError(`Message at index ${i} must have a role`, { field: 'messages' });
    }

    if (!ChatRoleHelpers.isValidRole(message.role)) {
      throw new ValidationError(
        `Invalid role '${String(message.role)}' at index ${i}. Must be one of: ${ChatRoleHelpers.getAllRoles().join(', ')}`,
        { field: 'messages' }
      );
    }

    // Validate content
    if (message.content === null && !message.tool_calls) {
      throw new ValidationError(
        `Message at index ${i} must have content or tool_calls`,
        { field: 'messages' }
      );
    }

    // Validate multi-modal content if it's an array
    if (Array.isArray(message.content)) {
      validateMultiModalContent(message.content, i);
    }

    if (message.role === 'tool' && !message.tool_call_id) {
      throw new ValidationError(
        `Tool message at index ${i} must have tool_call_id`,
        { field: 'messages' }
      );
    }
  }

  if (request.temperature !== undefined) {
    if (request.temperature < 0 || request.temperature > 2) {
      throw new ValidationError('Temperature must be between 0 and 2', { field: 'temperature' });
    }
  }

  if (request.top_p !== undefined) {
    if (request.top_p < 0 || request.top_p > 1) {
      throw new ValidationError('top_p must be between 0 and 1', { field: 'top_p' });
    }
  }

  if (request.frequency_penalty !== undefined) {
    if (request.frequency_penalty < -2 || request.frequency_penalty > 2) {
      throw new ValidationError('frequency_penalty must be between -2 and 2', { field: 'frequency_penalty' });
    }
  }

  if (request.presence_penalty !== undefined) {
    if (request.presence_penalty < -2 || request.presence_penalty > 2) {
      throw new ValidationError('presence_penalty must be between -2 and 2', { field: 'presence_penalty' });
    }
  }

  if (request.n !== undefined && request.n < 1) {
    throw new ValidationError('n must be at least 1', { field: 'n' });
  }

  if (request.max_tokens !== undefined && request.max_tokens < 1) {
    throw new ValidationError('max_tokens must be at least 1', { field: 'max_tokens' });
  }
}

export function validateImageGenerationRequest(request: ImageGenerationRequest): void {
  if (!request.prompt) {
    throw new ValidationError('Prompt is required', { field: 'prompt' });
  }

  if (request.prompt.trim().length === 0) {
    throw new ValidationError('Prompt cannot be empty', { field: 'prompt' });
  }

  // Validate model-specific constraints
  if (request.model && IMAGE_MODEL_CAPABILITIES[request.model as keyof typeof IMAGE_MODEL_CAPABILITIES]) {
    const capabilities = IMAGE_MODEL_CAPABILITIES[request.model as keyof typeof IMAGE_MODEL_CAPABILITIES];
    
    if (request.prompt.length > capabilities.maxPromptLength) {
      throw new ValidationError(
        `Prompt exceeds maximum length of ${capabilities.maxPromptLength} characters for model ${request.model}`,
        { field: 'prompt' }
      );
    }

    if (request.n !== undefined && request.n > capabilities.maxImages) {
      throw new ValidationError(
        `Number of images (${request.n}) exceeds maximum of ${capabilities.maxImages} for model ${request.model}`,
        { field: 'n' }
      );
    }

    if (request.size && !(capabilities.supportedSizes as readonly string[]).includes(request.size)) {
      throw new ValidationError(
        `Size '${request.size}' is not supported for model ${request.model}. Supported sizes: ${capabilities.supportedSizes.join(', ')}`,
        { field: 'size' }
      );
    }

    if (request.quality && !(capabilities.supportedQualities as readonly string[]).includes(request.quality)) {
      throw new ValidationError(
        `Quality '${request.quality}' is not supported for model ${request.model}. Supported qualities: ${capabilities.supportedQualities.join(', ')}`,
        { field: 'quality' }
      );
    }

    if (request.style && capabilities.supportedStyles.length > 0 && !(capabilities.supportedStyles as readonly string[]).includes(request.style)) {
      throw new ValidationError(
        `Style '${request.style}' is not supported for model ${request.model}. Supported styles: ${capabilities.supportedStyles.join(', ')}`,
        { field: 'style' }
      );
    }
  }

  // General validations
  if (request.n !== undefined && (request.n < 1 || request.n > 10)) {
    throw new ValidationError('Number of images must be between 1 and 10', { field: 'n' });
  }

  if (request.response_format && !ImageValidationHelpers.isValidResponseFormat(request.response_format)) {
    throw new ValidationError(`response_format must be one of: ${ImageValidationHelpers.getAllResponseFormats().join(', ')}`, { field: 'response_format' });
  }

  if (request.quality && !ImageValidationHelpers.isValidQuality(request.quality)) {
    throw new ValidationError(`quality must be one of: ${ImageValidationHelpers.getAllQualities().join(', ')}`, { field: 'quality' });
  }

  if (request.style && !ImageValidationHelpers.isValidStyle(request.style)) {
    throw new ValidationError(`style must be one of: ${ImageValidationHelpers.getAllStyles().join(', ')}`, { field: 'style' });
  }

  if (request.size && !ImageValidationHelpers.isValidSize(request.size)) {
    throw new ValidationError(
      `size must be one of: ${ImageValidationHelpers.getAllSizes().join(', ')}`,
      { field: 'size' }
    );
  }
}