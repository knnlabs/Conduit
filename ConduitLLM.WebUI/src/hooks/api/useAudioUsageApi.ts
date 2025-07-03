import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { adminApiKeys } from './useAdminApi';
import { notifications } from '@mantine/notifications';

export function useAudioUsageSummary(
  startDate: Date,
  endDate: Date,
  virtualKey?: string,
  provider?: string
) {
  return useQuery({
    queryKey: [adminApiKeys.audioUsage, 'summary', startDate, endDate, virtualKey, provider],
    queryFn: async () => {
      // Mock data for now - replace with actual API call when backend endpoint exists
      return {
        totalRequests: Math.floor(Math.random() * 10000) + 1000,
        totalCost: Math.random() * 500 + 50,
        totalDuration: Math.floor(Math.random() * 50000) + 10000, // in seconds
        averageLatency: Math.floor(Math.random() * 500) + 100, // in ms
        topModels: [
          { model: 'whisper-1', requests: 1500, cost: 45.50 },
          { model: 'tts-1', requests: 800, cost: 24.30 },
          { model: 'tts-1-hd', requests: 300, cost: 15.20 },
        ],
        dailyUsage: Array.from({ length: 7 }, (_, i) => ({
          date: new Date(Date.now() - i * 24 * 60 * 60 * 1000).toISOString().split('T')[0],
          requests: Math.floor(Math.random() * 500) + 100,
          cost: Math.random() * 50 + 10,
        })).reverse(),
      };
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
      // Mock data for now - replace with actual API call when backend endpoint exists
      const pageSize = query.pageSize || 20;
      return {
        items: Array.from({ length: Math.min(pageSize, 25) }, (_, i) => ({
          id: `req_${Date.now()}_${i}`,
          timestamp: new Date(Date.now() - Math.random() * 7 * 24 * 60 * 60 * 1000),
          virtualKey: query.virtualKey === 'all' ? ['vk_prod_001', 'vk_dev_001'][Math.floor(Math.random() * 2)] : (query.virtualKey || 'vk_prod_001'),
          provider: query.provider === 'all' ? ['openai', 'elevenlabs'][Math.floor(Math.random() * 2)] : (query.provider || 'openai'),
          model: query.model === 'all' ? ['whisper-1', 'tts-1', 'tts-1-hd'][Math.floor(Math.random() * 3)] : (query.model || 'whisper-1'),
          operation: ['transcription', 'text-to-speech', 'translation'][Math.floor(Math.random() * 3)],
          duration: Math.floor(Math.random() * 300) + 5, // 5-305 seconds
          cost: Math.random() * 2 + 0.1,
          status: ['completed', 'failed'][Math.floor(Math.random() * 10) > 8 ? 1 : 0], // 90% success rate
        })),
        totalCount: Math.floor(Math.random() * 1000) + 100,
        page: query.page || 1,
        pageSize,
        totalPages: Math.ceil((Math.floor(Math.random() * 1000) + 100) / pageSize),
      };
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
      // Mock data for now - replace with actual API call when backend endpoint exists
      return {
        virtualKey,
        totalRequests: Math.floor(Math.random() * 5000) + 500,
        totalCost: Math.random() * 200 + 20,
        totalDuration: Math.floor(Math.random() * 20000) + 5000,
        averageLatency: Math.floor(Math.random() * 300) + 150,
        operations: {
          transcription: Math.floor(Math.random() * 2000) + 200,
          textToSpeech: Math.floor(Math.random() * 1500) + 150,
          translation: Math.floor(Math.random() * 500) + 50,
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
      // Mock data for now - replace with actual API call when backend endpoint exists
      return {
        provider,
        totalRequests: Math.floor(Math.random() * 8000) + 1000,
        totalCost: Math.random() * 300 + 50,
        totalDuration: Math.floor(Math.random() * 30000) + 8000,
        averageLatency: Math.floor(Math.random() * 400) + 100,
        models: {
          'whisper-1': Math.floor(Math.random() * 3000) + 500,
          'tts-1': Math.floor(Math.random() * 2000) + 300,
          'tts-1-hd': Math.floor(Math.random() * 1000) + 100,
        },
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
      // Mock data for now - replace with actual API call when backend endpoint exists
      return {
        activeSessions: Math.floor(Math.random() * 50) + 5,
        totalSessionsToday: Math.floor(Math.random() * 500) + 100,
        averageSessionDuration: Math.floor(Math.random() * 300) + 180, // seconds
        peakConcurrentSessions: Math.floor(Math.random() * 100) + 20,
        bandwidthUsage: Math.floor(Math.random() * 1000) + 200, // MB
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
      // Mock data for now - replace with actual API call when backend endpoint exists
      const sessionCount = Math.floor(Math.random() * 20) + 3;
      return Array.from({ length: sessionCount }, (_, i) => ({
        id: `session_${Date.now()}_${i}`,
        virtualKey: ['vk_prod_001', 'vk_dev_001', 'vk_test_001'][Math.floor(Math.random() * 3)],
        provider: ['openai', 'elevenlabs'][Math.floor(Math.random() * 2)],
        startTime: new Date(Date.now() - Math.random() * 2 * 60 * 60 * 1000), // up to 2 hours ago
        duration: Math.floor(Math.random() * 1800) + 60, // 1-30 minutes
        status: ['active', 'idle'][Math.floor(Math.random() * 2)],
        clientIp: `192.168.1.${Math.floor(Math.random() * 254) + 1}`,
        bandwidthUsed: Math.floor(Math.random() * 50) + 5, // MB
      }));
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
      query: any; 
      format: 'csv' | 'json' 
    }) => {
      // Mock export for now - replace with actual API call when backend endpoint exists
      const mockData = format === 'csv' 
        ? 'Date,VirtualKey,Model,Operation,Duration,Cost,Status\n2024-01-01,vk_prod_001,whisper-1,transcription,120,0.50,completed\n2024-01-01,vk_dev_001,tts-1,text-to-speech,45,0.25,completed'
        : JSON.stringify({
            records: [
              { date: '2024-01-01', virtualKey: 'vk_prod_001', model: 'whisper-1', operation: 'transcription', duration: 120, cost: 0.50, status: 'completed' },
              { date: '2024-01-01', virtualKey: 'vk_dev_001', model: 'tts-1', operation: 'text-to-speech', duration: 45, cost: 0.25, status: 'completed' }
            ]
          }, null, 2);
      
      return mockData;
    },
    onSuccess: (data, { format }) => {
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
    onError: (error: any) => {
      notifications.show({
        title: 'Export Failed',
        message: error.message || 'Failed to export audio usage data',
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
    onError: (error: any) => {
      notifications.show({
        title: 'Termination Failed',
        message: error.message || 'Failed to terminate session',
        color: 'red',
      });
    },
  });
}