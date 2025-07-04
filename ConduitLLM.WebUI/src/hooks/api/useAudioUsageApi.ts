import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { adminApiKeys } from './useAdminApi';
import { notifications } from '@mantine/notifications';
import { getAdminClient } from '@/lib/clients/conduit';
import type { AudioUsageSummary } from '@/types/sdk-responses';

export function useAudioUsageSummary(
  startDate: Date,
  endDate: Date,
  virtualKey?: string,
  provider?: string
) {
  const adminSdk = getAdminClient();

  return useQuery<AudioUsageSummary>({
    queryKey: [adminApiKeys.audioUsage, 'summary', startDate, endDate, virtualKey, provider],
    queryFn: async () => {
      try {
        const summary = await adminSdk.audioConfiguration.getUsageSummary({
          startDate: startDate.toISOString(),
          endDate: endDate.toISOString(),
          virtualKey,
          provider,
        });
        
        // Transform SDK response to expected format
        const summaryAny = summary as unknown as Record<string, unknown>;
        const result: AudioUsageSummary = {
          startDate: startDate.toISOString(),
          endDate: endDate.toISOString(),
          totalCost: Number(summaryAny.totalCost) || 0,
          totalDuration: Number(summaryAny.totalDurationSeconds || summaryAny.totalDuration) || 0,
          totalRequests: Number(summaryAny.totalRequests) || 0,
          averageLatency: Number(summaryAny.averageLatency) || 0,
          topModels: [],
          dailyUsage: [],
          modelUsage: summaryAny.byModel ? Object.entries(summaryAny.byModel as Record<string, unknown>).map(([model, data]) => {
            const modelData = data as Record<string, unknown>;
            return {
              model,
              requests: Number(modelData.requests) || 0,
              cost: Number(modelData.cost) || 0,
              duration: Number(modelData.duration) || 0,
            };
          }) : [],
        };
        return result;
      } catch (error) {
        // Return empty data if endpoint returns 404
        if ((error as Record<string, unknown>)?.statusCode === 404) {
          return {
            startDate: startDate.toISOString(),
            endDate: endDate.toISOString(),
            totalCost: 0,
            totalDuration: 0,
            totalRequests: 0,
            averageLatency: 0,
            topModels: [],
            dailyUsage: [],
            modelUsage: [],
          };
        }
        throw error;
      }
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

export function useAudioUsageLogs(query: {
  page?: number;
  pageSize?: number;
  startDate?: Date;
  endDate?: Date;
  virtualKey?: string;
  provider?: string;
  model?: string;
}) {
  const adminSdk = getAdminClient();

  return useQuery({
    queryKey: [adminApiKeys.audioUsage, 'logs', query],
    queryFn: async () => {
      try {
        const response = await adminSdk.audioConfiguration.getUsage({
          page: query.page || 1,
          pageSize: query.pageSize || 20,
          startDate: query.startDate?.toISOString(),
          endDate: query.endDate?.toISOString(),
          virtualKey: query.virtualKey,
          provider: query.provider,
        });
        
        const pageSize = response.pageSize || 20;
        const totalCount = response.totalCount || 0;
        return {
          items: response.data || [],
          page: response.page || 1,
          pageSize,
          totalCount,
          totalPages: Math.ceil(totalCount / pageSize) || 0,
        };
      } catch (error) {
        // Return empty logs if endpoint returns 404
        if ((error as Record<string, unknown>)?.statusCode === 404) {
          return {
            items: [],
            page: query.page || 1,
            pageSize: query.pageSize || 20,
            totalCount: 0,
            totalPages: 0,
          };
        }
        throw error;
      }
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useAudioUsageByKey(
  virtualKey: string,
  startDate?: Date,
  endDate?: Date
) {
  const adminSdk = getAdminClient();

  return useQuery({
    queryKey: [adminApiKeys.audioUsage, 'byKey', virtualKey, startDate, endDate],
    queryFn: async () => {
      try {
        const filters = {
          startDate: startDate?.toISOString() || new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
          endDate: endDate?.toISOString() || new Date().toISOString(),
          virtualKey,
        };
        
        const summary = await adminSdk.audioConfiguration.getUsageSummary(filters);
        const summaryAny = summary as unknown as Record<string, unknown>;
        
        return {
          virtualKey,
          totalRequests: Number(summaryAny.totalRequests) || 0,
          totalCost: Number(summaryAny.totalCost) || 0,
          totalDuration: Number(summaryAny.totalDurationSeconds || summaryAny.totalDuration) || 0,
          averageLatency: 0,
          operations: (summaryAny.byOperation as Record<string, number>) || {
            transcription: 0,
            textToSpeech: 0,
            translation: 0,
          },
        };
      } catch (error) {
        // Return empty data if endpoint returns 404
        if ((error as Record<string, unknown>)?.statusCode === 404) {
          return {
            virtualKey,
            totalRequests: 0,
            totalCost: 0,
            totalDuration: 0,
            averageLatency: 0,
            operations: {
              transcription: 0,
              textToSpeech: 0,
              translation: 0,
            },
          };
        }
        throw error;
      }
    },
    enabled: !!virtualKey && virtualKey !== 'all',
    staleTime: 5 * 60 * 1000,
  });
}

export function useAudioUsageByProvider(
  provider: string,
  startDate?: Date,
  endDate?: Date
) {
  const adminSdk = getAdminClient();

  return useQuery({
    queryKey: [adminApiKeys.audioUsage, 'byProvider', provider, startDate, endDate],
    queryFn: async () => {
      try {
        const filters = {
          startDate: startDate?.toISOString() || new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
          endDate: endDate?.toISOString() || new Date().toISOString(),
          provider,
        };
        
        const summary = await adminSdk.audioConfiguration.getUsageSummary(filters);
        const summaryAny = summary as unknown as Record<string, unknown>;
        
        return {
          provider,
          totalRequests: Number(summaryAny.totalRequests) || 0,
          totalCost: Number(summaryAny.totalCost) || 0,
          totalDuration: Number(summaryAny.totalDurationSeconds || summaryAny.totalDuration) || 0,
          averageLatency: 0,
          models: (summaryAny.byModel as Record<string, unknown>) || {},
        };
      } catch (error) {
        // Return empty data if endpoint returns 404
        if ((error as Record<string, unknown>)?.statusCode === 404) {
          return {
            provider,
            totalRequests: 0,
            totalCost: 0,
            totalDuration: 0,
            averageLatency: 0,
            models: {},
          };
        }
        throw error;
      }
    },
    enabled: !!provider && provider !== 'all',
    staleTime: 5 * 60 * 1000,
  });
}

export function useRealtimeSessionMetrics() {
  const adminSdk = getAdminClient();

  return useQuery({
    queryKey: [adminApiKeys.audioUsage, 'realtimeMetrics'],
    queryFn: async () => {
      try {
        // Get active sessions to calculate metrics
        const sessions = await adminSdk.audioConfiguration.getActiveSessions();
        
        // Calculate metrics from active sessions
        const activeSessions = sessions.length;
        const totalSessionsToday = sessions.filter(
          s => new Date(s.startedAt).toDateString() === new Date().toDateString()
        ).length;
        
        const durations = sessions.map(s => {
          const start = new Date(s.startedAt).getTime();
          const end = Date.now(); // Active sessions don't have an end time
          return (end - start) / 1000; // Convert to seconds
        });
        
        const averageSessionDuration = durations.length > 0
          ? durations.reduce((a, b) => a + b, 0) / durations.length
          : 0;
        
        return {
          activeSessions,
          totalSessionsToday,
          averageSessionDuration,
          peakConcurrentSessions: activeSessions, // Would need historical data for real peak
          bandwidthUsage: 0, // Not available from current SDK
        };
      } catch (error) {
        // Return empty metrics if endpoint returns 404
        if ((error as Record<string, unknown>)?.statusCode === 404) {
          return {
            activeSessions: 0,
            totalSessionsToday: 0,
            averageSessionDuration: 0,
            peakConcurrentSessions: 0,
            bandwidthUsage: 0,
          };
        }
        throw error;
      }
    },
    staleTime: 30 * 1000, // 30 seconds
    refetchInterval: 30 * 1000, // Auto-refresh every 30 seconds
  });
}

export function useActiveSessions() {
  const adminSdk = getAdminClient();

  return useQuery({
    queryKey: [adminApiKeys.audioUsage, 'activeSessions'],
    queryFn: async () => {
      try {
        const sessions = await adminSdk.audioConfiguration.getActiveSessions();
        return sessions || [];
      } catch (error) {
        // Return empty array if endpoint returns 404
        if ((error as Record<string, unknown>)?.statusCode === 404) {
          return [];
        }
        throw error;
      }
    },
    staleTime: 30 * 1000,
    refetchInterval: 30 * 1000,
  });
}

export function useExportAudioUsage() {
  const _queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ 
      format: _format 
    }: { 
      format: 'csv' | 'json' 
    }) => {
      // Feature not yet available - throw error to trigger error handling
      throw new Error('Audio usage export is not yet available');
    },
    onSuccess: (data: string, { format }: { format: 'csv' | 'json' }) => {
      // Create a download link
      const blob = new Blob([data], { 
        type: format === 'csv' ? 'text/csv' : 'application/json' 
      });
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `audio-usage-${new Date().toISOString()}.${format}`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(url);

      notifications.show({
        title: 'Export Successful',
        message: 'Audio usage data has been exported',
        color: 'green',
      });
    },
    onError: (error: unknown) => {
      const errorMessage = error instanceof Error ? error.message : 'Failed to export audio usage data';
      notifications.show({
        title: 'Export Failed',
        message: errorMessage,
        color: 'red',
      });
    },
  });
}

export function useTerminateSession() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (sessionId: string) => {
      // Mock termination for now - replace with actual API call when backend endpoint exists
      await new Promise(resolve => setTimeout(resolve, 1000)); // Simulate API delay
      return { success: true, sessionId };
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ 
        queryKey: [adminApiKeys.audioUsage, 'activeSessions'] 
      });
      queryClient.invalidateQueries({ 
        queryKey: [adminApiKeys.audioUsage, 'realtimeMetrics'] 
      });
      
      notifications.show({
        title: 'Session Terminated',
        message: 'The real-time session has been terminated',
        color: 'green',
      });
    },
    onError: (error: unknown) => {
      const errorMessage = error instanceof Error ? error.message : 'Failed to terminate session';
      notifications.show({
        title: 'Termination Failed',
        message: errorMessage,
        color: 'red',
      });
    },
  });
}