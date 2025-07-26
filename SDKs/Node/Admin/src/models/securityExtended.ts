import { FilterOptions } from './common';
import type { ExtendedMetadata, ConfigValue, SecurityChangeRecord } from './common-types';

// IP Management types
export interface IpWhitelistDto {
  enabled: boolean;
  ips: IpEntry[];
  lastModified: string;
  totalBlocked: number;
}

export interface IpEntry {
  ip: string;
  cidr?: string;
  description?: string;
  addedBy: string;
  addedAt: string;
  lastSeen?: string;
}

// Extended Security Event types
export interface SecurityEventParams extends FilterOptions {
  startDate?: string;
  endDate?: string;
  severity?: 'low' | 'medium' | 'high' | 'critical';
  type?: SecurityEventType;
  status?: 'active' | 'acknowledged' | 'resolved';
}

export type SecurityEventType = 
  | 'suspicious_activity'
  | 'rate_limit_exceeded'
  | 'invalid_key_attempt'
  | 'ip_blocked'
  | 'unusual_usage_pattern'
  | 'potential_breach'
  | 'policy_violation';

export interface SecurityEventExtended {
  id: string;
  type: SecurityEventType;
  severity: 'low' | 'medium' | 'high' | 'critical';
  title: string;
  description: string;
  source: {
    ip?: string;
    virtualKeyId?: string;
    userId?: string;
  };
  timestamp: string;
  status: 'active' | 'acknowledged' | 'resolved';
  metadata?: ExtendedMetadata;
}

export interface SecurityEventPage {
  items: SecurityEventExtended[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// Threat Detection types
export interface ThreatSummaryDto {
  threatLevel: 'low' | 'medium' | 'high' | 'critical';
  activeThreats: number;
  blockedAttempts24h: number;
  suspiciousActivities24h: number;
  topThreats: ThreatCategory[];
}

export interface ThreatCategory {
  category: string;
  count: number;
  severity: 'low' | 'medium' | 'high' | 'critical';
  trend: 'increasing' | 'stable' | 'decreasing';
}

export interface ActiveThreat {
  id: string;
  type: string;
  severity: 'low' | 'medium' | 'high' | 'critical';
  source: string;
  firstDetected: string;
  lastActivity: string;
  attemptCount: number;
  status: 'monitoring' | 'blocking' | 'mitigated';
  recommendedAction?: string;
}

// Access Control types
export interface AccessPolicy {
  id: string;
  name: string;
  description?: string;
  type: 'ip_based' | 'key_based' | 'rate_limit' | 'custom';
  rules: PolicyRule[];
  enabled: boolean;
  priority: number;
  createdAt: string;
  updatedAt: string;
}

export interface PolicyRule {
  condition: {
    field: string;
    operator: 'equals' | 'contains' | 'gt' | 'lt' | 'regex';
    value: ConfigValue;
  };
  action: 'allow' | 'deny' | 'limit' | 'log';
  metadata?: ExtendedMetadata;
}

export interface CreateAccessPolicyDto {
  name: string;
  description?: string;
  type: 'ip_based' | 'key_based' | 'rate_limit' | 'custom';
  rules: PolicyRule[];
  enabled?: boolean;
  priority?: number;
}

export interface UpdateAccessPolicyDto {
  name?: string;
  description?: string;
  rules?: PolicyRule[];
  enabled?: boolean;
  priority?: number;
}

// Audit Log types
export interface AuditLogParams extends FilterOptions {
  startDate?: string;
  endDate?: string;
  action?: string;
  userId?: string;
  resourceType?: string;
  resourceId?: string;
}

export interface AuditLog {
  id: string;
  timestamp: string;
  userId: string;
  action: string;
  resourceType: string;
  resourceId?: string;
  changes?: SecurityChangeRecord[];
  ipAddress?: string;
  userAgent?: string;
  result: 'success' | 'failure';
  errorMessage?: string;
}

export interface AuditLogPage {
  items: AuditLog[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// Export types
export interface ExportParams {
  format: 'json' | 'csv' | 'pdf';
  startDate?: string;
  endDate?: string;
  includeMetadata?: boolean;
}

export interface ExportResult {
  exportId: string;
  status: 'pending' | 'processing' | 'completed' | 'failed';
  downloadUrl?: string;
  expiresAt?: string;
  error?: string;
}