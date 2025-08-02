#!/bin/bash

# Add missing endpoints to SDK based on API

set -e

SDK_FILE="$1"

if [ -z "$SDK_FILE" ]; then
    echo "Usage: $0 <sdk-constants-file>"
    exit 1
fi

echo "📝 Adding missing endpoints to SDK..."

# Add Audio endpoints section after SETTINGS section
cat >> "$SDK_FILE.additions" << 'EOF'

  // Audio Provider Management
  AUDIO: {
    PROVIDERS: {
      BASE: '/api/admin/audio/providers',
      BY_ID: (id: string) => `/api/admin/audio/providers/${id}`,
      BY_PROVIDER_ID: (providerId: string) => `/api/admin/audio/providers/by-id/${providerId}`,
      ENABLED: (operationType: string) => `/api/admin/audio/providers/enabled/${operationType}`,
      TEST: (id: string) => `/api/admin/audio/providers/${id}/test`,
    },
    COSTS: {
      BASE: '/api/admin/audio/costs',
      BY_ID: (id: string) => `/api/admin/audio/costs/${id}`,
      BY_PROVIDER: (providerId: string) => `/api/admin/audio/costs/by-provider/${providerId}`,
      CURRENT: '/api/admin/audio/costs/current',
    },
    USAGE: {
      BASE: '/api/admin/audio/usage',
      SUMMARY: '/api/admin/audio/usage/summary',
      BY_KEY: (virtualKey: string) => `/api/admin/audio/usage/by-key/${virtualKey}`,
      BY_PROVIDER: (providerId: string) => `/api/admin/audio/usage/by-provider/${providerId}`,
    },
    SESSIONS: {
      BASE: '/api/admin/audio/sessions',
      BY_ID: (sessionId: string) => `/api/admin/audio/sessions/${sessionId}`,
      METRICS: '/api/admin/audio/sessions/metrics',
    },
  },

  // Media Management
  MEDIA: {
    STATS: {
      BASE: '/api/admin/Media/stats',
      BY_VIRTUAL_KEY: (virtualKeyId: string) => `/api/admin/Media/stats/virtual-key/${virtualKeyId}`,
      BY_PROVIDER: '/api/admin/Media/stats/by-provider',
      BY_TYPE: '/api/admin/Media/stats/by-type',
    },
    BY_VIRTUAL_KEY: (virtualKeyId: string) => `/api/admin/Media/virtual-key/${virtualKeyId}`,
    SEARCH: '/api/admin/Media/search',
    BY_ID: (mediaId: string) => `/api/admin/Media/${mediaId}`,
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
    RESTORE: (backupId: string) => `/api/database/restore/${backupId}`,
    DOWNLOAD: (backupId: string) => `/api/database/download/${backupId}`,
  },

  // Updated Config endpoints
  CONFIG: {
    ROUTING: {
      BASE: '/api/config/routing',
      HEALTH: '/api/config/routing/health',
    },
    CACHING: {
      BASE: '/api/config/caching',
      CLEAR: (cacheId: string) => `/api/config/caching/${cacheId}/clear`,
      STATISTICS: '/api/config/caching/statistics',
      REGIONS: '/api/config/caching/regions',
      ENTRIES: (regionId: string) => `/api/config/caching/${regionId}/entries`,
      REFRESH: (regionId: string) => `/api/config/caching/${regionId}/refresh`,
      POLICY: (regionId: string) => `/api/config/caching/${regionId}/policy`,
    },
  },
EOF

echo "✅ Missing endpoints documented in $SDK_FILE.additions"
echo ""
echo "Next steps:"
echo "1. Review the additions file"
echo "2. Manually integrate these into the constants.ts file"
echo "3. Remove deprecated PROVIDER_MODELS section"
echo "4. Clean up duplicate or obsolete endpoints"