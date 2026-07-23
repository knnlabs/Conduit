import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { components } from '../generated/admin-api';
import type { RequestConfig } from '../client/types';
import { HttpMethod } from '../client/HttpMethod';
import {
  type FunctionConfigurationDto, type CreateFunctionConfigurationDto, type UpdateFunctionConfigurationDto,
  type FunctionCredentialDto, type CreateFunctionCredentialDto, type UpdateFunctionCredentialDto,
  type TestCredentialRequestDto, type TestCredentialResponseDto, type FunctionCostDto,
  type CreateFunctionCostDto, type UpdateFunctionCostDto, type FunctionExecutionDto,
  FunctionProviderType, FunctionPurpose, ExecutionState,
} from '../models/functions';
import { ValidationError } from '../utils/errors';
import { validateRequired, validateStringLength } from '../utils/validation';

type ConfigurationWire = components['schemas']['FunctionConfigurationDto'];
type CreateConfigurationWire = components['schemas']['CreateFunctionConfigurationRequest'];
type UpdateConfigurationWire = components['schemas']['UpdateFunctionConfigurationRequest'];
type CredentialWire = components['schemas']['FunctionCredential'];
type CostWire = components['schemas']['FunctionCostDto'];
type CreateCostWire = components['schemas']['CreateFunctionCostDto'];
type UpdateCostWire = components['schemas']['UpdateFunctionCostDto'];
type ExecutionWire = components['schemas']['FunctionExecutionDto'];

const configurationFromWire = (value: ConfigurationWire): FunctionConfigurationDto => value as unknown as FunctionConfigurationDto;
const credentialFromWire = (value: CredentialWire): FunctionCredentialDto => value as unknown as FunctionCredentialDto;
const executionFromWire = (value: ExecutionWire): FunctionExecutionDto => value as unknown as FunctionExecutionDto;
const costFromWire = (value: CostWire): FunctionCostDto => ({
  ...value,
  baseCost: value.baseCost ?? undefined,
  pricingConfiguration: value.pricingConfiguration ?? '',
  expiryDate: value.expiryDate ?? undefined,
  description: value.description ?? undefined,
}) as unknown as FunctionCostDto;

function validateCreateConfiguration(data: CreateFunctionConfigurationDto): void {
  validateRequired(data, ['configurationName', 'providerType', 'purpose']);
  validateStringLength(data.configurationName, 1, 255, 'configurationName');
  if (data.timeoutSeconds !== undefined && data.timeoutSeconds < 1) throw new ValidationError('timeoutSeconds must be at least 1');
}
function validateCreateCredential(data: CreateFunctionCredentialDto): void {
  validateRequired(data, ['providerType', 'keyName', 'apiKey']);
  validateStringLength(data.keyName, 1, 255, 'keyName');
  validateStringLength(data.apiKey, 1, 1000, 'apiKey');
}
function validateCreateCost(data: CreateFunctionCostDto): void {
  validateRequired(data, ['costName', 'providerType', 'pricingModel', 'pricingConfiguration']);
  validateStringLength(data.costName, 1, 255, 'costName');
  try { JSON.parse(data.pricingConfiguration); } catch { throw new ValidationError('pricingConfiguration must be valid JSON'); }
}

export class FetchFunctionConfigurationsService {
  constructor(private readonly client: FetchBaseApiClient) {}
  async list(config?: RequestConfig): Promise<FunctionConfigurationDto[]> {
    const data = await this.client['executeContractRead']('/api/FunctionConfigurations', (c, o) => c.GET('/api/FunctionConfigurations', o), config);
    return data.map(configurationFromWire);
  }
  async getById(id: number, config?: RequestConfig): Promise<FunctionConfigurationDto> {
    const data = await this.client['executeContractRead'](`/api/FunctionConfigurations/${id}`, (c, o) => c.GET('/api/FunctionConfigurations/{id}', { ...o, params: { path: { id } } }), config);
    return configurationFromWire(data);
  }
  async getByProvider(providerType: FunctionProviderType, config?: RequestConfig): Promise<FunctionConfigurationDto[]> {
    const provider = providerType;
    const data = await this.client['executeContractRead'](`/api/FunctionConfigurations/provider/${encodeURIComponent(provider)}`, (c, o) => c.GET('/api/FunctionConfigurations/provider/{providerType}', { ...o, params: { path: { providerType: provider } } }), config);
    return data.map(configurationFromWire);
  }
  async getByPurpose(purpose: FunctionPurpose, config?: RequestConfig): Promise<FunctionConfigurationDto[]> {
    const value = purpose;
    const data = await this.client['executeContractRead'](`/api/FunctionConfigurations/purpose/${encodeURIComponent(value)}`, (c, o) => c.GET('/api/FunctionConfigurations/purpose/{purpose}', { ...o, params: { path: { purpose: value } } }), config);
    return data.map(configurationFromWire);
  }
  async create(data: CreateFunctionConfigurationDto, config?: RequestConfig): Promise<FunctionConfigurationDto> {
    validateCreateConfiguration(data);
    const body = data as unknown as CreateConfigurationWire;
    const result = await this.client['executeContractOperation']<ConfigurationWire, CreateConfigurationWire>('/api/FunctionConfigurations', HttpMethod.POST, (c, o) => c.POST('/api/FunctionConfigurations', { ...o, body }), config, body);
    return configurationFromWire(result);
  }
  async update(id: number, data: UpdateFunctionConfigurationDto, config?: RequestConfig): Promise<FunctionConfigurationDto> {
    const body = data as unknown as UpdateConfigurationWire;
    const result = await this.client['executeContractOperation']<ConfigurationWire, UpdateConfigurationWire>(`/api/FunctionConfigurations/${id}`, HttpMethod.PUT, (c, o) => c.PUT('/api/FunctionConfigurations/{id}', { ...o, params: { path: { id } }, body }), config, body);
    return configurationFromWire(result);
  }
  async deleteById(id: number, config?: RequestConfig): Promise<void> {
    return this.client['executeContractOperation'](`/api/FunctionConfigurations/${id}`, HttpMethod.DELETE, (c, o) => c.DELETE('/api/FunctionConfigurations/{id}', { ...o, params: { path: { id } } }), config);
  }
}

