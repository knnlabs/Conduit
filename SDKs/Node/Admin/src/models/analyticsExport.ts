/**
 * Analytics export-related models for the Admin SDK
 */

import type { ExportDestinationConfig, ExtendedMetadata } from './common-types';
import { ProviderType } from './providerType';

/**
 * Base export parameters common to all export types
 */
export interface ExportParams {
  /** Export format */
  format: 'json' | 'csv' | 'excel';
  
  /** Date range for the export */
  dateRange: {
    startDate: string;
    endDate: string;
  };
  
  /** Whether to compress the export file */
  compression?: boolean;
  
  /** Whether to include metadata in the export */
  includeMetadata?: boolean;
}

/**
 * Parameters for exporting usage analytics
 */
export interface ExportUsageParams extends ExportParams {
  /** Group results by this field */
  groupBy?: 'virtualKey' | 'model' | 'provider' | 'day' | 'hour';
  
  /** Filter by virtual key IDs */
  virtualKeyIds?: string[];
  
  /** Filter by model patterns */
  modelPatterns?: string[];
  
  /** Include detailed token information */
  includeTokenDetails?: boolean;
}

/**
 * Parameters for exporting cost analytics
 */
export interface ExportCostParams extends ExportParams {
  /** Group results by this field */
  groupBy?: 'virtualKey' | 'model' | 'provider' | 'day' | 'month';
  
  /** Include cost predictions */
  includePredictions?: boolean;
  
  /** Currency for cost values */
  currency?: 'USD' | 'EUR' | 'GBP';
}

/**
 * Parameters for exporting virtual key analytics
 */
export interface ExportVirtualKeyParams extends ExportParams {
  /** Filter by virtual key IDs */
  virtualKeyIds?: string[];
  
  /** Include usage statistics */
  includeUsageStats?: boolean;
  
  /** Include key settings */
  includeSettings?: boolean;
  
  /** Include audit log */
  includeAuditLog?: boolean;
}

/**
 * Parameters for exporting provider analytics
 */
export interface ExportProviderParams extends ExportParams {
  /** Filter by provider IDs */
  providerIds?: string[];
  
  /** Include health metrics */
  includeHealthMetrics?: boolean;
  
  /** Include performance statistics */
  includePerformanceStats?: boolean;
  
  /** Include error analysis */
  includeErrorAnalysis?: boolean;
}

/**
 * Parameters for exporting security analytics
 */
export interface ExportSecurityParams extends ExportParams {
  /** Filter by event types */
  eventTypes?: string[];
  
  /** Filter by severity levels */
  severities?: string[];
  
  /** Include resolved incidents */
  includeResolvedIncidents?: boolean;
  
  /** Anonymize sensitive data */
  anonymizeData?: boolean;
}

/**
 * Result of an export operation
 */
export interface ExportResult {
  /** Unique identifier for the export */
  exportId: string;
  
  /** Current status of the export */
  status: 'pending' | 'processing' | 'completed' | 'failed';
  
  /** Export format */
  format: string;
  
  /** Size of the export file in bytes */
  sizeBytes?: number;
  
  /** URL to download the export */
  downloadUrl?: string;
  
  /** When the download URL expires */
  expiresAt?: string;
  
  /** Error message if the export failed */
  error?: string;
}

/**
 * DTO for creating an export schedule
 */
export interface CreateExportScheduleDto {
  /** Name of the schedule */
  name: string;
  
  /** Type of export */
  exportType: 'usage' | 'cost' | 'security' | 'full';
  
  /** Cron expression for scheduling */
  schedule: string;
  
  /** Export parameters */
  params: ExportParams;
  
  /** Export destination configuration */
  destination?: {
    type: 's3' | 'email' | 'webhook';
    config: ExportDestinationConfig;
  };
}

/**
 * Scheduled export configuration
 */
export interface ExportSchedule {
  /** Unique identifier */
  id: string;
  
  /** Schedule name */
  name: string;
  
  /** Export type */
  exportType: string;
  
  /** Cron schedule */
  schedule: string;
  
  /** Export parameters */
  params: ExportParams;
  
  /** Destination configuration */
  destination?: {
    type: 's3' | 'email' | 'webhook';
    config: ExportDestinationConfig;
  };
  
