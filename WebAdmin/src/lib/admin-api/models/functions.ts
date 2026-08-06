/** Function provider type (e.g., Exa, Tavily) */
export const FunctionProviderType = {
  Exa: 'exa',
  Perplexity: 'perplexity',
  CustomRAG: 'customRAG',
  Tavily: 'tavily',
  Mcp: 'mcp',
  Custom: 'custom',
} as const;
export type FunctionProviderType = (typeof FunctionProviderType)[keyof typeof FunctionProviderType];

/** Function purpose category */
export const FunctionPurpose = {
  Search: 'search',
  Answer: 'answer',
  ContentRetrieval: 'contentRetrieval',
  RAG: 'rag',
} as const;
export type FunctionPurpose = (typeof FunctionPurpose)[keyof typeof FunctionPurpose];

/** Function execution mode */
export const FunctionExecutionMode = {
  Synchronous: 'synchronous',
  Asynchronous: 'asynchronous',
} as const;
export type FunctionExecutionMode = (typeof FunctionExecutionMode)[keyof typeof FunctionExecutionMode];

/** Function execution state */
export const ExecutionState = {
  Pending: 'pending',
  Running: 'running',
  Completed: 'completed',
  Failed: 'failed',
  Cancelled: 'cancelled',
  TimedOut: 'timedOut',
} as const;
export type ExecutionState = (typeof ExecutionState)[keyof typeof ExecutionState];

/** Function cost pricing model */
export const FunctionPricingModel = {
  FlatRate: 'flatRate',
  PerResult: 'perResult',
  PerToken: 'perToken',
  TimeBased: 'timeBased',
  Tiered: 'tiered',
  Hybrid: 'hybrid',
} as const;
export type FunctionPricingModel = (typeof FunctionPricingModel)[keyof typeof FunctionPricingModel];

// ============================================================================
// Function Configuration
// ============================================================================

export interface FunctionConfigurationDto {
  id: number;
  configurationName: string;
  providerType: FunctionProviderType;
  purpose: FunctionPurpose;
  description?: string;
  defaultExecutionMode: FunctionExecutionMode;
  timeoutSeconds?: number | null;
  cacheTtlMinutes?: number | null;
  maxRetries?: number | null;
  baseUrl?: string | null;
  isEnabled: boolean;
  providerSettings?: string | null; // JSON
  parameterSchema?: string | null; // JSON Schema
  createdAt: string;
  updatedAt: string;
}

export interface CreateFunctionConfigurationDto {
  configurationName: string;
  providerType: FunctionProviderType;
  purpose: FunctionPurpose;
  description?: string;
  defaultExecutionMode?: FunctionExecutionMode; // Default: Synchronous
  timeoutSeconds?: number; // Default: 30
  isEnabled?: boolean; // Default: true
  baseUrl?: string; // Server URL — required for MCP (the remote MCP server endpoint)
  providerSettings?: string; // JSON
  parameterSchema?: string; // JSON Schema
}

export interface UpdateFunctionConfigurationDto {
  configurationName?: string;
  purpose?: FunctionPurpose;
  description?: string;
  defaultExecutionMode?: FunctionExecutionMode;
  timeoutSeconds?: number;
  isEnabled?: boolean;
  baseUrl?: string; // Server URL — required for MCP
  providerSettings?: string; // JSON
  parameterSchema?: string; // JSON Schema
}

// ============================================================================
// Function Credential
// ============================================================================

