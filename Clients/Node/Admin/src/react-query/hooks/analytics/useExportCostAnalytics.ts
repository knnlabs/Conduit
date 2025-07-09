import { useMutation, UseMutationOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import type { ExportCostParams, ExportResult } from '../../../models/analyticsExport';

export function useExportCostAnalytics(
  options?: UseMutationOptions<ExportResult, Error, ExportCostParams>
) {
  const { adminClient } = useConduitAdmin();

  return useMutation({
    mutationFn: (params: ExportCostParams) => adminClient.analytics.exportCostAnalytics(params),
    ...options,
  });
}