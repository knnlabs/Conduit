'use client';

import { notifications } from '@mantine/notifications';
import type { SecurityEvent } from './types';

/**
 * Hook for security dashboard event handlers
 */
export function useSecurityDashboardHandlers(
  refetchAll: () => Promise<void>,
  setIsExporting: (value: boolean) => void
) {
  /**
   * Refresh all security data
   */
  const handleRefresh = async () => {
    try {
      await refetchAll();
      notifications.show({
        title: 'Data Refreshed',
        message: 'Security data has been updated',
        color: 'green',
      });
    } catch {
      notifications.show({
        title: 'Refresh Failed',
        message: 'Failed to refresh security data',
        color: 'red',
      });
    }
  };

  /**
   * Export security events to file
   */
  const handleExportEvents = async (
    events: SecurityEvent[],
    format: 'json' | 'csv'
  ) => {
    setIsExporting(true);
    try {
      let content: string;
      let mimeType: string;
      let filename: string;

      const dateStr = new Date().toISOString().split('T')[0];

      if (format === 'json') {
        content = JSON.stringify(events, null, 2);
        mimeType = 'application/json';
        filename = `security-events-${dateStr}.json`;
      } else {
        // CSV format
        const headers = ['id', 'timestamp', 'type', 'severity', 'source', 'ipAddress'];
        const rows = events.map(e =>
          [
            e.id,
            e.timestamp,
            e.type,
            e.severity,
            e.source,
            e.ipAddress ?? '',
          ].map(val => `"${String(val).replace(/"/g, '""')}"`).join(',')
        );
        content = [headers.join(','), ...rows].join('\n');
        mimeType = 'text/csv';
        filename = `security-events-${dateStr}.csv`;
      }

      // Create and trigger download
      const blob = new Blob([content], { type: mimeType });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);

      notifications.show({
        title: 'Export Successful',
        message: `Security events exported as ${format.toUpperCase()}`,
        color: 'green',
      });
    } catch {
      notifications.show({
        title: 'Export Failed',
        message: 'Failed to export security events',
        color: 'red',
      });
    } finally {
      setIsExporting(false);
    }
  };

  return {
    handleRefresh,
    handleExportEvents,
  };
}
