/**
 * Function management types
 * Re-exported from the local Admin API boundary for WebAdmin usage
 */

// Re-export all function types from the local Admin API boundary
export type {
  // Function Configuration
  FunctionConfigurationDto,
  CreateFunctionConfigurationDto,
  UpdateFunctionConfigurationDto,

  // Function Credentials
  FunctionCredentialDto,
  CreateFunctionCredentialDto,
  UpdateFunctionCredentialDto,
  TestCredentialRequestDto,
  TestCredentialResponseDto,

  // Function Costs
  FunctionCostDto,
  CreateFunctionCostDto,
  UpdateFunctionCostDto,

  // Function Executions
  FunctionExecutionDto,

  // Pricing Configuration Types
  BasePricingConfig,
  FlatRatePricingConfig,
  PerResultPricingConfig,
  PerTokenPricingConfig,
  TimeBasedPricingConfig,
  TieredPricingConfig,
  ExaHybridPricingConfig,
  FunctionPricingConfig,
} from '@/lib/admin-api';

// Re-export enum values for runtime usage
import {
  FunctionProviderType,
  FunctionPurpose,
  FunctionExecutionMode,
  ExecutionState,
  FunctionPricingModel,
} from '@/lib/admin-api';

export {
  FunctionProviderType,
  FunctionPurpose,
  FunctionExecutionMode,
  ExecutionState,
  FunctionPricingModel,
};

// Re-export function provider registry utilities from the local Admin API boundary
export {
  getAvailableFunctionProviders,
  getFunctionProviderMetadata,
  getFunctionProviderTypeName,
  normalizeFunctionProviderType,
  isValidFunctionProviderType,
  FUNCTION_PROVIDER_REGISTRY,
} from '@/lib/admin-api';
export type { FunctionProviderMetadata } from '@/lib/admin-api';

// Helper functions for enum display
// Note: getProviderTypeName is provided as getFunctionProviderTypeName.
// Re-export with original name for backwards compatibility
export { getFunctionProviderTypeName as getProviderTypeName } from '@/lib/admin-api';

export function getPurposeName(purpose: FunctionPurpose): string {
  switch (purpose) {
    case FunctionPurpose.Search:
      return 'Search';
    case FunctionPurpose.Answer:
      return 'Answer';
    case FunctionPurpose.ContentRetrieval:
      return 'Content Retrieval';
    case FunctionPurpose.RAG:
      return 'RAG';
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
    case ExecutionState.TimedOut:
      return 'Timed Out';
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
    case ExecutionState.TimedOut:
      return 'text-orange-600 bg-orange-50';
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
  providerSettings?: string;
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
