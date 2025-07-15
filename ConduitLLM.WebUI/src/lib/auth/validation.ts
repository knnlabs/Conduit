import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';
import { SDK_CONFIG } from '../server/sdk-config';

export interface ValidateMasterKeyResult {
  isValid: boolean;
  virtualKey?: string;
}

export async function validateAdminPassword(adminPassword: string): Promise<ValidateMasterKeyResult> {
  try {
    if (!adminPassword || adminPassword.trim().length === 0) {
      return { isValid: false };
    }

    // If running on the client side, validate against the API endpoint
    if (typeof window !== 'undefined') {
      // Call the Next.js API route which uses the SDK
      const response = await fetch('/api/auth/validate', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ 
          adminPassword: adminPassword.trim()
        }),
        credentials: 'include',
      });
      
      if (response.ok) {
        const data = await response.json();
        return { 
          isValid: true, 
          virtualKey: data.virtualKey 
        };
      }
      
      return { isValid: false };
    }

    // Server-side validation - create a temporary client with the provided key
    const adminClient = new ConduitAdminClient({
      baseUrl: SDK_CONFIG.adminBaseURL,
      masterKey: adminPassword,
      timeout: SDK_CONFIG.timeout,
      retries: SDK_CONFIG.maxRetries,
    });

    // Try to make a simple request to validate the key
    // Using system info as a lightweight validation endpoint
    await adminClient.system.getSystemInfo();
    
    // For server-side, we don't have virtual key yet
    return { isValid: true };
  } catch (error: unknown) {
    const errorMessage = error instanceof Error ? error.message : 'Unknown error';
    console.warn('Master key validation failed:', errorMessage);
    
    // Check if it's an authentication error
    const status = error && typeof error === 'object' && 'status' in error ? error.status : null;
    if (status === 401 || status === 403) {
      return { isValid: false };
    }
    
    // For other errors (network, etc), we'll assume the key might be valid
    // This prevents network issues from blocking login
    return { isValid: false };
  }
}

export function sanitizeAdminPassword(adminPassword: string): string {
  return adminPassword.trim();
}

export function validateAdminPasswordFormat(adminPassword: string): string | null {
  const sanitized = sanitizeAdminPassword(adminPassword);
  
  if (!sanitized) {
    return 'Admin password is required';
  }
  
  // Allow shorter passwords for development/WebUI auth
  if (sanitized.length < 4) {
    return 'Admin password must be at least 4 characters';
  }
  
  if (sanitized.length > 100) {
    return 'Admin password is too long (maximum 100 characters)';
  }
  
  // Check for common patterns that indicate weak passwords
  if (/^[a-z]+$/.test(sanitized)) {
    return 'Admin password should contain mixed case, numbers, or special characters';
  }
  
  if (/^[0-9]+$/.test(sanitized)) {
    return 'Admin password should not be only numbers';
  }
  
  if (/^(.)\1+$/.test(sanitized)) {
    return 'Admin password should not contain repeated characters';
  }
  
  // Common weak patterns
  const weakPatterns = [
    'password', 'admin', 'test', 'demo', 'default', 'secret',
    '123456', 'qwerty', 'abc123', 'admin123', 'password123'
  ];
  
  const lowerKey = sanitized.toLowerCase();
  for (const pattern of weakPatterns) {
    if (lowerKey.includes(pattern)) {
      return 'Admin password appears to contain common patterns - please use a stronger password';
    }
  }
  
  return null;
}

export function getAdminPasswordStrength(adminPassword: string): { 
  score: number; 
  label: string; 
  suggestions: string[] 
} {
  const sanitized = sanitizeAdminPassword(adminPassword);
  let score = 0;
  const suggestions: string[] = [];
  
  if (!sanitized) {
    return { score: 0, label: 'Very Weak', suggestions: ['Enter an admin password'] };
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