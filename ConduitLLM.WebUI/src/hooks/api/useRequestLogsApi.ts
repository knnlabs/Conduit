import { useQuery, useMutation } from '@tanstack/react-query';
import { notifications } from '@mantine/notifications';
import { apiFetch } from '@/lib/utils/fetch-wrapper';

export interface RequestLog {
  id: string;
  timestamp: string;
  method: string;
  endpoint: string;
  statusCode: number;
  responseTime: number;
  virtualKeyId?: string;
  virtualKeyName?: string;
  ipAddress: string;
  userAgent?: string;
  requestSize: number;
  responseSize: number;
  error?: string;
  model?: string;
  tokensUsed?: number;
  cost?: number;
}

export interface RequestLogsResponse {
  items: RequestLog[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export interface RequestLogsFilters {
  startDate?: string;
  endDate?: string;
  status?: string;
  method?: string;
  endpoint?: string;
  virtualKeyId?: string;
  page?: number;
  pageSize?: number;
}

export interface ExportFilters {
  startDate?: string;
  endDate?: string;
  status?: string;
  method?: string;
  virtualKeyId?: string;
}

const requestLogsKeys = {
  all: ['request-logs'] as const,
  lists: () => [...requestLogsKeys.all, 'list'] as const,
  list: (filters?: RequestLogsFilters) => [...requestLogsKeys.lists(), filters] as const,
};

export function useRequestLogs(filters?: RequestLogsFilters) {
  return useQuery({
    queryKey: requestLogsKeys.list(filters),
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters?.startDate) params.append('startDate', filters.startDate);
      if (filters?.endDate) params.append('endDate', filters.endDate);
      if (filters?.status) params.append('status', filters.status);
      if (filters?.method) params.append('method', filters.method);
      if (filters?.endpoint) params.append('endpoint', filters.endpoint);
      if (filters?.virtualKeyId) params.append('virtualKeyId', filters.virtualKeyId);
      if (filters?.page) params.append('page', filters.page.toString());
      if (filters?.pageSize) params.append('pageSize', filters.pageSize.toString());

      const response = await apiFetch(`/api/admin/request-logs?${params}`, {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.error || 'Failed to fetch request logs');
      }

      return response.json() as Promise<RequestLogsResponse>;
    },
    staleTime: 30 * 1000, // 30 seconds
  });
}

export function useExportRequestLogs() {
  return useMutation({
    mutationFn: async ({ format, filters }: { format: 'csv' | 'json' | 'excel'; filters?: ExportFilters }) => {
      const response = await apiFetch('/api/admin/request-logs', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ format, filters }),
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.error || 'Failed to export request logs');
      }

      // Get the blob data
      const blob = await response.blob();
      
      // Create a download link
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `request-logs-${new Date().toISOString()}.${format}`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
      
      return { success: true };
    },
    onSuccess: () => {
      notifications.show({
        title: 'Export Successful',
        message: 'Request logs have been exported successfully',
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

// Helper function to calculate request statistics
export function calculateRequestStats(logs: RequestLog[]) {
  const totalRequests = logs.length;
  const successfulRequests = logs.filter(log => log.statusCode >= 200 && log.statusCode < 300).length;
  const failedRequests = logs.filter(log => log.statusCode >= 400).length;
  const avgResponseTime = logs.reduce((sum, log) => sum + log.responseTime, 0) / totalRequests || 0;
  const totalTokensUsed = logs.reduce((sum, log) => sum + (log.tokensUsed || 0), 0);
  const totalCost = logs.reduce((sum, log) => sum + (log.cost || 0), 0);
  
  const methodCounts = logs.reduce((acc, log) => {
    acc[log.method] = (acc[log.method] || 0) + 1;
    return acc;
  }, {} as Record<string, number>);
  
  const statusCounts = logs.reduce((acc, log) => {
    const statusGroup = `${Math.floor(log.statusCode / 100)}xx`;
    acc[statusGroup] = (acc[statusGroup] || 0) + 1;
    return acc;
  }, {} as Record<string, number>);
  
  return {
    totalRequests,
    successfulRequests,
    failedRequests,
    successRate: (successfulRequests / totalRequests) * 100,
    avgResponseTime,
    totalTokensUsed,
    totalCost,
    methodCounts,
    statusCounts,
  };
}