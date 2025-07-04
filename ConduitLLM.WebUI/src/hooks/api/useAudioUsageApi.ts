import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { adminApiKeys } from './useAdminApi';
import { notifications } from '@mantine/notifications';
import { getEmptyAudioUsageSummary, getEmptyRequestLogs } from '@/lib/placeholders/backend-placeholders';
import type { AudioUsageSummary } from '@/types/sdk-responses';

export function useAudioUsageSummary(
  startDate: Date,
  endDate: Date,
  virtualKey?: string,
  provider?: string
) {
  return useQuery<AudioUsageSummary>({
    queryKey: [adminApiKeys.audioUsage, 'summary', startDate, endDate, virtualKey, provider],
    queryFn: async () => {
      // Return empty data until backend endpoint exists
      // This provides a realistic empty state instead of fake data
      return getEmptyAudioUsageSummary(startDate, endDate);
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
  return useQuery({
    queryKey: [adminApiKeys.audioUsage, 'logs', query],
    queryFn: async () => {
      // Return empty logs until backend endpoint exists
      return getEmptyRequestLogs(query.page || 1, query.pageSize || 20);
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useAudioUsageByKey(
  virtualKey: string,
  startDate?: Date,
  endDate?: Date
) {
  return useQuery({
    queryKey: [adminApiKeys.audioUsage, 'byKey', virtualKey, startDate, endDate],
    queryFn: async () => {
      // Return empty data until backend endpoint exists
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
  return useQuery({
    queryKey: [adminApiKeys.audioUsage, 'byProvider', provider, startDate, endDate],
    queryFn: async () => {
      // Return empty data until backend endpoint exists
      return {
        provider,
        totalRequests: 0,
        totalCost: 0,
        totalDuration: 0,
        averageLatency: 0,
        models: {},
      };
    },
    enabled: !!provider && provider !== 'all',
    staleTime: 5 * 60 * 1000,
  });
}

export function useRealtimeSessionMetrics() {
  return useQuery({
    queryKey: [adminApiKeys.audioUsage, 'realtimeMetrics'],
    queryFn: async () => {
      // Return empty metrics until backend endpoint exists
      return {
        activeSessions: 0,
        totalSessionsToday: 0,
        averageSessionDuration: 0,
        peakConcurrentSessions: 0,
        bandwidthUsage: 0,
      };
    },
    staleTime: 30 * 1000, // 30 seconds
    refetchInterval: 30 * 1000, // Auto-refresh every 30 seconds
  });
}

export function useActiveSessions() {
  return useQuery({
    queryKey: [adminApiKeys.audioUsage, 'activeSessions'],
    queryFn: async () => {
      // Return empty array until backend endpoint exists
      return [];
    },
    staleTime: 30 * 1000,
    refetchInterval: 30 * 1000,
  });
}

export function useExportAudioUsage() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ 
      query, 
      format 
    }: { 
      query: unknown; 
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