export interface FunctionCredentialDto {
  id: number;
  providerType: FunctionProviderType;
  functionConfigurationId?: number | null;
  keyName?: string | null;
  apiKey?: string | null;
  baseUrl?: string | null;
  organization?: string | null;
  functionAccountGroup: number;
  isPrimary: boolean;
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateFunctionCredentialDto {
  providerType: FunctionProviderType;
  keyName: string;
  apiKey: string;
  baseUrl?: string; // Optional per-credential base URL override
  functionConfigurationId?: number; // Set to scope this credential to a single config (MCP tokens)
  functionAccountGroup?: number; // Default: 0
  isPrimary?: boolean; // Default: false
  isEnabled?: boolean; // Default: true
}

export interface UpdateFunctionCredentialDto {
  id: number;
  keyName?: string;
  apiKey?: string;
  baseUrl?: string | null;
  organization?: string | null;
  functionAccountGroup?: number;
  isPrimary?: boolean;
  isEnabled?: boolean;
}

export interface TestCredentialRequestDto {
  credentialId: number;
  apiKeyOverride?: string;
}

export interface TestCredentialResponseDto {
  success: boolean;
  message?: string;
  details?: Record<string, unknown>;
  durationMs: number;
}

// ============================================================================
// Function Cost
// ============================================================================

export interface FunctionCostDto {
  id: number;
  costName: string;
  providerType: FunctionProviderType;
  purpose?: FunctionPurpose;
  pricingModel: FunctionPricingModel;
  pricingConfiguration: string; // JSON, normalized from nullable wire value
  baseCost?: number;
  isActive: boolean;
  priority: number;
  effectiveDate: string;
  expiryDate?: string;
  description?: string;
  createdAt: string;
  updatedAt: string;
  // Related data
  associatedConfigurations?: string[]; // Configuration names using this cost
}

export interface CreateFunctionCostDto {
  costName: string;
  providerType: FunctionProviderType;
  purpose?: FunctionPurpose;
  pricingModel: FunctionPricingModel;
  pricingConfiguration: string; // JSON
  baseCost?: number;
  isActive?: boolean; // Default: true
  priority?: number; // Default: 0
  effectiveDate?: string; // Default: now
  expiryDate?: string;
  description?: string;
}

export interface UpdateFunctionCostDto {
  id: number;
  costName?: string;
  purpose?: FunctionPurpose;
  pricingModel?: FunctionPricingModel;
  pricingConfiguration?: string; // JSON
  baseCost?: number;
  isActive?: boolean;
  priority?: number;
  effectiveDate?: string;
  expiryDate?: string;
  description?: string;
}

// ============================================================================
// Function Execution
// ============================================================================

export interface FunctionExecutionDto {
  id: string; // Guid
  functionId: number;
  status: ExecutionState;
  input?: Record<string, unknown>;
  output?: Record<string, unknown>;
  error?: string;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
  durationMs?: number;
  cost: {
    estimated?: number;
    actual?: number;
    currency: string;
    breakdown?: Record<string, unknown>;
  };
  admin: {
    virtualKeyId: number;
    executionMode: FunctionExecutionMode;
    retryCount: number;
    nextRetryAt?: string | null;
    leasedBy?: string | null;
    leaseExpiresAt?: string | null;
    version: number;
    webhookUrl?: string | null;
    webhookDelivered: boolean;
    progressPercentage?: number | null;
    statusMessage?: string | null;
  };
}

// ============================================================================
// Pricing Configuration Types
// ============================================================================

/** Base pricing configuration interface */
export interface BasePricingConfig {
  pricingModel: FunctionPricingModel;
}

/** Flat rate pricing - single fixed cost per execution */
export interface FlatRatePricingConfig extends BasePricingConfig {
  pricingModel: typeof FunctionPricingModel.FlatRate;
  costPerExecution: number;
}

/** Per-result pricing - cost scales with number of results */
export interface PerResultPricingConfig extends BasePricingConfig {
  pricingModel: typeof FunctionPricingModel.PerResult;
  costPerResult: number;
  minimumCost?: number;
}

/** Per-token pricing - similar to LLM pricing */
export interface PerTokenPricingConfig extends BasePricingConfig {
  pricingModel: typeof FunctionPricingModel.PerToken;
  costPerMillionTokens: number;
  minimumCost?: number;
}

/** Time-based pricing - cost by execution duration */
export interface TimeBasedPricingConfig extends BasePricingConfig {
  pricingModel: typeof FunctionPricingModel.TimeBased;
  costPerSecond: number;
  minimumCost?: number;
  roundUpToNearestSecond?: boolean;
}

/** Tiered pricing - different rates for different result count ranges */
export interface TieredPricingConfig extends BasePricingConfig {
  pricingModel: typeof FunctionPricingModel.Tiered;
  tiers: Array<{
    minResults: number;
    maxResults?: number;
    costPerResult: number;
  }>;
  baseCost?: number;
}

/** Hybrid pricing - combines multiple cost factors (Exa-specific) */
export interface ExaHybridPricingConfig extends BasePricingConfig {
  pricingModel: typeof FunctionPricingModel.Hybrid;
  // Search costs
  neuralSearchCosts: {
    tier1: { minResults: number; maxResults: number; costPerResult: number }; // 1-25
    tier2: { minResults: number; maxResults?: number; costPerResult: number }; // 26-100
  };
  keywordSearchCosts: {
    tier1: { minResults: number; maxResults: number; costPerResult: number };
    tier2: { minResults: number; maxResults?: number; costPerResult: number };
  };
  // Content extraction costs
  contentExtractionCosts: {
    text: number; // Per page
    highlights: number; // Per page
    summary: number; // Per page
  };
}

/** Tavily search pricing - credit-based model (Tavily-specific) */
export interface TavilySearchPricingConfig extends BasePricingConfig {
  pricingModel: typeof FunctionPricingModel.Hybrid;
  // Credit costs
  costPerCredit: number; // Default: 0.008 USD
  basicSearchCredits: number; // Default: 1
  advancedSearchCredits: number; // Default: 2
  autoParametersCredits?: number; // Default: 2
  // Optional separate charges (future-proofing)
  chargeForAnswerGeneration?: boolean; // Default: false
  answerGenerationCost?: number; // Default: 0
  chargeForImageResults?: boolean; // Default: false
  costPerImage?: number; // Default: 0
}

/** Perplexity pricing - base request plus separate input/output token rates */
export interface PerplexityHybridPricingConfig extends BasePricingConfig {
  pricingModel: typeof FunctionPricingModel.Hybrid;
  baseRequestCost: number;
  inputTokenCostPerMillion: number;
  outputTokenCostPerMillion: number;
}

/** Union type for all pricing configurations */
export type FunctionPricingConfig =
  | FlatRatePricingConfig
  | PerResultPricingConfig
  | PerTokenPricingConfig
  | TimeBasedPricingConfig
  | TieredPricingConfig
  | ExaHybridPricingConfig
  | TavilySearchPricingConfig
  | PerplexityHybridPricingConfig;
