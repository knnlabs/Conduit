/**
 * Reusable form validation functions
 */

export const validators = {
  required: (fieldName: string) => (value: string | undefined) =>
    !value?.trim() ? `${fieldName} is required` : null,

  minLength: (fieldName: string, min: number) => (value: string | undefined) =>
    (value?.length || 0) < min ? `${fieldName} must be at least ${min} characters` : null,

  maxLength: (fieldName: string, max: number) => (value: string | undefined) =>
    (value?.length || 0) > max ? `${fieldName} must be no more than ${max} characters` : null,

  positiveNumber: (fieldName: string) => (value: number | undefined) =>
    (value !== undefined && value < 0) ? `${fieldName} must be positive` : null,

  url: (value: string | undefined) => {
    if (!value?.trim()) return null;
    try {
      new URL(value);
      return null;
    } catch {
      return 'Must be a valid URL';
    }
  },

  email: (value: string | undefined) => {
    if (!value?.trim()) return null;
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return emailRegex.test(value) ? null : 'Must be a valid email address';
  },

  minValue: (fieldName: string, min: number) => (value: number | undefined) =>
    (value !== undefined && value < min) ? `${fieldName} must be at least ${min}` : null,

  ipAddresses: (value: string[] | undefined) => {
    if (!value || value.length === 0) return null;
    
    const ipRegex = /^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$/;
    const cidrRegex = /^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\/(?:3[0-2]|[12]?[0-9])$/;
    
    for (const ip of value) {
      if (!ipRegex.test(ip) && !cidrRegex.test(ip)) {
        return `Invalid IP address or CIDR: ${ip}`;
      }
    }
    return null;
  },

  arrayMinLength: (fieldName: string, min: number) => (value: unknown[] | undefined) =>
    (!value || value.length < min) ? `At least ${min} ${fieldName} must be selected` : null,
};

// Common validation combinations
export const commonValidations = {
  name: {
    validate: validators.required('Name'),
  },
  
  nameWithLength: (min = 3, max = 100) => ({
    validate: {
      required: validators.required('Name'),
      minLength: validators.minLength('Name', min),
      maxLength: validators.maxLength('Name', max),
    },
  }),
  
  description: {
    validate: validators.maxLength('Description', 500),
  },
  
  apiKey: {
    validate: validators.required('API Key'),
  },
  
  budget: {
    validate: validators.positiveNumber('Budget'),
  },

  rateLimit: {
    validate: validators.minValue('Rate limit', 1),
  },

  virtualKeyName: {
    validate: {
      required: validators.required('Key name'),
      minLength: validators.minLength('Key name', 3),
      maxLength: validators.maxLength('Key name', 100),
    },
  },

  allowedModels: {
    validate: validators.arrayMinLength('model', 1),
  },

  allowedEndpoints: {
    validate: validators.arrayMinLength('endpoint', 1),
  },

  ipAddresses: {
    validate: validators.ipAddresses,
  },
};