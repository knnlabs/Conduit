export interface MediaRecord {
  id: string;
  storageKey: string;
  virtualKeyId: number;
  virtualKeyGroupId?: number;
  virtualKeyGroupName?: string;
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
  deletedAt?: string | null;
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
  groupQuotaUsage: MediaGroupQuotaUsage[];
}

export interface MediaGroupQuotaUsage {
  virtualKeyGroupId: number;
  virtualKeyGroupName: string;
  mediaRetentionPolicyName?: string | null;
  totalSizeBytes: number;
  totalFiles: number;
  maxStorageSizeBytes?: number | null;
  maxFileCount?: number | null;
  quotaExceededBehavior: 'reject' | 'allowAndEvict';
  isOverQuota: boolean;
  storageUsagePercent?: number | null;
  fileUsagePercent?: number | null;
}

export interface MediaFilters {
  mediaType?: 'image' | 'video' | 'all';
  deletionState?: 'active' | 'deleted' | 'all';
  provider?: string;
  virtualKeyId?: number;
  fromDate?: Date | string;
  toDate?: Date | string;
  searchQuery?: string;
  sortBy?: 'createdAt' | 'sizeBytes' | 'accessCount';
  sortOrder?: 'asc' | 'desc';
}

export interface VirtualKeyInfo {
  id: number;
  name: string;
  key: string;
}
