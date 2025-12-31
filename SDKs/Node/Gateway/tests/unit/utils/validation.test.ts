import { validateChatCompletionRequest } from '../../../src/utils/validation';
import { ValidationError } from '../../../src/utils/errors';
import { ChatCompletionRequest } from '../../../src/models/chat';

describe('validateChatCompletionRequest', () => {
  const validRequest: ChatCompletionRequest = {
    model: 'gpt-4',
    messages: [
      { role: 'user', content: 'Hello' },
    ],
  };

  it('should accept valid request', () => {
    expect(() => validateChatCompletionRequest(validRequest)).not.toThrow();
  });

  it('should reject request without model', () => {
    const request = { ...validRequest, model: '' };
    expect(() => validateChatCompletionRequest(request))
      .toThrow(new ValidationError('Model is required', 'model'));
  });

  it('should reject request without messages', () => {
    const request = { ...validRequest, messages: undefined as any };
    expect(() => validateChatCompletionRequest(request))
      .toThrow(new ValidationError('Messages must be an array', 'messages'));
  });

  it('should reject empty messages array', () => {
    const request = { ...validRequest, messages: [] };
    expect(() => validateChatCompletionRequest(request))
      .toThrow(new ValidationError('Messages array cannot be empty', 'messages'));
  });

  it('should reject invalid role', () => {
    const request = {
      ...validRequest,
      messages: [{ role: 'invalid' as any, content: 'test' }],
    };
    expect(() => validateChatCompletionRequest(request))
      .toThrow(/Invalid role/);
  });

  it('should reject message without content or tool_calls', () => {
    const request = {
      ...validRequest,
      messages: [{ role: 'user', content: null }],
    };
    expect(() => validateChatCompletionRequest(request))
      .toThrow(/must have content or tool_calls/);
  });

  it('should reject tool message without tool_call_id', () => {
    const request = {
      ...validRequest,
      messages: [{ role: 'tool', content: 'result' }],
    };
    expect(() => validateChatCompletionRequest(request))
      .toThrow(/must have tool_call_id/);
  });

  it('should reject invalid temperature', () => {
    const request = { ...validRequest, temperature: 3 };
    expect(() => validateChatCompletionRequest(request))
      .toThrow(new ValidationError('Temperature must be between 0 and 2', 'temperature'));
  });

  it('should reject invalid top_p', () => {
    const request = { ...validRequest, top_p: 1.5 };
    expect(() => validateChatCompletionRequest(request))
      .toThrow(new ValidationError('top_p must be between 0 and 1', 'top_p'));
  });

  it('should accept valid tool message', () => {
    const request = {
      ...validRequest,
      messages: [
        { role: 'tool', content: 'result', tool_call_id: 'call_123' },
      ],
    };
    expect(() => validateChatCompletionRequest(request)).not.toThrow();
  });
});