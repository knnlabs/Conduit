/**
 * Security-related models for the Admin SDK
 */

/**
 * Represents a security event in the system
 */
export interface SecurityEvent {
  /** Unique identifier for the security event */
  id: string;
  
  /** Timestamp when the event occurred */
  timestamp: string;
  
  /** Type of security event */
  type: 'authentication_failure' | 'rate_limit_exceeded' | 'suspicious_activity' | 'invalid_api_key';
  
  /** Severity level of the event */
  severity: 'low' | 'medium' | 'high' | 'critical';
  
  /** Source of the security event */
  source: string;
  
  /** Associated virtual key ID, if applicable */
  virtualKeyId?: string;
  
  /** IP address associated with the event */
  ipAddress?: string;
  
  /** Additional event details */
  details: Record<string, any>;
  
  /** HTTP status code, if applicable */
  statusCode?: number;
}

/**
 * Data transfer object for creating a security event
 */
export interface CreateSecurityEventDto {
  /** Type of security event */
  type: 'authentication_failure' | 'rate_limit_exceeded' | 'suspicious_activity' | 'invalid_api_key';
  
  /** Severity level of the event */
  severity: 'low' | 'medium' | 'high' | 'critical';
  
  /** Source of the security event */
  source: string;
  
  /** Associated virtual key ID, if applicable */
  virtualKeyId?: string;
  
  /** IP address associated with the event */
  ipAddress?: string;
  
  /** Additional event details */
  details: Record<string, any>;
  
  /** HTTP status code, if applicable */
  statusCode?: number;
}

/**
 * Filters for querying security events
 */
export interface SecurityEventFilters {
  /** Number of hours to look back */
  hours?: number;
  
  /** Start date for the query range */
  startDate?: string;
  
  /** End date for the query range */
  endDate?: string;
  
  /** Filter by severity level */
  severity?: 'low' | 'medium' | 'high' | 'critical';
  
  /** Filter by event type */
  type?: 'authentication_failure' | 'rate_limit_exceeded' | 'suspicious_activity' | 'invalid_api_key';
  
  /** Page number for pagination */
  page?: number;
  
  /** Number of items per page */
  pageSize?: number;
}

/**
 * Represents a detected threat in the system
 */
export interface ThreatDetection {
  /** Unique identifier for the threat */
  id: string;
  
  /** Title of the threat */
  title: string;
  
  /** Type of threat */
  type: string;
  
  /** Severity level of the threat */
  severity: 'minor' | 'major' | 'critical';
  
  /** Current status of the threat */
  status: 'active' | 'acknowledged' | 'resolved';
  
  /** Timestamp when the threat was detected */
  detectedAt: string;
  
  /** Source of the threat detection */
  source: string;
  
  /** Resources affected by the threat */
  affectedResources: string[];
  
  /** Detailed description of the threat */
  description: string;
  
  /** Recommended actions to address the threat */
  recommendations: string[];
}

/**
 * Filters for querying threats
 */
export interface ThreatFilters {
  /** Filter by threat status */
  status?: 'active' | 'acknowledged' | 'resolved';
  
  /** Filter by severity level */
  severity?: 'minor' | 'major' | 'critical';
  
  /** Page number for pagination */
  page?: number;
  
  /** Number of items per page */
  pageSize?: number;
}

/**
 * Analytics data for threat detection
 */
export interface ThreatAnalytics {
  /** Overall threat level */
  threatLevel: 'low' | 'medium' | 'high' | 'critical';
  
  /** Threat-related metrics */
  metrics: {
    /** Number of blocked requests */
    blockedRequests: number;
    
    /** Number of suspicious activities detected */
    suspiciousActivity: number;
    
    /** Number of rate limit hits */
    rateLimitHits: number;
    
    /** Number of failed authentication attempts */
    failedAuthentications: number;
    
    /** Number of currently active threats */
    activeThreats: number;
  };
  
  /** Top threats by type */
  topThreats: Array<{
    /** Type of threat */
    type: string;
    
    /** Number of occurrences */
    count: number;
  }>;
  
  /** Threat trend over time */
  threatTrend: Array<{
    /** Date of the data point */
    date: string;
    
    /** Number of threats on that date */
    count: number;
  }>;
}

/**
 * Compliance metrics for the system
 */
export interface ComplianceMetrics {
  /** Overall compliance score (0-100) */
  overallScore: number;
  
  /** Compliance scores by category */
  categories: {
    /** Data protection compliance score */
    dataProtection: number;
    
    /** Access control compliance score */
    accessControl: number;
    
    /** Audit logging compliance score */
    auditLogging: number;
    
    /** Incident response compliance score */
    incidentResponse: number;
    
    /** Monitoring compliance score */
    monitoring: number;
  };
  
  /** Timestamp of the last compliance assessment */
  lastAssessment: string;
  
  /** List of compliance issues */
  issues: Array<{
    /** Category of the issue */
    category: string;
    
    /** Severity of the issue */
    severity: string;
    
    /** Description of the issue */
    description: string;
  }>;
}

/**
 * Paged result for security-related queries
 */
export interface PagedResult<T> {
  /** Array of items in the current page */
  items: T[];
  
  /** Total number of items across all pages */
  totalCount: number;
  
  /** Current page number */
  page: number;
  
  /** Number of items per page */
  pageSize: number;
  
  /** Total number of pages */
  totalPages: number;
}

/**
 * Actions that can be taken on a threat
 */
export type ThreatAction = 'acknowledge' | 'resolve' | 'ignore';

/**
 * Export formats supported by the security service
 */
export type ExportFormat = 'json' | 'csv' | 'pdf';