import { FilterOptions } from './common';

export type FilterType = 'Allow' | 'Deny';
export type FilterMode = 'permissive' | 'restrictive';

export interface IpFilterDto {
  id: number;
  name: string;
  cidrRange: string;
  filterType: FilterType;
  isEnabled: boolean;
  description?: string;
  createdAt: string;
  updatedAt: string;
  lastMatchedAt?: string;
  matchCount?: number;
}

export interface CreateIpFilterDto {
  name: string;
  cidrRange: string;
  filterType: FilterType;
  isEnabled?: boolean;
  description?: string;
}

export interface UpdateIpFilterDto {
  name?: string;
  cidrRange?: string;
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
  reason?: string;
  matchedFilter?: string;
  matchedFilterId?: number;
  filterType?: FilterType;
  isDefaultAction?: boolean;
}

export interface IpFilterFilters extends FilterOptions {
  filterType?: FilterType;
  isEnabled?: boolean;
  nameContains?: string;
  cidrContains?: string;
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
    cidrRange: string;
  }[];
}