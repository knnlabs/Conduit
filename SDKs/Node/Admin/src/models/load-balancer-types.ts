// Load Balancing types
export interface LoadBalancerConfigDto {
  algorithm: 'round_robin' | 'weighted_round_robin' | 'least_connections' | 'ip_hash' | 'random';
  healthCheck: {
    enabled: boolean;
    intervalSeconds: number;
    timeoutSeconds: number;
    unhealthyThreshold: number;
    healthyThreshold: number;
  };
  weights?: Record<string, number>;
  stickySession?: {
    enabled: boolean;
    cookieName: string;
    ttlSeconds: number;
  };
}

export interface UpdateLoadBalancerConfigDto {
  algorithm?: 'round_robin' | 'weighted_round_robin' | 'least_connections' | 'ip_hash' | 'random';
  healthCheck?: Partial<LoadBalancerConfigDto['healthCheck']>;
  weights?: Record<string, number>;
  stickySession?: Partial<LoadBalancerConfigDto['stickySession']>;
}

export interface LoadBalancerHealthDto {
  status: 'healthy' | 'degraded' | 'unhealthy';
  nodes: LoadBalancerNode[];
  lastCheck: string;
  distribution: Record<string, number>;
}

export interface LoadBalancerNode {
  id: string;
  endpoint: string;
  status: 'healthy' | 'unhealthy' | 'draining';
  weight: number;
  activeConnections: number;
  totalRequests: number;
  avgResponseTime: number;
  lastHealthCheck: string;
}