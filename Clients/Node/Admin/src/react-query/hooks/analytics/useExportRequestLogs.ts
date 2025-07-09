import { useMutation, UseMutationOptions } from '@tanstack/react-query';
import { useConduitAdmin } from '../../ConduitAdminProvider';
import type { ExportRequestLogsParams, ExportResult } from '../../../models/analyticsExport';

export function useExportRequestLogs(
  options?: UseMutationOptions<ExportResult, Error, ExportRequestLogsParams>
) {
  const { adminClient } = useConduitAdmin();

  return useMutation({
    mutationFn: (params: ExportRequestLogsParams) => adminClient.analytics.exportRequestLogs(params),
    ...options,
  });
}