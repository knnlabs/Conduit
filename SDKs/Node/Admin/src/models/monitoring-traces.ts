import type { EventData } from './common-types';

/**
 * Distributed trace
 */
export interface TraceDto {
  traceId: string;
  spans: SpanDto[];
  startTime: string;
  endTime: string;
  duration: number;
  serviceName: string;
  status: 'ok' | 'error' | 'timeout';
  tags: Record<string, string>;
}

/**
 * Trace span
 */
export interface SpanDto {
  spanId: string;
  parentSpanId?: string;
  operationName: string;
  serviceName: string;
  startTime: string;
  endTime: string;
  duration: number;
  status: 'ok' | 'error' | 'timeout';
  tags: Record<string, string>;
  logs: SpanLog[];
}

/**
 * Span log entry
 */
export interface SpanLog {
  timestamp: string;
  level: 'debug' | 'info' | 'warn' | 'error';
  message: string;
  fields?: { [key: string]: string | number | boolean | null };
}

/**
 * Trace query parameters
 */
export interface TraceQueryParams {
  service?: string;
  operation?: string;
  minDuration?: number;
  maxDuration?: number;
  status?: 'ok' | 'error' | 'timeout';
  startTime?: string;
  endTime?: string;
  tags?: Record<string, string>;
  limit?: number;
}

/**
 * Log entry
 */
export interface LogEntry {
  id: string;
  timestamp: string;
  level: 'debug' | 'info' | 'warn' | 'error' | 'fatal';
  message: string;
  service: string;
  traceId?: string;
  spanId?: string;
  fields: EventData;
  stackTrace?: string;
}

/**
 * Log query parameters
 */
export interface LogQueryParams {
  query?: string;
  level?: 'debug' | 'info' | 'warn' | 'error' | 'fatal';
  service?: string;
  startTime?: string;
  endTime?: string;
  traceId?: string;
  fields?: Record<string, string>;
  limit?: number;
  offset?: number;
}

/**
 * Log stream options
 */
export interface LogStreamOptions {
  query?: string;
  level?: 'debug' | 'info' | 'warn' | 'error' | 'fatal';
  service?: string;
  follow?: boolean;
  tail?: number;
}