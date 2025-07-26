/**
 * Base response types shared across all Conduit SDK clients
 */

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export interface PagedResponse<T> {
  data: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface ErrorResponse {
  error: string;
  message?: string;
  details?: Record<string, unknown>;
  statusCode?: number;
}

// NOTE: ApiResponse moved to http/types.ts

export type SortDirection = 'asc' | 'desc';

export interface SortOptions {
  field: string;
  direction: SortDirection;
}

export interface FilterOptions {
  search?: string;
  sortBy?: SortOptions;
  pageNumber?: number;
  pageSize?: number;
}

export interface DateRange {
  startDate: string;
  endDate: string;
}

// NOTE: HttpMethod and RequestOptions moved to http/types.ts

/**
 * Common usage tracking interface
 */
export interface Usage {
  prompt_tokens: number;
  completion_tokens: number;
  total_tokens: number;
  // Phase 1 fields
  is_batch?: boolean;
  image_quality?: string;
  // Phase 2 fields
  cached_input_tokens?: number;
  cached_write_tokens?: number;
  search_units?: number;
  inference_steps?: number;
  // Additional fields from backend Usage model
  image_count?: number;
  video_duration_seconds?: number;
  video_resolution?: string;
  audio_duration_seconds?: number;
}

/**
 * Performance metrics for API calls
 */
export interface PerformanceMetrics {
  provider_name: string;
  provider_response_time_ms: number;
  total_response_time_ms: number;
  tokens_per_second?: number;
}