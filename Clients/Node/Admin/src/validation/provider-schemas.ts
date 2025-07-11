import { z } from 'zod';
import { TimestampSchema, IdSchema } from './schemas';

// Provider status enum
export const ProviderStatusSchema = z.enum(['Active', 'Inactive', 'Testing']);

// Provider credential schema
export const ProviderCredentialSchema = IdSchema.extend({
  name: z.string(),
  providerTypeId: z.string(),
  baseUrl: z.string().url().optional(),
  apiKey: z.string(),
  orgId: z.string().optional(),
  deploymentId: z.string().optional(),
  apiVersion: z.string().optional(),
  region: z.string().optional(),
  projectId: z.string().optional(),
  defaultModel: z.string().optional(),
  maxRetries: z.number().int().positive().optional(),
  timeout: z.number().int().positive().optional(),
  rateLimit: z.object({
    requestsPerMinute: z.number().int().positive().optional(),
    tokensPerMinute: z.number().int().positive().optional(),
    concurrentRequests: z.number().int().positive().optional(),
  }).optional(),
  customHeaders: z.record(z.string()).optional(),
  metadata: z.record(z.unknown()).optional(),
  status: ProviderStatusSchema,
  healthCheckInterval: z.number().int().positive().optional(),
  lastHealthCheck: z.string().datetime().optional(),
  isHealthy: z.boolean().optional(),
  capabilities: z.array(z.string()).optional(),
}).merge(TimestampSchema);

export type ProviderCredential = z.infer<typeof ProviderCredentialSchema>;

// Create provider request schema
export const CreateProviderRequestSchema = z.object({
  name: z.string(),
  providerTypeId: z.string(),
  baseUrl: z.string().url().optional(),
  apiKey: z.string(),
  orgId: z.string().optional(),
  deploymentId: z.string().optional(),
  apiVersion: z.string().optional(),
  region: z.string().optional(),
  projectId: z.string().optional(),
  defaultModel: z.string().optional(),
  maxRetries: z.number().int().positive().optional(),
  timeout: z.number().int().positive().optional(),
  rateLimit: z.object({
    requestsPerMinute: z.number().int().positive().optional(),
    tokensPerMinute: z.number().int().positive().optional(),
    concurrentRequests: z.number().int().positive().optional(),
  }).optional(),
  customHeaders: z.record(z.string()).optional(),
  metadata: z.record(z.unknown()).optional(),
});

export type CreateProviderRequest = z.infer<typeof CreateProviderRequestSchema>;

// Update provider request schema
export const UpdateProviderRequestSchema = CreateProviderRequestSchema.partial();
export type UpdateProviderRequest = z.infer<typeof UpdateProviderRequestSchema>;

// Provider filters schema
export const ProviderFiltersSchema = z.object({
  status: ProviderStatusSchema.optional(),
  providerTypeId: z.string().optional(),
  isHealthy: z.boolean().optional(),
  name: z.string().optional(),
}).optional();

export type ProviderFilters = z.infer<typeof ProviderFiltersSchema>;

// Provider list response
export const ProviderListResponseSchema = z.array(ProviderCredentialSchema);
export type ProviderListResponse = z.infer<typeof ProviderListResponseSchema>;

// Provider discovery response
export const DiscoveredModelSchema = z.object({
  id: z.string(),
  name: z.string(),
  contextWindow: z.number().int().positive().optional(),
  maxTokens: z.number().int().positive().optional(),
  supportsFunctions: z.boolean().optional(),
  supportsVision: z.boolean().optional(),
  supportsStreaming: z.boolean().optional(),
  description: z.string().optional(),
});

export const ProviderDiscoveryResponseSchema = z.object({
  provider: z.string(),
  models: z.array(DiscoveredModelSchema),
  timestamp: z.string().datetime(),
});

export type ProviderDiscoveryResponse = z.infer<typeof ProviderDiscoveryResponseSchema>;