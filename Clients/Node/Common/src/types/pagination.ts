/**
 * Pagination and filtering types shared across Conduit SDK clients
 */

export interface PaginationParams {
  page?: number;
  pageSize?: number;
}

export interface SearchParams extends PaginationParams {
  search?: string;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

export interface TimeRangeParams {
  startDate?: string;
  endDate?: string;
  timezone?: string;
}

export interface BatchOperationParams {
  batchSize?: number;
  parallel?: boolean;
  continueOnError?: boolean;
}