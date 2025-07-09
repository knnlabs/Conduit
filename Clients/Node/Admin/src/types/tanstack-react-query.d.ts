declare module '@tanstack/react-query' {
  export interface QueryClientConfig {
    defaultOptions?: {
      queries?: any;
      mutations?: any;
    };
  }
  
  export class QueryClient {
    constructor(config?: QueryClientConfig);
    invalidateQueries(options: { queryKey: readonly any[] }): Promise<void>;
    setQueryData<TData>(queryKey: readonly any[], data: TData): void;
    removeQueries(options: { queryKey: readonly any[] }): void;
  }
  
  export const QueryClientProvider: any;
  
  export function useQuery(options: any): any;
  export function useMutation(options: any): any;
  export function useQueryClient(): QueryClient;
  
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  export type UseQueryOptions<TData = unknown, TError = unknown> = any;
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  export type UseMutationOptions<TData = unknown, TError = unknown, TVariables = unknown> = any;
}