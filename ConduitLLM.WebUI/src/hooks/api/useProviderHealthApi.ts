'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getAdminClient } from '@/lib/clients/conduit';
import { reportError } from '@/lib/utils/logging';
import { HealthStatusMap } from '@/types/sdk-extensions';

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
        
        // Get provider health data from SDK
        const healthSummary = await client.providerHealth.getHealthSummary();
        
        // Transform SDK health data to match our interface
        const providerHealth: ProviderHealth[] = await Promise.all(
          healthSummary.providers.map(async (provider) => {
            try {
              // Get detailed health status for each provider
              const detailedStatus = await client.providerHealth.getProviderHealthStatus(provider.providerName);
              
              // Determine status based on health data
              let status: 'healthy' | 'degraded' | 'down' | 'maintenance' = 'healthy';
              if (!provider.isHealthy) {
                if (provider.consecutiveFailures > 5) {
                  status = 'down';
                } else if (provider.consecutiveFailures > 2) {
                  status = 'degraded';
                }
              }
              
              return {
                providerId: provider.providerName.toLowerCase().replace(/\s+/g, '-'),
                providerName: provider.providerName,
                status,
                lastChecked: detailedStatus.lastCheckTime || new Date().toISOString(),
                responseTime: detailedStatus.averageResponseTimeMs || 0,
                uptime: detailedStatus.uptime || 0,
                availability: provider.isHealthy ? 99.9 : ((1 - (detailedStatus.errorRate || 0) / 100) * 100),
                errorRate: detailedStatus.errorRate || 0,
                requestsPerMinute: 0, // Not available in SDK - would need metrics data
                activeModels: 0, // Not available in SDK
                totalModels: 0, // Not available in SDK
                region: 'Global', // Not available in SDK
                endpoint: 'Unknown', // Not available in SDK
                version: 'v1',
                capabilities: ['chat', 'completion'], // Default capabilities
                issues: !provider.isHealthy ? [{
                  id: `issue_${Date.now()}_${provider.providerName}`,
                  severity: provider.consecutiveFailures > 5 ? 'high' : 'medium' as 'low' | 'medium' | 'high' | 'critical',
                  message: `Provider health check failed (${provider.consecutiveFailures} consecutive failures)`,
                  timestamp: detailedStatus.lastFailureTime || new Date().toISOString(),
                  resolved: false,
                }] : [],
              };
            } catch (error) {
              reportError(error as Error, `Failed to get detailed status for ${provider.providerName}`);
              // Return basic info if detailed fetch fails
              return {
                providerId: provider.providerName.toLowerCase().replace(/\s+/g, '-'),
                providerName: provider.providerName,
                status: provider.isHealthy ? 'healthy' : 'down' as 'healthy' | 'degraded' | 'down' | 'maintenance',
                lastChecked: provider.lastCheckTime || new Date().toISOString(),
                responseTime: provider.averageResponseTimeMs || 0,
                uptime: provider.uptime || 0,
                availability: provider.isHealthy ? 99.9 : 0,
                errorRate: provider.errorRate || 0,
                requestsPerMinute: 0,
                activeModels: 0,
                totalModels: 0,
                region: 'Global',
                endpoint: 'Unknown',
                version: 'v1',
                capabilities: ['chat', 'completion'],
                issues: [],
              };
            }
          })
        );
        
        // If no providers found, return empty array
        return providerHealth.length > 0 ? providerHealth : [];
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
        
        // Get provider health summary and metrics
        const [healthSummary, systemMetrics] = await Promise.all([
          client.providerHealth.getHealthSummary(),
          client.metrics.getAllMetrics(),
        ]);
        
        // Calculate provider status from health data
        const totalProviders = healthSummary.totalProviders;
        const healthyProviders = healthSummary.healthyProviders;
        
        // Categorize unhealthy providers
        let degradedProviders = 0;
        let downProviders = 0;
        
        for (const provider of healthSummary.providers) {
          if (!provider.isHealthy) {
            if (provider.consecutiveFailures > 5) {
              downProviders++;
            } else {
              degradedProviders++;
            }
          }
        }
        
        const overall: 'operational' | 'degraded' | 'outage' | 'maintenance' = 
          downProviders > totalProviders * 0.5 ? 'outage' :
          downProviders > 0 || degradedProviders > 0 ? 'degraded' : 'operational';
        
        return {
          overall,
          totalProviders,
          healthyProviders,
          degradedProviders,
          downProviders,
          averageResponseTime: systemMetrics.metrics.requests.averageResponseTime,
          averageUptime: 99.0, // TODO: Calculate based on actual uptime data
          totalRequests: systemMetrics.metrics.requests.totalRequests,
          failedRequests: Math.floor(systemMetrics.metrics.requests.totalRequests * systemMetrics.metrics.requests.errorRate / 100),
          lastUpdated: new Date().toISOString(),
        };
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
        
        // Calculate date range
        const now = new Date();
        const hours = timeRange === '1h' ? 1 : 
                     timeRange === '24h' ? 24 :
                     timeRange === '7d' ? 24 * 7 : 24;
        const startDate = new Date(now.getTime() - hours * 60 * 60 * 1000);
        
        // Get request logs for this provider
        const requestLogs = await client.analytics.getRequestLogs({
          startDate: startDate.toISOString(),
          endDate: now.toISOString(),
          provider: providerId,
          pageSize: 1000,
        });
        
        // Get provider info
        const provider = await client.providers.getByName(providerId);
        
        // Group requests by hour
        const hourlyMetrics = new Map<string, {
          timestamp: string;
          requestCount: number;
          errorCount: number;
          totalResponseTime: number;
        }>();
        
        requestLogs.items.forEach(log => {
          const hour = new Date(log.timestamp);
          hour.setMinutes(0, 0, 0);
          const hourKey = hour.toISOString();
          
          const existing = hourlyMetrics.get(hourKey) || {
            timestamp: hourKey,
            requestCount: 0,
            errorCount: 0,
            totalResponseTime: 0,
          };
          
          existing.requestCount++;
          if (log.status === 'error') existing.errorCount++;
          existing.totalResponseTime += log.duration;
          
          hourlyMetrics.set(hourKey, existing);
        });
        
        // Transform to metrics format
        const metrics = Array.from(hourlyMetrics.values()).map(metric => ({
          timestamp: metric.timestamp,
          responseTime: metric.requestCount > 0 ? Math.round(metric.totalResponseTime / metric.requestCount) : 0,
          requestCount: metric.requestCount,
          errorCount: metric.errorCount,
          successRate: metric.requestCount > 0 ? Math.round(((metric.requestCount - metric.errorCount) / metric.requestCount) * 1000) / 10 : 100,
          throughput: Math.round(metric.requestCount / 60 * 10) / 10,
        }));
        
        // Fill in missing hours with zero data
        for (let i = 0; i < hours; i++) {
          const hour = new Date(now.getTime() - i * 60 * 60 * 1000);
          hour.setMinutes(0, 0, 0);
          const hourKey = hour.toISOString();
          
          if (!hourlyMetrics.has(hourKey)) {
            metrics.push({
              timestamp: hourKey,
              responseTime: 0,
              requestCount: 0,
              errorCount: 0,
              successRate: 100,
              throughput: 0,
            });
          }
        }
        
        // Sort by timestamp
        metrics.sort((a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime());
        
        return {
          providerId,
          providerName: provider.providerName,
          timeRange,
          metrics,
        };
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