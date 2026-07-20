import { FilterOptions } from './common';

export type FilterType = 'whitelist' | 'blacklist';
export type FilterMode = 'permissive' | 'restrictive';

// Matches the wire `IpFilterDto` (filterType narrows the wire `string` to a union — asserted
// with SameKeys). Client-only match-stats fields (lastMatchedAt/matchCount/blockedCount) and
// the never-sent expiresAt/lastModifiedBy were removed in issue #1038; added updatedBy.
export interface IpFilterDto {
  id: number;
  name: string;
  ipAddressOrCidr: string;
  filterType: FilterType;
  isEnabled: boolean;
  description?: string;
  createdAt: string;
  updatedAt: string;
  createdBy?: string;
  updatedBy?: string;
  /** Virtual key this filter is scoped to, or null/undefined if it is a global filter. */
  virtualKeyId?: number | null;
}

export interface CreateIpFilterDto {
  name: string;
  ipAddressOrCidr: string;
  filterType: FilterType;
  isEnabled?: boolean;
  description?: string;
  /** Scope this filter to a virtual key. Omit for a global filter. */
  virtualKeyId?: number | null;
}

export interface UpdateIpFilterDto {
  id: number;
  name?: string;
  ipAddressOrCidr?: string;
  filterType?: FilterType;
  isEnabled?: boolean;
  description?: string;
}

// Reconciled to wire shape — issue #1038 (dropped client-only maxFiltersPerType/ipv6Enabled)
export interface IpFilterSettingsDto {
  isEnabled: boolean;
  defaultAllow: boolean;
  bypassForAdminUi: boolean;
  excludedEndpoints: string[];
  filterMode: FilterMode;
  whitelistFilters: IpFilterDto[];
  blacklistFilters: IpFilterDto[];
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

// Matches the wire `IpCheckResult`. The API returns only allow/deny + reason; the client-only
// matchedFilter/matchedFilterId/filterType/isDefaultAction fields were removed in issue #1038.
export interface IpCheckResult {
  isAllowed: boolean;
  deniedReason?: string;
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
  /** Total number of IP filter rules */
  totalRules: number;
  /** Number of enabled rules */
  enabledRules: number;
  /** Number of disabled rules */
  disabledRules: number;
  /** Number of whitelist (allow) rules */
  whitelistRules: number;
  /** Number of blacklist (deny) rules */
  blacklistRules: number;
  /** Total number of blocked requests (requires server-side tracking) */
  totalBlockedRequests: number;
  /** When rules were last updated */
  lastUpdated: string;
  /** @deprecated Use totalRules */
  totalFilters?: number;
  /** @deprecated Use enabledRules */
  enabledFilters?: number;
  /** @deprecated Use whitelistRules */
  allowFilters?: number;
  /** @deprecated Use blacklistRules */
  denyFilters?: number;
  /** @deprecated Requires server-side tracking */
  totalMatches?: number;
  /** @deprecated Requires server-side tracking */
  recentMatches?: {
    timestamp: string;
    ipAddress: string;
    filterName: string;
    action: 'allowed' | 'denied';
  }[];
  /** @deprecated Requires server-side tracking */
  topMatchedFilters?: {
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
  /** Whether the IP/CIDR is valid */
  isValid: boolean;
  /** Error message if invalid */
  error?: string;
  /** Parsed IP address portion */
  ipAddress?: string;
  /** Parsed CIDR prefix length */
  prefixLength?: number;
  /** Whether the IP is IPv6 */
  isIpv6?: boolean;
  /** Normalized CIDR string */
  normalizedCidr?: string;
  /** @deprecated Use error */
  errors?: string[];
  /** @deprecated Not currently implemented */
  warnings?: string[];
  /** @deprecated Not currently implemented */
  suggestedCidr?: string;
  /** @deprecated Requires server-side implementation */
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