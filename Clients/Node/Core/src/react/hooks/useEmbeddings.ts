import { useMutation, UseMutationOptions } from '@tanstack/react-query';
import { useConduit } from '../ConduitProvider';
import type { EmbeddingRequest, EmbeddingResponse } from '../../models/embeddings';

export interface UseEmbeddingsOptions 
  extends Omit<
    UseMutationOptions<EmbeddingResponse, Error, EmbeddingRequest>,
    'mutationFn'
  > {}

export function useEmbeddings(options?: UseEmbeddingsOptions) {
  const { client } = useConduit();

  return useMutation({
    mutationFn: async (request: EmbeddingRequest) => {
      return await client.embeddings.createEmbedding(request);
    },
    ...options,
  });
}