'use client';

import { useState, useEffect, useCallback, useRef } from 'react';
import { withAdminClient } from '@/lib/client/adminClient';

/**
 * Request log entry as returned by the backend API
 * This matches the C# LogRequestDto structure
 */
export interface RequestLogEntry {
  id: number;
  virtualKeyId: number;
  modelName: string;
  providerId: number | null;
  providerType: string | null;
  modelProviderMappingId: number | null;
  promptCachingEligible: boolean;
  promptCachingPolicyApplied: boolean;
  cachedReadSavings: number;
  cacheWritePremium: number;
  routingAffinityUsed: boolean;
  routingDecisionReason: string | null;
  routingFailoverCount: number;
  requestType: string;
  inputTokens: number;
  outputTokens: number;
  cachedInputTokens: number | null;
  cachedWriteTokens: number | null;
  cost: number;
  billingMethod: number | null;
  providerReportedCostUsd: number | null;
  providerCostMarkupMultiplier: number | null;
  billedAtUtc: string | null;
  responseTimeMs: number;
  userId: string | null;
  clientIp: string | null;
  requestPath: string | null;
  statusCode: number | null;
  timestamp: string;
  metadata: string | null;
}

/**
 * Tool call item in chat completion responses (basic format)
 */
export interface ChatToolCallItem {
  id?: string;
  type?: string;
  functionName?: string;
  hasArguments?: boolean;
}

/**
 * Function execution result with rich metadata (for agentic chat completions)
 */
export interface FunctionCallResult {
  toolCallId?: string;
  functionName?: string;
  status?: 'completed' | 'failed';
  cost?: number;
  errorMessage?: string;
  functionExecutionId?: string;
}

/**
 * Parsed metadata for different request types
 */
export interface RequestLogMetadata {
  [key: string]: unknown;
  type?: string;
  // Function-specific (for /functions/execute endpoint)
  functionConfigurationId?: number;
  functionName?: string;
  executionId?: string;
  state?: string;
  errorMessage?: string;
  // Chat tool calls (for chat completions with tools - basic format)
  toolCallCount?: number;
  toolCalls?: ChatToolCallItem[];
  // Chat function executions (for agentic chat completions - rich format)
  functionCallCount?: number;
  totalCost?: number;
  successCount?: number;
  failedCount?: number;
  functionCalls?: FunctionCallResult[];
  // Image-specific
  size?: string;
  quality?: string;
  style?: string;
  imageCount?: number;
  // Video-specific
  duration?: number;
  resolution?: string;
  aspectRatio?: string;
  // Audio-specific
  voice?: string;
  format?: string;
  durationSeconds?: number;
}

/**
 * Paginated response from the backend
 */
export interface RequestLogPageResult {
  items: RequestLogEntry[];
  totalCount: number;
  currentPage: number;
  pageSize: number;
  totalPages: number;
}

/**
 * Filter options for request logs
 */
export interface RequestLogFilters {
  startDate?: Date;
  endDate?: Date;
  model?: string;
  virtualKeyId?: number;
  status?: number;
}

/**
 * Statistics calculated from request logs
 */
export interface RequestLogStats {
  totalRequests: number;
  successCount: number;
  errorCount: number;
  totalCost: number;
  avgLatency: number;
  successRate: number;
}

interface UseRequestLogsOptions {
  page: number;
  pageSize: number;
  filters?: RequestLogFilters;
  autoRefresh?: boolean;
  refreshInterval?: number;
}

interface UseRequestLogsResult {
  logs: RequestLogEntry[];
  totalCount: number;
  totalPages: number;
  currentPage: number;
  isLoading: boolean;
  error: Error | null;
  stats: RequestLogStats | null;
  refetch: () => Promise<void>;
}

/**
 * Hook for fetching paginated request logs with filtering
 */
