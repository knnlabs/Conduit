# SDK Implementation Example

This document provides a concrete example of how to implement the missing services in the Conduit Admin SDK.

## Example: Implementing the Providers Service

Based on the pattern used in `FetchVirtualKeyService`, here's how the `FetchProvidersService` should be implemented:

### 1. Service Implementation

**File**: `/SDKs/Node/Admin/src/services/FetchProvidersService.ts`

```typescript
import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { components } from '../generated/admin-api';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';

// Type aliases from OpenAPI schema
type ProviderDto = components['schemas']['ProviderDto'];
type CreateProviderDto = components['schemas']['CreateProviderRequestDto'];
type UpdateProviderDto = components['schemas']['UpdateProviderRequestDto'];
type TestConnectionResult = components['schemas']['TestConnectionResultDto'];
type ProviderHealthStatus = components['schemas']['ProviderHealthStatusDto'];

// Response types
interface ProviderListResponseDto {
  items: ProviderDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

/**
 * Type-safe Providers service using native fetch
 */
export class FetchProvidersService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get all providers with optional pagination
   */
  async list(
    page: number = 1,
    pageSize: number = 10,
    config?: RequestConfig
  ): Promise<ProviderListResponseDto> {
    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: pageSize.toString(),
    });

    return this.client.request<ProviderListResponseDto>({
      method: 'GET',
      url: `${ENDPOINTS.PROVIDERS}?${params}`,
      ...config,
    });
  }

  /**
   * Get a specific provider by ID
   */
  async getById(id: number, config?: RequestConfig): Promise<ProviderDto> {
    return this.client.request<ProviderDto>({
      method: 'GET',
      url: `${ENDPOINTS.PROVIDERS}/${id}`,
      ...config,
    });
  }

  /**
   * Create a new provider
   */
  async create(
    data: CreateProviderDto,
    config?: RequestConfig
  ): Promise<ProviderDto> {
    return this.client.request<ProviderDto>({
      method: 'POST',
      url: ENDPOINTS.PROVIDERS,
      body: data,
      ...config,
    });
  }

  /**
   * Update an existing provider
   */
  async update(
    id: number,
    data: UpdateProviderDto,
    config?: RequestConfig
  ): Promise<ProviderDto> {
    return this.client.request<ProviderDto>({
      method: 'PUT',
      url: `${ENDPOINTS.PROVIDERS}/${id}`,
      body: data,
      ...config,
    });
  }

  /**
   * Delete a provider
   */
  async deleteById(id: number, config?: RequestConfig): Promise<void> {
    await this.client.request<void>({
      method: 'DELETE',
      url: `${ENDPOINTS.PROVIDERS}/${id}`,
      ...config,
    });
  }

  /**
   * Test connection for a specific provider
   */
  async testConnectionById(
    id: number,
    config?: RequestConfig
  ): Promise<TestConnectionResult> {
    return this.client.request<TestConnectionResult>({
      method: 'POST',
      url: `${ENDPOINTS.PROVIDERS}/${id}/test`,
      ...config,
    });
  }

  /**
   * Test a provider configuration without creating it
   */
  async testConfig(
    providerConfig: CreateProviderDto,
    config?: RequestConfig
  ): Promise<TestConnectionResult> {
    return this.client.request<TestConnectionResult>({
      method: 'POST',
      url: `${ENDPOINTS.PROVIDERS}/test`,
      body: providerConfig,
      ...config,
    });
  }

  /**
   * Get health status for all providers
   */
  async getHealthStatus(
    params?: { includeHistory?: boolean },
    config?: RequestConfig
  ): Promise<ProviderHealthStatus[]> {
    const searchParams = new URLSearchParams();
    if (params?.includeHistory) {
      searchParams.set('includeHistory', 'true');
    }

    return this.client.request<ProviderHealthStatus[]>({
      method: 'GET',
      url: `${ENDPOINTS.PROVIDERS}/health${searchParams.toString() ? `?${searchParams}` : ''}`,
      ...config,
    });
  }
}
```

### 2. Update Constants

**File**: `/SDKs/Node/Admin/src/constants.ts`

```typescript
export const ENDPOINTS = {
  // Existing endpoints
  VIRTUAL_KEYS: '/virtual-keys',
  DASHBOARD: '/dashboard',
  
  // New endpoints
  PROVIDERS: '/providers',
  PROVIDER_MODELS: '/provider-models',
  MODEL_MAPPINGS: '/model-mappings',
  SYSTEM: '/system',
  SETTINGS: '/settings',
  ANALYTICS: '/analytics',
  REQUEST_LOGS: '/request-logs',
} as const;
```

