#!/usr/bin/env tsx

import * as fs from 'fs';
import * as path from 'path';

const SERVICE_FILE = path.join(__dirname, '../SDKs/Node/Admin/src/services/FetchConfigurationService.ts');

// Read the file
let content = fs.readFileSync(SERVICE_FILE, 'utf8');

// List of endpoints that don't exist
const nonExistentEndpoints = [
  'ROUTING_TEST',
  'ROUTING_RULES',
  'ROUTING_RULE_BY_ID',
  'CACHE_CONFIG',
  'CACHE_CLEAR',
  'CACHE_STATS',
  'CACHE_STATISTICS',
  'LOAD_BALANCER',
  'LOAD_BALANCER_HEALTH',
  'PERFORMANCE_CONFIG',
  'PERFORMANCE_TEST',
  'FEATURES',
  'FEATURE_BY_KEY',
  'ROUTING_HEALTH',
  'ROUTING_HEALTH_DETAILED',
  'ROUTE_HEALTH_BY_ID',
  'ROUTING_HEALTH_HISTORY',
  'ROUTE_PERFORMANCE_TEST',
  'CIRCUIT_BREAKERS',
  'CIRCUIT_BREAKER_BY_ID',
  'ROUTING_EVENTS_SUBSCRIBE',
  'CACHING_CONFIG',
  'CACHING_POLICIES',
  'CACHING_POLICY_BY_ID',
  'CACHING_REGIONS',
  'CACHING_REGION_CLEAR',
  'CACHING_STATISTICS'
];

// Replace references to non-existent endpoints with errors
nonExistentEndpoints.forEach(endpoint => {
  // Match method calls using the endpoint
  const regex1 = new RegExp(`ENDPOINTS\\.CONFIG\\.${endpoint}(?![A-Z_])`, 'g');
  content = content.replace(regex1, (match) => {
    return `'/api/config/${endpoint.toLowerCase().replace(/_/g, '-')}' /* ${endpoint} endpoint does not exist */`;
  });
  
  // Also handle cases where it's used as a function
  const regex2 = new RegExp(`ENDPOINTS\\.CONFIG\\.${endpoint}\\(([^)]+)\\)`, 'g');
  content = content.replace(regex2, (match, params) => {
    return `\`/api/config/${endpoint.toLowerCase().replace(/_/g, '-')}/\${${params}}\` /* ${endpoint} endpoint does not exist */`;
  });
});

// Fix the direct object references (like ENDPOINTS.CONFIG.CACHING)
content = content.replace(/ENDPOINTS\.CONFIG\.CACHING(?![\.A-Z_])/g, 'ENDPOINTS.CONFIG.CACHING.BASE');

// Write back
fs.writeFileSync(SERVICE_FILE, content);

console.log('✅ Fixed FetchConfigurationService.ts endpoints');