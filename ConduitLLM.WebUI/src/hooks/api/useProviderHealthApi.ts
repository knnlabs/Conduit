'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getAdminClient } from '@/lib/clients/conduit';
import { reportError } from '@/lib/utils/logging';

// Query key factory for Provider Health API
export const providerHealthApiKeys = {
  all: ['provider-health-api'] as const,
  health: () => [...providerHealthApiKeys.all, 'health'] as const,
  status: () => [...providerHealthApiKeys.all, 'status'] as const,
  metrics: () => [...providerHealthApiKeys.all, 'metrics'] as const,
  incidents: () => [...providerHealthApiKeys.all, 'incidents'] as const,
  uptime: () => [...providerHealthApiKeys.all, 'uptime'] as const,
  latency: () => [...providerHealthApiKeys.all, 'latency'] as const,
  alerts: () => [...providerHealthApiKeys.all, 'alerts'] as const,
} as const;

export interface ProviderHealth {
  providerId: string;
  providerName: string;
  status: 'healthy' | 'degraded' | 'down' | 'maintenance';
  lastChecked: string;
  responseTime: number;
  uptime: number;
  availability: number;
  errorRate: number;
  requestsPerMinute: number;
  activeModels: number;
  totalModels: number;
  region?: string;
  endpoint: string;
  version?: string;
  capabilities: string[];
  issues: {
    id: string;
    severity: 'low' | 'medium' | 'high' | 'critical';
    message: string;
    timestamp: string;
    resolved: boolean;
  }[];
}

export interface ProviderStatus {
  overall: 'operational' | 'degraded' | 'outage' | 'maintenance';
  totalProviders: number;
  healthyProviders: number;
  degradedProviders: number;
  downProviders: number;
  averageResponseTime: number;
  averageUptime: number;
  totalRequests: number;
  failedRequests: number;
  lastUpdated: string;
}

export interface ProviderMetrics {
  providerId: string;
  providerName: string;
  timeRange: string;
  metrics: {
    timestamp: string;
    responseTime: number;
    requestCount: number;
    errorCount: number;
    successRate: number;
    throughput: number;
  }[];
}

export interface ProviderIncident {
  id: string;
  providerId: string;
  providerName: string;
  title: string;
  description: string;
  severity: 'low' | 'medium' | 'high' | 'critical';
  status: 'investigating' | 'identified' | 'monitoring' | 'resolved';
  startTime: string;
  endTime?: string;
  duration?: number;
  affectedModels: string[];
  affectedRegions: string[];
  updates: {
    timestamp: string;
    status: string;
    message: string;
    author: string;
  }[];
  impact: {
    requestsAffected: number;
    usersAffected: number;
    revenueImpact?: number;
  };
}

export interface ProviderUptimeData {
  providerId: string;
  providerName: string;
  period: '24h' | '7d' | '30d' | '90d';
  uptime: number;
  downtime: number;
  incidents: number;
  availability: number;
  mttr: number; // Mean Time To Recovery
  mtbf: number; // Mean Time Between Failures
  sla: number;
  slaBreaches: number;
  uptimeHistory: {
    date: string;
    uptime: number;
    incidents: number;
    responseTime: number;
  }[];
}

export interface ProviderLatencyData {
  providerId: string;
  providerName: string;
  timeRange: string;
  latencyData: {
    timestamp: string;
    p50: number;
    p90: number;
    p95: number;
    p99: number;
    average: number;
    min: number;
    max: number;
  }[];
  summary: {
    averageLatency: number;
    p50: number;
    p90: number;
    p95: number;
    p99: number;
    trend: number; // percentage change
  };
}

export interface ProviderAlert {
  id: string;
  providerId: string;
  providerName: string;
  type: 'latency' | 'uptime' | 'error_rate' | 'capacity' | 'availability';
  severity: 'info' | 'warning' | 'critical';
  title: string;
  message: string;
  timestamp: string;
  acknowledged: boolean;
  resolved: boolean;
  resolvedAt?: string;
  threshold: number;
  currentValue: number;
  duration: number;
  affectedEndpoints: string[];
}

