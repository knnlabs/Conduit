import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { components } from '../generated/admin-api';
import type { RequestConfig } from '../client/types';
import { HttpMethod } from '../client/HttpMethod';

export type ProviderTool = components['schemas']['ProviderToolDto'] & Required<Pick<components['schemas']['ProviderToolDto'], 'id' | 'provider' | 'toolName' | 'isActive' | 'updatedAt'>>;
export type CreateProviderTool = components['schemas']['CreateProviderToolDto'];
export type UpdateProviderTool = components['schemas']['UpdateProviderToolDto'];
export type ProviderOption = components['schemas']['ToolProviderDto'] & Required<Pick<components['schemas']['ToolProviderDto'], 'value' | 'name' | 'description'>>;
export type ImportResult = components['schemas']['ProviderToolImportResultDto'];
type ProviderToolWire = components['schemas']['ProviderToolDto'];
type ProviderOptionWire = components['schemas']['ToolProviderDto'];

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
    const query = { provider, isActive };
    const params = new URLSearchParams();
    if (provider !== undefined) params.set('provider', String(provider));
    if (isActive !== undefined) params.set('isActive', String(isActive));
    const resolvedPath = params.size ? `/api/admin/provider-tools?${params}` : '/api/admin/provider-tools';
    const data = await this.client['executeContractRead']<ProviderToolWire[]>(resolvedPath, (client, options) => client.GET('/api/admin/provider-tools', { ...options, params: { query } }), config);
    return data.map(tool => tool as ProviderTool);
  }

  /**
   * Gets a specific provider tool by ID
   * @param id Tool ID
   */
  async getProviderTool(id: number, config?: RequestConfig): Promise<ProviderTool> {
    return this.client['executeContractRead'](`/api/admin/provider-tools/${id}`, (client, options) => client.GET('/api/admin/provider-tools/{id}', { ...options, params: { path: { id } } }), config) as Promise<ProviderTool>;
  }

  /**
   * Creates a new provider tool
   * @param tool Provider tool creation data
   */
  async createProviderTool(tool: CreateProviderTool, config?: RequestConfig): Promise<ProviderTool> {
    return this.client['executeContractOperation']<ProviderToolWire, CreateProviderTool>('/api/admin/provider-tools', HttpMethod.POST, (client, options) => client.POST('/api/admin/provider-tools', { ...options, body: tool }), config, tool) as Promise<ProviderTool>;
  }

  /**
   * Updates an existing provider tool
   * @param id Tool ID
   * @param updates Updated tool data
   */
  async updateProviderTool(id: number, updates: UpdateProviderTool, config?: RequestConfig): Promise<ProviderTool> {
    return this.client['executeContractOperation']<ProviderToolWire, UpdateProviderTool>(`/api/admin/provider-tools/${id}`, HttpMethod.PUT, (client, options) => client.PUT('/api/admin/provider-tools/{id}', { ...options, params: { path: { id } }, body: updates }), config, updates) as Promise<ProviderTool>;
  }

  /**
   * Deletes a provider tool
   * @param id Tool ID
   */
  async deleteProviderTool(id: number, config?: RequestConfig): Promise<void> {
    return this.client['executeContractOperation'](`/api/admin/provider-tools/${id}`, HttpMethod.DELETE, (client, options) => client.DELETE('/api/admin/provider-tools/{id}', { ...options, params: { path: { id } } }), config);
  }

  /**
   * Gets available provider types that support tools
   */
  async getToolProviders(config?: RequestConfig): Promise<ProviderOption[]> {
    const data = await this.client['executeContractRead']<ProviderOptionWire[]>('/api/admin/provider-tools/providers', (client, options) => client.GET('/api/admin/provider-tools/providers', options), config);
    return data.map(provider => provider as ProviderOption);
  }

  /**
   * Gets available billing units
   */
  async getBillingUnits(config?: RequestConfig): Promise<string[]> {
    return this.client['executeContractRead']('/api/admin/provider-tools/billing-units', (client, options) => client.GET('/api/admin/provider-tools/billing-units', options), config);
  }

  /**
   * Bulk import provider tools from a JSON array
   * @param tools Array of provider tools to import
   */
  async importProviderTools(tools: CreateProviderTool[], config?: RequestConfig): Promise<ImportResult> {
    return this.client['executeContractOperation']<ImportResult, CreateProviderTool[]>('/api/admin/provider-tools/import', HttpMethod.POST, (client, options) => client.POST('/api/admin/provider-tools/import', { ...options, body: tools }), config, tools);
  }

  /**
   * Exports all provider tools as JSON
   */
  async exportProviderTools(config?: RequestConfig): Promise<ProviderTool[]> {
    const data = await this.client['executeContractRead']<ProviderToolWire[]>('/api/admin/provider-tools/export', (client, options) => client.GET('/api/admin/provider-tools/export', options), config);
    return data.map(tool => tool as ProviderTool);
  }
}
