/**
 * Request for creating embeddings
 */
export interface EmbeddingRequest {
  /**
   * Input text(s) to generate embeddings for
   */
  input: string | string[];

  /**
   * ID of the model to use
   */
  model: string;

  /**
   * The format to return the embeddings in
   * Can be either "float" or "base64"
   * Default: "float"
   */
  encoding_format?: 'float' | 'base64';

  /**
   * The number of dimensions the resulting output embeddings should have
   * Only supported in text-embedding-3 and later models
   */
  dimensions?: number;

  /**
   * A unique identifier representing your end-user
   */
  user?: string;
}

/**
 * Response from creating embeddings
 */
export interface EmbeddingResponse {
  /**
   * The list of embeddings generated
   */
  data: EmbeddingData[];

  /**
   * The model used for embedding generation
   */
  model: string;

  /**
   * The object type, always "embedding"
   */
  object: 'list';

  /**
   * Usage statistics for the request
   */
  usage: EmbeddingUsage;
}

/**
 * Individual embedding data
 */
export interface EmbeddingData {
  /**
   * The embedding vector represented as an array of floats or base64 encoded string
   */
  embedding: number[] | string;

  /**
   * The index of the embedding in the list of embeddings
   */
  index: number;

  /**
   * The object type, always "embedding"
   */
  object: 'embedding';
}

/**
 * Usage statistics for embeddings
 */
export interface EmbeddingUsage {
  /**
   * The number of tokens used by the prompt
   */
  prompt_tokens: number;

  /**
   * The total number of tokens used by the request
   */
  total_tokens: number;
}

/**
 * Available embedding models
 */
export const EmbeddingModels = {
  /**
   * OpenAI text-embedding-ada-002
   * Dimensions: 1536
   */
  ADA_002: 'text-embedding-ada-002',

  /**
   * OpenAI text-embedding-3-small
   * Dimensions: 1536 (can be reduced)
   */
  EMBEDDING_3_SMALL: 'text-embedding-3-small',

  /**
   * OpenAI text-embedding-3-large
   * Dimensions: 3072 (can be reduced)
   */
  EMBEDDING_3_LARGE: 'text-embedding-3-large',

  /**
   * Default embedding model
   */
  DEFAULT: 'text-embedding-3-small',
} as const;

/**
 * Encoding format options
 */
export const EmbeddingEncodingFormats = {
  /**
   * Return embeddings as array of floats (default)
   */
  FLOAT: 'float',

  /**
   * Return embeddings as base64-encoded string
   */
  BASE64: 'base64',
} as const;

/**
 * Validates an embedding request
 */
export function validateEmbeddingRequest(request: EmbeddingRequest): void {
  if (!request.input) {
    throw new Error('Input is required');
  }

  if (typeof request.input === 'string') {
    if (!request.input.trim()) {
      throw new Error('Input text cannot be empty');
    }
  } else if (Array.isArray(request.input)) {
    if (request.input.length === 0) {
      throw new Error('At least one input text is required');
    }
    if (request.input.some(text => !text || !text.trim())) {
      throw new Error('Input texts cannot be null or empty');
    }
  } else {
    throw new Error('Input must be a string or array of strings');
  }

  if (!request.model) {
    throw new Error('Model is required');
  }

  if (request.encoding_format && 
      request.encoding_format !== EmbeddingEncodingFormats.FLOAT &&
      request.encoding_format !== EmbeddingEncodingFormats.BASE64) {
    throw new Error(`Encoding format must be '${EmbeddingEncodingFormats.FLOAT}' or '${EmbeddingEncodingFormats.BASE64}'`);
  }

  if (request.dimensions !== undefined && request.dimensions <= 0) {
    throw new Error('Dimensions must be a positive integer');
  }
}

/**
 * Converts embedding response to float array
 */
export function convertEmbeddingToFloatArray(embedding: number[] | string): number[] {
  if (Array.isArray(embedding)) {
    return embedding;
  }
  
  if (typeof embedding === 'string') {
    // Base64 encoded embedding
    const buffer = Buffer.from(embedding, 'base64');
    const floats = new Float32Array(buffer.buffer, buffer.byteOffset, buffer.length / Float32Array.BYTES_PER_ELEMENT);
    return Array.from(floats);
  }
  
  throw new Error(`Unexpected embedding type: ${typeof embedding}`);
}

/**
 * Calculates cosine similarity between two embedding vectors
 */
export function calculateCosineSimilarity(embedding1: number[], embedding2: number[]): number {
  if (embedding1.length !== embedding2.length) {
    throw new Error('Embeddings must have the same dimensions');
  }

  let dotProduct = 0;
  let magnitude1 = 0;
  let magnitude2 = 0;

  for (let i = 0; i < embedding1.length; i++) {
    dotProduct += embedding1[i] * embedding2[i];
    magnitude1 += embedding1[i] * embedding1[i];
    magnitude2 += embedding2[i] * embedding2[i];
  }

  magnitude1 = Math.sqrt(magnitude1);
  magnitude2 = Math.sqrt(magnitude2);

  if (magnitude1 === 0 || magnitude2 === 0) {
    return 0;
  }

  return dotProduct / (magnitude1 * magnitude2);
}