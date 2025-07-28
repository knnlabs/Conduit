/**
 * Common API request and response types shared between Core and Admin SDKs
 */

/**
 * Paginated request parameters
 */
export interface PaginatedRequest {
  /** Page number (1-based) */
  page?: number;
  /** Number of items per page */
  pageSize?: number;
  /** Field to sort by */
  sortBy?: string;
  /** Sort direction */
  sortOrder?: 'asc' | 'desc';
}

/**
 * Extended paginated response wrapper with navigation helpers
 */
export interface ExtendedPaginatedResponse<T> {
  /** Array of items for current page */
  items: T[];
  /** Total number of items */
  totalCount: number;
  /** Current page number (1-based) */
  page: number;
  /** Number of items per page */
  pageSize: number;
  /** Total number of pages */
  totalPages: number;
  /** Whether there's a next page */
  hasNextPage?: boolean;
  /** Whether there's a previous page */
  hasPreviousPage?: boolean;
}

/**
 * Standard API error response
 */
export interface ApiError {
  /** Error code for programmatic handling */
  code: string;
  /** Human-readable error message */
  message: string;
  /** Additional error details */
  details?: Record<string, unknown>;
  /** Field-specific errors for validation */
  fieldErrors?: Record<string, string[]>;
  /** Request ID for debugging */
  requestId?: string;
  /** Timestamp when error occurred */
  timestamp?: string;
}

/**
 * Standard API success response wrapper
 */
export interface ApiSuccessResponse<T> {
  /** Response data */
  data: T;
  /** Success status */
  success: boolean;
  /** Optional message */
  message?: string;
  /** Response metadata */
  meta?: ResponseMetadata;
}

/**
 * Response metadata
 */
export interface ResponseMetadata {
  /** Request ID for tracing */
  requestId: string;
  /** Response timestamp */
  timestamp: string;
  /** API version */
  version: string;
  /** Response time in ms */
  responseTime?: number;
}

/**
 * Batch operation request
 */
export interface BatchRequest<T> {
  /** Array of items to process */
  items: T[];
  /** Whether to continue on error */
  continueOnError?: boolean;
  /** Maximum items to process in parallel */
  parallelism?: number;
}

/**
 * Batch operation response
 */
export interface BatchResponse<T> {
  /** Successfully processed items */
  succeeded: BatchResult<T>[];
  /** Failed items */
  failed: BatchError[];
  /** Summary statistics */
  summary: {
    total: number;
    succeeded: number;
    failed: number;
    duration: number;
  };
}

/**
 * Individual batch result
 */
export interface BatchResult<T> {
  /** Index in original request */
  index: number;
  /** Processed result */
  result: T;
}

/**
 * Individual batch error
 */
export interface BatchError {
  /** Index in original request */
  index: number;
  /** Error details */
  error: ApiError;
}

/**
 * Date range filter
 */
export interface DateRangeFilter {
  /** Start date (inclusive) */
  startDate?: string;
  /** End date (inclusive) */
  endDate?: string;
}

/**
 * Numeric range filter
 */
export interface NumericRangeFilter {
  /** Minimum value (inclusive) */
  min?: number;
  /** Maximum value (inclusive) */
  max?: number;
}

/**
 * Alternative paginated result interface used in some endpoints
 */
export interface PagedResult<T> {
  /** Array of items in the current page */
  items: T[];
  /** Total number of items across all pages */
  totalCount: number;
  /** Current page number */
  page: number;
  /** Number of items per page */
  pageSize: number;
  /** Total number of pages */
  totalPages: number;
}

/**
 * Sort configuration
 */
export interface SortConfig {
  /** Field to sort by */
  field: string;
  /** Sort direction */
  direction: 'asc' | 'desc';
}

/**
 * Filter operators
 */
export enum FilterOperator {
  EQUALS = 'eq',
  NOT_EQUALS = 'ne',
  GREATER_THAN = 'gt',
  GREATER_THAN_OR_EQUAL = 'gte',
  LESS_THAN = 'lt',
  LESS_THAN_OR_EQUAL = 'lte',
  IN = 'in',
  NOT_IN = 'nin',
  CONTAINS = 'contains',
  STARTS_WITH = 'startsWith',
  ENDS_WITH = 'endsWith'
}

/**
 * Generic filter
 */
export interface Filter {
  /** Field to filter on */
  field: string;
  /** Filter operator */
  operator: FilterOperator;
  /** Filter value */
  value: unknown;
}

/**
 * Health check response
 */
export interface HealthCheckResponse {
  /** Overall health status */
  status: 'healthy' | 'degraded' | 'unhealthy';
  /** Service version */
  version: string;
  /** Uptime in seconds */
  uptime: number;
  /** Individual component health */
  components: Record<string, ComponentHealth>;
}

/**
 * Base interface for create DTOs
 */
export interface CreateDto<T> {
  /** The data to create */
  data: Partial<T>;
}

/**
 * Base interface for update DTOs
 */
export interface UpdateDto<T> {
  /** The fields to update */
  data: Partial<T>;
  /** Optional version for optimistic concurrency */
  version?: string;
}

/**
 * Base interface for delete operations
 */
export interface DeleteDto {
  /** Optional reason for deletion */
  reason?: string;
  /** Whether to force delete (bypass soft delete) */
  force?: boolean;
}

/**
 * Bulk operation request
 */
export interface BulkOperationRequest<T> {
  /** Items to process */
  items: T[];
  /** Whether to continue on error */
  continueOnError?: boolean;
  /** Maximum parallel operations */
  parallelism?: number;
}

/**
 * Bulk operation response
 */
export interface BulkOperationResponse<T> {
  /** Successfully processed items */
  succeeded: T[];
  /** Failed items with errors */
  failed: Array<{
    item: T;
    error: ApiError;
  }>;
  /** Summary statistics */
  summary: {
    total: number;
    succeeded: number;
    failed: number;
  };
}

/**
 * Component health status
 */
export interface ComponentHealth {
  /** Component status */
  status: 'healthy' | 'degraded' | 'unhealthy';
  /** Optional message */
  message?: string;
  /** Last check timestamp */
  lastCheck: string;
  /** Response time in ms */
  responseTime?: number;
}