import { useMutation, UseMutationOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import type { ExportUsageParams, ExportResult } from '../../../models/analyticsExport';

export function useExportUsageAnalytics(
  options?: UseMutationOptions<ExportResult, Error, ExportUsageParams>
) {
  const { adminClient } = useConduitAdmin();

  return useMutation({
    mutationFn: (params: ExportUsageParams) => adminClient.analytics.exportUsageAnalytics(params),
    ...options,
  });
}