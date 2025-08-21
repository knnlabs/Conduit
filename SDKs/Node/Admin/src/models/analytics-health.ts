export interface ServiceHealthMetrics {
  name: string;
  status: 'healthy' | 'degraded' | 'unhealthy';
  uptime: number; // percentage
  responseTime: number; // ms
  errorRate: number;
  lastChecked: string;
}

export interface QueueMetrics {
  name: string;
  size: number;
  processing: number;
  failed: number;
  throughput: number; // messages per second
}

export interface DatabaseMetrics {
  connections: {
    active: number;
    idle: number;
    total: number;
  };
  queryPerformance: {
    averageTime: number;
    slowQueries: number;
  };
  size: number; // bytes
}

export interface SystemAlert {
  id: string;
  severity: 'info' | 'warning' | 'error' | 'critical';
  message: string;
  timestamp: string;
  service?: string;
}

export interface ProviderHealthDetails {
  provider: string;
  status: 'healthy' | 'degraded' | 'unhealthy';
  uptime: number; // percentage
  averageLatency: number; // ms
  errorRate: number; // percentage
  lastChecked: string;
  endpoints: EndpointHealth[];
  history?: HealthHistoryPoint[];
}

export interface EndpointHealth {
  endpoint: string;
  status: 'healthy' | 'degraded' | 'unhealthy';
  responseTime: number;
  successRate: number;
  lastError?: string;
}

export interface HealthHistoryPoint {
  timestamp: string;
  status: 'healthy' | 'degraded' | 'unhealthy';
  uptime: number;
  errorRate: number;
}

export interface ProviderIncident {
  id: string;
  provider: string;
  severity: 'low' | 'medium' | 'high' | 'critical';
  status: 'active' | 'resolved';
  startTime: string;
  endTime?: string;
  description: string;
  impact: string;
}