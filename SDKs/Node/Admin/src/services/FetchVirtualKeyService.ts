import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { components } from '../generated/admin-api';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';

// Type aliases for better readability
type VirtualKeyDto = components['schemas']['ConduitLLM.Configuration.DTOs.VirtualKey.VirtualKeyDto'];
type CreateVirtualKeyRequestDto = components['schemas']['ConduitLLM.Configuration.DTOs.VirtualKey.CreateVirtualKeyRequestDto'];
type CreateVirtualKeyResponseDto = components['schemas']['ConduitLLM.Configuration.DTOs.VirtualKey.CreateVirtualKeyResponseDto'];
type UpdateVirtualKeyRequestDto = components['schemas']['ConduitLLM.Configuration.DTOs.VirtualKey.UpdateVirtualKeyRequestDto'];
type VirtualKeyValidationResponseDto = components['schemas']['ConduitLLM.Configuration.DTOs.VirtualKey.VirtualKeyValidationResult'];

// Define inline types for responses that aren't in the generated schemas
export interface VirtualKeyListResponseDto {
  items: VirtualKeyDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

interface VirtualKeySpendDto {
  id: number;
  virtualKeyId: number;
  timestamp: string;
  modelUsed: string;
  inputTokens: number;
  outputTokens: number;
  totalTokens: number;
  cost: number;
  requestId?: string;
  metadata?: string;
}

interface VirtualKeyDiscoveryPreviewDto {
  data: DiscoveredModelDto[];
  count: number;
}

interface DiscoveredModelDto {
  id: string;
  provider?: string;
  displayName: string;
  capabilities: Record<string, unknown>;
}

/**
 * Type-safe Virtual Key service using native fetch
 */
export class FetchVirtualKeyService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get all virtual keys with optional pagination
   */
  async list(
    page: number = 1,
    pageSize: number = 10,
    config?: RequestConfig
  ): Promise<VirtualKeyListResponseDto> {
    // The Admin API returns VirtualKeyDto[] directly, not a paginated response
    // So we need to fetch all items and simulate pagination
    const allItems = await this.client['get']<VirtualKeyDto[]>(
      ENDPOINTS.VIRTUAL_KEYS.BASE,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
    
    // Simulate pagination on the client side
    const totalCount = allItems.length;
    const startIndex = (page - 1) * pageSize;
    const endIndex = Math.min(startIndex + pageSize, totalCount);
    const items = allItems.slice(startIndex, endIndex);
    const totalPages = Math.ceil(totalCount / pageSize);
    
    return {
      items,
      totalCount,
      page,
      pageSize,
      totalPages,
    };
  }

  /**
   * Get a virtual key by ID
   */
  async get(id: string, config?: RequestConfig): Promise<VirtualKeyDto> {
    return this.client['get']<VirtualKeyDto>(
      ENDPOINTS.VIRTUAL_KEYS.BY_ID(parseInt(id)),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get a virtual key by the key value
   */
  async getByKey(key: string, config?: RequestConfig): Promise<VirtualKeyDto> {
    return this.client['get']<VirtualKeyDto>(
      `/virtualkeys/by-key/${encodeURIComponent(key)}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Create a new virtual key
   */
  async create(
    data: CreateVirtualKeyRequestDto,
    config?: RequestConfig
  ): Promise<CreateVirtualKeyResponseDto> {
    return this.client['post']<CreateVirtualKeyResponseDto, CreateVirtualKeyRequestDto>(
      ENDPOINTS.VIRTUAL_KEYS.BASE,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update an existing virtual key
   */
  async update(
    id: string,
    data: UpdateVirtualKeyRequestDto,
    config?: RequestConfig
  ): Promise<VirtualKeyDto> {
    return this.client['put']<VirtualKeyDto, UpdateVirtualKeyRequestDto>(
      ENDPOINTS.VIRTUAL_KEYS.BY_ID(parseInt(id)),
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete a virtual key
   */
  async delete(id: string, config?: RequestConfig): Promise<void> {
    return this.client['delete']<void>(
      ENDPOINTS.VIRTUAL_KEYS.BY_ID(parseInt(id)),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Regenerate a virtual key's key value
   */
  async regenerateKey(id: string, config?: RequestConfig): Promise<VirtualKeyDto> {
    return this.client['post']<VirtualKeyDto>(
      `/virtualkeys/${id}/regenerate-key`,
      undefined,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Validate a virtual key
   */
  async validate(
    key: string,
    config?: RequestConfig
  ): Promise<VirtualKeyValidationResponseDto> {
    return this.client['post']<VirtualKeyValidationResponseDto>(
      ENDPOINTS.VIRTUAL_KEYS.VALIDATE,
      { key },
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get spend history for a virtual key
   */
  async getSpend(
    id: string,
    page: number = 1,
    pageSize: number = 10,
    startDate?: string,
    endDate?: string,
    config?: RequestConfig
  ): Promise<VirtualKeySpendDto[]> {
    const params = new URLSearchParams();
    params.append('page', page.toString());
    params.append('pageSize', pageSize.toString());
    if (startDate) params.append('startDate', startDate);
    if (endDate) params.append('endDate', endDate);
    
    return this.client['get']<VirtualKeySpendDto[]>(
      `${ENDPOINTS.VIRTUAL_KEYS.SPEND(parseInt(id))}?${params.toString()}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Reset spend for a virtual key
   */
  async resetSpend(id: string, config?: RequestConfig): Promise<void> {
    return this.client['post']<void>(
      ENDPOINTS.VIRTUAL_KEYS.RESET_SPEND(parseInt(id)),
      undefined,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Run maintenance tasks for virtual keys
   */
  async maintenance(config?: RequestConfig): Promise<{ message: string }> {
    return this.client['post']<{ message: string }>(
      ENDPOINTS.VIRTUAL_KEYS.MAINTENANCE,
      undefined,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Preview what models and capabilities a virtual key would see when calling the discovery endpoint
   */
  async previewDiscovery(
    id: string,
    capability?: string,
    config?: RequestConfig
  ): Promise<VirtualKeyDiscoveryPreviewDto> {
    const params = capability ? `?capability=${encodeURIComponent(capability)}` : '';
    
    return this.client['get']<VirtualKeyDiscoveryPreviewDto>(
      `${ENDPOINTS.VIRTUAL_KEYS.DISCOVERY_PREVIEW(parseInt(id))}${params}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Helper method to check if a key is active and not expired
   */
  isKeyValid(key: VirtualKeyDto): boolean {
    // Check if key is enabled
    if (!key.isEnabled) return false;
    
    const now = new Date();
    const expiresAt = key.expiresAt ? new Date(key.expiresAt) : null;
    
    if (expiresAt && expiresAt < now) {
      return false;
    }
    
    return true;
  }
}