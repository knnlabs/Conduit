import { SystemInfoDto } from '@knn_labs/conduit-admin-client';

export interface SystemMetric {
  name: string;
  value: string | number;
  unit?: string;
  status: 'healthy' | 'warning' | 'critical';
  description?: string;
}

export interface ServiceInfo {
  name: string;
  version: string;
  status: 'running' | 'stopped' | 'degraded';
  uptime?: string;
  port?: number;
  memory?: string;
  cpu?: string;
}

// Helper function to format uptime from seconds
export const formatUptime = (uptimeSeconds: number): string => {
  if (!uptimeSeconds) return 'Unknown';
  
  const days = Math.floor(uptimeSeconds / 86400);
  const hours = Math.floor((uptimeSeconds % 86400) / 3600);
  const minutes = Math.floor((uptimeSeconds % 3600) / 60);
  
  if (days > 0) {
    return `${days}d ${hours}h ${minutes}m`;
  } else if (hours > 0) {
    return `${hours}h ${minutes}m`;
  } else {
    return `${minutes}m`;
  }
};

// Generate system metrics from real data
export const generateSystemMetrics = (systemInfo: SystemInfoDto | null): SystemMetric[] => {
  const systemMetrics: SystemMetric[] = [];

  if (systemInfo?.database?.isConnected !== undefined) {
    systemMetrics.push({
      name: 'Database Status',
      value: systemInfo.database.isConnected ? 'Connected' : 'Disconnected',
      status: systemInfo.database.isConnected ? 'healthy' : 'critical',
      description: `Provider: ${systemInfo.database.provider ?? 'Unknown'}`
    });
  }

  return systemMetrics;
};

// Generate service information from real data
export const generateServiceInfo = (systemInfo: SystemInfoDto | null): ServiceInfo[] => {
  const services: ServiceInfo[] = [];
  
  if (systemInfo) {
    services.push({
      name: 'Conduit Core API',
      version: systemInfo.version ?? 'Unknown',
      status: 'running',
      uptime: formatUptime(systemInfo.uptime ?? 0)
    });
    
    if (systemInfo.database?.isConnected) {
      services.push({
        name: systemInfo.database.provider ?? 'Database',
        version: 'Unknown',
        status: systemInfo.database.isConnected ? 'running' : 'stopped'
      });
    }
  }

  return services;
};

export const getStatusIcon = (status: string) => {
  switch (status) {
    case 'running':
    case 'healthy':
    case 'latest':
      return 'circle-check';
    case 'degraded':
    case 'warning':
    case 'outdated':
      return 'alert-triangle';
    default:
      return 'alert-triangle';
  }
};

export const getStatusColor = (status: string): string => {
  switch (status) {
    case 'running':
    case 'healthy':
    case 'latest':
      return 'green';
    case 'degraded':
    case 'warning':
    case 'outdated':
      return 'orange';
    case 'stopped':
    case 'unhealthy':
    case 'error':
      return 'red';
    default:
      return 'gray';
  }
};