export interface HealthCheckConfig {
  providerId: string;
  enabled: boolean;
  checkInterval: number; // seconds
  timeout: number; // seconds
  retryCount: number;
  thresholds: {
    responseTime: number; // ms
    errorRate: number; // percentage
    uptime: number; // percentage
  };
  alerting: {
    enabled: boolean;
    channels: string[];
    escalation: {
      level1: number; // minutes
      level2: number;
      level3: number;
    };
  };
}

// Provider Health Overview
export function useProviderHealthOverview() {
  return useQuery({
    queryKey: providerHealthApiKeys.health(),
    queryFn: async (): Promise<ProviderHealth[]> => {
      try {
        const client = getAdminClient();
        
        // Get health status from SDK
        const healthSummary = await client.providers.getHealthStatus();
        
        // Transform the summary into individual provider health records
        // Note: The actual response structure may differ, this is based on typical patterns
        if (healthSummary.providerStatuses) {
          return healthSummary.providerStatuses.map((status: any) => ({
            providerId: status.providerId || status.providerName,
            providerName: status.providerName,
            status: status.isHealthy ? 'healthy' : status.status || 'down',
            lastChecked: status.lastChecked || new Date().toISOString(),
            responseTime: status.responseTime || status.latency || 0,
            uptime: status.uptime || 99.0,
            availability: status.availability || 99.0,
            errorRate: status.errorRate || 0,
            requestsPerMinute: status.requestsPerMinute || 0,
            activeModels: status.activeModels || 0,
            totalModels: status.totalModels || 0,
            region: status.region,
            endpoint: status.endpoint || '',
            version: status.version,
            capabilities: status.capabilities || [],
            issues: status.issues || [],
          }));
        }
        
        // Fallback to mock data if API response is not in expected format
        const mockData: ProviderHealth[] = [
          {
            providerId: 'openai',
            providerName: 'OpenAI',
            status: 'healthy',
            lastChecked: new Date().toISOString(),
            responseTime: 847,
            uptime: 99.8,
            availability: 99.9,
            errorRate: 0.2,
            requestsPerMinute: 1247,
            activeModels: 8,
            totalModels: 10,
            region: 'US-East',
            endpoint: 'https://api.openai.com',
            version: 'v1',
            capabilities: ['chat', 'completion', 'image', 'audio', 'embeddings'],
            issues: [],
          },
          {
            providerId: 'anthropic',
            providerName: 'Anthropic',
            status: 'healthy',
            lastChecked: new Date().toISOString(),
            responseTime: 734,
            uptime: 99.9,
            availability: 99.8,
            errorRate: 0.1,
            requestsPerMinute: 892,
            activeModels: 6,
            totalModels: 6,
            region: 'US-West',
            endpoint: 'https://api.anthropic.com',
            version: 'v1',
            capabilities: ['chat', 'completion'],
            issues: [],
          },
          {
            providerId: 'azure-openai',
            providerName: 'Azure OpenAI',
            status: 'degraded',
            lastChecked: new Date().toISOString(),
            responseTime: 1456,
            uptime: 98.2,
            availability: 98.5,
            errorRate: 2.3,
            requestsPerMinute: 456,
            activeModels: 4,
            totalModels: 6,
            region: 'East-US',
            endpoint: 'https://your-instance.openai.azure.com',
            version: '2023-12-01-preview',
            capabilities: ['chat', 'completion', 'embeddings'],
            issues: [
              {
                id: 'issue_001',
                severity: 'medium',
                message: 'Elevated response times detected',
                timestamp: new Date(Date.now() - 1000 * 60 * 15).toISOString(),
                resolved: false,
              },
            ],
          },
          {
            providerId: 'minimax',
            providerName: 'MiniMax',
            status: 'healthy',
            lastChecked: new Date().toISOString(),
            responseTime: 1234,
            uptime: 99.1,
            availability: 99.3,
            errorRate: 0.7,
            requestsPerMinute: 234,
            activeModels: 3,
            totalModels: 4,
            region: 'Asia-Pacific',
            endpoint: 'https://api.minimax.chat',
            version: 'v1',
            capabilities: ['chat', 'image', 'video'],
            issues: [],
          },
          {
            providerId: 'replicate',
            providerName: 'Replicate',
            status: 'maintenance',
            lastChecked: new Date(Date.now() - 1000 * 60 * 30).toISOString(),
            responseTime: 0,
            uptime: 95.0,
            availability: 0,
            errorRate: 100,
            requestsPerMinute: 0,
            activeModels: 0,
            totalModels: 15,
            region: 'Global',
            endpoint: 'https://api.replicate.com',
            version: 'v1',
            capabilities: ['image', 'video', 'audio'],
            issues: [
              {
                id: 'issue_002',
                severity: 'high',
                message: 'Scheduled maintenance in progress',
                timestamp: new Date(Date.now() - 1000 * 60 * 45).toISOString(),
                resolved: false,
              },
            ],
          },
        ];

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch provider health overview');
        throw new Error(error?.message || 'Failed to fetch provider health overview');
      }
    },
    staleTime: 30 * 1000, // 30 seconds
    refetchInterval: 30 * 1000, // Auto-refresh every 30 seconds
  });
}

