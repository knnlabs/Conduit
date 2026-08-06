import { ValidationError } from '@/lib/conduit-common';
import { z } from 'zod';

const nonEmpty = z.string().min(1);
const dateTime = z.string().datetime({ offset: true });

export const adminEphemeralKeySchema = z.object({
  ephemeralMasterKey: nonEmpty,
  expiresAt: dateTime,
  expiresInSeconds: z.number().int().positive(),
});

export const webAdminEphemeralMasterKeySchema = adminEphemeralKeySchema.extend({
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

export const webAdminEphemeralKeySchema = z.object({
  ephemeralKey: nonEmpty,
  expiresAt: dateTime,
  expiresInSeconds: z.number().int().positive(),
  coreApiUrl: z.string().url(),
});

export const virtualKeyIssueSchema = z.object({
  virtualKey: nonEmpty,
  keyInfo: z.object({ id: z.number().int().positive() }).passthrough(),
});

export const virtualKeyValidationSchema = z.object({
  isValid: z.boolean(),
  virtualKeyId: z.number().int().positive().nullish(),
  keyName: z.string().nullish(),
  allowedModels: z.array(z.string()).nullish(),
  errorMessage: z.string().nullish(),
});

export const balanceAdjustmentSchema = z.object({
  id: z.number().int().positive(),
  groupName: nonEmpty,
  balance: z.number().finite(),
}).passthrough();

export const gatewayFunctionExecutionSchema = z.object({
  id: nonEmpty,
  function_id: z.coerce.number().int().positive(),
  status: nonEmpty,
  input: z.record(z.string(), z.unknown()).nullish(),
  output: z.record(z.string(), z.unknown()).nullish(),
  error: z.string().nullish(),
  created_at: dateTime,
  started_at: z.string().nullish(),
  completed_at: z.string().nullish(),
  duration_ms: z.coerce.number().int().nonnegative().nullish(),
  cost: z.object({
    estimated: z.coerce.number().nullish(),
    actual: z.coerce.number().nullish(),
    currency: nonEmpty,
    breakdown: z.record(z.string(), z.unknown()).nullish(),
  }),
}).passthrough().transform(value => ({
  id: value.id,
  functionId: value.function_id,
  status: value.status,
  input: value.input ?? undefined,
  output: value.output ?? undefined,
  error: value.error ?? undefined,
  createdAt: value.created_at,
  startedAt: value.started_at ?? undefined,
  completedAt: value.completed_at ?? undefined,
  durationMs: value.duration_ms ?? undefined,
  cost: {
    estimated: value.cost.estimated ?? undefined,
    actual: value.cost.actual ?? undefined,
    currency: value.cost.currency,
    breakdown: value.cost.breakdown ?? undefined,
  },
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
