#!/bin/bash

# Fix test files in src/__tests__/
for file in src/__tests__/FetchProviderModelsService.test.ts \
           src/__tests__/FetchAnalyticsService.test.ts \
           src/__tests__/FetchSettingsService.test.ts \
           src/__tests__/FetchModelMappingsService.test.ts \
           src/__tests__/FetchSystemService.test.ts; do
  if [ -f "$file" ]; then
    echo "Fixing $file..."
    # Add import for mock helper
    sed -i '1s/^/import { createMockClient, type MockClient } from '\''\.\/helpers\/mockClient'\'';\n/' "$file"
    
    # Replace FetchBaseApiClient type with MockClient
    sed -i 's/let mockClient: FetchBaseApiClient;/let mockClient: MockClient;/' "$file"
    sed -i 's/import type { FetchBaseApiClient }.*$//' "$file"
    
    # Replace mock client creation
    sed -i '/mockClient = {/,/} as any;/{
      s/mockClient = {/mockClient = createMockClient();/
      /get: jest.fn/d
      /post: jest.fn/d
      /put: jest.fn/d
      /patch: jest.fn/d
      /delete: jest.fn/d
      /request: jest.fn/d
      /} as any;/d
    }' "$file"
    
    # Update service instantiation
    sed -i 's/new \(.*Service\)(mockClient);/new \1(mockClient as any);/' "$file"
    
    # Remove type casting from mock calls
    sed -i 's/(mockClient\.get as jest\.Mock)/mockClient.get/g' "$file"
    sed -i 's/(mockClient\.post as jest\.Mock)/mockClient.post/g' "$file"
    sed -i 's/(mockClient\.put as jest\.Mock)/mockClient.put/g' "$file"
    sed -i 's/(mockClient\.delete as jest\.Mock)/mockClient.delete/g' "$file"
    sed -i 's/(mockClient\.patch as jest\.Mock)/mockClient.patch/g' "$file"
    
    # Clean up empty lines
    sed -i '/^$/N;/^\n$/d' "$file"
  fi
done

echo "Test files fixed!"