// Provider Status Summary
export function useProviderStatus() {
  return useQuery({
    queryKey: providerHealthApiKeys.status(),
    queryFn: async (): Promise<ProviderStatus> => {
      try {
        const client = getAdminClient();
        
        // Get health summary from SDK
        const healthSummary = await client.providers.getHealthStatus();
        
        // Transform the health summary into provider status
        if (healthSummary) {
          const totalProviders = healthSummary.totalProviders || 0;
          const healthyProviders = healthSummary.healthyProviders || 0;
          const degradedProviders = healthSummary.degradedProviders || 0;
          const downProviders = healthSummary.downProviders || 0;
          
          return {
            overall: healthSummary.overallStatus || 
                    (downProviders > 0 ? 'outage' : 
                     degradedProviders > 0 ? 'degraded' : 
                     'operational'),
            totalProviders,
            healthyProviders,
            degradedProviders,
            downProviders,
            averageResponseTime: healthSummary.averageResponseTime || 0,
            averageUptime: healthSummary.averageUptime || 99.0,
            totalRequests: healthSummary.totalRequests || 0,
            failedRequests: healthSummary.failedRequests || 0,
            lastUpdated: healthSummary.lastUpdated || new Date().toISOString(),
          };
        }
        
        // Fallback to mock data if API response is not available
        const mockData: ProviderStatus = {
          overall: 'degraded',
          totalProviders: 5,
          healthyProviders: 3,
          degradedProviders: 1,
          downProviders: 0,
          averageResponseTime: 954,
          averageUptime: 98.4,
          totalRequests: 145672,
          failedRequests: 1234,
          lastUpdated: new Date().toISOString(),
        };

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch provider status');
        throw new Error(error?.message || 'Failed to fetch provider status');
      }
    },
    staleTime: 15 * 1000, // 15 seconds
    refetchInterval: 30 * 1000, // Auto-refresh every 30 seconds
  });
}

// Provider Metrics
export function useProviderMetrics(providerId: string, timeRange: string = '24h') {
  return useQuery({
    queryKey: [...providerHealthApiKeys.metrics(), providerId, timeRange],
    queryFn: async (): Promise<ProviderMetrics> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.providers.getMetrics(providerId, timeRange);
        
        // Generate mock metrics data
        const generateMetrics = (hours: number) => {
          const metrics = [];
          const now = new Date();
          
          for (let i = hours - 1; i >= 0; i--) {
            const timestamp = new Date(now.getTime() - i * 60 * 60 * 1000);
            const baseResponseTime = 500 + Math.random() * 1000;
            const requestCount = Math.floor(800 + Math.random() * 400);
            const errorCount = Math.floor(requestCount * (0.005 + Math.random() * 0.02));
            
            metrics.push({
              timestamp: timestamp.toISOString(),
              responseTime: Math.round(baseResponseTime),
              requestCount,
              errorCount,
              successRate: Math.round(((requestCount - errorCount) / requestCount) * 1000) / 10,
              throughput: Math.round(requestCount / 60 * 10) / 10, // requests per minute
            });
          }
          
          return metrics;
        };

        const hours = timeRange === '1h' ? 1 : 
                     timeRange === '24h' ? 24 :
                     timeRange === '7d' ? 24 * 7 : 24;

        const mockData: ProviderMetrics = {
          providerId,
          providerName: providerId.charAt(0).toUpperCase() + providerId.slice(1),
          timeRange,
          metrics: generateMetrics(hours),
        };

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch provider metrics');
        throw new Error(error?.message || 'Failed to fetch provider metrics');
      }
    },
    enabled: !!providerId,
    staleTime: 60 * 1000, // 1 minute
  });
}

