import { ValidationResult } from './core-route-helpers';

export function validateChatCompletionRequest(body: unknown): ValidationResult {
  if (!body || typeof body !== 'object') {
    return { isValid: false, error: 'Request body must be an object' };
  }

  const chatBody = body as Record<string, unknown>;

  if (!chatBody.messages || !Array.isArray(chatBody.messages)) {
    return { 
      isValid: false, 
      error: 'messages field is required and must be an array',
      details: { field: 'messages', received: typeof chatBody.messages }
    };
  }

  if (chatBody.messages.length === 0) {
    return { 
      isValid: false, 
      error: 'messages array cannot be empty',
      details: { field: 'messages', length: 0 }
    };
  }

  for (let i = 0; i < chatBody.messages.length; i++) {
    const message = chatBody.messages[i];
    if (!message || typeof message !== 'object') {
      return {
        isValid: false,
        error: `messages[${i}] must be an object`,
        details: { field: `messages[${i}]`, received: typeof message }
      };
    }
    
    const msg = message as Record<string, unknown>;
    if (!msg.role || typeof msg.role !== 'string') {
      return {
        isValid: false,
        error: `messages[${i}].role is required and must be a string`,
        details: { field: `messages[${i}].role`, received: typeof msg.role }
      };
    }
    
    if (!['system', 'user', 'assistant', 'function', 'tool'].includes(msg.role)) {
      return {
        isValid: false,
        error: `messages[${i}].role must be one of: system, user, assistant, function, tool`,
        details: { field: `messages[${i}].role`, received: msg.role }
      };
    }
    
    if (!msg.content || (typeof msg.content !== 'string' && !Array.isArray(msg.content))) {
      return {
        isValid: false,
        error: `messages[${i}].content is required and must be a string or array`,
        details: { field: `messages[${i}].content`, received: typeof msg.content }
      };
    }
  }

  if (chatBody.max_tokens && typeof chatBody.max_tokens !== 'number') {
    return { 
      isValid: false, 
      error: 'max_tokens must be a number',
      details: { field: 'max_tokens', received: typeof chatBody.max_tokens }
    };
  }

  if (chatBody.temperature !== undefined) {
    if (typeof chatBody.temperature !== 'number') {
      return { 
        isValid: false, 
        error: 'temperature must be a number',
        details: { field: 'temperature', received: typeof chatBody.temperature }
      };
    }
    if (chatBody.temperature < 0 || chatBody.temperature > 2) {
      return { 
        isValid: false, 
        error: 'temperature must be between 0 and 2',
        details: { field: 'temperature', received: chatBody.temperature }
      };
    }
  }

  if (chatBody.top_p !== undefined) {
    if (typeof chatBody.top_p !== 'number') {
      return { 
        isValid: false, 
        error: 'top_p must be a number',
        details: { field: 'top_p', received: typeof chatBody.top_p }
      };
    }
    if (chatBody.top_p < 0 || chatBody.top_p > 1) {
      return { 
        isValid: false, 
        error: 'top_p must be between 0 and 1',
        details: { field: 'top_p', received: chatBody.top_p }
      };
    }
  }

  if (chatBody.n !== undefined) {
    if (typeof chatBody.n !== 'number' || !Number.isInteger(chatBody.n)) {
      return { 
        isValid: false, 
        error: 'n must be an integer',
        details: { field: 'n', received: chatBody.n }
      };
    }
    if (chatBody.n < 1 || chatBody.n > 128) {
      return { 
        isValid: false, 
        error: 'n must be between 1 and 128',
        details: { field: 'n', received: chatBody.n }
      };
    }
  }

  if (chatBody.stream !== undefined && typeof chatBody.stream !== 'boolean') {
    return { 
      isValid: false, 
      error: 'stream must be a boolean',
      details: { field: 'stream', received: typeof chatBody.stream }
    };
  }

  return { isValid: true };
}

