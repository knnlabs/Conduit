import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
import type { 
  VirtualKeyGroupDto, 
  CreateVirtualKeyGroupRequestDto, 
  UpdateVirtualKeyGroupRequestDto,
  AdjustBalanceDto,
  VirtualKeyDto,
  VirtualKeyGroupTransactionDto,
  TransactionHistoryParams
} from '../models/virtualKey';
import type { PagedResult } from '../models/security';

/**
 * Type-safe Virtual Key Group service using native fetch
 */
export class FetchVirtualKeyGroupService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get all virtual key groups
   */
  async list(config?: RequestConfig): Promise<VirtualKeyGroupDto[]> {
    return this.client['get']<VirtualKeyGroupDto[]>(
      ENDPOINTS.VIRTUAL_KEY_GROUPS,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get a specific virtual key group by ID
   */
  async get(id: number, config?: RequestConfig): Promise<VirtualKeyGroupDto> {
    return this.client['get']<VirtualKeyGroupDto>(
      `${ENDPOINTS.VIRTUAL_KEY_GROUPS}/${id}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Create a new virtual key group
   */
  async create(data: CreateVirtualKeyGroupRequestDto, config?: RequestConfig): Promise<VirtualKeyGroupDto> {
    return this.client['post']<VirtualKeyGroupDto>(
      ENDPOINTS.VIRTUAL_KEY_GROUPS,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update a virtual key group
   */
  async update(id: number, data: UpdateVirtualKeyGroupRequestDto, config?: RequestConfig): Promise<void> {
    await this.client['put'](
      `${ENDPOINTS.VIRTUAL_KEY_GROUPS}/${id}`,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Adjust the balance of a virtual key group
   */
  async adjustBalance(id: number, data: AdjustBalanceDto, config?: RequestConfig): Promise<VirtualKeyGroupDto> {
    return this.client['post']<VirtualKeyGroupDto>(
      `${ENDPOINTS.VIRTUAL_KEY_GROUPS}/${id}/adjust-balance`,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete a virtual key group
   */
  async delete(id: number, config?: RequestConfig): Promise<void> {
    await this.client['delete'](
      `${ENDPOINTS.VIRTUAL_KEY_GROUPS}/${id}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get virtual keys in a group
   */
  async getKeys(id: number, config?: RequestConfig): Promise<VirtualKeyDto[]> {
    return this.client['get']<VirtualKeyDto[]>(
      `${ENDPOINTS.VIRTUAL_KEY_GROUPS}/${id}/keys`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get transaction history for a virtual key group (paginated)
   */
  async getTransactionHistory(
    id: number, 
    params?: TransactionHistoryParams,
    config?: RequestConfig
  ): Promise<PagedResult<VirtualKeyGroupTransactionDto>> {
    const queryParams = new URLSearchParams();
    if (params?.page !== undefined) {
      queryParams.append('page', params.page.toString());
    }
    if (params?.pageSize !== undefined) {
      queryParams.append('pageSize', params.pageSize.toString());
    }

    const queryString = queryParams.toString();
    const url = `${ENDPOINTS.VIRTUAL_KEY_GROUPS}/${id}/transactions${queryString ? `?${queryString}` : ''}`;

    return this.client['get']<PagedResult<VirtualKeyGroupTransactionDto>>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }
}