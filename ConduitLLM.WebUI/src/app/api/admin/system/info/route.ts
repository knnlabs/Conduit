import { createLegacyToSDKMigration } from '@/lib/utils/route-migration';

export const GET = createLegacyToSDKMigration(
  async (client) => client.system.getSystemInfo(),
  { 
    requireAdmin: true,
    errorContext: 'fetch system info'
  }
);