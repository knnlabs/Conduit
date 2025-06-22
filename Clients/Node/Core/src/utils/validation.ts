import type { ChatCompletionRequest } from '../models/chat';
import type { ImageGenerationRequest } from '../models/images';
import { ValidationError } from './errors';
import { IMAGE_MODEL_CAPABILITIES } from '../models/images';

export function validateChatCompletionRequest(request: ChatCompletionRequest): void {
  if (!request.model) {
    throw new ValidationError('Model is required', 'model');
  }

  if (!request.messages || !Array.isArray(request.messages)) {
    throw new ValidationError('Messages must be an array', 'messages');
  }

  if (request.messages.length === 0) {
    throw new ValidationError('Messages array cannot be empty', 'messages');
  }

  for (let i = 0; i < request.messages.length; i++) {
    const message = request.messages[i];
    
    if (!message.role) {
      throw new ValidationError(`Message at index ${i} must have a role`, 'messages');
    }

    const validRoles = ['system', 'user', 'assistant', 'tool'];
    if (!validRoles.includes(message.role)) {
      throw new ValidationError(
        `Invalid role '${message.role}' at index ${i}. Must be one of: ${validRoles.join(', ')}`,
        'messages'
      );
    }

    if (message.content === null && !message.tool_calls) {
      throw new ValidationError(
        `Message at index ${i} must have content or tool_calls`,
        'messages'
      );
    }

    if (message.role === 'tool' && !message.tool_call_id) {
      throw new ValidationError(
        `Tool message at index ${i} must have tool_call_id`,
        'messages'
      );
    }
  }

  if (request.temperature !== undefined) {
    if (request.temperature < 0 || request.temperature > 2) {
      throw new ValidationError('Temperature must be between 0 and 2', 'temperature');
    }
  }

  if (request.top_p !== undefined) {
    if (request.top_p < 0 || request.top_p > 1) {
      throw new ValidationError('top_p must be between 0 and 1', 'top_p');
    }
  }

  if (request.frequency_penalty !== undefined) {
    if (request.frequency_penalty < -2 || request.frequency_penalty > 2) {
      throw new ValidationError('frequency_penalty must be between -2 and 2', 'frequency_penalty');
    }
  }

  if (request.presence_penalty !== undefined) {
    if (request.presence_penalty < -2 || request.presence_penalty > 2) {
      throw new ValidationError('presence_penalty must be between -2 and 2', 'presence_penalty');
    }
  }

  if (request.n !== undefined && request.n < 1) {
    throw new ValidationError('n must be at least 1', 'n');
  }

  if (request.max_tokens !== undefined && request.max_tokens < 1) {
    throw new ValidationError('max_tokens must be at least 1', 'max_tokens');
  }
}

export function validateImageGenerationRequest(request: ImageGenerationRequest): void {
  if (!request.prompt) {
    throw new ValidationError('Prompt is required', 'prompt');
  }

  if (request.prompt.trim().length === 0) {
    throw new ValidationError('Prompt cannot be empty', 'prompt');
  }

  // Validate model-specific constraints
  if (request.model && IMAGE_MODEL_CAPABILITIES[request.model as keyof typeof IMAGE_MODEL_CAPABILITIES]) {
    const capabilities = IMAGE_MODEL_CAPABILITIES[request.model as keyof typeof IMAGE_MODEL_CAPABILITIES];
    
    if (request.prompt.length > capabilities.maxPromptLength) {
      throw new ValidationError(
        `Prompt exceeds maximum length of ${capabilities.maxPromptLength} characters for model ${request.model}`,
        'prompt'
      );
    }

    if (request.n !== undefined && request.n > capabilities.maxImages) {
      throw new ValidationError(
        `Number of images (${request.n}) exceeds maximum of ${capabilities.maxImages} for model ${request.model}`,
        'n'
      );
    }

    if (request.size && !capabilities.supportedSizes.includes(request.size as any)) {
      throw new ValidationError(
        `Size '${request.size}' is not supported for model ${request.model}. Supported sizes: ${capabilities.supportedSizes.join(', ')}`,
        'size'
      );
    }

    if (request.quality && !capabilities.supportedQualities.includes(request.quality as any)) {
      throw new ValidationError(
        `Quality '${request.quality}' is not supported for model ${request.model}. Supported qualities: ${capabilities.supportedQualities.join(', ')}`,
        'quality'
      );
    }

    if (request.style && capabilities.supportedStyles.length > 0 && !capabilities.supportedStyles.includes(request.style as any)) {
      throw new ValidationError(
        `Style '${request.style}' is not supported for model ${request.model}. Supported styles: ${capabilities.supportedStyles.join(', ')}`,
        'style'
      );
    }
  }

  // General validations
  if (request.n !== undefined && (request.n < 1 || request.n > 10)) {
    throw new ValidationError('Number of images must be between 1 and 10', 'n');
  }

  if (request.response_format && !['url', 'b64_json'].includes(request.response_format)) {
    throw new ValidationError('response_format must be either "url" or "b64_json"', 'response_format');
  }

  if (request.quality && !['standard', 'hd'].includes(request.quality)) {
    throw new ValidationError('quality must be either "standard" or "hd"', 'quality');
  }

  if (request.style && !['vivid', 'natural'].includes(request.style)) {
    throw new ValidationError('style must be either "vivid" or "natural"', 'style');
  }

  const validSizes = ['256x256', '512x512', '1024x1024', '1792x1024', '1024x1792'];
  if (request.size && !validSizes.includes(request.size)) {
    throw new ValidationError(
      `size must be one of: ${validSizes.join(', ')}`,
      'size'
    );
  }
}