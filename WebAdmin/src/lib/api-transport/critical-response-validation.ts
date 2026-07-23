import { ValidationError } from '@/lib/conduit-common';
import { z } from 'zod';

const nonEmpty = z.string().min(1);
const dateTime = z.string().datetime({ offset: true });

export const adminEphemeralKeySchema = z.object({
  ephemeralMasterKey: nonEmpty,
  expiresAt: dateTime,
  expiresInSeconds: z.number().int().positive(),
});

export const webAdminEphemeralKeySchema = adminEphemeralKeySchema.extend({
  adminApiUrl: z.string().url(),
});

export const gatewayEphemeralKeySchema = z.object({
  ephemeral_key: nonEmpty,
  expires_at: dateTime,
  expires_in_seconds: z.coerce.number().int().positive(),
}).transform(value => ({
  ephemeralKey: value.ephemeral_key,
  expiresAt: value.expires_at,
  expiresInSeconds: value.expires_in_seconds,
}));

export const virtualKeyIssueSchema = z.object({
  virtualKey: nonEmpty,
  keyInfo: z.object({ id: z.number().int().positive() }).passthrough(),
});

export const virtualKeyValidationSchema = z.object({
  isValid: z.boolean(),
  virtualKeyId: z.number().int().positive().nullish(),
  keyName: z.string().nullish(),
  allowedModels: z.string().nullish(),
  errorMessage: z.string().nullish(),
});

export const balanceAdjustmentSchema = z.object({
  id: z.number().int().positive(),
  groupName: nonEmpty,
  balance: z.number().finite(),
}).passthrough();

export const gatewayFunctionExecutionSchema = z.object({
  execution_id: nonEmpty,
  function_configuration_id: z.coerce.number().int().positive(),
  state: nonEmpty,
  result: z.record(z.string(), z.unknown()).nullish(),
  error_message: z.string().nullish(),
  estimated_cost: z.coerce.number().nullish(),
  actual_cost: z.coerce.number().nullish(),
  started_at: z.string().nullish(),
  completed_at: z.string().nullish(),
  duration: z.coerce.number().nullish(),
}).passthrough().transform(value => ({
  executionId: value.execution_id,
  functionConfigurationId: value.function_configuration_id,
  state: value.state,
  result: value.result ?? undefined,
  errorMessage: value.error_message ?? undefined,
  estimatedCost: value.estimated_cost ?? undefined,
  actualCost: value.actual_cost ?? undefined,
  startedAt: value.started_at ?? undefined,
  completedAt: value.completed_at ?? undefined,
  duration: value.duration ?? undefined,
}));

export const gatewayMediaUploadSchema = z.object({
  success: z.boolean(),
  url: nonEmpty,
}).passthrough();

export const gatewayVideoTaskSchema = z.object({
  task_id: nonEmpty,
  status: nonEmpty,
}).passthrough();

export function parseCriticalResponse<T>(
  schema: z.ZodType<T>,
  value: unknown,
  operation: string,
): T {
  const result = schema.safeParse(value);
  if (result.success) return result.data;
  throw new ValidationError(`Invalid response from ${operation}`, {
    operation,
    details: result.error.issues,
  });
}
