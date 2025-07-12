import type { FetchBasedClient } from '../client/FetchBasedClient';
import { HttpMethod } from '../client/HttpMethod';
import type {
  EmbeddingRequest,
  EmbeddingResponse
} from '../models/embeddings';
import {
  EmbeddingModels,
  validateEmbeddingRequest,
  convertEmbeddingToFloatArray,
  calculateCosineSimilarity
} from '../models/embeddings';
import { ConduitError } from '../utils/errors';
import { API_ENDPOINTS } from '../constants/endpoints';

/**
 * Service for creating text embeddings using the Conduit Core API
 */
export class EmbeddingsService {
  constructor(private readonly client: FetchBasedClient) {}

  /**
   * Creates embeddings for the given input text(s)
   * 
   * @param request - The embedding request
   * @param options - Request options
   * @returns Promise<EmbeddingResponse> The embedding response containing the generated embeddings
   * @throws {ConduitError} When the API request fails or validation fails
   * 
   * @example
   * ```typescript
   * const response = await client.embeddings.createEmbedding({
   *   input: "Hello, world!",
   *   model: "text-embedding-3-small"
   * });
   * 
   * console.log(`Generated ${response.data.length} embeddings`);
   * console.log(`Used ${response.usage.total_tokens} tokens`);
   * ```
   */
  async createEmbedding(
    request: EmbeddingRequest,
    options?: { signal?: AbortSignal }
  ): Promise<EmbeddingResponse> {
    try {
      validateEmbeddingRequest(request);

      const response = await this.client['request']<EmbeddingResponse>(
        API_ENDPOINTS.V1.EMBEDDINGS.BASE,
        {
          method: HttpMethod.POST,
          body: request,
          ...options
        }
      );

      return response;
    } catch (error) {
      if (error instanceof ConduitError) {
        throw error;
      }
      throw new ConduitError(
        `Embedding creation failed: ${error instanceof Error ? error.message : String(error)}`
      );
    }
  }

  /**
   * Creates embeddings for a single text input
   * 
   * @param text - The input text
   * @param model - The model to use (defaults to text-embedding-3-small)
   * @param options - Additional options
   * @returns Promise<number[]> The embedding vector for the input text
   * 
   * @example
   * ```typescript
   * const embedding = await client.embeddings.createSingleEmbedding(
   *   "Hello, world!",
   *   "text-embedding-3-small"
   * );
   * 
   * console.log(`Embedding dimension: ${embedding.length}`);
   * ```
   */
  async createSingleEmbedding(
    text: string,
    model?: string,
    options?: {
      dimensions?: number;
      encoding_format?: 'float' | 'base64';
      user?: string;
      signal?: AbortSignal;
    }
  ): Promise<number[]> {
    const request: EmbeddingRequest = {
      input: text,
      model: model || EmbeddingModels.DEFAULT,
      dimensions: options?.dimensions,
      encoding_format: options?.encoding_format,
      user: options?.user,
    };

    const response = await this.createEmbedding(request, { signal: options?.signal });
    
    if (response.data.length === 0) {
      throw new ConduitError('No embeddings returned');
    }

    return convertEmbeddingToFloatArray(response.data[0].embedding);
  }

  /**
   * Creates embeddings for multiple text inputs
   * 
   * @param texts - The input texts
   * @param model - The model to use (defaults to text-embedding-3-small)
   * @param options - Additional options
   * @returns Promise<number[][]> A list of embedding vectors for each input text
   * 
   * @example
   * ```typescript
   * const embeddings = await client.embeddings.createBatchEmbeddings(
   *   ["Hello", "World", "AI"],
   *   "text-embedding-3-small"
   * );
   * 
   * embeddings.forEach((embedding, i) => {
   *   console.log(`Text ${i}: ${embedding.length} dimensions`);
   * });
   * ```
   */
  async createBatchEmbeddings(
    texts: string[],
    model?: string,
    options?: {
      dimensions?: number;
      encoding_format?: 'float' | 'base64';
      user?: string;
      signal?: AbortSignal;
    }
  ): Promise<number[][]> {
    if (texts.length === 0) {
      throw new Error('At least one text input is required');
    }

    const request: EmbeddingRequest = {
      input: texts,
      model: model || EmbeddingModels.DEFAULT,
      dimensions: options?.dimensions,
      encoding_format: options?.encoding_format,
      user: options?.user,
    };

    const response = await this.createEmbedding(request, { signal: options?.signal });
    
    // Sort by index to ensure correct order
    return response.data
      .sort((a, b) => a.index - b.index)
      .map(data => convertEmbeddingToFloatArray(data.embedding));
  }

