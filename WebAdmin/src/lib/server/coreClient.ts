// Re-export from centralized config
// Note: getServerGatewayClient is now async and returns a Promise<ConduitGatewayClient>
export { getServerGatewayClient, getServerCoreClient } from './sdk-config';
export type { ConduitGatewayClient, ConduitCoreClient } from '@/lib/gateway-api';