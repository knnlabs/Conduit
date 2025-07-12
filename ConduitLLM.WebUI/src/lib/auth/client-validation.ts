/**
 * Client-safe validation functions that don't import the SDK
 */

export function validateMasterKeyFormat(key: string): boolean {
  if (!key || typeof key !== 'string') {
    return false;
  }
  
  // Basic format validation - adjust as needed
  // Minimum 8 characters, contains letters and numbers
  return key.length >= 8 && /[a-zA-Z]/.test(key) && /[0-9]/.test(key);
}


export function sanitizeMasterKey(key: string): string {
  if (!key || typeof key !== 'string') {
    return '';
  }
  
  // Remove whitespace and control characters
  return key.trim().replace(/[\x00-\x1F\x7F]/g, '');
}

export async function validateMasterKey(key: string): Promise<{ isValid: boolean; error?: string }> {
  // On client side, we can only do format validation
  // The actual validation happens through the API
  if (!validateMasterKeyFormat(key)) {
    return { isValid: false, error: 'Invalid key format' };
  }
  
  // For client-side, we assume it's valid if format is correct
  // Real validation happens on server via API calls
  return { isValid: true };
}