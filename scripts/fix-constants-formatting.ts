#!/usr/bin/env tsx

import * as fs from 'fs';
import * as path from 'path';

const CONSTANTS_FILE = path.join(__dirname, '../SDKs/Node/Admin/src/constants.ts');

// Read the file
let content = fs.readFileSync(CONSTANTS_FILE, 'utf8');

// Fix the main issues:
// 1. Missing commas after IMPORT key
content = content.replace(/IMPORT: '\/api\/ModelCosts\/import'\}/g, "IMPORT: '/api/ModelCosts/import',\n  },");

// 2. Missing comma after OVERVIEW
content = content.replace(/OVERVIEW: '\/api\/ModelCosts\/overview'\}/g, "OVERVIEW: '/api/ModelCosts/overview',\n  },");

// 3. Fix AUDIO section formatting
content = content.replace(/TEST: (.*?)\}/g, "TEST: $1,\n    },");
content = content.replace(/CURRENT: '\/api\/admin\/audio\/costs\/current'\}/g, "CURRENT: '/api/admin/audio/costs/current',\n    },");
content = content.replace(/BY_PROVIDER: (.*?)\}/g, "BY_PROVIDER: $1,\n    },");
content = content.replace(/METRICS: '\/api\/admin\/audio\/sessions\/metrics'\}\}/g, "METRICS: '/api/admin/audio/sessions/metrics',\n    },\n  },");

// 4. Fix MEDIA section
content = content.replace(/BY_TYPE: '\/api\/admin\/Media\/stats\/by-type'\}/g, "BY_TYPE: '/api/admin/Media/stats/by-type',\n    },");
content = content.replace(/PRUNE: '\/api\/admin\/Media\/cleanup\/prune'\}\}/g, "PRUNE: '/api/admin/Media/cleanup/prune',\n    },\n  },");

// 5. Fix other sections
content = content.replace(/HEALTH: '\/api\/cache\/monitoring\/health'\}/g, "HEALTH: '/api/cache/monitoring/health',\n  },");
content = content.replace(/DOWNLOAD: (.*?)\}/g, "DOWNLOAD: $1,\n  },");
content = content.replace(/POLICY: (.*?)\}\}/g, "POLICY: $1,\n    },\n  },");
content = content.replace(/SUMMARY: '\/api\/Logs\/summary'\}/g, "SUMMARY: '/api/Logs/summary',\n  },");
content = content.replace(/MARK_ALL_READ: '\/api\/Notifications\/mark-all-read'\}/g, "MARK_ALL_READ: '/api/Notifications/mark-all-read',\n  },");
content = content.replace(/FALLBACK_BY_MODEL: (.*?)\}/g, "FALLBACK_BY_MODEL: $1,\n  },");
content = content.replace(/COMPLIANCE: '\/api\/security\/compliance'\}/g, "COMPLIANCE: '/api/security/compliance',\n  },");

// 6. Fix COSTS section
content = content.replace(/VIRTUAL_KEYS: '\/api\/costs\/virtualkeys'\}/g, "VIRTUAL_KEYS: '/api/costs/virtualkeys',\n  },");

// 7. Fix duplicate issues and formatting
content = content.replace(/HISTORY_BY_PROVIDER:.*?\n.*?CHECK/g, "HISTORY_BY_PROVIDER: (providerType: ProviderType) => `/api/ProviderHealth/history/${providerType}`,\n    CHECK");
content = content.replace(/PERFORMANCE:.*?performance'\}/g, "");

// 8. Clean up SYSTEM section
content = content.replace(/NOTIFICATIONS: '\/api\/Notifications',\s*NOTIFICATION_BY_ID/g, "NOTIFICATIONS: '/api/Notifications',\n    NOTIFICATION_BY_ID");
content = content.replace(/NOTIFICATION_BY_ID: .*?\}/g, "NOTIFICATION_BY_ID: (id: number) => `/api/Notifications/${id}`,\n  },");

// 9. Fix METRICS section
content = content.replace(/ADMIN_DATABASE_POOL: '\/metrics\/database\/pool'\}/g, "ADMIN_DATABASE_POOL: '/metrics/database/pool',\n  },");

// 10. Fix SETTINGS section  
content = content.replace(/ROUTER: '\/api\/Router\/config'\}/g, "ROUTER: '/api/Router/config',\n  },");

// 11. Remove ERROR_QUEUES if it still exists after main content
content = content.replace(/\/\/ Error Queue Management[\s\S]*?CLEAR:.*?\}\}/g, '');

// 12. Fix final closing
content = content.replace(/\}\} as const;/g, '} as const;');
content = content.replace(/RESTRICTIVE: 'restrictive'\} as const;/g, "RESTRICTIVE: 'restrictive',\n} as const;");

// Remove "No newline at end of file" comments
content = content.replace(/\s*No newline at end of file/g, '');

// Ensure file ends with newline
if (!content.endsWith('\n')) {
  content += '\n';
}

// Write back
fs.writeFileSync(CONSTANTS_FILE, content);

console.log('✅ Fixed constants file formatting');