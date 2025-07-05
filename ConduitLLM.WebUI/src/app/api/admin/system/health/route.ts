import { createLegacyToSDKMigration } from '@/lib/utils/route-migration';

export const GET = createLegacyToSDKMigration(
  async (client) => client.system.getHealth(),
  { 
    requireAdmin: true,
    errorContext: 'fetch system health'
  }
);