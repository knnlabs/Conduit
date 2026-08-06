'use client';

import { useQuery } from '@tanstack/react-query';
import { withAdminClient } from '@/lib/client/adminClient';
import type { ConduitAdminClient, ModelProviderAvailabilityDto } from '@/lib/admin-api';

export type AssociationWithProvider = ModelProviderAvailabilityDto;

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
      
      return response;
    },
    enabled: !!modelId,
  });
}
