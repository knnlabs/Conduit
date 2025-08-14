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

export type ConditionOperator = 
  | 'equals' 
  | 'contains' 
  | 'greater_than' 
  | 'less_than'
  | 'between'
  | 'in_list'
  | 'regex'
  | 'exists';

export interface RoutingCondition {
  type: 'model' | 'header' | 'body' | 'time' | 'load' | 'key' | 'metadata' | 'cost' | 'region' | 'virtualKeyId';
  field?: string;
  operator: ConditionOperator;
  value: string | number | boolean | string[] | number[];
  logicalOperator?: 'AND' | 'OR';
}


export interface RoutingAction {
  type: 'route' | 'transform' | 'cache' | 'rate_limit' | 'log' | 'block';
  target?: string;
  parameters?: Record<string, unknown>;
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

export interface LoadBalancerHealth {
  status: 'healthy' | 'degraded' | 'unhealthy';
  nodes: LoadBalancerNode[];
  lastCheck: string;
  distribution: Record<string, number>;
}


export interface RouteTestRequest {
  method: 'GET' | 'POST' | 'PUT' | 'DELETE';
  path: string;
  headers?: Record<string, string>;
  body?: Record<string, unknown> | string;
  model?: string;
  metadata?: Record<string, unknown>;
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

// Enhanced Testing Interfaces
export interface TestRequest {
  model: string;
  region?: string;
  costThreshold?: number;
  virtualKeyId?: string;
  customFields: Record<string, unknown>;
  headers?: Record<string, string>;
  metadata?: Record<string, unknown>;
}

export interface ConditionMatch {
  condition: RoutingCondition;
  matched: boolean;
  actualValue: unknown;
  reason: string;
}

export interface MatchedRule {
  rule: RoutingRule;
  matchedConditions: ConditionMatch[];
  applied: boolean;
  priority: number;
}

export interface EvaluationStep {
  timestamp: number;
  stepNumber: number;
  action: string;
  details: string;
  success: boolean;
  duration: number;
  ruleId?: string;
  ruleName?: string;
}

export interface Provider {
  id: string;
  name: string;
  type: 'primary' | 'backup' | 'special';
  isEnabled: boolean;
  priority: number;
  endpoint?: string;
}

export interface TestResult {
  matchedRules: MatchedRule[];
  selectedProvider?: Provider;
  fallbackChain: Provider[];
  evaluationTime: number;
  evaluationSteps: EvaluationStep[];
  routingDecision: {
    strategy: string;
    reason: string;
    fallbackUsed: boolean;
    processingTimeMs: number;
  };
  success: boolean;
  errors?: string[];
}

export interface TestCase {
  id: string;
  name: string;
  request: TestRequest;
  expectedResult?: Partial<TestResult>;
  timestamp: string;
  description?: string;
}

export interface TestHistory {
  cases: TestCase[];
  lastRun?: string;
}