export function validateImageGenerationRequest(body: unknown): ValidationResult {
  if (!body || typeof body !== 'object') {
    return { isValid: false, error: 'Request body must be an object' };
  }

  const imageBody = body as Record<string, unknown>;

  if (!imageBody.prompt || typeof imageBody.prompt !== 'string') {
    return { 
      isValid: false, 
      error: 'prompt field is required and must be a string',
      details: { field: 'prompt', received: typeof imageBody.prompt }
    };
  }

  if (imageBody.prompt.trim().length === 0) {
    return { 
      isValid: false, 
      error: 'prompt cannot be empty',
      details: { field: 'prompt', length: imageBody.prompt.length }
    };
  }

  if (imageBody.prompt.length > 4000) {
    return { 
      isValid: false, 
      error: 'prompt must be 4000 characters or less',
      details: { field: 'prompt', length: imageBody.prompt.length }
    };
  }

  if (imageBody.n !== undefined) {
    if (typeof imageBody.n !== 'number' || !Number.isInteger(imageBody.n)) {
      return { 
        isValid: false, 
        error: 'n must be an integer',
        details: { field: 'n', received: imageBody.n }
      };
    }
    if (imageBody.n < 1 || imageBody.n > 10) {
      return { 
        isValid: false, 
        error: 'n must be between 1 and 10',
        details: { field: 'n', received: imageBody.n }
      };
    }
  }

  if (imageBody.size !== undefined) {
    if (typeof imageBody.size !== 'string') {
      return { 
        isValid: false, 
        error: 'size must be a string',
        details: { field: 'size', received: typeof imageBody.size }
      };
    }
    const validSizes = ['256x256', '512x512', '1024x1024', '1792x1024', '1024x1792'];
    if (!validSizes.includes(imageBody.size)) {
      return { 
        isValid: false, 
        error: `size must be one of: ${validSizes.join(', ')}`,
        details: { field: 'size', received: imageBody.size }
      };
    }
  }

  if (imageBody.quality !== undefined) {
    if (typeof imageBody.quality !== 'string') {
      return { 
        isValid: false, 
        error: 'quality must be a string',
        details: { field: 'quality', received: typeof imageBody.quality }
      };
    }
    if (!['standard', 'hd'].includes(imageBody.quality)) {
      return { 
        isValid: false, 
        error: 'quality must be either "standard" or "hd"',
        details: { field: 'quality', received: imageBody.quality }
      };
    }
  }

  if (imageBody.style !== undefined) {
    if (typeof imageBody.style !== 'string') {
      return { 
        isValid: false, 
        error: 'style must be a string',
        details: { field: 'style', received: typeof imageBody.style }
      };
    }
    if (!['vivid', 'natural'].includes(imageBody.style)) {
      return { 
        isValid: false, 
        error: 'style must be either "vivid" or "natural"',
        details: { field: 'style', received: imageBody.style }
      };
    }
  }

  if (imageBody.response_format !== undefined) {
    if (typeof imageBody.response_format !== 'string') {
      return { 
        isValid: false, 
        error: 'response_format must be a string',
        details: { field: 'response_format', received: typeof imageBody.response_format }
      };
    }
    if (!['url', 'b64_json'].includes(imageBody.response_format)) {
      return { 
        isValid: false, 
        error: 'response_format must be either "url" or "b64_json"',
        details: { field: 'response_format', received: imageBody.response_format }
      };
    }
  }

  return { isValid: true };
}

