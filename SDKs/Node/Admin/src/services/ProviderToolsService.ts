import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';

export interface ProviderTool {
  id: number;
  isActive: boolean;
  updatedAt: string;
  provider: number; // ProviderType enum value
  toolName: string;
  toolParameters?: string | null;
  costPerUnit?: number | null;
  billingUnit?: string | null;
  costDescription?: string | null;
  providerName?: string | null;
}

export interface CreateProviderTool {
  provider: number;
  toolName: string;
  toolParameters?: string | null;
  costPerUnit?: number | null;
  billingUnit?: string | null;
  costDescription?: string | null;
  isActive?: boolean;
}

export interface UpdateProviderTool {
  isActive: boolean;
  toolParameters?: string | null;
  costPerUnit?: number | null;
  billingUnit?: string | null;
  costDescription?: string | null;
}

export interface ProviderOption {
  value: number;
  name: string;
  description: string;
}

export interface ImportResult {
  imported: number;
  skipped: number;
  total: number;
  errors?: string[] | null;
}

/**
 * Service for managing provider tools and their costs
 */
export class ProviderToolsService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Gets all provider tools
   * @param provider Optional provider type filter
   * @param isActive Optional active status filter
   */
  async getProviderTools(provider?: number, isActive?: boolean, config?: RequestConfig): Promise<ProviderTool[]> {
    const params = new URLSearchParams();
    if (provider !== undefined) {
      params.append('provider', provider.toString());
    }
    if (isActive !== undefined) {
      params.append('isActive', isActive.toString());
    }

    const queryString = params.toString();
    const url = `/api/admin/provider-tools${queryString ? `?${queryString}` : ''}`;
    
    return this.client['get']<ProviderTool[]>(url, {
      signal: config?.signal,
      timeout: config?.timeout,
      headers: config?.headers,
    });
  }

  /**
   * Gets a specific provider tool by ID
   * @param id Tool ID
   */
  async getProviderTool(id: number, config?: RequestConfig): Promise<ProviderTool> {
    return this.client['get']<ProviderTool>(
      `/api/admin/provider-tools/${id}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Creates a new provider tool
   * @param tool Provider tool creation data
   */
  async createProviderTool(tool: CreateProviderTool, config?: RequestConfig): Promise<ProviderTool> {
    return this.client['post']<ProviderTool>(
      '/api/admin/provider-tools',
      tool,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Updates an existing provider tool
   * @param id Tool ID
   * @param updates Updated tool data
   */
  async updateProviderTool(id: number, updates: UpdateProviderTool, config?: RequestConfig): Promise<ProviderTool> {
    return this.client['put']<ProviderTool>(
      `/api/admin/provider-tools/${id}`,
      updates,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Deletes a provider tool
   * @param id Tool ID
   */
  async deleteProviderTool(id: number, config?: RequestConfig): Promise<void> {
    return this.client['delete']<void>(
      `/api/admin/provider-tools/${id}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Gets available provider types that support tools
   */
  async getToolProviders(config?: RequestConfig): Promise<ProviderOption[]> {
    return this.client['get']<ProviderOption[]>(
      '/api/admin/provider-tools/providers',
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Gets available billing units
   */
  async getBillingUnits(config?: RequestConfig): Promise<string[]> {
    return this.client['get']<string[]>(
      '/api/admin/provider-tools/billing-units',
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Bulk import provider tools from a JSON array
   * @param tools Array of provider tools to import
   */
  async importProviderTools(tools: CreateProviderTool[], config?: RequestConfig): Promise<ImportResult> {
    return this.client['post']<ImportResult>(
      '/api/admin/provider-tools/import',
      tools,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Exports all provider tools as JSON
   */
  async exportProviderTools(config?: RequestConfig): Promise<ProviderTool[]> {
    return this.client['get']<ProviderTool[]>(
      '/api/admin/provider-tools/export',
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }
}