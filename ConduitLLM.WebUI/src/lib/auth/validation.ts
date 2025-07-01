import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';

export async function validateMasterKey(masterKey: string): Promise<boolean> {
  try {
    if (!masterKey || masterKey.trim().length === 0) {
      return false;
    }

    // Create a temporary admin client to test the master key
    const adminClient = new ConduitAdminClient({
      adminApiUrl: process.env.NEXT_PUBLIC_CONDUIT_ADMIN_API_URL!,
      masterKey: masterKey,
    });

    // Try to make a simple request to validate the key
    // Using system info as a lightweight validation endpoint
    await adminClient.system.getSystemInfo();
    
    return true;
  } catch (error: any) {
    console.warn('Master key validation failed:', error.message);
    
    // Check if it's an authentication error
    if (error.status === 401 || error.status === 403) {
      return false;
    }
    
    // For other errors (network, etc), we'll assume the key might be valid
    // This prevents network issues from blocking login
    return true;
  }
}

export function sanitizeMasterKey(masterKey: string): string {
  return masterKey.trim();
}

export function validateMasterKeyFormat(masterKey: string): string | null {
  const sanitized = sanitizeMasterKey(masterKey);
  
  if (!sanitized) {
    return 'Master key is required';
  }
  
  if (sanitized.length < 16) {
    return 'Master key must be at least 16 characters for security';
  }
  
  if (sanitized.length > 100) {
    return 'Master key is too long (maximum 100 characters)';
  }
  
  // Check for common patterns that indicate weak keys
  if (/^[a-z]+$/.test(sanitized)) {
    return 'Master key should contain mixed case, numbers, or special characters';
  }
  
  if (/^[0-9]+$/.test(sanitized)) {
    return 'Master key should not be only numbers';
  }
  
  if (/^(.)\1+$/.test(sanitized)) {
    return 'Master key should not contain repeated characters';
  }
  
  // Common weak patterns
  const weakPatterns = [
    'password', 'admin', 'test', 'demo', 'default', 'secret',
    '123456', 'qwerty', 'abc123', 'admin123', 'password123'
  ];
  
  const lowerKey = sanitized.toLowerCase();
  for (const pattern of weakPatterns) {
    if (lowerKey.includes(pattern)) {
      return 'Master key appears to contain common patterns - please use a stronger key';
    }
  }
  
  return null;
}

export function getMasterKeyStrength(masterKey: string): { 
  score: number; 
  label: string; 
  suggestions: string[] 
} {
  const sanitized = sanitizeMasterKey(masterKey);
  let score = 0;
  const suggestions: string[] = [];
  
  if (!sanitized) {
    return { score: 0, label: 'Very Weak', suggestions: ['Enter a master key'] };
  }
  
  // Length scoring
  if (sanitized.length >= 16) score += 25;
  else if (sanitized.length >= 12) score += 15;
  else if (sanitized.length >= 8) score += 10;
  else suggestions.push('Use at least 16 characters');
  
  // Character variety scoring
  if (/[a-z]/.test(sanitized)) score += 15;
  else suggestions.push('Include lowercase letters');
  
  if (/[A-Z]/.test(sanitized)) score += 15;
  else suggestions.push('Include uppercase letters');
  
  if (/[0-9]/.test(sanitized)) score += 15;
  else suggestions.push('Include numbers');
  
  if (/[^a-zA-Z0-9]/.test(sanitized)) score += 20;
  else suggestions.push('Include special characters');
  
  // Complexity bonus
  if (sanitized.length >= 20 && /[a-z]/.test(sanitized) && /[A-Z]/.test(sanitized) && 
      /[0-9]/.test(sanitized) && /[^a-zA-Z0-9]/.test(sanitized)) {
    score += 10;
  }
  
  // Penalties for weak patterns
  if (/(.)\1{2,}/.test(sanitized)) {
    score -= 10;
    suggestions.push('Avoid repeating characters');
  }
  
  if (/123|abc|qwe/i.test(sanitized)) {
    score -= 15;
    suggestions.push('Avoid sequential patterns');
  }
  
  // Determine label
  let label = 'Very Weak';
  if (score >= 85) label = 'Very Strong';
  else if (score >= 70) label = 'Strong';
  else if (score >= 50) label = 'Medium';
  else if (score >= 30) label = 'Weak';
  
  return { score: Math.max(0, Math.min(100, score)), label, suggestions };
}