export async function validateAudioTranscriptionRequest(formData: FormData): Promise<ValidationResult> {
  const audioFile = formData.get('file') as File | null;
  if (!audioFile) {
    return {
      isValid: false,
      error: 'Audio file is required',
      details: { field: 'file' }
    };
  }

  const supportedFormats = ['mp3', 'mp4', 'mpeg', 'mpga', 'm4a', 'wav', 'webm'];
  const fileExtension = audioFile.name.split('.').pop()?.toLowerCase();
  
  if (!fileExtension || !supportedFormats.includes(fileExtension)) {
    const mimeTypeMap: Record<string, string> = {
      'audio/mpeg': 'mp3',
      'audio/mp3': 'mp3',
      'audio/mp4': 'mp4',
      'audio/wav': 'wav',
      'audio/webm': 'webm',
      'audio/m4a': 'm4a',
    };
    
    const mappedFormat = mimeTypeMap[audioFile.type];
    if (!mappedFormat || !supportedFormats.includes(mappedFormat)) {
      return {
        isValid: false,
        error: `Unsupported audio format. Supported formats: ${supportedFormats.join(', ')}`,
        details: { 
          field: 'file',
          providedType: audioFile.type,
          providedExtension: fileExtension
        }
      };
    }
  }

  const maxSize = 25 * 1024 * 1024; // 25MB
  if (audioFile.size > maxSize) {
    return {
      isValid: false,
      error: 'Audio file size must not exceed 25MB',
      details: { 
        field: 'file',
        maxSize: '25MB',
        providedSize: `${Math.round(audioFile.size / 1024 / 1024)}MB`
      }
    };
  }

  const model = formData.get('model') as string | null;
  if (model && !['whisper-1', 'whisper-large-v3'].includes(model)) {
    return {
      isValid: false,
      error: 'model must be either "whisper-1" or "whisper-large-v3"',
      details: { field: 'model', received: model }
    };
  }

  const temperature = formData.get('temperature');
  if (temperature !== null) {
    const tempNum = parseFloat(temperature as string);
    if (isNaN(tempNum) || tempNum < 0 || tempNum > 1) {
      return {
        isValid: false,
        error: 'temperature must be a number between 0 and 1',
        details: { field: 'temperature', received: temperature }
      };
    }
  }

  const responseFormat = formData.get('response_format') as string | null;
  if (responseFormat && !['json', 'text', 'srt', 'verbose_json', 'vtt'].includes(responseFormat)) {
    return {
      isValid: false,
      error: 'response_format must be one of: json, text, srt, verbose_json, vtt',
      details: { field: 'response_format', received: responseFormat }
    };
  }

  return { isValid: true };
}

export function validateVideoGenerationRequest(body: unknown): ValidationResult {
  if (!body || typeof body !== 'object') {
    return { isValid: false, error: 'Request body must be an object' };
  }

  const videoBody = body as Record<string, unknown>;

  if (!videoBody.prompt || typeof videoBody.prompt !== 'string') {
    return { 
      isValid: false, 
      error: 'prompt field is required and must be a string',
      details: { field: 'prompt', received: typeof videoBody.prompt }
    };
  }

  if (videoBody.prompt.trim().length === 0) {
    return { 
      isValid: false, 
      error: 'prompt cannot be empty',
      details: { field: 'prompt', length: videoBody.prompt.length }
    };
  }

  if (videoBody.prompt.length > 2000) {
    return { 
      isValid: false, 
      error: 'prompt must be 2000 characters or less',
      details: { field: 'prompt', length: videoBody.prompt.length }
    };
  }

  if (videoBody.model !== undefined && typeof videoBody.model !== 'string') {
    return { 
      isValid: false, 
      error: 'model must be a string',
      details: { field: 'model', received: typeof videoBody.model }
    };
  }

  if (videoBody.duration !== undefined) {
    if (typeof videoBody.duration !== 'number' || !Number.isInteger(videoBody.duration)) {
      return { 
        isValid: false, 
        error: 'duration must be an integer',
        details: { field: 'duration', received: videoBody.duration }
      };
    }
    if (videoBody.duration < 1 || videoBody.duration > 60) {
      return { 
        isValid: false, 
        error: 'duration must be between 1 and 60 seconds',
        details: { field: 'duration', received: videoBody.duration }
      };
    }
  }

  if (videoBody.resolution !== undefined) {
    if (typeof videoBody.resolution !== 'string') {
      return { 
        isValid: false, 
        error: 'resolution must be a string',
        details: { field: 'resolution', received: typeof videoBody.resolution }
      };
    }
    const validResolutions = ['1080p', '720p', '480p', '360p'];
    if (!validResolutions.includes(videoBody.resolution)) {
      return { 
        isValid: false, 
        error: `resolution must be one of: ${validResolutions.join(', ')}`,
        details: { field: 'resolution', received: videoBody.resolution }
      };
    }
  }

  if (videoBody.fps !== undefined) {
    if (typeof videoBody.fps !== 'number' || !Number.isInteger(videoBody.fps)) {
      return { 
        isValid: false, 
        error: 'fps must be an integer',
        details: { field: 'fps', received: videoBody.fps }
      };
    }
    const validFps = [24, 25, 30, 60];
    if (!validFps.includes(videoBody.fps)) {
      return { 
        isValid: false, 
        error: `fps must be one of: ${validFps.join(', ')}`,
        details: { field: 'fps', received: videoBody.fps }
      };
    }
  }

  return { isValid: true };
}