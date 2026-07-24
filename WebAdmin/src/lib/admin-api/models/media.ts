// Media models and types for Admin SDK

// Reconciled to wire shape — issue #1038 (dropped client-only virtualKeyGroupId/Name; added virtualKey)
export interface MediaRecord {
  id: string;
  storageKey: string;
  virtualKeyId: number;
  /** Wire references the VirtualKey schema; typed loosely on the client. */
  virtualKey?: unknown;
  mediaType: 'image' | 'video';
  contentType?: string;
  sizeBytes?: number;
  contentHash?: string;
  provider?: string;
  model?: string;
  prompt?: string;
  storageUrl?: string;
  publicUrl?: string;
  expiresAt?: string;
  createdAt: string;
  lastAccessedAt?: string;
  accessCount: number;
}

export interface MediaTypeStats {
  fileCount: number;
  sizeBytes: number;
}

export interface MediaStorageStats {
  virtualKeyId: number;
  totalSizeBytes: number;
  totalFiles: number;
  byMediaType: Record<string, MediaTypeStats>;
}

export interface OverallMediaStorageStats {
  totalSizeBytes: number;
  totalFiles: number;
  orphanedFiles: number;
  byProvider: Record<string, number>;
  byMediaType: Record<string, MediaTypeStats>;
  storageByVirtualKey: Record<string, number>;
}

export interface MediaFilters {
  mediaType?: 'image' | 'video' | 'all';
  provider?: string;
  virtualKeyId?: number;
  fromDate?: Date | string;
  toDate?: Date | string;
  searchQuery?: string;
  sortBy?: 'createdAt' | 'sizeBytes' | 'accessCount';
  sortOrder?: 'asc' | 'desc';
}

// Cleanup types
export interface MediaCleanupRequest {
  type: 'expired' | 'reconciliation' | 'prune';
  daysToKeep?: number;
  force?: boolean;
}

export interface MediaCleanupResponse {
  message: string;
  deletedCount: number;
  failedCount: number;
  isDryRun: boolean;
  wouldDeleteCount: number;
  bytesWouldFree: number;
  triggeredBy: string;
}

export interface MediaCleanupPreview {
  fileCount: number;
  sizeBytes: number;
  confirmationPhrase: string;
}

// Search types
export interface MediaSearchParams {
  pattern: string;
}

// Delete types
export interface MediaDeleteRequest {
  mediaId: string;
}

export interface MediaDeleteResponse {
  message: string;
}

// Media Cleanup Service Status types
export interface MediaCleanupStatus {
  isEnabled: boolean;
  isDryRunMode: boolean;
  storageBackend: string;
  untrackedObjectCount: number;
  untrackedBytes: number;
  lastRunTimeUtc: string | null;
  lastRunStatus: string | null;
  lastRunTriggeredBy: string | null;
  lastRunFilesDeleted: number;
  lastRunBytesFreed: number;
  lastRunDurationSeconds: number | null;
  monthlyDeleteCount: number;
  monthlyDeleteBudget: number;
  monthlyDeleteBudgetRemaining: number;
  monthlyBudgetUsedPercent: number;
  scheduleIntervalMinutes: number;
  maxBatchSize: number;
  defaultRetentionPolicy: RetentionPolicySummary | null;
  activeRetentionPoliciesCount: number;
  /** Simple retention override in days. When set, all media is deleted after this many days regardless of account balance. */
  simpleRetentionOverrideDays: number | null;
  /** Whether simple retention override is active (convenience boolean, true when simpleRetentionOverrideDays is set) */
  isSimpleRetentionOverrideActive: boolean;
  nextScheduledRunUtc: string | null;
  currentLeaderInstanceId: string | null;
  /** Last known result for each scheduler-owned cleanup phase. */
  operationStatuses: MediaCleanupOperationStatus[];
}

export interface MediaCleanupOperationStatus {
  cleanupType: 'expiration' | 'reconciliation' | 'retention';
  isEnabled: boolean;
  lastRunTimeUtc: string | null;
  lastRunStatus: string | null;
  triggeredBy: string | null;
  lastRunFilesDeleted: number;
  lastRunBytesFreed: number;
  lastRunDurationSeconds: number | null;
}

export interface RetentionPolicySummary {
  name: string;
  positiveBalanceRetentionDays: number;
  zeroBalanceRetentionDays: number;
  negativeBalanceRetentionDays: number;
}

export interface MediaCleanupEnabledResponse {
  enabled: boolean;
  message?: string;
}

// Simple Retention Override types
export interface SimpleRetentionResponse {
  /** Current retention days setting (null if using policy-based retention) */
  retentionDays: number | null;
  /** Whether the simple override is currently active */
  isOverrideActive: boolean;
  /** Informational message about the change */
  message?: string;
}

export interface UpdateSimpleRetentionRequest {
  /** Retention days (1-365), or null to clear the override and use policy-based retention */
  retentionDays: number | null;
}

// Full Media Retention Policy types
// Reconciled to wire shape — issue #1038 (added virtualKeyGroups)
export interface MediaRetentionPolicy {
  id: number;
  name: string;
  description?: string;
  /** Days to retain media for accounts with positive balance */
  positiveBalanceRetentionDays: number;
  /** Days to retain media for accounts with zero balance */
  zeroBalanceRetentionDays: number;
  /** Days to retain media for accounts with negative balance */
  negativeBalanceRetentionDays: number;
  /** Grace period in days before permanently deleting soft-deleted media */
  softDeleteGracePeriodDays: number;
  /** Whether to respect recent access when determining cleanup eligibility */
  respectRecentAccess: boolean;
  /** Days to consider as "recent" for access tracking */
  recentAccessWindowDays: number;
  /** Whether this is the default policy for new virtual key groups */
  isDefault: boolean;
  /** Maximum storage size in bytes (null means no limit) */
  maxStorageSizeBytes?: number | null;
  /** Maximum number of media files (null means no limit) */
  maxFileCount?: number | null;
  /** Whether this policy is active and can be assigned */
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  /** Wire references the VirtualKeyGroup schema; typed loosely on the client. */
  virtualKeyGroups?: unknown[];
}

// Reconciled to wire shape — issue #1038 (dropped client-only isActive)
export interface CreateMediaRetentionPolicyRequest {
  name: string;
  description?: string;
  positiveBalanceRetentionDays: number;
  zeroBalanceRetentionDays: number;
  negativeBalanceRetentionDays: number;
  softDeleteGracePeriodDays?: number;
  respectRecentAccess?: boolean;
  recentAccessWindowDays?: number;
  isDefault?: boolean;
  maxStorageSizeBytes?: number | null;
  maxFileCount?: number | null;
}

export interface UpdateMediaRetentionPolicyRequest {
  name?: string;
  description?: string;
  positiveBalanceRetentionDays?: number;
  zeroBalanceRetentionDays?: number;
  negativeBalanceRetentionDays?: number;
  softDeleteGracePeriodDays?: number;
  respectRecentAccess?: boolean;
  recentAccessWindowDays?: number;
  isDefault?: boolean;
  maxStorageSizeBytes?: number | null;
  maxFileCount?: number | null;
  isActive?: boolean;
}