  /** Last execution time */
  lastRun?: string;
  
  /** Next scheduled execution */
  nextRun?: string;
  
  /** Whether the schedule is enabled */
  enabled: boolean;
}

/**
 * Export history entry
 */
export interface ExportHistory {
  /** Export ID */
  exportId: string;
  
  /** Export type */
  type: string;
  
  /** When the export was requested */
  requestedAt: string;
  
  /** When the export completed */
  completedAt?: string;
  
  /** Export status */
  status: string;
  
  /** Export format */
  format: string;
  
  /** File size in bytes */
  sizeBytes?: number;
  
  /** Number of records exported */
  recordCount?: number;
  
  /** Download URL */
  downloadUrl?: string;
  
  /** Error message if failed */
  error?: string;
}

/**
 * Parameters for exporting request logs
 */
export interface ExportRequestLogsParams {
  /** Export format */
  format: 'json' | 'csv' | 'excel';
  
  /** Filters for request logs */
  filters: {
    startDate?: string;
    endDate?: string;
    statusCodes?: number[];
    methods?: string[];
    endpoints?: string[];
    virtualKeyIds?: string[];
    providerTypes?: ProviderType[];
    minResponseTime?: number;
    maxResponseTime?: number;
    minCost?: number;
    ipAddresses?: string[];
  };
  
  /** Specific fields to include in export */
  fields?: string[];
  
  /** Sort configuration */
  sortBy?: {
    field: string;
    direction: 'asc' | 'desc';
  };
  
  /** Whether to compress the export */
  compression?: boolean;
  
  /** Maximum number of records to export */
  maxRecords?: number;
}

/**
 * Request log statistics
 */
export interface RequestLogStatistics {
  totalRequests: number;
  uniqueVirtualKeys: number;
  uniqueIpAddresses: number;
  averageResponseTime: number;
  medianResponseTime: number;
  p95ResponseTime: number;
  p99ResponseTime: number;
  totalCost: number;
  totalTokensUsed: number;
  errorRate: number;
  statusCodeDistribution: Record<number, number>;
  endpointDistribution: Array<{ 
    endpoint: string; 
    count: number; 
    avgResponseTime: number;
  }>;
  hourlyDistribution: Array<{ 
    hour: number; 
    count: number;
  }>;
}

/**
 * Parameters for request log summary
 */
export interface RequestLogSummaryParams {
  /** Field to group by */
  groupBy: 'virtualKey' | 'endpoint' | 'provider' | 'hour' | 'day' | 'statusCode';
  
  /** Start date filter */
  startDate?: string;
  
  /** End date filter */
  endDate?: string;
  
  /** Maximum number of groups to return */
  limit?: number;
}

/**
 * Request log summary
 */
export interface RequestLogSummary {
  /** Field used for grouping */
  groupBy: string;
  
  /** Time period of the summary */
  period: {
    start: string;
    end: string;
  };
  
  /** Summary groups */
  groups: Array<{
    key: string;
    count: number;
    totalCost: number;
    avgResponseTime: number;
    errorRate: number;
    totalTokens: number;
  }>;
}

/**
 * Enhanced request log type with additional fields
 */
export interface RequestLog {
  id: string;
  timestamp: string;
  method: string;
  endpoint: string;
  statusCode: number;
  responseTime: number;
  virtualKeyId: string;
  virtualKeyName?: string;
  providerType?: ProviderType;
  modelName?: string;
  ipAddress: string;
  userAgent?: string;
  requestSize: number;
  responseSize: number;
  tokensUsed?: {
    prompt: number;
    completion: number;
    total: number;
  };
  cost?: number;
  error?: {
    type: string;
    message: string;
  };
  metadata?: ExtendedMetadata;
}

/**
 * Export status details
 */
export interface ExportStatus {
  exportId: string;
  status: 'pending' | 'processing' | 'completed' | 'failed';
  progress?: {
    current: number;
    total: number;
    percentage: number;
  };
  startedAt?: string;
  completedAt?: string;
  error?: string;
  result?: {
    recordCount: number;
    sizeBytes: number;
    downloadUrl: string;
    expiresAt: string;
  };
}