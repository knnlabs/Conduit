import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
import {
  FunctionConfigurationDto,
  CreateFunctionConfigurationDto,
  UpdateFunctionConfigurationDto,
  FunctionCredentialDto,
  CreateFunctionCredentialDto,
  UpdateFunctionCredentialDto,
  TestCredentialRequestDto,
  TestCredentialResponseDto,
  FunctionCostDto,
  CreateFunctionCostDto,
  UpdateFunctionCostDto,
  FunctionCostMappingDto,
  CreateFunctionCostMappingDto,
  UpdateFunctionCostMappingDto,
  FunctionExecutionDto,
  FunctionConfigurationListResponse,
  FunctionCredentialListResponse,
  FunctionCostListResponse,
  FunctionExecutionListResponse,
  FunctionProviderType,
  FunctionPurpose,
  ExecutionState,
} from '../models/functions';
import { ValidationError } from '../utils/errors';
import { validateRequired, validateStringLength } from '../utils/validation';

// ============================================================================
// Validation Functions
// ============================================================================

function validateCreateConfiguration(data: CreateFunctionConfigurationDto): void {
  validateRequired(data, ['configurationName', 'providerType', 'purpose']);
  validateStringLength(data.configurationName, 1, 255, 'configurationName');

  if (data.timeoutSeconds !== undefined && data.timeoutSeconds < 1) {
    throw new ValidationError('timeoutSeconds must be at least 1');
  }
}

function validateCreateCredential(data: CreateFunctionCredentialDto): void {
  validateRequired(data, ['providerType', 'keyName', 'apiKey']);
  validateStringLength(data.keyName, 1, 255, 'keyName');
  validateStringLength(data.apiKey, 1, 1000, 'apiKey');
}

function validateCreateCost(data: CreateFunctionCostDto): void {
  validateRequired(data, ['costName', 'providerType', 'pricingModel', 'pricingConfiguration']);
  validateStringLength(data.costName, 1, 255, 'costName');

  // Validate pricing configuration is valid JSON
  try {
    JSON.parse(data.pricingConfiguration);
  } catch {
    throw new ValidationError('pricingConfiguration must be valid JSON');
  }
}

// ============================================================================
// Function Configurations Service
// ============================================================================

export class FetchFunctionConfigurationsService {
  constructor(private readonly client: FetchBaseApiClient) {}