export class FetchFunctionCredentialsService {
  constructor(private readonly client: FetchBaseApiClient) {}
  async list(config?: RequestConfig): Promise<FunctionCredentialDto[]> { const data = await this.client['executeContractRead']('/api/FunctionCredentials', (c, o) => c.GET('/api/FunctionCredentials', o), config); return data.map(credentialFromWire); }
  async getById(id: number, config?: RequestConfig): Promise<FunctionCredentialDto> { const data = await this.client['executeContractRead'](`/api/FunctionCredentials/${id}`, (c, o) => c.GET('/api/FunctionCredentials/{id}', { ...o, params: { path: { id } } }), config); return credentialFromWire(data); }
  async getByConfiguration(functionConfigurationId: number, config?: RequestConfig): Promise<FunctionCredentialDto[]> { const data = await this.client['executeContractRead'](`/api/FunctionCredentials/configuration/${functionConfigurationId}`, (c, o) => c.GET('/api/FunctionCredentials/configuration/{functionConfigurationId}', { ...o, params: { path: { functionConfigurationId } } }), config); return data.map(credentialFromWire); }
  async create(data: CreateFunctionCredentialDto, config?: RequestConfig): Promise<FunctionCredentialDto> { validateCreateCredential(data); const body = data as unknown as CredentialWire; const result = await this.client['executeContractOperation']<CredentialWire, CreateFunctionCredentialDto>('/api/FunctionCredentials', HttpMethod.POST, (c, o) => c.POST('/api/FunctionCredentials', { ...o, body }), config, data); return credentialFromWire(result); }
  async update(id: number, data: UpdateFunctionCredentialDto, config?: RequestConfig): Promise<FunctionCredentialDto> { const body = data as unknown as CredentialWire; const result = await this.client['executeContractOperation']<CredentialWire, UpdateFunctionCredentialDto>(`/api/FunctionCredentials/${id}`, HttpMethod.PUT, (c, o) => c.PUT('/api/FunctionCredentials/{id}', { ...o, params: { path: { id } }, body }), config, data); return credentialFromWire(result); }
  async deleteById(id: number, config?: RequestConfig): Promise<void> { return this.client['executeContractOperation'](`/api/FunctionCredentials/${id}`, HttpMethod.DELETE, (c, o) => c.DELETE('/api/FunctionCredentials/{id}', { ...o, params: { path: { id } } }), config); }
  async testCredential(data: TestCredentialRequestDto, config?: RequestConfig): Promise<TestCredentialResponseDto> { return this.client['executeContractOperation']('/api/FunctionCredentials/test', HttpMethod.POST, (c, o) => c.POST('/api/FunctionCredentials/test', { ...o, body: data }), config, data) as Promise<TestCredentialResponseDto>; }
}

