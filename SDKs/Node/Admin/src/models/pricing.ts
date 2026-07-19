/**
 * Pricing rules engine types for flexible JSON-based pricing configuration.
 */

/** Supported pricing calculation types */
export type PricingType = 'per_unit' | 'per_second' | 'per_step';

/** A single pricing rule with conditions and rate */
export interface PricingRule {
  /** Key-value pairs that must all match (AND logic) */
  conditions: Record<string, string | number | boolean>;
  /** The rate to apply when conditions match */
  rate: number;
  /** Higher priority rules are evaluated first (default: 0) */
  priority?: number;
  /** Human-readable description of this rule */
  description?: string;
}

/** Validation constraints for pricing parameters */
export interface PricingConstraints {
  minDuration?: number;
  maxDuration?: number;
  minSteps?: number;
  maxSteps?: number;
  allowedResolutions?: string[];
}

/** Root configuration for the pricing rules engine */
export interface PricingRulesConfig {
  /** Schema version for future compatibility */
  version?: string;
  /** How the rate is applied: per_unit, per_second, per_step */
  pricingType: PricingType;
  /** Field name from Usage that provides the quantity */
  unitField?: string;
  /** Fallback rate if no rules match */
  defaultRate: number;
  /** Array of pricing rules evaluated in priority order */
  rules: PricingRule[];
  /** Optional validation constraints */
  constraints?: PricingConstraints;
}

/** Validation error for pricing configuration */
export interface PricingValidationError {
  /** Field name that has the error */
  field: string;
  /** Error message */
  message: string;
  /** Rule index if error is in a specific rule */
  ruleIndex?: number;
}

/** Result of validating a pricing configuration */
export interface PricingValidationResult {
  /** Whether the configuration is valid */
  isValid: boolean;
  /** List of validation errors */
  errors: PricingValidationError[];
  /** List of warnings (non-blocking issues) */
  warnings: string[];
  /** Parsed configuration if valid */
  parsedConfig?: PricingRulesConfig;
}

/**
 * Request for simulating pricing calculation.
 *
 * Reconciled to wire shape — issue #1038.
 */
export interface PricingSimulationRequest {
  /** The pricing configuration JSON */
  pricingConfiguration?: string;
  /** Parameters for the simulation */
  parameters?: Record<string, string | number | boolean> | null;
  /** Video duration in seconds (for per_second pricing) */
  videoDurationSeconds?: number | null;
  /** Video resolution (e.g., "1080p") */
  videoResolution?: string | null;
  /** Image count (for per_unit pricing) */
  imageCount?: number | null;
  /** Image resolution (e.g., "1024x1024") */
  imageResolution?: string | null;
  /** Image quality (e.g., "hd", "standard") */
  imageQuality?: string | null;
}

/** Result of pricing simulation */
export interface PricingSimulationResult {
  /** The rule that matched, if any */
  matchedRule?: PricingRule;
  /** Whether default rate was used */
  usedDefaultRate: boolean;
  /** The rate that was applied */
  rate: number;
  /** The quantity used in calculation */
  quantity: number;
  /** Final calculated cost */
  cost: number;
}

/** Pricing audit event for tracking rule evaluations */
export interface PricingAuditEvent {
  id: number;
  timestamp: string;
  virtualKeyId: number;
  modelId: string;
  modelCostId: number;
  pricingType: string;
  inputParameters: string;
  matchedRule?: string;
  usedDefaultRate: boolean;
  appliedRate: number;
  quantity: number;
  calculatedCost: number;
  requestId?: string;
}

/** Parameters for querying pricing audit events */
export interface PricingAuditQueryParams {
  virtualKeyId?: number;
  modelId?: string;
  modelCostId?: number;
  startDate?: string;
  endDate?: string;
  page?: number;
  pageSize?: number;
}

/** Summary of a single pricing rule's match activity. Matches the wire `RuleMatchSummary`. */
export interface RuleMatchSummary {
  ruleDescription?: string;
  matchCount: number;
  totalRevenue: number;
}

/** Summary statistics for pricing audit. Matches the wire `PricingAuditSummary`. See issue #1038. */
export interface PricingAuditSummary {
  totalEvaluations: number;
  defaultRateUsed: number;
  rulesMatched: number;
  totalRevenue: number;
  averageRate: number;
  pricingTypeBreakdown: Record<string, number>;
  modelBreakdown: Record<string, number>;
  topMatchedRules: RuleMatchSummary[];
}

/** Pricing rules template for different use cases */
export interface PricingTemplate {
  name: string;
  description: string;
  pricingType: PricingType;
  template: PricingRulesConfig;
}

/** Default unit fields based on pricing type */
export const DEFAULT_UNIT_FIELDS: Record<PricingType, string> = {
  'per_unit': 'ImageCount',
  'per_second': 'VideoDurationSeconds',
  'per_step': 'InferenceSteps',
};

/** Human-readable labels for pricing types */
export const PRICING_TYPE_LABELS: Record<PricingType, string> = {
  'per_unit': 'Per Unit (images, videos)',
  'per_second': 'Per Second (video duration)',
  'per_step': 'Per Step (inference steps)',
};

/** Available unit fields for selection */
export const AVAILABLE_UNIT_FIELDS = [
  { value: 'ImageCount', label: 'Image Count' },
  { value: 'VideoCount', label: 'Video Count' },
  { value: 'VideoDurationSeconds', label: 'Video Duration (seconds)' },
  { value: 'InferenceSteps', label: 'Inference Steps' },
];

/**
 * Creates a new empty pricing rules configuration
 */
export function createEmptyPricingConfig(): PricingRulesConfig {
  return {
    version: '1.0',
    pricingType: 'per_unit',
    unitField: 'ImageCount',
    defaultRate: 0,
    rules: [],
  };
}

/**
 * Creates a new empty pricing rule
 */
export function createEmptyRule(): PricingRule {
  return {
    conditions: {},
    rate: 0,
    priority: 0,
    description: '',
  };
}
