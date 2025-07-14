/**
 * Client-safe validation functions that don't import the SDK
 */

export function validateMasterKeyFormat(key: string): string | null {
  if (!key || typeof key !== 'string') {
    return 'Master key is required';
  }
  
  const sanitized = key.trim();
  
  // Use same validation as backend - minimum 4 characters
  if (sanitized.length < 4) {
    return 'Master key must be at least 4 characters';
  }
  
  if (sanitized.length > 100) {
    return 'Master key is too long (maximum 100 characters)';
  }
  
  // Allow any format - the server will validate strength
  // This prevents the form from blocking weak passwords entirely
  return null;
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
  const formatError = validateMasterKeyFormat(key);
  if (formatError) {
    return { isValid: false, error: formatError };
  }
  
  // For client-side, we assume it's valid if format is correct
  // Real validation happens on server via API calls
  return { isValid: true };
}