import { z } from 'zod';

/**
 * Common schemas used across Admin API
 */

// Virtual Key schemas
export const VirtualKeyDtoSchema = z.object({
  id: z.number(),
  keyName: z.string(),
  apiKey: z.string().optional(),
  keyPrefix: z.string().optional(),
  allowedModels: z.string(),
  maxBudget: z.number(),
  currentSpend: z.number(),
  budgetDuration: z.string(),
  budgetStartDate: z.string(),
  isEnabled: z.boolean(),
  expiresAt: z.string().optional(),
  createdAt: z.string(),
  updatedAt: z.string(),
  metadata: z.string().optional(),
  rateLimitRpm: z.number().optional(),
});

export const CreateVirtualKeyResponseSchema = z.object({
  virtualKey: z.string(),
  keyInfo: VirtualKeyDtoSchema,
});

export const VirtualKeyListResponseSchema = z.object({
  items: z.array(VirtualKeyDtoSchema),
  totalCount: z.number(),
  page: z.number(),
  pageSize: z.number(),
  totalPages: z.number(),
});

// Provider schemas
export const ProviderDtoSchema = z.object({
  id: z.number(),
  name: z.string(),
  displayName: z.string(),
  type: z.string(),
  baseUrl: z.string(),
  apiKey: z.string().optional(),
  isEnabled: z.boolean(),
  priority: z.number(),
  weight: z.number(),
  timeout: z.number(),
  maxRetries: z.number(),
  supportedModels: z.string().optional(),
  customHeaders: z.string().optional(),
  createdAt: z.string(),
  updatedAt: z.string(),
  metadata: z.string().optional(),
});

// Model Mapping schemas
export const ModelProviderMappingDtoSchema = z.object({
  id: z.number(),
  modelIdentifier: z.string(),
  providerId: z.number(),
  providerModelId: z.string(),
  priority: z.number(),
  isEnabled: z.boolean(),
  overrides: z.string().optional(),
  createdAt: z.string(),
  updatedAt: z.string(),
  provider: ProviderDtoSchema.optional(),
});

// Settings schemas
export const GlobalSettingDtoSchema = z.object({
  id: z.number(),
  key: z.string(),
  value: z.string(),
  category: z.string().optional(),
  description: z.string().optional(),
  isSecret: z.boolean(),
  createdAt: z.string(),
  updatedAt: z.string(),
});

// System schemas
export const SystemInfoDtoSchema = z.object({
  version: z.string(),
  environment: z.string(),
  uptime: z.string(),
  serverTime: z.string(),
  features: z.record(z.boolean()),
  database: z.object({
    type: z.string(),
    version: z.string(),
    connectionStatus: z.string(),
  }),
  cache: z.object({
    type: z.string(),
    connectionStatus: z.string(),
  }).optional(),
  messageQueue: z.object({
    type: z.string(),
    connectionStatus: z.string(),
  }).optional(),
});

export const HealthStatusDtoSchema = z.object({
  status: z.enum(['Healthy', 'Unhealthy', 'Degraded']),
  checks: z.record(z.object({
    status: z.enum(['Healthy', 'Unhealthy', 'Degraded']),
    description: z.string().optional(),
    data: z.record(z.any()).optional(),
  })),
  totalDuration: z.string(),
  timestamp: z.string(),
});

// Analytics schemas
export const AnalyticsSummaryDtoSchema = z.object({
  totalRequests: z.number(),
  totalTokens: z.number(),
  totalCost: z.number(),
  uniqueUsers: z.number(),
  averageResponseTime: z.number(),
  errorRate: z.number(),
  topModels: z.array(z.object({
    model: z.string(),
    requestCount: z.number(),
    tokenCount: z.number(),
    totalCost: z.number(),
  })),
  topUsers: z.array(z.object({
    keyName: z.string(),
    requestCount: z.number(),
    tokenCount: z.number(),
    totalCost: z.number(),
  })),
  hourlyMetrics: z.array(z.object({
    hour: z.string(),
    requests: z.number(),
    tokens: z.number(),
    cost: z.number(),
    averageResponseTime: z.number(),
    errorCount: z.number(),
  })),
});

