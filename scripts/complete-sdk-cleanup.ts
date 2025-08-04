#!/usr/bin/env tsx

import * as fs from 'fs';
import * as path from 'path';

const CONSTANTS_FILE = path.join(__dirname, '../SDKs/Node/Admin/src/constants.ts');

// Read the current constants file
let content = fs.readFileSync(CONSTANTS_FILE, 'utf8');

// 1. Remove the entire PROVIDER_MODELS section
console.log('🗑️  Removing deprecated PROVIDER_MODELS section...');
content = content.replace(
  /\/\/ Provider Models[\s\S]*?SEARCH: '\/api\/provider-models\/search',\s*},/g,
  ''
);

// 2. Remove deprecated MODEL_MAPPINGS endpoints
console.log('🗑️  Removing deprecated MODEL_MAPPINGS endpoints...');
const deprecatedMappingEndpoints = [
  'BY_MODEL',
  'DISCOVER_PROVIDER', 
  'DISCOVER_MODEL',
  'TEST_CAPABILITY',
  'IMPORT',
  'EXPORT', 
  'SUGGEST',
  'ROUTING'
];

deprecatedMappingEndpoints.forEach(endpoint => {
  const regex = new RegExp(`\\s*${endpoint}:.*?,?\\n`, 'g');
  content = content.replace(regex, '\n');
});

// 3. Remove non-existent IP_FILTERS endpoints
console.log('🗑️  Removing non-existent IP_FILTERS endpoints...');
const nonExistentIpEndpoints = [
  'BULK_CREATE',
  'BULK_UPDATE', 
  'BULK_DELETE',
  'CREATE_TEMPORARY',
  'EXPIRING',
  'IMPORT',
  'EXPORT',
  'BLOCKED_STATS'
];

nonExistentIpEndpoints.forEach(endpoint => {
  const regex = new RegExp(`\\s*${endpoint}:.*?,?\\n`, 'g');
  content = content.replace(regex, '\n');
});

// 4. Remove non-existent MODEL_COSTS endpoints
console.log('🗑️  Removing non-existent MODEL_COSTS endpoints...');
const nonExistentCostEndpoints = [
  'BY_MODEL',
  'BATCH',
  'BULK_UPDATE',
  'TRENDS'
];

nonExistentCostEndpoints.forEach(endpoint => {
  const regex = new RegExp(`\\s*${endpoint}:.*?,?\\n`, 'g');
  content = content.replace(regex, '\n');
});

// 5. Remove deprecated ANALYTICS export endpoints (already removed)