  async list(config?: RequestConfig): Promise<FunctionConfigurationDto[]> {
    return this.client['get']<FunctionConfigurationDto[]>(
      ENDPOINTS.FUNCTION_CONFIGURATIONS.BASE,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async getById(id: number, config?: RequestConfig): Promise<FunctionConfigurationDto> {
    return this.client['get']<FunctionConfigurationDto>(
      ENDPOINTS.FUNCTION_CONFIGURATIONS.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async getByProvider(providerType: FunctionProviderType | string, config?: RequestConfig): Promise<FunctionConfigurationDto[]> {
    const provider = typeof providerType === 'number' ? FunctionProviderType[providerType] : providerType;
    return this.client['get']<FunctionConfigurationDto[]>(
      ENDPOINTS.FUNCTION_CONFIGURATIONS.BY_PROVIDER(provider),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async getByPurpose(purpose: FunctionPurpose | string, config?: RequestConfig): Promise<FunctionConfigurationDto[]> {
    const purposeStr = typeof purpose === 'number' ? FunctionPurpose[purpose] : purpose;
    return this.client['get']<FunctionConfigurationDto[]>(
      ENDPOINTS.FUNCTION_CONFIGURATIONS.BY_PURPOSE(purposeStr),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async create(data: CreateFunctionConfigurationDto, config?: RequestConfig): Promise<FunctionConfigurationDto> {
    validateCreateConfiguration(data);
    return this.client['post']<FunctionConfigurationDto, CreateFunctionConfigurationDto>(
      ENDPOINTS.FUNCTION_CONFIGURATIONS.BASE,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async update(id: number, data: UpdateFunctionConfigurationDto, config?: RequestConfig): Promise<FunctionConfigurationDto> {
    return this.client['put']<FunctionConfigurationDto, UpdateFunctionConfigurationDto>(
      ENDPOINTS.FUNCTION_CONFIGURATIONS.BY_ID(id),
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async deleteById(id: number, config?: RequestConfig): Promise<void> {
    return this.client['delete']<void>(
      ENDPOINTS.FUNCTION_CONFIGURATIONS.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }
}

// ============================================================================
// Function Credentials Service
// ============================================================================

export class FetchFunctionCredentialsService {
  constructor(private readonly client: FetchBaseApiClient) {}

  async list(config?: RequestConfig): Promise<FunctionCredentialDto[]> {
    return this.client['get']<FunctionCredentialDto[]>(
      ENDPOINTS.FUNCTION_CREDENTIALS.BASE,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async getById(id: number, config?: RequestConfig): Promise<FunctionCredentialDto> {
    return this.client['get']<FunctionCredentialDto>(
      ENDPOINTS.FUNCTION_CREDENTIALS.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async getByConfiguration(configId: number, config?: RequestConfig): Promise<FunctionCredentialDto[]> {
    return this.client['get']<FunctionCredentialDto[]>(
      ENDPOINTS.FUNCTION_CREDENTIALS.BY_CONFIGURATION(configId),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async create(data: CreateFunctionCredentialDto, config?: RequestConfig): Promise<FunctionCredentialDto> {
    validateCreateCredential(data);
    return this.client['post']<FunctionCredentialDto, CreateFunctionCredentialDto>(
      ENDPOINTS.FUNCTION_CREDENTIALS.BASE,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async update(id: number, data: UpdateFunctionCredentialDto, config?: RequestConfig): Promise<FunctionCredentialDto> {
    return this.client['put']<FunctionCredentialDto, UpdateFunctionCredentialDto>(
      ENDPOINTS.FUNCTION_CREDENTIALS.BY_ID(id),
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async deleteById(id: number, config?: RequestConfig): Promise<void> {
    return this.client['delete']<void>(
      ENDPOINTS.FUNCTION_CREDENTIALS.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async testCredential(data: TestCredentialRequestDto, config?: RequestConfig): Promise<TestCredentialResponseDto> {
    return this.client['post']<TestCredentialResponseDto, TestCredentialRequestDto>(
      ENDPOINTS.FUNCTION_CREDENTIALS.TEST,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }
}

// ============================================================================
// Function Costs Service
// ============================================================================

export class FetchFunctionCostsService {
  constructor(private readonly client: FetchBaseApiClient) {}

  async list(config?: RequestConfig): Promise<FunctionCostDto[]> {
    return this.client['get']<FunctionCostDto[]>(
      ENDPOINTS.FUNCTION_COSTS.BASE,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async getById(id: number, config?: RequestConfig): Promise<FunctionCostDto> {
    return this.client['get']<FunctionCostDto>(
      ENDPOINTS.FUNCTION_COSTS.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async getByConfiguration(configId: number, config?: RequestConfig): Promise<FunctionCostDto> {
    return this.client['get']<FunctionCostDto>(
      ENDPOINTS.FUNCTION_COSTS.BY_CONFIGURATION(configId),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async create(data: CreateFunctionCostDto, config?: RequestConfig): Promise<FunctionCostDto> {
    validateCreateCost(data);
    return this.client['post']<FunctionCostDto, CreateFunctionCostDto>(
      ENDPOINTS.FUNCTION_COSTS.BASE,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async update(id: number, data: UpdateFunctionCostDto, config?: RequestConfig): Promise<FunctionCostDto> {
    return this.client['put']<FunctionCostDto, UpdateFunctionCostDto>(
      ENDPOINTS.FUNCTION_COSTS.BY_ID(id),
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async deleteById(id: number, config?: RequestConfig): Promise<void> {
    return this.client['delete']<void>(
      ENDPOINTS.FUNCTION_COSTS.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async clearCache(config?: RequestConfig): Promise<{ message: string }> {
    return this.client['post']<{ message: string }, undefined>(
      ENDPOINTS.FUNCTION_COSTS.CLEAR_CACHE,
      undefined,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }
}

// ============================================================================
// Function Executions Service
// ============================================================================

export class FetchFunctionExecutionsService {
  constructor(private readonly client: FetchBaseApiClient) {}

  async getById(id: string, config?: RequestConfig): Promise<FunctionExecutionDto> {
    return this.client['get']<FunctionExecutionDto>(
      ENDPOINTS.FUNCTION_EXECUTIONS.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async getByVirtualKey(
    virtualKeyId: number,
    config?: RequestConfig
  ): Promise<FunctionExecutionDto[]> {
    return this.client['get']<FunctionExecutionDto[]>(
      ENDPOINTS.FUNCTION_EXECUTIONS.BY_VIRTUAL_KEY(virtualKeyId),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async getByConfiguration(
    configId: number,
    config?: RequestConfig
  ): Promise<FunctionExecutionDto[]> {
    return this.client['get']<FunctionExecutionDto[]>(
      ENDPOINTS.FUNCTION_EXECUTIONS.BY_CONFIGURATION(configId),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async getByState(
    state: ExecutionState | string,
    config?: RequestConfig
  ): Promise<FunctionExecutionDto[]> {
    const stateStr = typeof state === 'number' ? ExecutionState[state] : state;

    return this.client['get']<FunctionExecutionDto[]>(
      ENDPOINTS.FUNCTION_EXECUTIONS.BY_STATE(stateStr),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async getExpiredLeases(config?: RequestConfig): Promise<FunctionExecutionDto[]> {
    return this.client['get']<FunctionExecutionDto[]>(
      ENDPOINTS.FUNCTION_EXECUTIONS.EXPIRED_LEASES,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async getReadyForRetry(config?: RequestConfig): Promise<FunctionExecutionDto[]> {
    return this.client['get']<FunctionExecutionDto[]>(
      ENDPOINTS.FUNCTION_EXECUTIONS.READY_FOR_RETRY,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async cleanup(olderThanDays: number = 30, config?: RequestConfig): Promise<{ deletedCount: number; message: string }> {
    const queryParams = new URLSearchParams({ olderThanDays: String(olderThanDays) });
    return this.client['delete']<{ deletedCount: number; message: string }>(
      `${ENDPOINTS.FUNCTION_EXECUTIONS.CLEANUP}?${queryParams}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }
}
