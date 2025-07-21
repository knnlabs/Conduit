import { FilterOptions } from './common';

export type FilterType = 'whitelist' | 'blacklist';
export type FilterMode = 'permissive' | 'restrictive';

export interface IpFilterDto {
  id: number;
  name: string;
  ipAddressOrCidr: string;
  filterType: FilterType;
  isEnabled: boolean;
  description?: string;
  createdAt: string;
  updatedAt: string;
  lastMatchedAt?: string;
  matchCount?: number;
  expiresAt?: string; // For temporary rules
  createdBy?: string;
  lastModifiedBy?: string;
  blockedCount?: number; // Number of requests blocked
}

export interface CreateIpFilterDto {
  name: string;
  ipAddressOrCidr: string;
  filterType: FilterType;
  isEnabled?: boolean;
  description?: string;
}

export interface UpdateIpFilterDto {
  id: number;
  name?: string;
  ipAddressOrCidr?: string;
  filterType?: FilterType;
  isEnabled?: boolean;
  description?: string;
}

export interface IpFilterSettingsDto {
  isEnabled: boolean;
  defaultAllow: boolean;
  bypassForAdminUi: boolean;
  excludedEndpoints: string[];
  filterMode: FilterMode;
  whitelistFilters: IpFilterDto[];
  blacklistFilters: IpFilterDto[];
  maxFiltersPerType?: number;
  ipv6Enabled?: boolean;
}

export interface UpdateIpFilterSettingsDto {
  isEnabled?: boolean;
  defaultAllow?: boolean;
  bypassForAdminUi?: boolean;
  excludedEndpoints?: string[];
  filterMode?: FilterMode;
  ipv6Enabled?: boolean;
}

export interface IpCheckRequest {
  ipAddress: string;
  endpoint?: string;
}

export interface IpCheckResult {
  isAllowed: boolean;
  deniedReason?: string;
  matchedFilter?: string;
  matchedFilterId?: number;
  filterType?: FilterType;
  isDefaultAction?: boolean;
}

export interface IpFilterFilters extends FilterOptions {
  filterType?: FilterType;
  isEnabled?: boolean;
  nameContains?: string;
  ipAddressOrCidrContains?: string;
  lastMatchedAfter?: string;
  lastMatchedBefore?: string;
  minMatchCount?: number;
}

export interface IpFilterStatistics {
  totalFilters: number;
  enabledFilters: number;
  allowFilters: number;
  denyFilters: number;
  totalMatches: number;
  recentMatches: {
    timestamp: string;
    ipAddress: string;
    filterName: string;
    action: 'allowed' | 'denied';
  }[];
  topMatchedFilters: {
    filterId: number;
    filterName: string;
    matchCount: number;
  }[];
}

export interface BulkIpFilterRequest {
  filters: CreateIpFilterDto[];
  replaceExisting?: boolean;
  filterType?: FilterType;
}

export interface BulkIpFilterResponse {
  created: IpFilterDto[];
  updated: IpFilterDto[];
  failed: {
    index: number;
    error: string;
    filter: CreateIpFilterDto;
  }[];
}

export interface IpFilterValidationResult {
  isValid: boolean;
  errors: string[];
  warnings: string[];
  suggestedCidr?: string;
  overlappingFilters?: {
    id: number;
    name: string;
    ipAddressOrCidr: string;
  }[];
}

export interface CreateTemporaryIpFilterDto extends CreateIpFilterDto {
  expiresAt: string; // ISO date string
  reason?: string;
}

export interface BulkOperationResult {
  success: number;
  failed: number;
  errors: Array<{
    id: string;
    error: string;
  }>;
}

export interface IpFilterImport {
  ipAddress?: string;
  ipRange?: string;
  rule: 'allow' | 'deny';
  description?: string;
  expiresAt?: string;
}

export interface IpFilterImportResult {
  imported: number;
  skipped: number;
  failed: number;
  errors: Array<{
    row: number;
    error: string;
  }>;
}

export interface BlockedRequestStats {
  totalBlocked: number;
  uniqueIps: number;
  topBlockedIps: Array<{
    ipAddress: string;
    count: number;
    country?: string;
  }>;
  blocksByRule: Array<{
    ruleId: string;
    ruleName: string;
    count: number;
  }>;
  timeline: Array<{
    timestamp: string;
    count: number;
  }>;
}