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
  ephemeralKey: nonEmpty,
  expiresAt: dateTime,
  expiresInSeconds: z.coerce.number().int().positive(),
});

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
  executionId: nonEmpty,
  functionConfigurationId: z.coerce.number().int().positive(),
  state: nonEmpty,
}).passthrough();

export const gatewayMediaUploadSchema = z.object({
  success: z.boolean(),
  url: nonEmpty,
}).passthrough();

export const gatewayVideoTaskSchema = z.object({
  taskId: nonEmpty.optional(),
  task_id: nonEmpty.optional(),
  TaskId: nonEmpty.optional(),
  status: nonEmpty.optional(),
  Status: nonEmpty.optional(),
}).passthrough().superRefine((value, context) => {
  if (!(value.taskId ?? value.task_id ?? value.TaskId)) {
    context.addIssue({ code: 'custom', message: 'Video task response is missing a task ID' });
  }
  if (!(value.status ?? value.Status)) {
    context.addIssue({ code: 'custom', message: 'Video task response is missing a status' });
  }
});

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
