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

const EXECUTION_STATE_PRESENTATION: Record<
  ExecutionState,
  { label: string; badgeColor: string; className: string }
> = {
  [ExecutionState.Pending]: {
    label: 'Pending',
    badgeColor: 'yellow',
    className: 'text-yellow-600 bg-yellow-50',
  },
  [ExecutionState.Running]: {
    label: 'Running',
    badgeColor: 'blue',
    className: 'text-blue-600 bg-blue-50',
  },
  [ExecutionState.Completed]: {
    label: 'Completed',
    badgeColor: 'green',
    className: 'text-green-600 bg-green-50',
  },
  [ExecutionState.Failed]: {
    label: 'Failed',
    badgeColor: 'red',
    className: 'text-red-600 bg-red-50',
  },
  [ExecutionState.Cancelled]: {
    label: 'Cancelled',
    badgeColor: 'gray',
    className: 'text-gray-600 bg-gray-50',
  },
  [ExecutionState.TimedOut]: {
    label: 'Timed Out',
    badgeColor: 'orange',
    className: 'text-orange-600 bg-orange-50',
  },
};

export function getExecutionStateName(state: ExecutionState): string {
  return EXECUTION_STATE_PRESENTATION[state]?.label ?? `Unknown (${String(state)})`;
}

export function getExecutionStateColor(state: ExecutionState): string {
  return EXECUTION_STATE_PRESENTATION[state]?.className ?? 'text-gray-600 bg-gray-50';
}

export function getExecutionStateBadgeColor(state: string): string {
  const normalized = normalizeExecutionState(state);
  return normalized
    ? EXECUTION_STATE_PRESENTATION[normalized].badgeColor
    : 'gray';
}

function normalizeExecutionState(state: string): ExecutionState | undefined {
  const normalized = state.toLowerCase().replace(/[_\s-]/g, '');
  return Object.values(ExecutionState).find(
    candidate => candidate.toLowerCase() === normalized,
  );
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
