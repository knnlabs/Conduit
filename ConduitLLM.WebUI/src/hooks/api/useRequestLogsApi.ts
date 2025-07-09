'use client';

import type { RequestLogDto, RequestLogFilters as SDKRequestLogFilters } from '@knn_labs/conduit-admin-client';

// Re-export hooks and types from the SDK
export { 
  useRequestLogs,
  useRequestLog,
  useSearchLogs,
  useExportRequestLogs 
} from '@/hooks/useConduitAdmin';

// Create type alias for backward compatibility
export type RequestLog = RequestLogDto;
export type RequestLogFilters = SDKRequestLogFilters;

// For backward compatibility with components expecting specific response format
export interface RequestLogsResponse {
  items: RequestLogDto[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

// Note: The SDK returns data directly, not wrapped in a response object
// Components using this hook will need to be updated to handle the SDK response format