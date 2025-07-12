/**
 * Type-safe metadata interfaces to replace Record<string, any>
 * These interfaces provide proper typing for metadata fields used throughout the SDK
 */

/**
 * Base metadata interface for common fields
 */
export interface BaseMetadata {
  /** User or system that created/owns this resource */
  createdBy?: string;
  /** Purpose or description of the resource */
  purpose?: string;
  /** Department, team, or project */
  department?: string;
  /** Environment (dev, staging, prod) */
  environment?: string;
  /** Custom tags for categorization */
  tags?: string[];
}

/**
 * Virtual key metadata
 */
export interface VirtualKeyMetadata extends BaseMetadata {
  /** Customer or client ID */
  customerId?: string;
  /** Project or application name */
  projectName?: string;
  /** Cost center for billing */
  costCenter?: string;
  /** Contact email for notifications */
  contactEmail?: string;
  /** Additional notes */
  notes?: string;
}

/**
 * Provider configuration metadata
 */
export interface ProviderConfigMetadata {
  /** Region or location */
  region?: string;
  /** API version */
  apiVersion?: string;
  /** Custom endpoint URL */
  endpoint?: string;
  /** Additional provider-specific settings */
  settings?: {
    [key: string]: string | number | boolean;
  };
}

/**
 * Analytics and monitoring metadata
 */
export interface AnalyticsMetadata extends BaseMetadata {
  /** Source system or service */
  source?: string;
  /** Correlation ID for tracking */
  correlationId?: string;
  /** Session or request ID */
  sessionId?: string;
  /** User agent or client info */
  userAgent?: string;
  /** IP address or location */
  ipAddress?: string;
  /** Custom metrics */
  metrics?: {
    [key: string]: number;
  };
}

/**
 * Alert configuration metadata
 */
export interface AlertMetadata {
  /** Severity level */
  severity?: 'low' | 'medium' | 'high' | 'critical';
  /** Alert category */
  category?: string;
  /** Runbook URL */
  runbookUrl?: string;
  /** Escalation policy */
  escalationPolicy?: string;
  /** Notification channels */
  notificationChannels?: string[];
}

/**
 * Security event metadata
 */
export interface SecurityEventMetadata {
  /** Event type */
  eventType?: string;
  /** Actor or user */
  actor?: string;
  /** Resource affected */
  resource?: string;
  /** Action performed */
  action?: string;
  /** Result or outcome */
  result?: 'success' | 'failure' | 'blocked';
  /** Risk score */
  riskScore?: number;
  /** Additional context */
  context?: {
    [key: string]: string | number | boolean;
  };
}

/**
 * Export configuration metadata
 */
export interface ExportConfigMetadata {
  /** Export format */
  format?: 'csv' | 'json' | 'xml' | 'parquet';
  /** Compression type */
  compression?: 'none' | 'gzip' | 'zip';
  /** Encryption settings */
  encryption?: {
    enabled: boolean;
    algorithm?: string;
  };
  /** Destination details */
  destination?: {
    type: 's3' | 'email' | 'webhook' | 'ftp';
    url?: string;
    bucket?: string;
    path?: string;
  };
}

/**
 * Model configuration metadata
 */
export interface ModelConfigMetadata {
  /** Model description */
  description?: string;
  /** Model version */
  version?: string;
  /** Model family or type */
  family?: string;
  /** Supported features */
  features?: string[];
  /** Performance tier */
  tier?: 'basic' | 'standard' | 'premium';
  /** Custom parameters */
  parameters?: {
    [key: string]: string | number | boolean;
  };
}

/**
 * Audio configuration metadata
 */
export interface AudioConfigMetadata {
  /** Audio format */
  format?: 'mp3' | 'wav' | 'ogg' | 'flac';
  /** Sample rate in Hz */
  sampleRate?: number;
  /** Bit rate in kbps */
  bitRate?: number;
  /** Number of channels */
  channels?: 1 | 2;
  /** Language code */
  language?: string;
  /** Voice ID or name */
  voice?: string;
}

/**
 * Video generation metadata
 */
export interface VideoGenerationMetadata extends BaseMetadata {
  /** Video resolution */
  resolution?: string;
  /** Frame rate */
  fps?: number;
  /** Duration in seconds */
  duration?: number;
  /** Style or theme */
  style?: string;
  /** Webhook URL for completion */
  webhookUrl?: string;
  /** Callback metadata */
  callbackMetadata?: {
    [key: string]: string | number | boolean;
  };
}

/**
 * Type guard to check if a value is a valid metadata object
 */
export function isValidMetadata(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/**
 * Safely parse JSON metadata from string
 */
export function parseMetadata<T extends Record<string, unknown>>(
  metadataString: string | null | undefined
): T | undefined {
  if (!metadataString) {
    return undefined;
  }
  
  try {
    const parsed = JSON.parse(metadataString);
    if (isValidMetadata(parsed)) {
      return parsed as T;
    }
    return undefined;
  } catch {
    return undefined;
  }
}

/**
 * Safely stringify metadata to JSON
 */
export function stringifyMetadata<T extends Record<string, unknown>>(
  metadata: T | null | undefined
): string | undefined {
  if (!metadata || Object.keys(metadata).length === 0) {
    return undefined;
  }
  
  try {
    return JSON.stringify(metadata);
  } catch {
    return undefined;
  }
}