// 6. Remove non-existent HEALTH endpoints
console.log('🗑️  Removing non-existent HEALTH endpoints...');
content = content.replace(/HISTORY: '\/api\/ProviderHealth\/history',\s*\n/g, '');
content = content.replace(/SUMMARY: '\/api\/health\/providers',\s*\n/g, '');
content = content.replace(/ALERTS: '\/api\/health\/alerts',\s*\n/g, '');
content = content.replace(/PERFORMANCE:.*?performance',\s*\n/g, '');

// 7. Remove non-existent SYSTEM endpoints
console.log('🗑️  Removing non-existent SYSTEM endpoints...');
content = content.replace(/HEALTH_EVENTS: '\/api\/health\/events',\s*\n/g, '');
content = content.replace(/BACKUP: '\/api\/DatabaseBackup',\s*\n/g, '');
content = content.replace(/RESTORE: '\/api\/DatabaseBackup\/restore',\s*\n/g, '');

// 8. Remove non-existent SETTINGS endpoints
console.log('🗑️  Removing non-existent SETTINGS endpoints...');
content = content.replace(/BATCH_UPDATE: '\/api\/GlobalSettings\/batch',\s*\n/g, '');
content = content.replace(/AUDIO: '\/api\/AudioConfiguration',\s*\n/g, '');
content = content.replace(/AUDIO_BY_PROVIDER:.*?AudioConfiguration.*?\n/g, '');

// 9. Remove entire SECURITY section (non-existent)
console.log('🗑️  Removing non-existent SECURITY section...');
content = content.replace(/\/\/ Security[\s\S]*?COMPLIANCE_REPORT:.*?',\s*},/g, '');

// 10. Remove entire ERROR_QUEUES section (non-existent)
console.log('🗑️  Removing non-existent ERROR_QUEUES section...');
content = content.replace(/\/\/ Error Queue Management[\s\S]*?CLEAR:.*?messages',\s*},/g, '');

// 11. Remove most CONFIGURATION endpoints (non-existent)
console.log('🗑️  Removing non-existent CONFIGURATION endpoints...');
content = content.replace(/\/\/ Configuration[\s\S]*?ROUTING_EVENTS_SUBSCRIBE:.*?subscribe',\s*},/g, '');

// Now add the missing endpoints
console.log('\n✨ Adding missing endpoints...');

// Find where to insert new sections (after COSTS section)
const costsEndIndex = content.indexOf('  },', content.indexOf('COSTS:')) + 3;

const newEndpoints = `

  // Audio Provider Management
  AUDIO: {
    PROVIDERS: {
      BASE: '/api/admin/audio/providers',
      BY_ID: (id: string) => \`/api/admin/audio/providers/\${id}\`,
      BY_PROVIDER_ID: (providerId: string) => \`/api/admin/audio/providers/by-id/\${providerId}\`,
      ENABLED: (operationType: string) => \`/api/admin/audio/providers/enabled/\${operationType}\`,
      TEST: (id: string) => \`/api/admin/audio/providers/\${id}/test\`,
    },
    COSTS: {
      BASE: '/api/admin/audio/costs',
      BY_ID: (id: string) => \`/api/admin/audio/costs/\${id}\`,
      BY_PROVIDER: (providerId: string) => \`/api/admin/audio/costs/by-provider/\${providerId}\`,
      CURRENT: '/api/admin/audio/costs/current',
    },
    USAGE: {
      BASE: '/api/admin/audio/usage',
      SUMMARY: '/api/admin/audio/usage/summary',
      BY_KEY: (virtualKey: string) => \`/api/admin/audio/usage/by-key/\${virtualKey}\`,
      BY_PROVIDER: (providerId: string) => \`/api/admin/audio/usage/by-provider/\${providerId}\`,
    },
    SESSIONS: {
      BASE: '/api/admin/audio/sessions',
      BY_ID: (sessionId: string) => \`/api/admin/audio/sessions/\${sessionId}\`,
      METRICS: '/api/admin/audio/sessions/metrics',
    },
  },

  // Media Management
  MEDIA: {
    STATS: {
      BASE: '/api/admin/Media/stats',
      BY_VIRTUAL_KEY: (virtualKeyId: string) => \`/api/admin/Media/stats/virtual-key/\${virtualKeyId}\`,
      BY_PROVIDER: '/api/admin/Media/stats/by-provider',
      BY_TYPE: '/api/admin/Media/stats/by-type',
    },
    BY_VIRTUAL_KEY: (virtualKeyId: string) => \`/api/admin/Media/virtual-key/\${virtualKeyId}\`,
    SEARCH: '/api/admin/Media/search',
    BY_ID: (mediaId: string) => \`/api/admin/Media/\${mediaId}\`,
    CLEANUP: {
      EXPIRED: '/api/admin/Media/cleanup/expired',
      ORPHANED: '/api/admin/Media/cleanup/orphaned',
      PRUNE: '/api/admin/Media/cleanup/prune',
    },
  },

  // Cache Monitoring
  CACHE_MONITORING: {
    STATUS: '/api/cache/monitoring/status',
    THRESHOLDS: '/api/cache/monitoring/thresholds',
    ALERTS: '/api/cache/monitoring/alerts',
    CHECK: '/api/cache/monitoring/check',
    ALERT_DEFINITIONS: '/api/cache/monitoring/alert-definitions',
    HEALTH: '/api/cache/monitoring/health',
  },

  // Database Management
  DATABASE: {
    BACKUP: '/api/database/backup',
    BACKUPS: '/api/database/backups',
    RESTORE: (backupId: string) => \`/api/database/restore/\${backupId}\`,
    DOWNLOAD: (backupId: string) => \`/api/database/download/\${backupId}\`,
  },

  // Configuration endpoints (only the ones that exist)
  CONFIG: {
    ROUTING: '/api/config/routing',
    CACHING: {
      BASE: '/api/config/caching',
      CLEAR: (cacheId: string) => \`/api/config/caching/\${cacheId}/clear\`,
      STATISTICS: '/api/config/caching/statistics',
      REGIONS: '/api/config/caching/regions',
      ENTRIES: (regionId: string) => \`/api/config/caching/\${regionId}/entries\`,
      REFRESH: (regionId: string) => \`/api/config/caching/\${regionId}/refresh\`,
      POLICY: (regionId: string) => \`/api/config/caching/\${regionId}/policy\`,
    },
  },

  // Logs endpoints
  LOGS: {
    BASE: '/api/Logs',
    BY_ID: (id: string) => \`/api/Logs/\${id}\`,
    MODELS: '/api/Logs/models',
    SUMMARY: '/api/Logs/summary',
  },

  // Notifications endpoints
  NOTIFICATIONS: {
    BASE: '/api/Notifications',
    BY_ID: (id: number) => \`/api/Notifications/\${id}\`,
    UNREAD: '/api/Notifications/unread',
    MARK_READ: (id: number) => \`/api/Notifications/\${id}/read\`,
    MARK_ALL_READ: '/api/Notifications/mark-all-read',
  },

  // Router endpoints
  ROUTER: {
    CONFIG: '/api/Router/config',
    DEPLOYMENTS: '/api/Router/deployments',
    DEPLOYMENT_BY_NAME: (deploymentName: string) => \`/api/Router/deployments/\${deploymentName}\`,
    FALLBACKS: '/api/Router/fallbacks',
    FALLBACK_BY_MODEL: (primaryModel: string) => \`/api/Router/fallbacks/\${primaryModel}\`,
  },

  // Security endpoints (simplified)
  SECURITY: {
    EVENTS: '/api/security/events',
    THREATS: '/api/security/threats', 
    COMPLIANCE: '/api/security/compliance',
  },`;

// Insert new endpoints
content = content.slice(0, costsEndIndex) + newEndpoints + content.slice(costsEndIndex);

// Clean up any double commas or trailing commas
content = content.replace(/,\s*,/g, ',');
content = content.replace(/,\s*}/g, '}');

// Write the updated file
fs.writeFileSync(CONSTANTS_FILE, content);

console.log('\n✅ SDK cleanup complete!');
console.log('📊 Summary:');
console.log('  - Removed deprecated PROVIDER_MODELS section');
console.log('  - Removed non-existent MODEL_MAPPINGS endpoints');
console.log('  - Removed non-existent IP_FILTERS endpoints');
console.log('  - Removed non-existent MODEL_COSTS endpoints');
console.log('  - Removed non-existent HEALTH endpoints');
console.log('  - Removed non-existent SYSTEM endpoints');
console.log('  - Removed non-existent SETTINGS endpoints');
console.log('  - Removed non-existent SECURITY section');
console.log('  - Removed non-existent ERROR_QUEUES section');
console.log('  - Removed non-existent CONFIGURATION section');
console.log('  - Added AUDIO provider endpoints');
console.log('  - Added MEDIA management endpoints');
console.log('  - Added CACHE_MONITORING endpoints');
console.log('  - Added DATABASE management endpoints');
console.log('  - Added CONFIG endpoints (only existing ones)');
console.log('  - Added LOGS endpoints');
console.log('  - Added NOTIFICATIONS endpoints');
console.log('  - Added ROUTER endpoints');
console.log('  - Added simplified SECURITY endpoints');