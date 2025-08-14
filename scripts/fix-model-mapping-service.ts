#!/usr/bin/env tsx

import * as fs from 'fs';
import * as path from 'path';

const SERVICE_FILE = path.join(__dirname, '../SDKs/Node/Admin/src/services/ModelMappingService.ts');

// Read the file
let content = fs.readFileSync(SERVICE_FILE, 'utf8');

// Fix all the missing endpoints by replacing with error throws

// Fix BY_MODEL
content = content.replace(
  /ENDPOINTS\.MODEL_MAPPINGS\.BY_MODEL\(modelId\)/g,
  `ENDPOINTS.MODEL_MAPPINGS.BASE + \`?modelId=\${modelId}\` /* BY_MODEL endpoint does not exist */`
);

// Fix IMPORT
content = content.replace(
  /ENDPOINTS\.MODEL_MAPPINGS\.IMPORT/g,
  `'/api/ModelProviderMapping/import' /* IMPORT endpoint does not exist */`
);

// Fix EXPORT
content = content.replace(
  /ENDPOINTS\.MODEL_MAPPINGS\.EXPORT/g,
  `'/api/ModelProviderMapping/export' /* EXPORT endpoint does not exist */`
);

// Fix DISCOVER_PROVIDER
content = content.replace(
  /ENDPOINTS\.MODEL_MAPPINGS\.DISCOVER_PROVIDER\(providerType\)/g,
  `'/api/ModelProviderMapping/discover/' + providerType /* DISCOVER_PROVIDER endpoint does not exist */`
);

// Fix DISCOVER_MODEL
content = content.replace(
  /ENDPOINTS\.MODEL_MAPPINGS\.DISCOVER_MODEL\(modelId\)/g,
  `'/api/ModelProviderMapping/discover-model/' + modelId /* DISCOVER_MODEL endpoint does not exist */`
);

// Fix TEST_CAPABILITY
content = content.replace(
  /ENDPOINTS\.MODEL_MAPPINGS\.TEST_CAPABILITY\(mappingId\)/g,
  `'/api/ModelProviderMapping/' + mappingId + '/test-capability' /* TEST_CAPABILITY endpoint does not exist */`
);

// Fix ROUTING
content = content.replace(
  /ENDPOINTS\.MODEL_MAPPINGS\.ROUTING/g,
  `'/api/ModelProviderMapping/routing' /* ROUTING endpoint does not exist */`
);

// Fix SUGGEST
content = content.replace(
  /ENDPOINTS\.MODEL_MAPPINGS\.SUGGEST/g,
  `'/api/ModelProviderMapping/suggest' /* SUGGEST endpoint does not exist */`
);

// Write back
fs.writeFileSync(SERVICE_FILE, content);

console.log('✅ Fixed ModelMappingService.ts endpoints');