  /**
   * Finds the most similar texts from a list of candidates to a query text
   * 
   * @param query - The query text
   * @param candidates - The list of candidate texts
   * @param options - Search options
   * @returns Promise<Array<{ text: string; similarity: number; index: number }>> 
   *          Sorted list of candidates with similarity scores
   * 
   * @example
   * ```typescript
   * const candidates = [
   *   "The cat sat on the mat",
   *   "Dogs are loyal animals",
   *   "The feline rested on the rug",
   *   "Birds can fly"
   * ];
   * 
   * const results = await client.embeddings.findSimilar(
   *   "A cat is sleeping",
   *   candidates,
   *   { topK: 2 }
   * );
   * 
   * results.forEach(result => {
   *   console.log(`"${result.text}" - Similarity: ${result.similarity.toFixed(3)}`);
   * });
   * ```
   */
  async findSimilar(
    query: string,
    candidates: string[],
    options?: {
      model?: string;
      topK?: number;
      dimensions?: number;
      signal?: AbortSignal;
    }
  ): Promise<Array<{ text: string; similarity: number; index: number }>> {
    if (candidates.length === 0) {
      throw new Error('At least one candidate is required');
    }

    // Create embeddings for query and all candidates in a single batch
    const allTexts = [query, ...candidates];
    const embeddings = await this.createBatchEmbeddings(
      allTexts,
      options?.model,
      {
        dimensions: options?.dimensions,
        signal: options?.signal,
      }
    );

    const queryEmbedding = embeddings[0];
    const candidateEmbeddings = embeddings.slice(1);

    // Calculate similarities
    const results = candidates.map((text, index) => ({
      text,
      similarity: calculateCosineSimilarity(queryEmbedding, candidateEmbeddings[index]),
      index,
    }));

    // Sort by similarity (descending) and take top K
    results.sort((a, b) => b.similarity - a.similarity);
    
    const topK = options?.topK || candidates.length;
    return results.slice(0, topK);
  }

  /**
   * Calculates the similarity between two texts
   * 
   * @param text1 - The first text
   * @param text2 - The second text
   * @param model - The model to use for embeddings
   * @param options - Additional options
   * @returns Promise<number> The cosine similarity between -1 and 1
   * 
   * @example
   * ```typescript
   * const similarity = await client.embeddings.calculateSimilarity(
   *   "The weather is nice today",
   *   "It's a beautiful day outside"
   * );
   * 
   * console.log(`Similarity: ${(similarity * 100).toFixed(1)}%`);
   * ```
   */
  async calculateSimilarity(
    text1: string,
    text2: string,
    model?: string,
    options?: {
      dimensions?: number;
      signal?: AbortSignal;
    }
  ): Promise<number> {
    const embeddings = await this.createBatchEmbeddings(
      [text1, text2],
      model,
      options
    );

    return calculateCosineSimilarity(embeddings[0], embeddings[1]);
  }

  /**
   * Groups texts by similarity using embeddings
   * 
   * @param texts - The texts to group
   * @param threshold - Similarity threshold for grouping (0-1)
   * @param model - The model to use for embeddings
   * @param options - Additional options
   * @returns Promise<string[][]> Groups of similar texts
   * 
   * @example
   * ```typescript
   * const texts = [
   *   "Python programming",
   *   "JavaScript coding",
   *   "Cooking recipes",
   *   "Software development",
   *   "Baking cakes"
   * ];
   * 
   * const groups = await client.embeddings.groupBySimilarity(
   *   texts,
   *   0.7 // 70% similarity threshold
   * );
   * 
   * groups.forEach((group, i) => {
   *   console.log(`Group ${i + 1}: ${group.join(", ")}`);
   * });
   * ```
   */
  async groupBySimilarity(
    texts: string[],
    threshold: number = 0.7,
    model?: string,
    options?: {
      dimensions?: number;
      signal?: AbortSignal;
    }
  ): Promise<string[][]> {
    if (texts.length === 0) {
      return [];
    }

    if (threshold < 0 || threshold > 1) {
      throw new Error('Threshold must be between 0 and 1');
    }

    const embeddings = await this.createBatchEmbeddings(texts, model, options);
    const groups: number[][] = [];
    const assigned = new Set<number>();

    for (let i = 0; i < texts.length; i++) {
      if (assigned.has(i)) continue;

      const group = [i];
      assigned.add(i);

      for (let j = i + 1; j < texts.length; j++) {
        if (assigned.has(j)) continue;

        const similarity = calculateCosineSimilarity(embeddings[i], embeddings[j]);
        if (similarity >= threshold) {
          group.push(j);
          assigned.add(j);
        }
      }

      groups.push(group);
    }

    return groups.map(group => group.map(index => texts[index]));
  }
}

/**
 * Helper functions for embeddings
 */
export const EmbeddingHelpers = {
  /**
   * Normalizes an embedding vector to unit length
   */
  normalize(embedding: number[]): number[] {
    const magnitude = Math.sqrt(embedding.reduce((sum, val) => sum + val * val, 0));
    if (magnitude === 0) return embedding;
    return embedding.map(val => val / magnitude);
  },

  /**
   * Calculates euclidean distance between two embeddings
   */
  euclideanDistance(embedding1: number[], embedding2: number[]): number {
    if (embedding1.length !== embedding2.length) {
      throw new Error('Embeddings must have the same dimensions');
    }
    
    let sum = 0;
    for (let i = 0; i < embedding1.length; i++) {
      const diff = embedding1[i] - embedding2[i];
      sum += diff * diff;
    }
    return Math.sqrt(sum);
  },

  /**
   * Calculates the centroid of multiple embeddings
   */
  centroid(embeddings: number[][]): number[] {
    if (embeddings.length === 0) {
      throw new Error('At least one embedding is required');
    }

    const dimensions = embeddings[0].length;
    const result = new Array(dimensions).fill(0);

    for (const embedding of embeddings) {
      for (let i = 0; i < dimensions; i++) {
        result[i] += embedding[i];
      }
    }

    return result.map(val => val / embeddings.length);
  },
};