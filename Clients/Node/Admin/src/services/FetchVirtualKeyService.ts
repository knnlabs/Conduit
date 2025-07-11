import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { components } from '../generated/admin-api';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';

// Type aliases for better readability
type VirtualKeyDto = components['schemas']['VirtualKeyDto'];
type CreateVirtualKeyRequestDto = components['schemas']['CreateVirtualKeyRequestDto'];
type UpdateVirtualKeyRequestDto = components['schemas']['UpdateVirtualKeyRequestDto'];
type VirtualKeyValidationResponseDto = components['schemas']['VirtualKeyValidationResult'];

// Define inline types for responses that aren't in the generated schemas
interface VirtualKeyListResponseDto {
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
    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: pageSize.toString(),
    });
    
    return this.client['get']<VirtualKeyListResponseDto>(
      `${ENDPOINTS.VIRTUAL_KEYS.BASE}?${params.toString()}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
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
  ): Promise<VirtualKeyDto> {
    return this.client['post']<VirtualKeyDto, CreateVirtualKeyRequestDto>(
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
   * Helper method to check if a key is active and within budget
   */
  isKeyValid(key: VirtualKeyDto): boolean {
    if (!key.isActive) return false;
    
    const now = new Date();
    const expiresAt = key.expiresAt ? new Date(key.expiresAt) : null;
    
    if (expiresAt && expiresAt < now) {
      return false;
    }
    
    if (key.maxBudget !== null && key.maxBudget !== undefined) {
      const currentSpend = key.currentSpend || 0;
      if (currentSpend >= key.maxBudget) {
        return false;
      }
    }
    
    return true;
  }

  /**
   * Helper method to calculate remaining budget
   */
  getRemainingBudget(key: VirtualKeyDto): number | null {
    if (key.maxBudget === null || key.maxBudget === undefined) {
      return null;
    }
    
    const currentSpend = key.currentSpend || 0;
    return Math.max(0, key.maxBudget - currentSpend);
  }

  /**
   * Helper method to format budget duration
   */
  formatBudgetDuration(duration: VirtualKeyDto['budgetDuration']): string {
    switch (duration) {
      case 'Daily':
        return 'per day';
      case 'Weekly':
        return 'per week';
      case 'Monthly':
        return 'per month';
      case 'Yearly':
        return 'per year';
      case 'OneTime':
        return 'one-time';
      default:
        return 'unknown';
    }
  }
}