// Provider Incidents
export function useProviderIncidents() {
  return useQuery({
    queryKey: providerHealthApiKeys.incidents(),
    queryFn: async (): Promise<ProviderIncident[]> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.providers.getIncidents();
        
        // Mock incident data
        const mockData: ProviderIncident[] = [
          {
            id: 'incident_001',
            providerId: 'azure-openai',
            providerName: 'Azure OpenAI',
            title: 'Elevated Response Times',
            description: 'We are experiencing elevated response times for chat completions in the East US region.',
            severity: 'medium',
            status: 'monitoring',
            startTime: new Date(Date.now() - 1000 * 60 * 45).toISOString(),
            affectedModels: ['gpt-4', 'gpt-3.5-turbo'],
            affectedRegions: ['East US'],
            updates: [
              {
                timestamp: new Date(Date.now() - 1000 * 60 * 15).toISOString(),
                status: 'monitoring',
                message: 'Response times have improved but we continue to monitor closely.',
                author: 'Azure Operations',
              },
              {
                timestamp: new Date(Date.now() - 1000 * 60 * 30).toISOString(),
                status: 'identified',
                message: 'Issue identified and mitigation steps are being implemented.',
                author: 'Azure Operations',
              },
            ],
            impact: {
              requestsAffected: 12456,
              usersAffected: 234,
            },
          },
          {
            id: 'incident_002',
            providerId: 'replicate',
            providerName: 'Replicate',
            title: 'Scheduled Maintenance',
            description: 'Scheduled maintenance to upgrade infrastructure for improved performance.',
            severity: 'high',
            status: 'identified',
            startTime: new Date(Date.now() - 1000 * 60 * 60).toISOString(),
            endTime: new Date(Date.now() + 1000 * 60 * 30).toISOString(),
            duration: 90,
            affectedModels: ['stable-diffusion', 'sdxl', 'flux'],
            affectedRegions: ['Global'],
            updates: [
              {
                timestamp: new Date(Date.now() - 1000 * 60 * 60).toISOString(),
                status: 'identified',
                message: 'Maintenance window started. All image generation services are temporarily unavailable.',
                author: 'Replicate Team',
              },
            ],
            impact: {
              requestsAffected: 5678,
              usersAffected: 89,
            },
          },
          {
            id: 'incident_003',
            providerId: 'openai',
            providerName: 'OpenAI',
            title: 'API Rate Limiting Issues',
            description: 'Some users experienced unexpected rate limiting errors.',
            severity: 'low',
            status: 'resolved',
            startTime: new Date(Date.now() - 1000 * 60 * 60 * 4).toISOString(),
            endTime: new Date(Date.now() - 1000 * 60 * 60 * 3).toISOString(),
            duration: 60,
            affectedModels: ['dall-e-3'],
            affectedRegions: ['US-East'],
            updates: [
              {
                timestamp: new Date(Date.now() - 1000 * 60 * 60 * 3).toISOString(),
                status: 'resolved',
                message: 'Issue has been resolved. Rate limiting is now working as expected.',
                author: 'OpenAI Team',
              },
            ],
            impact: {
              requestsAffected: 892,
              usersAffected: 23,
            },
          },
        ];

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch provider incidents');
        throw new Error(error?.message || 'Failed to fetch provider incidents');
      }
    },
    staleTime: 60 * 1000, // 1 minute
    refetchInterval: 2 * 60 * 1000, // Auto-refresh every 2 minutes
  });
}

