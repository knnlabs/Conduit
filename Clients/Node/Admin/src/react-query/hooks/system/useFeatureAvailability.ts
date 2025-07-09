import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { FeatureAvailability } from '../../../models/system';

export interface UseFeatureAvailabilityOptions extends Omit<UseQueryOptions<FeatureAvailability, Error>, 'queryKey' | 'queryFn'> {}

export function useFeatureAvailability(options?: UseFeatureAvailabilityOptions) {
  const { adminClient } = useConduitAdmin();
  
  return useQuery({
    queryKey: adminQueryKeys.system.featureAvailability(),
    queryFn: () => adminClient.system.getFeatureAvailability(),
    staleTime: 60000, // 1 minute
    ...options,
  });
}