/**
 * Client-safe validation functions that don't import the SDK
 */

export function validateAdminPasswordFormat(adminPassword: string): string | null {
  if (!adminPassword || typeof adminPassword !== 'string') {
    return 'Admin password is required';
  }
  
  const sanitized = adminPassword.trim();
  
  // Use same validation as backend - minimum 4 characters
  if (sanitized.length < 4) {
    return 'Admin password must be at least 4 characters';
  }
  
  if (sanitized.length > 100) {
    return 'Admin password is too long (maximum 100 characters)';
  }
  
  // Allow any format - the server will validate strength
  // This prevents the form from blocking weak passwords entirely
  return null;
}


export function sanitizeAdminPassword(adminPassword: string): string {
  if (!adminPassword || typeof adminPassword !== 'string') {
    return '';
  }
  
  // Remove whitespace and control characters
  return adminPassword.trim().replace(/[\x00-\x1F\x7F]/g, '');
}

export async function validateAdminPassword(adminPassword: string): Promise<{ isValid: boolean; error?: string }> {
  // On client side, we can only do format validation
  // The actual validation happens through the API
  const formatError = validateAdminPasswordFormat(adminPassword);
  if (formatError) {
    return { isValid: false, error: formatError };
  }
  
  // For client-side, we assume it's valid if format is correct
  // Real validation happens on server via API calls
  return { isValid: true };
}