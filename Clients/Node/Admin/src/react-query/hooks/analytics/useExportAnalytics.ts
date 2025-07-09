import { useMutation, UseMutationOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import type { AnalyticsFilters } from '../../../models/analytics';

export interface ExportAnalyticsParams {
  filters: AnalyticsFilters;
  format: 'csv' | 'excel' | 'json';
}

export function useExportAnalytics(
  options?: UseMutationOptions<Blob, Error, ExportAnalyticsParams>
) {
  const { adminClient } = useConduitAdmin();

  return useMutation({
    mutationFn: ({ filters, format }: ExportAnalyticsParams) => 
      adminClient.analytics.export(filters, format),
    ...options,
  });
}