export function useRequestLogs({
  page,
  pageSize,
  filters,
  autoRefresh = false,
  refreshInterval = 30000,
}: UseRequestLogsOptions): UseRequestLogsResult {
  const [logs, setLogs] = useState<RequestLogEntry[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [currentPage, setCurrentPage] = useState(1);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);
  const [stats, setStats] = useState<RequestLogStats | null>(null);
  const requestSequence = useRef(0);
  const isMounted = useRef(true);

  useEffect(() => {
    isMounted.current = true;
    return () => {
      isMounted.current = false;
    };
  }, []);

  const fetchLogs = useCallback(async () => {
    const requestId = ++requestSequence.current;
    const isCurrentRequest = () => isMounted.current && requestId === requestSequence.current;

    try {
      setIsLoading(true);
      setError(null);

      // Build query parameters
      const params: Record<string, string> = {
        page: page.toString(),
        pageSize: pageSize.toString(),
      };

      if (filters?.startDate) {
        params.startDate = filters.startDate.toISOString();
      }
      if (filters?.endDate) {
        params.endDate = filters.endDate.toISOString();
      }
      if (filters?.model) {
        params.model = filters.model;
      }
      if (filters?.virtualKeyId !== undefined) {
        params.virtualKeyId = filters.virtualKeyId.toString();
      }
      if (filters?.status !== undefined) {
        params.status = filters.status.toString();
      }

      const result = await withAdminClient(async (client) => {
        // Use the analytics service getRequestLogs method
        const response = await client.analytics.getRequestLogs({
          page,
          pageSize,
          startDate: filters?.startDate?.toISOString(),
          endDate: filters?.endDate?.toISOString(),
          model: filters?.model,
          virtualKeyId: filters?.virtualKeyId?.toString(),
          statusCode: filters?.status,
        });
        return response;
      });

      // Map the response to our interface
      // The SDK returns items with slightly different field names
      // Using type assertion to handle SDK/backend type differences
      const items = result.items ?? [];
      const mappedLogs: RequestLogEntry[] = items.map((item) => {
        // Cast to unknown first to safely access properties that may have different names
        const rawItem = item as unknown as {
          id?: number | string;
          virtualKeyId?: number;
          model?: string;
          modelName?: string;
          providerId?: number | null;
          providerType?: string | null;
          modelProviderMappingId?: number | null;
          promptCachingEligible?: boolean;
          promptCachingPolicyApplied?: boolean;
          cachedReadSavings?: number;
          cacheWritePremium?: number;
          routingAffinityUsed?: boolean;
          routingDecisionReason?: string | null;
          routingFailoverCount?: number;
          requestType?: string;
          inputTokens?: number;
          outputTokens?: number;
          cachedInputTokens?: number | null;
          cachedWriteTokens?: number | null;
          cost?: number;
          billingMethod?: number | null;
          providerReportedCostUsd?: number | null;
          providerCostMarkupMultiplier?: number | null;
          billedAtUtc?: string | null;
          duration?: number;
          responseTimeMs?: number;
          userId?: string | null;
          clientIp?: string | null;
          ipAddress?: string | null;
          requestPath?: string | null;
          statusCode?: number | null;
          timestamp?: string;
          metadata?: string | null;
        };

        return {
          id: typeof rawItem.id === 'string' ? parseInt(rawItem.id, 10) : (rawItem.id ?? 0),
          virtualKeyId: rawItem.virtualKeyId ?? 0,
          modelName: rawItem.model ?? rawItem.modelName ?? '',
          providerId: rawItem.providerId ?? null,
          providerType: rawItem.providerType ?? null,
          modelProviderMappingId: rawItem.modelProviderMappingId ?? null,
          promptCachingEligible: rawItem.promptCachingEligible ?? false,
          promptCachingPolicyApplied: rawItem.promptCachingPolicyApplied ?? false,
          cachedReadSavings: rawItem.cachedReadSavings ?? 0,
          cacheWritePremium: rawItem.cacheWritePremium ?? 0,
          routingAffinityUsed: rawItem.routingAffinityUsed ?? false,
          routingDecisionReason: rawItem.routingDecisionReason ?? null,
          routingFailoverCount: rawItem.routingFailoverCount ?? 0,
          requestType: rawItem.requestType ?? '',
          inputTokens: rawItem.inputTokens ?? 0,
          outputTokens: rawItem.outputTokens ?? 0,
          cachedInputTokens: rawItem.cachedInputTokens ?? null,
          cachedWriteTokens: rawItem.cachedWriteTokens ?? null,
          cost: rawItem.cost ?? 0,
          billingMethod: rawItem.billingMethod ?? null,
          providerReportedCostUsd: rawItem.providerReportedCostUsd ?? null,
          providerCostMarkupMultiplier: rawItem.providerCostMarkupMultiplier ?? null,
          billedAtUtc: rawItem.billedAtUtc ?? null,
          responseTimeMs: rawItem.duration ?? rawItem.responseTimeMs ?? 0,
          userId: rawItem.userId ?? null,
          clientIp: rawItem.clientIp ?? rawItem.ipAddress ?? null,
          requestPath: rawItem.requestPath ?? null,
          statusCode: rawItem.statusCode ?? null,
          timestamp: rawItem.timestamp ?? new Date().toISOString(),
          metadata: rawItem.metadata ?? null,
        };
      });

      if (!isCurrentRequest()) return;

      setLogs(mappedLogs);
      setTotalCount(result.totalCount ?? 0);
      setTotalPages(result.totalPages ?? Math.ceil((result.totalCount ?? 0) / pageSize));
      setCurrentPage(result.page ?? page);

      // Calculate stats from current page (for display purposes)
      // In a real scenario, you might want to fetch stats from a separate endpoint
      if (mappedLogs.length > 0) {
        const successCount = mappedLogs.filter(
          (log) => log.statusCode === null || (log.statusCode >= 200 && log.statusCode < 400)
        ).length;
        const errorCount = mappedLogs.filter(
          (log) => log.statusCode !== null && log.statusCode >= 400
        ).length;
        const totalCost = mappedLogs.reduce((sum, log) => sum + log.cost, 0);
        const avgLatency =
          mappedLogs.reduce((sum, log) => sum + log.responseTimeMs, 0) / mappedLogs.length;

        setStats({
          totalRequests: result.totalCount ?? mappedLogs.length,
          successCount,
          errorCount,
          totalCost,
          avgLatency,
          successRate: mappedLogs.length > 0 ? (successCount / mappedLogs.length) * 100 : 0,
        });
      } else {
        setStats({
          totalRequests: 0,
          successCount: 0,
          errorCount: 0,
          totalCost: 0,
          avgLatency: 0,
          successRate: 0,
        });
      }
    } catch (err) {
      if (!isCurrentRequest()) return;

      console.error('Error fetching request logs:', err);
      setError(err instanceof Error ? err : new Error('Failed to fetch request logs'));
      setLogs([]);
      setTotalCount(0);
      setTotalPages(0);
      setStats(null);
    } finally {
      if (isCurrentRequest()) {
        setIsLoading(false);
      }
    }
  }, [page, pageSize, filters]);

  // Initial fetch and refetch on dependency changes
  useEffect(() => {
    void fetchLogs();
  }, [fetchLogs]);

  // Auto-refresh if enabled
  useEffect(() => {
    if (!autoRefresh) return;

    const intervalId = setInterval(() => {
      void fetchLogs();
    }, refreshInterval);

    return () => clearInterval(intervalId);
  }, [autoRefresh, refreshInterval, fetchLogs]);

  return {
    logs,
    totalCount,
    totalPages,
    currentPage,
    isLoading,
    error,
    stats,
    refetch: fetchLogs,
  };
}

/**
 * Hook for fetching distinct model names for filter dropdown
 */
export function useDistinctModels() {
  const [models, setModels] = useState<string[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    const fetchModels = async () => {
      try {
        setIsLoading(true);
        // The SDK doesn't have a direct method for this, so we'll use a workaround
        // by fetching logs and extracting unique models, or call the endpoint directly
        const result = await withAdminClient(async (client) => {
          // Try to get distinct models from the logs endpoint
          // This is a temporary solution - ideally the SDK would have this method
          const response = await client.analytics.getRequestLogs({
            page: 1,
            pageSize: 100,
          });
          return response;
        });

        // Extract unique model names
        const items = result.items ?? [];
        const uniqueModels = [
          ...new Set(
            items
              .map((item) => {
                const rawItem = item as unknown as { model?: string; modelName?: string };
                return rawItem.model ?? rawItem.modelName ?? '';
              })
              .filter((model) => model !== '')
          ),
        ];
        setModels(uniqueModels.sort());
      } catch (err) {
        console.error('Error fetching distinct models:', err);
        setError(err instanceof Error ? err : new Error('Failed to fetch models'));
      } finally {
        setIsLoading(false);
      }
    };

    void fetchModels();
  }, []);

  return { models, isLoading, error };
}
