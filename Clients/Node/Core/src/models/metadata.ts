/**
 * Type-safe metadata interfaces for Core SDK
 */

/**
 * Chat completion metadata
 */
export interface ChatMetadata {
  /** Conversation or session ID */
  conversationId?: string;
  /** User ID making the request */
  userId?: string;
  /** Application or client name */
  application?: string;
  /** Request purpose or context */
  context?: string;
  /** Custom tracking ID */
  trackingId?: string;
  /** Additional properties */
  custom?: {
    [key: string]: string | number | boolean;
  };
}

/**
 * Video generation webhook metadata
 */
export interface VideoWebhookMetadata {
  /** Job or task ID */
  jobId?: string;
  /** User or customer ID */
  userId?: string;
  /** Callback URL for status updates */
  callbackUrl?: string;
  /** Custom reference ID */
  referenceId?: string;
  /** Priority level */
  priority?: 'low' | 'normal' | 'high';
  /** Additional callback data */
  callbackData?: {
    [key: string]: string | number | boolean;
  };
}

/**
 * Tool/Function call parameters
 */
export interface ToolParameters {
  [key: string]: string | number | boolean | null | ToolParameters | ToolParameters[];
}

/**
 * Notification metadata
 */
export interface NotificationMetadata {
  /** Notification type */
  type?: string;
  /** Source system */
  source?: string;
  /** Target user or group */
  target?: string;
  /** Priority level */
  priority?: 'low' | 'normal' | 'high' | 'urgent';
  /** Expiration time */
  expiresAt?: string;
  /** Action URL */
  actionUrl?: string;
  /** Custom data */
  data?: {
    [key: string]: string | number | boolean;
  };
}

/**
 * Type guard to check if a value is valid metadata
 */
export function isValidMetadata(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/**
 * Safely parse metadata from various sources
 */
export function parseMetadata<T extends Record<string, unknown>>(
  metadata: string | Record<string, unknown> | null | undefined
): T | undefined {
  if (!metadata) {
    return undefined;
  }
  
  // If already an object, return it
  if (typeof metadata === 'object') {
    return metadata as T;
  }
  
  // If string, try to parse as JSON
  if (typeof metadata === 'string') {
    try {
      const parsed = JSON.parse(metadata);
      if (isValidMetadata(parsed)) {
        return parsed as T;
      }
    } catch {
      // Invalid JSON
    }
  }
  
  return undefined;
}

/**
 * Convert metadata to string if needed
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