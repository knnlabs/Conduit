import { useMutation } from '@tanstack/react-query';
import { notifications } from '@mantine/notifications';
import { apiFetch } from '@/lib/utils/fetch-wrapper';

export interface ExportOptions {
  type: 'usage' | 'cost' | 'virtual-keys' | 'security-events';
  format: 'csv' | 'json' | 'excel';
  filters?: {
    startDate?: string;
    endDate?: string;
    virtualKeyId?: string;
    groupBy?: string;
    severity?: string;
    status?: string;
  };
}

export function useExportData() {
  return useMutation({
    mutationFn: async (options: ExportOptions) => {
      const response = await apiFetch('/api/admin/analytics/export', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(options),
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.error || 'Failed to export data');
      }

      // Get the blob data
      const blob = await response.blob();
      
      // Create a download link
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${options.type}-export-${new Date().toISOString()}.${options.format}`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
      
      return { success: true };
    },
    onSuccess: () => {
      notifications.show({
        title: 'Export Successful',
        message: 'Data has been exported successfully',
        color: 'green',
      });
    },
    onError: (error: Error) => {
      notifications.show({
        title: 'Export Failed',
        message: error.message,
        color: 'red',
      });
    },
  });
}