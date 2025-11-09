'use client';

import { useQuery } from '@tanstack/react-query';
import { withAdminClient } from '@/lib/client/adminClient';
import type { ConduitAdminClient } from '@knn_labs/conduit-admin-client';

export interface ModelIdentifier {
  id: number;
  identifier: string;
  provider: string | null;
  isPrimary: boolean;
  maxInputTokens?: number | null;
  maxOutputTokens?: number | null;
  speedScore?: number | null;
  qualityScore?: number | null;
  providerVariation?: string | null;
  modelCostId?: number | null;
}

export interface CreateModelIdentifierDto {
  identifier: string;
  provider?: string;
  isPrimary?: boolean;
  maxInputTokens?: number | null;
  maxOutputTokens?: number | null;
  speedScore?: number | null;
  qualityScore?: number | null;
  providerVariation?: string | null;
  metadata?: string;
}

/**
 * Hook to fetch model identifiers for a specific model
 */
export function useModelIdentifiers(modelId: number | null) {
  return useQuery({
    queryKey: ['model-identifiers', modelId],
    queryFn: async () => {
      if (!modelId) return [];
      const response = await withAdminClient((client: ConduitAdminClient) => 
        client.models.getIdentifiers(modelId)
      );
      return response as ModelIdentifier[];
    },
    enabled: !!modelId,
  });
}

// REMOVED: useCreateModelIdentifier and useFindOrCreateModelIdentifier
// These hooks were DANGEROUS - they automatically created ModelProviderTypeAssociations
// ModelProviderTypeAssociations should ONLY be created by administrators with proper configuration
// Including token limits, quality scores, variations, etc.