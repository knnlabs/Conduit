/**
 * Common type definitions to replace Record<string, any> usage
 * These types provide proper structure for various SDK operations
 */

/**
 * Feature flag evaluation context
 */
export interface FeatureFlagContext {
  /** User ID for user-specific flags */
  userId?: string;
  /** Virtual key ID for key-specific flags */
  keyId?: string;
  /** Environment (dev, staging, prod) */
  environment?: string;
  /** Custom attributes for evaluation */
  attributes?: {
    [key: string]: string | number | boolean | undefined;
  };
}

/**
 * Provider-specific settings for different providers
 */
export interface ProviderSettings {
  /** OpenAI specific settings */
  openai?: {
    organization?: string;
    apiVersion?: string;
    maxRetries?: number;
    timeout?: number;
  };
  /** Anthropic specific settings */
  anthropic?: {
    anthropicVersion?: string;
    maxTokensToSample?: number;
  };
  /** Azure OpenAI specific settings */
  azure?: {
    deploymentName?: string;
    apiVersion?: string;
    resourceName?: string;
  };
  /** Generic provider settings */
  [provider: string]: {
    [key: string]: string | number | boolean | undefined;
  } | undefined;
}

/**
 * Audio provider settings
 */
export interface AudioProviderSettings {
  /** Provider name */
  provider: string;
  /** Voice selection */
  defaultVoice?: string;
  /** Language preference */
  defaultLanguage?: string;
  /** Audio format settings */
  outputFormat?: 'mp3' | 'wav' | 'ogg' | 'flac';
  /** Sample rate */
  sampleRate?: number;
  /** Additional provider-specific settings */
  providerSpecific?: {
    [key: string]: string | number | boolean;
  };
}

/**
 * Query parameters for provider models
 */
export interface ModelQueryParams {
  /** Page number for pagination */
  page?: number;
  /** Items per page */
  pageSize?: number;
  /** Filter by provider */
  provider?: string;
  /** Filter by model type */
  modelType?: 'chat' | 'completion' | 'embedding' | 'audio' | 'image';
  /** Filter by active status */
  isActive?: boolean;
  /** Sort field */
  sortBy?: 'name' | 'provider' | 'created' | 'updated';
  /** Sort direction */
  sortDirection?: 'asc' | 'desc';
}

/**
 * Analytics query options
 */
export interface AnalyticsOptions {
  /** Include detailed breakdowns */
  includeDetails?: boolean;
  /** Aggregation level */
  aggregation?: 'hour' | 'day' | 'week' | 'month';
  /** Time zone for aggregation */
  timezone?: string;
  /** Include zero values */
  includeZeros?: boolean;
}

/**
 * System diagnostic check result
 */
export interface DiagnosticResult {
  /** Check status */
  status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
  /** Response time in milliseconds */
  responseTime?: number;
  /** Error message if unhealthy */
  error?: string;
  /** Additional details */
  details?: {
    [key: string]: string | number | boolean;
  };
}

/**
 * System diagnostic checks
 */
export interface DiagnosticChecks {
  /** Database health */
  database: DiagnosticResult;
  /** Cache health */
  cache: DiagnosticResult;
  /** Queue health */
  queue: DiagnosticResult;
  /** Storage health */
  storage: DiagnosticResult;
  /** Provider health checks */
  providers?: {
    [provider: string]: DiagnosticResult;
  };
}

/**
 * Session metadata for authentication
 */
export interface SessionMetadata {
  /** Login timestamp */
  loginTime?: string;
  /** Last activity timestamp */
  lastActivity?: string;
  /** User agent string */
  userAgent?: string;
  /** IP address */
  ipAddress?: string;
  /** Session source */
  source?: 'web' | 'api' | 'cli';
  /** Additional session attributes */
  attributes?: {
    [key: string]: string | number | boolean;
  };
}

/**
 * Monitoring field definitions
 */
export interface MonitoringFields {
  /** Request ID */
  requestId?: string;
  /** Correlation ID */
  correlationId?: string;
  /** User ID */
  userId?: string;
  /** Virtual key ID */
  keyId?: string;
  /** Provider name */
  provider?: string;
  /** Model name */
  model?: string;
  /** Response time */
  responseTime?: number;
  /** Token usage */
  tokens?: {
    prompt?: number;
    completion?: number;
    total?: number;
  };
  /** Custom fields */
  custom?: {
    [key: string]: string | number | boolean;
  };
}

/**
 * Export destination configuration
 */
export interface ExportDestinationConfig {
  /** S3 configuration */
  s3?: {
    bucket: string;
    region: string;
    prefix?: string;
    accessKeyId?: string;
    secretAccessKey?: string;
    serverSideEncryption?: boolean;
  };
  /** Email configuration */
  email?: {
    recipients: string[];
    subject?: string;
    body?: string;
    attachmentFormat?: 'csv' | 'json' | 'zip';
  };
  /** Webhook configuration */
  webhook?: {
    url: string;
    headers?: { [key: string]: string };
    method?: 'POST' | 'PUT';
    retryCount?: number;
    timeoutSeconds?: number;
  };
}

/**
 * Provider health details
 */
export interface HealthCheckDetails {
  /** Last successful check timestamp */
  lastSuccessAt?: string;
  /** Last failure timestamp */
  lastFailureAt?: string;
  /** Consecutive failure count */
  consecutiveFailures?: number;
  /** Average response time */
  avgResponseTime?: number;
  /** Error messages */
  recentErrors?: string[];
  /** Additional metrics */
  metrics?: {
    [key: string]: number;
  };
}

/**
 * Security event details for different event types
 */
export interface SecurityEventDetails {
  /** Authentication failure details */
  authFailure?: {
    attemptedUsername?: string;
    reason: string;
    attemptCount?: number;
    sourceIp?: string;
  };
  /** Rate limit violation details */
  rateLimit?: {
    limit: number;
    windowSeconds: number;
    currentUsage: number;
    resetAt?: string;
  };
  /** Access violation details */
  accessViolation?: {
    resource: string;
    action: string;
    reason: string;
  };
  /** Generic details */
  [eventType: string]: {
    [key: string]: string | number | boolean | undefined;
  } | undefined;
}

/**
 * Security change record
 */
export interface SecurityChangeRecord {
  /** Changed field name */
  field: string;
  /** Previous value */
  oldValue?: string | number | boolean | null;
  /** New value */
  newValue?: string | number | boolean | null;
  /** Change timestamp */
  changedAt: string;
  /** User who made the change */
  changedBy: string;
  /** Change reason */
  reason?: string;
}

/**
 * System parameters for various operations
 */
export interface SystemParameters {
  /** Cache parameters */
  cache?: {
    ttl?: number;
    maxSize?: number;
    evictionPolicy?: string;
  };
  /** Queue parameters */
  queue?: {
    maxRetries?: number;
    retryDelay?: number;
    priority?: number;
  };
  /** Rate limit parameters */
  rateLimit?: {
    requests?: number;
    windowSeconds?: number;
    burstSize?: number;
  };
  /** Generic parameters */
  [category: string]: {
    [key: string]: string | number | boolean | undefined;
  } | undefined;
}