// Model Cost schemas
export const ModelCostDtoSchema = z.object({
  id: z.number(),
  modelIdentifier: z.string(),
  providerId: z.number(),
  inputTokenCost: z.number(),
  outputTokenCost: z.number(),
  baseCost: z.number().optional(),
  perRequestCost: z.number().optional(),
  currency: z.string(),
  effectiveDate: z.string(),
  expirationDate: z.string().optional(),
  notes: z.string().optional(),
  createdAt: z.string(),
  updatedAt: z.string(),
  provider: ProviderDtoSchema.optional(),
});

// Paginated response schema factory
export function createPaginatedResponseSchema<T>(itemSchema: z.ZodSchema<T>) {
  return z.object({
    items: z.array(itemSchema),
    page: z.number(),
    pageSize: z.number(),
    totalCount: z.number(),
    totalPages: z.number(),
    hasNextPage: z.boolean(),
    hasPreviousPage: z.boolean(),
  });
}

// Export common paginated schemas
export const PaginatedVirtualKeyResponseSchema = createPaginatedResponseSchema(VirtualKeyDtoSchema);
export const PaginatedProviderResponseSchema = createPaginatedResponseSchema(ProviderDtoSchema);
export const PaginatedModelMappingResponseSchema = createPaginatedResponseSchema(ModelProviderMappingDtoSchema);
export const PaginatedGlobalSettingResponseSchema = createPaginatedResponseSchema(GlobalSettingDtoSchema);
export const PaginatedModelCostResponseSchema = createPaginatedResponseSchema(ModelCostDtoSchema);

// Base error schema
export const ErrorSchema = z.object({
  error: z.object({
    code: z.string(),
    message: z.string(),
    type: z.string().optional(),
    param: z.string().optional(),
  }),
});

// Success response schema
export const SuccessResponseSchema = z.object({
  success: z.boolean(),
  message: z.string().optional(),
});

/**
 * Validation options for runtime checking
 */
export interface ValidationOptions {
  /** Whether to validate responses (default: true in development, false in production) */
  enabled?: boolean;
  /** Whether to throw on validation errors or just log warnings (default: false) */
  throwOnError?: boolean;
  /** Custom error handler */
  onValidationError?: (error: z.ZodError, response: unknown) => void;
}

/**
 * Default validation options
 */
export const defaultValidationOptions: ValidationOptions = {
  enabled: process.env.NODE_ENV !== 'production',
  throwOnError: false,
  onValidationError: (error, response) => {
    console.warn('API response validation failed:', {
      errors: error.errors,
      response,
    });
  },
};

/**
 * Validates a response against a schema
 */
export function validateResponse<T>(
  schema: z.ZodSchema<T>,
  response: unknown,
  options: ValidationOptions = defaultValidationOptions
): T {
  if (!options.enabled) {
    return response as T;
  }

  try {
    return schema.parse(response);
  } catch (error) {
    if (error instanceof z.ZodError) {
      if (options.onValidationError) {
        options.onValidationError(error, response);
      }
      
      if (options.throwOnError) {
        throw new Error(`API response validation failed: ${error.message}`);
      }
    }
    
    // Return the original response if validation fails and throwOnError is false
    return response as T;
  }
}

// Safe validation helper that returns Result type
export type ValidationResult<T> = 
  | { success: true; data: T }
  | { success: false; error: z.ZodError };

export function safeValidateResponse<T>(
  schema: z.ZodType<T>,
  data: unknown
): ValidationResult<T> {
  const result = schema.safeParse(data);
  if (result.success) {
    return { success: true, data: result.data };
  }
  return { success: false, error: result.error };
}