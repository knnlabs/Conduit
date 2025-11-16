import { validators } from '@/lib/utils/form-validators';

// Validation helpers for common form field patterns
export const formValidation = {
  // Standard name validation
  name: (minLength = 3, maxLength = 100) => (value: string) => {
    const requiredError = validators.required('Name')(value);
    if (requiredError) return requiredError;
    const minLengthError = validators.minLength('Name', minLength)(value);
    if (minLengthError) return minLengthError;
    const maxLengthError = validators.maxLength('Name', maxLength)(value);
    if (maxLengthError) return maxLengthError;
    return null;
  },

  // API key validation
  apiKey: validators.required('API Key'),

  // Budget validation
  budget: validators.positiveNumber('Budget'),

  // Priority validation
  priority: (min = 0, max = 1000) => (value: number | undefined) => {
    if (value === undefined) return null;
    if (value < min) return `Priority must be at least ${min}`;
    if (value > max) return `Priority must be no more than ${max}`;
    return null;
  },

  // Rate limit validation
  rateLimit: (value: number | undefined) => {
    if (value === undefined) return null;
    if (value < 0) return 'Rate limit must be non-negative';
    if (!Number.isInteger(value)) return 'Rate limit must be a whole number';
    return null;
  },

  // URL validation
  url: validators.url('URL'),

  // Array minimum length validation
  capabilities: (minLength = 1) => validators.arrayMinLength('capability', minLength),
};