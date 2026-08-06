// Media types come from the Admin SDK (the wire shape the API actually returns)
export type {
  MediaRecord,
  MediaTypeStats,
  MediaStorageStats,
  OverallMediaStorageStats,
  MediaGroupQuotaUsage,
  MediaFilters,
} from '@/lib/admin-api';

export interface VirtualKeyInfo {
  id: number;
  name: string;
  key: string;
}