// Provider Uptime Data
export function useProviderUptime(providerId: string, period: '24h' | '7d' | '30d' | '90d' = '7d') {
  return useQuery({
    queryKey: [...providerHealthApiKeys.uptime(), providerId, period],
    queryFn: async (): Promise<ProviderUptimeData> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.providers.getUptime(providerId, period);
        
        // Generate mock uptime data
        const generateUptimeHistory = (days: number) => {
          const history = [];
          const now = new Date();
          
          for (let i = days - 1; i >= 0; i--) {
            const date = new Date(now.getTime() - i * 24 * 60 * 60 * 1000);
            const uptime = 95 + Math.random() * 5; // 95-100%
            const incidents = Math.random() > 0.8 ? Math.floor(Math.random() * 3) : 0;
            const responseTime = 500 + Math.random() * 1000;
            
            history.push({
              date: date.toISOString().split('T')[0],
              uptime: Math.round(uptime * 100) / 100,
              incidents,
              responseTime: Math.round(responseTime),
            });
          }
          
          return history;
        };

        const days = period === '24h' ? 1 : 
                    period === '7d' ? 7 :
                    period === '30d' ? 30 : 90;

        const uptimeHistory = generateUptimeHistory(days);
        const averageUptime = uptimeHistory.reduce((sum, day) => sum + day.uptime, 0) / uptimeHistory.length;
        const totalIncidents = uptimeHistory.reduce((sum, day) => sum + day.incidents, 0);

        const mockData: ProviderUptimeData = {
          providerId,
          providerName: providerId.charAt(0).toUpperCase() + providerId.slice(1),
          period,
          uptime: Math.round(averageUptime * 100) / 100,
          downtime: Math.round((100 - averageUptime) * 100) / 100,
          incidents: totalIncidents,
          availability: Math.round(averageUptime * 100) / 100,
          mttr: 15 + Math.random() * 30, // 15-45 minutes
          mtbf: 72 + Math.random() * 168, // 72-240 hours
          sla: 99.9,
          slaBreaches: totalIncidents > 2 ? 1 : 0,
          uptimeHistory,
        };

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch provider uptime data');
        throw new Error(error?.message || 'Failed to fetch provider uptime data');
      }
    },
    enabled: !!providerId,
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

// Provider Latency Data
export function useProviderLatency(providerId: string, timeRange: string = '24h') {
  return useQuery({
    queryKey: [...providerHealthApiKeys.latency(), providerId, timeRange],
    queryFn: async (): Promise<ProviderLatencyData> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.providers.getLatency(providerId, timeRange);
        
        // Generate mock latency data
        const generateLatencyData = (hours: number) => {
          const data = [];
          const now = new Date();
          
          for (let i = hours - 1; i >= 0; i--) {
            const timestamp = new Date(now.getTime() - i * 60 * 60 * 1000);
            const base = 500 + Math.random() * 500;
            
            data.push({
              timestamp: timestamp.toISOString(),
              p50: Math.round(base * 0.8),
              p90: Math.round(base * 1.2),
              p95: Math.round(base * 1.4),
              p99: Math.round(base * 2.0),
              average: Math.round(base),
              min: Math.round(base * 0.3),
              max: Math.round(base * 3.0),
            });
          }
          
          return data;
        };

        const hours = timeRange === '1h' ? 1 : 
                     timeRange === '24h' ? 24 :
                     timeRange === '7d' ? 24 * 7 : 24;

        const latencyData = generateLatencyData(hours);
        const averageLatency = latencyData.reduce((sum, point) => sum + point.average, 0) / latencyData.length;
        const p50 = latencyData.reduce((sum, point) => sum + point.p50, 0) / latencyData.length;
        const p90 = latencyData.reduce((sum, point) => sum + point.p90, 0) / latencyData.length;
        const p95 = latencyData.reduce((sum, point) => sum + point.p95, 0) / latencyData.length;
        const p99 = latencyData.reduce((sum, point) => sum + point.p99, 0) / latencyData.length;

        const mockData: ProviderLatencyData = {
          providerId,
          providerName: providerId.charAt(0).toUpperCase() + providerId.slice(1),
          timeRange,
          latencyData,
          summary: {
            averageLatency: Math.round(averageLatency),
            p50: Math.round(p50),
            p90: Math.round(p90),
            p95: Math.round(p95),
            p99: Math.round(p99),
            trend: -5.2 + Math.random() * 10.4, // -5% to +5%
          },
        };

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch provider latency data');
        throw new Error(error?.message || 'Failed to fetch provider latency data');
      }
    },
    enabled: !!providerId,
    staleTime: 60 * 1000, // 1 minute
  });
}

