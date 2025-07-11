declare module '@tanstack/react-query' {
  import { ReactNode } from 'react';

  export interface DefaultOptions {
    queries?: QueryObserverOptions;
    mutations?: MutationObserverOptions;
  }

  export interface QueryClientConfig {
    defaultOptions?: DefaultOptions;
    queryCache?: QueryCache;
    mutationCache?: MutationCache;
  }

  export interface QueryObserverOptions<
    TQueryFnData = unknown,
    TError = unknown,
    TData = TQueryFnData,
    TQueryKey extends QueryKey = QueryKey
  > {
    enabled?: boolean;
    staleTime?: number;
    cacheTime?: number;
    refetchOnWindowFocus?: boolean | 'always';
    refetchOnReconnect?: boolean | 'always';
    refetchOnMount?: boolean | 'always';
    refetchInterval?: number | false;
    refetchIntervalInBackground?: boolean;
    retry?: boolean | number | ((failureCount: number, error: TError) => boolean);
    retryDelay?: number | ((retryAttempt: number, error: TError) => number);
    networkMode?: 'online' | 'always' | 'offlineFirst';
    gcTime?: number;
  }

  export interface MutationObserverOptions<
    TData = unknown,
    TError = unknown,
    TVariables = void,
    TContext = unknown
  > {
    networkMode?: 'online' | 'always' | 'offlineFirst';
    retry?: boolean | number | ((failureCount: number, error: TError) => boolean);
    retryDelay?: number | ((retryAttempt: number, error: TError) => number);
    gcTime?: number;
    onMutate?: (variables: TVariables) => Promise<TContext> | TContext | void;
    onSuccess?: (data: TData, variables: TVariables, context: TContext) => void;
    onError?: (error: TError, variables: TVariables, context: TContext | undefined) => void;
    onSettled?: (data: TData | undefined, error: TError | null, variables: TVariables, context: TContext | undefined) => void;
  }

  export type QueryKey = readonly unknown[];

  export class QueryCache {
    find<TQueryFnData = unknown>(
      filters: { queryKey: QueryKey }
    ): Query<TQueryFnData> | undefined;
    findAll(filters?: { queryKey?: QueryKey }): Query[];
  }

  export class MutationCache {
    findAll(filters?: { mutationKey?: QueryKey }): Mutation[];
  }

  export class Query<TQueryFnData = unknown> {
    queryKey: QueryKey;
    state: { data?: TQueryFnData; error?: unknown };
  }

  export class Mutation<TData = unknown, TError = unknown, TVariables = unknown> {
    mutationKey?: QueryKey;
    state: { data?: TData; error?: TError };
  }
  
  export class QueryClient {
    constructor(config?: QueryClientConfig);
    invalidateQueries(filters?: {
      queryKey?: QueryKey;
      exact?: boolean;
      type?: 'active' | 'inactive' | 'all';
    }): Promise<void>;
    setQueryData<TData>(queryKey: QueryKey, updater: TData | ((old?: TData) => TData | undefined)): TData | undefined;
    removeQueries(filters?: { queryKey?: QueryKey; exact?: boolean }): void;
    getQueryData<TData>(queryKey: QueryKey): TData | undefined;
    ensureQueryData<TData>(options: {
      queryKey: QueryKey;
      queryFn: () => Promise<TData>;
    }): Promise<TData>;
    prefetchQuery<TData>(options: {
      queryKey: QueryKey;
      queryFn: () => Promise<TData>;
      staleTime?: number;
    }): Promise<void>;
    fetchQuery<TData>(options: {
      queryKey: QueryKey;
      queryFn: () => Promise<TData>;
    }): Promise<TData>;
    cancelQueries(filters?: { queryKey?: QueryKey }): Promise<void>;
    resetQueries(filters?: { queryKey?: QueryKey }): Promise<void>;
    isFetching(filters?: { queryKey?: QueryKey }): number;
    isMutating(filters?: { mutationKey?: QueryKey }): number;
  }

  export interface QueryClientProviderProps {
    client: QueryClient;
    children?: ReactNode;
  }
  
  export const QueryClientProvider: React.FC<QueryClientProviderProps>;
  
  export interface UseQueryResult<TData = unknown, TError = unknown> {
    data: TData | undefined;
    error: TError | null;
    isLoading: boolean;
    isError: boolean;
    isSuccess: boolean;
    isIdle: boolean;
    isPending: boolean;
    isRefetching: boolean;
    isRefetchError: boolean;
    status: 'pending' | 'error' | 'success';
    fetchStatus: 'idle' | 'fetching' | 'paused';
    refetch: () => Promise<UseQueryResult<TData, TError>>;
  }

  export interface UseMutationResult<TData = unknown, TError = unknown, TVariables = void, TContext = unknown> {
    data: TData | undefined;
    error: TError | null;
    isIdle: boolean;
    isPending: boolean;
    isError: boolean;
    isSuccess: boolean;
    status: 'idle' | 'pending' | 'error' | 'success';
    mutate: (variables: TVariables, options?: MutateOptions<TData, TError, TVariables, TContext>) => void;
    mutateAsync: (variables: TVariables, options?: MutateOptions<TData, TError, TVariables, TContext>) => Promise<TData>;
    reset: () => void;
  }

  export interface MutateOptions<TData = unknown, TError = unknown, TVariables = void, TContext = unknown> {
    onSuccess?: (data: TData, variables: TVariables, context: TContext) => void;
    onError?: (error: TError, variables: TVariables, context: TContext | undefined) => void;
    onSettled?: (data: TData | undefined, error: TError | null, variables: TVariables, context: TContext | undefined) => void;
  }

  export type UseQueryOptions<
    TQueryFnData = unknown,
    TError = unknown,
    TData = TQueryFnData,
    TQueryKey extends QueryKey = QueryKey
  > = QueryObserverOptions<TQueryFnData, TError, TData, TQueryKey> & {
    queryKey: TQueryKey;
    queryFn?: (context: { queryKey: TQueryKey; signal?: AbortSignal }) => Promise<TQueryFnData>;
    select?: (data: TQueryFnData) => TData;
  };

  export type UseMutationOptions<
    TData = unknown,
    TError = unknown,
    TVariables = void,
    TContext = unknown
  > = MutationObserverOptions<TData, TError, TVariables, TContext> & {
    mutationKey?: QueryKey;
    mutationFn?: (variables: TVariables) => Promise<TData>;
  };

  export function useQuery<
    TQueryFnData = unknown,
    TError = unknown,
    TData = TQueryFnData,
    TQueryKey extends QueryKey = QueryKey
  >(
    options: UseQueryOptions<TQueryFnData, TError, TData, TQueryKey>
  ): UseQueryResult<TData, TError>;

  export function useMutation<
    TData = unknown,
    TError = unknown,
    TVariables = void,
    TContext = unknown
  >(
    options: UseMutationOptions<TData, TError, TVariables, TContext>
  ): UseMutationResult<TData, TError, TVariables, TContext>;

  export function useQueryClient(): QueryClient;
}