export class FetchFunctionCostsService {
  constructor(private readonly client: FetchBaseApiClient) {}
  async list(config?: RequestConfig): Promise<FunctionCostDto[]> { const data = await this.client['executeContractRead']('/api/FunctionCosts', (c, o) => c.GET('/api/FunctionCosts', o), config); return data.map(costFromWire); }
  async getById(id: number, config?: RequestConfig): Promise<FunctionCostDto> { const data = await this.client['executeContractRead'](`/api/FunctionCosts/${id}`, (c, o) => c.GET('/api/FunctionCosts/{id}', { ...o, params: { path: { id } } }), config); return costFromWire(data); }
  async getByConfiguration(functionConfigurationId: number, config?: RequestConfig): Promise<FunctionCostDto> { const data = await this.client['executeContractRead'](`/api/FunctionCosts/configuration/${functionConfigurationId}`, (c, o) => c.GET('/api/FunctionCosts/configuration/{functionConfigurationId}', { ...o, params: { path: { functionConfigurationId } } }), config); return costFromWire(data); }
  async create(data: CreateFunctionCostDto, config?: RequestConfig): Promise<FunctionCostDto> { validateCreateCost(data); const body = data as unknown as CreateCostWire; const result = await this.client['executeContractOperation']<CostWire, CreateFunctionCostDto>('/api/FunctionCosts', HttpMethod.POST, (c, o) => c.POST('/api/FunctionCosts', { ...o, body }), config, data); return costFromWire(result); }
  async update(id: number, data: UpdateFunctionCostDto, config?: RequestConfig): Promise<FunctionCostDto> { const body = data as unknown as UpdateCostWire; const result = await this.client['executeContractOperation']<CostWire, UpdateFunctionCostDto>(`/api/FunctionCosts/${id}`, HttpMethod.PUT, (c, o) => c.PUT('/api/FunctionCosts/{id}', { ...o, params: { path: { id } }, body }), config, data); return costFromWire(result); }
  async deleteById(id: number, config?: RequestConfig): Promise<void> { return this.client['executeContractOperation'](`/api/FunctionCosts/${id}`, HttpMethod.DELETE, (c, o) => c.DELETE('/api/FunctionCosts/{id}', { ...o, params: { path: { id } } }), config); }
  async clearCache(config?: RequestConfig): Promise<{ message: string }> { return this.client['executeContractOperation']('/api/FunctionCosts/cache/clear', HttpMethod.POST, (c, o) => c.POST('/api/FunctionCosts/cache/clear', o), config); }
}

export class FetchFunctionExecutionsService {
  constructor(private readonly client: FetchBaseApiClient) {}
  async getById(id: string, config?: RequestConfig): Promise<FunctionExecutionDto> { const data = await this.client['executeContractRead'](`/api/FunctionExecutions/${encodeURIComponent(id)}`, (c, o) => c.GET('/api/FunctionExecutions/{id}', { ...o, params: { path: { id } } }), config); return executionFromWire(data); }
  async getByVirtualKey(virtualKeyId: number, config?: RequestConfig): Promise<FunctionExecutionDto[]> { const data = await this.client['executeContractRead'](`/api/FunctionExecutions/virtualkey/${virtualKeyId}`, (c, o) => c.GET('/api/FunctionExecutions/virtualkey/{virtualKeyId}', { ...o, params: { path: { virtualKeyId } } }), config); return data.map(executionFromWire); }
  async getByConfiguration(functionConfigurationId: number, config?: RequestConfig): Promise<FunctionExecutionDto[]> { const data = await this.client['executeContractRead'](`/api/FunctionExecutions/configuration/${functionConfigurationId}`, (c, o) => c.GET('/api/FunctionExecutions/configuration/{functionConfigurationId}', { ...o, params: { path: { functionConfigurationId } } }), config); return data.map(executionFromWire); }
  async getByState(state: ExecutionState, config?: RequestConfig): Promise<FunctionExecutionDto[]> { const value = state; const data = await this.client['executeContractRead'](`/api/FunctionExecutions/state/${encodeURIComponent(value)}`, (c, o) => c.GET('/api/FunctionExecutions/state/{state}', { ...o, params: { path: { state: value } } }), config); return data.map(executionFromWire); }
  async getExpiredLeases(config?: RequestConfig): Promise<FunctionExecutionDto[]> { const data = await this.client['executeContractRead']('/api/FunctionExecutions/expired-leases', (c, o) => c.GET('/api/FunctionExecutions/expired-leases', o), config); return data.map(executionFromWire); }
  async getReadyForRetry(config?: RequestConfig): Promise<FunctionExecutionDto[]> { const data = await this.client['executeContractRead']('/api/FunctionExecutions/ready-for-retry', (c, o) => c.GET('/api/FunctionExecutions/ready-for-retry', o), config); return data.map(executionFromWire); }
  async cleanup(olderThanDays = 30, config?: RequestConfig): Promise<{ deletedCount: number; message: string }> { return this.client['executeContractOperation'](`/api/FunctionExecutions/cleanup?olderThanDays=${olderThanDays}`, HttpMethod.DELETE, (c, o) => c.DELETE('/api/FunctionExecutions/cleanup', { ...o, params: { query: { olderThanDays } } }), config) as Promise<{ deletedCount: number; message: string }>; }
}
