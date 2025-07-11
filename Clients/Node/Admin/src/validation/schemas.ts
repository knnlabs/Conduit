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
export const PaginationSchema = z.object({
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
  totalCount: z.number().int().nonnegative(),
  totalPages: z.number().int().nonnegative(),
});

export const TimestampSchema = z.object({
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
});

export const IdSchema = z.object({
  id: z.string().uuid(),
});

// Response wrapper schemas
export const ListResponseSchema = <T extends z.ZodType>(itemSchema: T) =>
  z.object({
    items: z.array(itemSchema),
    pagination: PaginationSchema,
  });

export const SingleResponseSchema = <T extends z.ZodType>(dataSchema: T) =>
  z.object({
    data: dataSchema,
  });

// Success response schema
export const SuccessResponseSchema = z.object({
  success: z.boolean(),
  message: z.string().optional(),
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