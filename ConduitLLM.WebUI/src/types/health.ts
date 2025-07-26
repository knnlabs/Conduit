// Health check types for the UI
export interface HealthCheckData {
  // Flexible structure for additional health check data
  [key: string]: string | number | boolean | HealthCheckData | HealthCheckData[];
}

export interface HealthCheckDetail {
  status: 'healthy' | 'degraded' | 'unhealthy';
  description?: string;
  duration?: number;
  error?: string;
  data?: HealthCheckData;
}