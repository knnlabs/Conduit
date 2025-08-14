// Re-export from centralized config
// Note: getServerCoreClient is now async and returns a Promise<ConduitCoreClient>
export { getServerCoreClient } from './sdk-config';
export type { ConduitCoreClient } from '@knn_labs/conduit-core-client';