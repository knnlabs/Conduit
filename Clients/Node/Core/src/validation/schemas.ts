import { z } from 'zod';

// Base error schema
export const ErrorSchema = z.object({
  error: z.object({
    code: z.string(),
    message: z.string(),
    type: z.string().optional(),
    param: z.string().optional(),
  }),
});

// Common schemas
export const TimestampSchema = z.object({
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime().optional(),
});

export const IdSchema = z.object({
  id: z.string(),
});

// Validation helper function
export function validateResponse<T>(schema: z.ZodType<T>, data: unknown): T {
  try {
    return schema.parse(data);
  } catch (error) {
    if (error instanceof z.ZodError) {
      throw new Error(`Response validation failed: ${error.message}`);
    }
    throw error;
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