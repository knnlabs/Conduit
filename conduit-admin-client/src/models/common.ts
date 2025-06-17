export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export interface ErrorResponse {
  error: string;
  message?: string;
  details?: Record<string, any>;
  statusCode?: number;
}

export interface ApiResponse<T = any> {
  success: boolean;
  data?: T;
  error?: ErrorResponse;
}

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

export type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH';

export interface RequestOptions {
  timeout?: number;
  retries?: number;
  headers?: Record<string, string>;
}