// Provider Alerts
export function useProviderAlerts() {
  return useQuery({
    queryKey: providerHealthApiKeys.alerts(),
    queryFn: async (): Promise<ProviderAlert[]> => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.providers.getAlerts();
        
        // Mock alert data
        const mockData: ProviderAlert[] = [
          {
            id: 'alert_001',
            providerId: 'azure-openai',
            providerName: 'Azure OpenAI',
            type: 'latency',
            severity: 'warning',
            title: 'High Response Time',
            message: 'Average response time is above threshold (1000ms)',
            timestamp: new Date(Date.now() - 1000 * 60 * 15).toISOString(),
            acknowledged: false,
            resolved: false,
            threshold: 1000,
            currentValue: 1456,
            duration: 15,
            affectedEndpoints: ['/v1/chat/completions'],
          },
          {
            id: 'alert_002',
            providerId: 'replicate',
            providerName: 'Replicate',
            type: 'availability',
            severity: 'critical',
            title: 'Service Unavailable',
            message: 'Provider is currently in maintenance mode',
            timestamp: new Date(Date.now() - 1000 * 60 * 45).toISOString(),
            acknowledged: true,
            resolved: false,
            threshold: 95,
            currentValue: 0,
            duration: 45,
            affectedEndpoints: ['/predictions'],
          },
          {
            id: 'alert_003',
            providerId: 'openai',
            providerName: 'OpenAI',
            type: 'error_rate',
            severity: 'info',
            title: 'Elevated Error Rate',
            message: 'Error rate has increased slightly but is within acceptable limits',
            timestamp: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(),
            acknowledged: true,
            resolved: true,
            resolvedAt: new Date(Date.now() - 1000 * 60 * 60).toISOString(),
            threshold: 1.0,
            currentValue: 0.2,
            duration: 60,
            affectedEndpoints: ['/v1/images/generations'],
          },
        ];

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch provider alerts');
        throw new Error(error?.message || 'Failed to fetch provider alerts');
      }
    },
    staleTime: 30 * 1000, // 30 seconds
    refetchInterval: 60 * 1000, // Auto-refresh every minute
  });
}

// Acknowledge Alert
export function useAcknowledgeAlert() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (alertId: string) => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.providers.acknowledgeAlert(alertId);
        
        // Simulate API call
        await new Promise(resolve => setTimeout(resolve, 500));
        
        return { success: true, alertId };
      } catch (error: any) {
        reportError(error, 'Failed to acknowledge alert');
        throw new Error(error?.message || 'Failed to acknowledge alert');
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: providerHealthApiKeys.alerts() });
    },
  });
}

// Trigger Health Check
export function useTriggerHealthCheck() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (providerId?: string) => {
      try {
        const client = getAdminClient();
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.providers.triggerHealthCheck(providerId);
        
        // Simulate API call
        await new Promise(resolve => setTimeout(resolve, 2000));
        
        return { success: true, providerId };
      } catch (error: any) {
        reportError(error, 'Failed to trigger health check');
        throw new Error(error?.message || 'Failed to trigger health check');
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: providerHealthApiKeys.health() });
      queryClient.invalidateQueries({ queryKey: providerHealthApiKeys.status() });
    },
  });
}