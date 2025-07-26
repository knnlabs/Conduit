#!/bin/bash

# Fix test files in src/services/__tests__/
for file in src/services/__tests__/FetchSecurityService.test.ts \
           src/services/__tests__/FetchProviderHealthService.test.ts \
           src/services/__tests__/FetchMonitoringService.test.ts \
           src/services/__tests__/FetchConfigurationService.test.ts; do
  if [ -f "$file" ]; then
    echo "Fixing $file..."
    
    # Add import for mock helper at the top
    sed -i '1s/^/import { createMockClient, type MockClient } from '\''..\/..\/\__tests__\/helpers\/mockClient'\'';\n/' "$file"
    
    # Update mock creation - remove the new FetchBaseApiClient instantiation
    sed -i '/mockClient = new FetchBaseApiClient/,/as jest.Mocked<FetchBaseApiClient>;/{
      s/mockClient = new FetchBaseApiClient.*/mockClient = createMockClient();/
      /baseUrl:/d
      /masterKey:/d
      /}) as jest.Mocked<FetchBaseApiClient>;/d
    }' "$file"
    
    # Update type declaration
    sed -i 's/let mockClient: jest.Mocked<FetchBaseApiClient>;/let mockClient: MockClient;/' "$file"
    
    # Update service instantiation
    sed -i 's/new \(.*Service\)(mockClient);/new \1(mockClient as any);/' "$file"
    
    # Remove (mockClient as any).method patterns
    sed -i 's/(mockClient as any)\.get/mockClient.get/g' "$file"
    sed -i 's/(mockClient as any)\.post/mockClient.post/g' "$file"
    sed -i 's/(mockClient as any)\.put/mockClient.put/g' "$file"
    sed -i 's/(mockClient as any)\.delete/mockClient.delete/g' "$file"
    sed -i 's/(mockClient as any)\.patch/mockClient.patch/g' "$file"
    
    # Clean up imports - remove jest mock for FetchBaseApiClient
    sed -i "/jest\.mock('.*FetchBaseApiClient.*/d" "$file"
    
    # Clean up empty lines
    sed -i '/^$/N;/^\n$/d' "$file"
  fi
done

echo "Services test files fixed!"