import { useMemo } from 'react';
import { useModelMappings } from '@/hooks/useModelMappingsApi';
import type { ModelCostDto } from '@/lib/admin-api';
import type { ExtendedModelProviderMappingDto } from '../types/modelCost';

interface ProviderInfo {
  providerId: number;
  providerName: string;
  providerType: string;
}

export interface EnrichedModelCost extends ModelCostDto {
  providers: ProviderInfo[];
}

export function useEnrichedModelCosts(modelCosts: ModelCostDto[] | undefined) {
  const { mappings, isLoading: mappingsLoading } = useModelMappings();

  const enrichedCosts = useMemo(() => {
    if (!modelCosts || !mappings || mappings.length === 0) {
      return modelCosts?.map(cost => ({
        ...cost,
        providers: []
      })) ?? [];
    }

    return modelCosts.map(cost => {
      // Find all unique providers for this cost's model aliases
      const providersMap = new Map<number, ProviderInfo>();
      
      cost.associatedModelAliases.forEach(alias => {
        const extendedMapping = mappings.find(m => {
          const extended = m as ExtendedModelProviderMappingDto;
          return extended.modelAlias === alias || extended.providerModelId === alias;
        }) as ExtendedModelProviderMappingDto | undefined;
        
        if (extendedMapping) {
          const providerId = extendedMapping.providerId;
          if (!providersMap.has(providerId)) {
            providersMap.set(providerId, {
              providerId,
              providerName: extendedMapping.providerName ?? `Provider ${providerId}`,
              providerType: extendedMapping.providerTypeName ?? 'Unknown'
            });
          }
        }
      });

      return {
        ...cost,
        providers: Array.from(providersMap.values())
      };
    });
  }, [modelCosts, mappings]);

  return {
    enrichedCosts,
    isLoading: mappingsLoading
  };
}