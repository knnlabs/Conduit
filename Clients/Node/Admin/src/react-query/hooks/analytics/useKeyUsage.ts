import { useQuery, UseQueryOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import { adminQueryKeys } from '../../queryKeys';
import type { KeyUsageDto } from '../../../models/analytics';
import type { DateRange } from '../../../models/common';

export interface UseKeyUsageOptions {
  keyId: number;
  dateRange: DateRange;
}

export function useKeyUsage(
  { keyId, dateRange }: UseKeyUsageOptions,
  options?: Omit<UseQueryOptions<KeyUsageDto>, 'queryKey' | 'queryFn'>
) {
  const { adminClient } = useConduitAdmin();

  return useQuery({
    queryKey: adminQueryKeys.analytics.keyUsage(keyId, dateRange),
    queryFn: () => adminClient.analytics.getKeyUsage(keyId, dateRange),
    enabled: !!keyId,
    staleTime: 60 * 1000, // 1 minute
    ...options,
  });
}