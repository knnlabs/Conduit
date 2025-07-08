import { UseQueryOptions, UseMutationOptions } from '@tanstack/react-query';
import { ConduitError } from '../../utils/errors';

export type QueryOptions<TData = unknown, TError = ConduitError> = Omit<
  UseQueryOptions<TData, TError>,
  'queryKey' | 'queryFn'
>;

export type MutationOptions<TData = unknown, TVariables = unknown, TError = ConduitError> = Omit<
  UseMutationOptions<TData, TError, TVariables>,
  'mutationFn'
>;

export function handleError(error: unknown): ConduitError {
  if (error instanceof ConduitError) {
    return error;
  }
  return new ConduitError('An unexpected error occurred', 500, 'internal_error', 'internal_error');
}

export const queryKeys = {
  all: ['conduit'] as const,
  models: () => [...queryKeys.all, 'models'] as const,
  model: (id: string) => [...queryKeys.models(), id] as const,
  health: () => [...queryKeys.all, 'health'] as const,
  healthCheck: (component: string) => [...queryKeys.health(), component] as const,
  metrics: () => [...queryKeys.all, 'metrics'] as const,
  metricType: (type: string) => [...queryKeys.metrics(), type] as const,
  discovery: () => [...queryKeys.all, 'discovery'] as const,
  providerModels: (provider: string) => [...queryKeys.discovery(), 'provider', provider] as const,
  taskStatus: (taskId: string) => [...queryKeys.all, 'task', taskId] as const,
  batchOperation: (operationId: string) => [...queryKeys.all, 'batch', operationId] as const,
} as const;