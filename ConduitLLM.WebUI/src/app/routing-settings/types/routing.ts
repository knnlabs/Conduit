export interface RoutingRule {
  id: string;
  name: string;
  description?: string;
  priority: number;
  isEnabled: boolean;
  conditions: RoutingCondition[];
  actions: RoutingAction[];
  stats?: {
    matchCount: number;
    lastMatched?: string;
  };
  createdAt: string;
  updatedAt: string;
}

export interface RoutingCondition {
  type: 'model' | 'header' | 'body' | 'time' | 'load' | 'key' | 'metadata' | 'cost' | 'region' | 'virtualKeyId';
  field?: string;
  operator: ConditionOperator;
  value: any;
  logicalOperator?: 'AND' | 'OR';
}

export type ConditionOperator = 
  | 'equals' 
  | 'contains' 
  | 'greater_than' 
  | 'less_than'
  | 'between'
  | 'in_list'
  | 'regex'
  | 'exists';

export interface RoutingAction {
  type: 'route' | 'transform' | 'cache' | 'rate_limit' | 'log' | 'block';
  target?: string;
  parameters?: Record<string, any>;
}

export interface ProviderPriority {
  providerId: string;
  providerName: string;
  priority: number;
  weight?: number;
  isEnabled: boolean;
}

export interface RoutingConfiguration {
  defaultStrategy: 'round_robin' | 'least_latency' | 'cost_optimized' | 'priority';
  fallbackEnabled: boolean;
  retryPolicy: {
    maxAttempts: number;
    initialDelayMs: number;
    maxDelayMs: number;
    backoffMultiplier: number;
    retryableStatuses: number[];
  };
  timeoutMs: number;
  maxConcurrentRequests: number;
}

export interface LoadBalancerHealth {
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

export interface RouteTestRequest {
  method: 'GET' | 'POST' | 'PUT' | 'DELETE';
  path: string;
  headers?: Record<string, string>;
  body?: any;
  model?: string;
  metadata?: Record<string, any>;
}

export interface RouteTestResult {
  success: boolean;
  matchedRules: RoutingRule[];
  selectedProvider?: string;
  routingDecision: {
    strategy: string;
    reason: string;
    fallbackUsed: boolean;
    processingTimeMs: number;
  };
  errors?: string[];
}

export interface CreateRoutingRuleRequest {
  name: string;
  description?: string;
  priority?: number;
  conditions: Omit<RoutingCondition, 'logicalOperator'>[];
  actions: RoutingAction[];
  enabled?: boolean;
}

export interface UpdateRoutingRuleRequest {
  name?: string;
  description?: string;
  priority?: number;
  conditions?: RoutingCondition[];
  actions?: RoutingAction[];
  enabled?: boolean;
}