### 3. Update the Main Client

**File**: `/SDKs/Node/Admin/src/FetchConduitAdminClient.ts`

```typescript
import { FetchBaseApiClient } from './client/FetchBaseApiClient';
import { FetchVirtualKeyService } from './services/FetchVirtualKeyService';
import { FetchDashboardService } from './services/FetchDashboardService';
import { FetchProvidersService } from './services/FetchProvidersService';
import { FetchProviderModelsService } from './services/FetchProviderModelsService';
import { FetchModelMappingsService } from './services/FetchModelMappingsService';
import { FetchSystemService } from './services/FetchSystemService';
import { FetchSettingsService } from './services/FetchSettingsService';
import { FetchAnalyticsService } from './services/FetchAnalyticsService';
import { FetchProviderHealthService } from './services/FetchProviderHealthService';
import type { ApiClientConfig } from './client/types';

export class FetchConduitAdminClient extends FetchBaseApiClient {
  // Existing services
  public readonly virtualKeys: FetchVirtualKeyService;
  public readonly dashboard: FetchDashboardService;
  
  // New services
  public readonly providers: FetchProvidersService;
  public readonly providerModels: FetchProviderModelsService;
  public readonly modelMappings: FetchModelMappingsService;
  public readonly system: FetchSystemService;
  public readonly settings: FetchSettingsService;
  public readonly analytics: FetchAnalyticsService;
  public readonly providerHealth: FetchProviderHealthService;

  constructor(config: ApiClientConfig) {
    super(config);

    // Initialize all services
    this.virtualKeys = new FetchVirtualKeyService(this);
    this.dashboard = new FetchDashboardService(this);
    this.providers = new FetchProvidersService(this);
    this.providerModels = new FetchProviderModelsService(this);
    this.modelMappings = new FetchModelMappingsService(this);
    this.system = new FetchSystemService(this);
    this.settings = new FetchSettingsService(this);
    this.analytics = new FetchAnalyticsService(this);
    this.providerHealth = new FetchProviderHealthService(this);
  }
}
```

### 4. Export Services and Types

**File**: `/SDKs/Node/Admin/src/index.ts`

```typescript
// Services
export { FetchVirtualKeyService as VirtualKeyService } from './services/FetchVirtualKeyService';
export { FetchProvidersService as ProvidersService } from './services/FetchProvidersService';
export { FetchProviderModelsService as ProviderModelsService } from './services/FetchProviderModelsService';
export { FetchModelMappingsService as ModelMappingsService } from './services/FetchModelMappingsService';
export { FetchSystemService as SystemService } from './services/FetchSystemService';
export { FetchSettingsService as SettingsService } from './services/FetchSettingsService';
export { FetchAnalyticsService as AnalyticsService } from './services/FetchAnalyticsService';
export { FetchProviderHealthService as ProviderHealthService } from './services/FetchProviderHealthService';

// Export provider-related types
export type {
  ProviderDto,
  CreateProviderRequestDto,
  UpdateProviderRequestDto,
  TestConnectionResultDto,
  ProviderHealthStatusDto,
} from './models/provider';
```

## Implementation Checklist

For each service that needs to be implemented:

1. **Create Service File**
   - Follow the pattern from `FetchVirtualKeyService`
   - Import necessary types from generated schemas
   - Define response types for paginated endpoints
   - Implement all required methods

2. **Update Constants**
   - Add endpoint paths to `ENDPOINTS` constant

3. **Update Main Client**
   - Add service property
   - Initialize service in constructor

4. **Export Types and Services**
   - Export service class
   - Export related DTOs and types

5. **Update Generated Types (if needed)**
   - Ensure OpenAPI schema includes all required types
   - Regenerate types if schema is updated

## Testing Strategy

1. **Unit Tests**
   ```typescript
   describe('FetchProvidersService', () => {
     it('should list providers', async () => {
       const mockClient = createMockClient();
       const service = new FetchProvidersService(mockClient);
       
       const result = await service.list();
       
       expect(mockClient.request).toHaveBeenCalledWith({
         method: 'GET',
         url: '/providers?page=1&pageSize=10',
       });
     });
   });
   ```

2. **Integration Tests**
   - Test against actual backend API
   - Verify response types match expectations
   - Test error handling

3. **Type Tests**
   - Ensure all methods have proper type inference
   - Verify no `any` types are exposed

## Notes

- The SDK should maintain backward compatibility
- All methods should support optional `RequestConfig` for custom headers, timeouts, etc.
- Error handling should use the existing `ConduitError` types
- Follow the existing naming conventions (e.g., `getById`, `deleteById`)
- Paginated endpoints should return consistent response structure