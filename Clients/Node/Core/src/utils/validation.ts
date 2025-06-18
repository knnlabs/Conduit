import type { ChatCompletionRequest } from '../models/chat';
import { ValidationError } from './errors';

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