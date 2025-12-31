'use client';

import { useQuery } from '@tanstack/react-query';
import { withAdminClient } from '@/lib/client/adminClient';
import type { ConduitAdminClient } from '@knn_labs/conduit-admin-client';

export interface AssociationWithProvider {
  associationId: number;
  identifier: string;
  provider: string | null;
  providerVariation: string | null;
  maxInputTokens: number | null;
  maxOutputTokens: number | null;
  speedScore: number | null;
  qualityScore: number | null;
  isPrimary: boolean;
  availableProviders: Array<{
    providerId: number;
    providerName: string;
    providerType: string;
  }>;
}

/**
 * Hook to fetch model associations that have configured providers
 * Uses the backend endpoint that properly joins:
 * Model -> ModelProviderTypeAssociation -> Provider
 */
export function useModelAssociations(modelId: number | null) {
  return useQuery({
    queryKey: ['model-available-providers', modelId],
    queryFn: async () => {
      if (!modelId) return [];

      const response = await withAdminClient(async (client: ConduitAdminClient) => 
        client.models.getModelProviders(modelId)
      );
      
      return response as AssociationWithProvider[];
    },
    enabled: !!modelId,
  });
}