/**
 * Function management types
 * Re-exported from Admin SDK for WebAdmin usage
 */

// Re-export all function types from SDK
export type {
  // Function Configuration
  FunctionConfigurationDto,
  CreateFunctionConfigurationDto,
  UpdateFunctionConfigurationDto,
  FunctionConfigurationListResponse,
  FunctionConfigurationFilters,

  // Function Credentials
  FunctionCredentialDto,
  CreateFunctionCredentialDto,
  UpdateFunctionCredentialDto,
  FunctionCredentialListResponse,
  TestCredentialRequestDto,
  TestCredentialResponseDto,

  // Function Costs
  FunctionCostDto,
  CreateFunctionCostDto,
  UpdateFunctionCostDto,
  FunctionCostListResponse,
  FunctionCostFilters,

  // Function Cost Mappings
  FunctionCostMappingDto,
  CreateFunctionCostMappingDto,
  UpdateFunctionCostMappingDto,

  // Function Executions
  FunctionExecutionDto,
  FunctionExecutionListResponse,
  FunctionExecutionFilters,

  // Pricing Configuration Types
  BasePricingConfig,
  FlatRatePricingConfig,
  PerResultPricingConfig,
  PerTokenPricingConfig,
  TimeBasedPricingConfig,
  TieredPricingConfig,
  ExaHybridPricingConfig,
  FunctionPricingConfig,
} from '@knn_labs/conduit-admin-client';

// Re-export enum values for runtime usage
import {
  FunctionProviderType,
  FunctionPurpose,
  FunctionExecutionMode,
  ExecutionState,
  FunctionPricingModel,
} from '@knn_labs/conduit-admin-client';

export {
  FunctionProviderType,
  FunctionPurpose,
  FunctionExecutionMode,
  ExecutionState,
  FunctionPricingModel,
};

// Re-export function provider registry utilities from SDK
export {
  getAvailableFunctionProviders,
  getFunctionProviderMetadata,
  getFunctionProviderTypeName,
  normalizeFunctionProviderType,
  isValidFunctionProviderType,
  FUNCTION_PROVIDER_REGISTRY,
} from '@knn_labs/conduit-admin-client';
export type { FunctionProviderMetadata } from '@knn_labs/conduit-admin-client';

// Helper functions for enum display
// Note: getProviderTypeName is now provided by SDK (getFunctionProviderTypeName)
// Re-export with original name for backwards compatibility
export { getFunctionProviderTypeName as getProviderTypeName } from '@knn_labs/conduit-admin-client';

export function getPurposeName(purpose: FunctionPurpose): string {
  switch (purpose) {
    case FunctionPurpose.Search:
      return 'Search';
    case FunctionPurpose.Answer:
      return 'Answer';
    case FunctionPurpose.Enrich:
      return 'Enrich';
    case FunctionPurpose.Other:
      return 'Other';
    default:
      return `Unknown (${String(purpose)})`;
  }
}

export function getExecutionModeName(mode: FunctionExecutionMode): string {
  switch (mode) {
    case FunctionExecutionMode.Synchronous:
      return 'Synchronous';
    case FunctionExecutionMode.Asynchronous:
      return 'Asynchronous';
    default:
      return `Unknown (${String(mode)})`;
  }
}

export function getExecutionStateName(state: ExecutionState): string {
  switch (state) {
    case ExecutionState.Pending:
      return 'Pending';
    case ExecutionState.Running:
      return 'Running';
    case ExecutionState.Completed:
      return 'Completed';
    case ExecutionState.Failed:
      return 'Failed';
    case ExecutionState.Cancelled:
      return 'Cancelled';
    default:
      return `Unknown (${String(state)})`;
  }
}

export function getExecutionStateColor(state: ExecutionState): string {
  switch (state) {
    case ExecutionState.Pending:
      return 'text-yellow-600 bg-yellow-50';
    case ExecutionState.Running:
      return 'text-blue-600 bg-blue-50';
    case ExecutionState.Completed:
      return 'text-green-600 bg-green-50';
    case ExecutionState.Failed:
      return 'text-red-600 bg-red-50';
    case ExecutionState.Cancelled:
      return 'text-gray-600 bg-gray-50';
    default:
      return 'text-gray-600 bg-gray-50';
  }
}

export function getPricingModelName(model: FunctionPricingModel): string {
  switch (model) {
    case FunctionPricingModel.FlatRate:
      return 'Flat Rate';
    case FunctionPricingModel.PerResult:
      return 'Per Result';
    case FunctionPricingModel.PerToken:
      return 'Per Token';
    case FunctionPricingModel.TimeBased:
      return 'Time Based';
    case FunctionPricingModel.Tiered:
      return 'Tiered';
    case FunctionPricingModel.Hybrid:
      return 'Hybrid (Exa)';
    default:
      return `Unknown (${String(model)})`;
  }
}

// UI-specific types
export interface FunctionConfigurationFormData {
  configurationName: string;
  providerType: FunctionProviderType;
  purpose: FunctionPurpose;
  description?: string;
  defaultExecutionMode?: FunctionExecutionMode;
  timeoutSeconds?: number;
  isEnabled?: boolean;
  metadata?: string;
}

export interface FunctionCostFormData {
  costName: string;
  providerType: FunctionProviderType;
  purpose?: FunctionPurpose;
  pricingModel: FunctionPricingModel;
  pricingConfiguration: string;
  baseCost?: number;
  isActive?: boolean;
  priority?: number;
  effectiveDate?: string;
  expiryDate?: string;
  description?: string;
}
