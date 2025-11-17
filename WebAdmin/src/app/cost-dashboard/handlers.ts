import { notifications } from '@mantine/notifications';
import { withAdminClient } from '@/lib/client/adminClient';
import { safeLog } from '@/lib/utils/logging';
import type { DateRange } from './types';

export function useCostDashboardHandlers(
  refetchAll: () => Promise<void>,
  timeRange: string,
  setIsExporting: (value: boolean) => void
) {
  const handleRefresh = async () => {
    try {
      await refetchAll();
      notifications.show({
        title: 'Data Refreshed',
        message: 'Cost data has been updated',
        color: 'green',
      });
    } catch (err) {
      safeLog('error', 'Failed to refresh cost data', err);
      notifications.show({
        title: 'Refresh Failed',
        message: 'Failed to refresh cost data',
        color: 'red',
      });
    }
  };

  const handleExport = async () => {
    setIsExporting(true);
    try {
      // Calculate date range based on timeRange
      const getDateRange = (): DateRange => {
        const now = new Date();
        const startDate = new Date();
        
        switch (timeRange) {
          case '7d':
            startDate.setDate(now.getDate() - 7);
            break;
          case '30d':
            startDate.setDate(now.getDate() - 30);
            break;
          case '90d':
            startDate.setDate(now.getDate() - 90);
            break;
          default:
            startDate.setDate(now.getDate() - 30);
        }
        
        return {
          startDate: startDate.toISOString().split('T')[0],
          endDate: now.toISOString().split('T')[0],
        };
      };

      const { startDate, endDate } = getDateRange();
      
      // Get export data from Admin SDK (returns Uint8Array)
      const exportData = await withAdminClient(client =>
        client.analytics.exportAnalyticsAsync('csv', startDate, endDate)
      );
      
      // Create a blob from the Uint8Array and download
      // Cast to unknown then to BlobPart to avoid TypeScript ArrayBufferLike vs ArrayBuffer issue
      const blob = new Blob([exportData as unknown as BlobPart], { type: 'text/csv; charset=utf-8' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `cost-report-${timeRange}-${new Date().toISOString().split('T')[0]}.csv`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
      
      notifications.show({
        title: 'Export Successful',
        message: 'Cost report has been downloaded',
        color: 'green',
      });
    } catch (err) {
      safeLog('error', 'Failed to export cost data', err);
      notifications.show({
        title: 'Export Failed',
        message: 'Failed to export cost data',
        color: 'red',
      });
    } finally {
      setIsExporting(false);
    }
  };

  return {
    handleRefresh,
    handleExport,
  };
}