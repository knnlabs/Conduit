export interface MediaRecord {
  id: string;
  storageKey: string;
  virtualKeyId: number;
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

export interface VirtualKeyInfo {
  id: number;
  name: string;
  key: string;
}