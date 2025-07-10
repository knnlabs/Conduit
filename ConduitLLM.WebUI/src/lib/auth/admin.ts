import { NextRequest } from 'next/server';
import { validateSession } from './middleware';

export interface AdminAuth {
  isAuthenticated: boolean;
  error?: string;
}

export async function getAdminAuth(request: NextRequest): Promise<AdminAuth> {
  const validation = await validateSession(request);
  
  if (!validation.isValid) {
    return {
      isAuthenticated: false,
      error: validation.error || 'Unauthorized',
    };
  }
  
  // For now, any authenticated user can access admin endpoints
  // In the future, we might want to check for specific admin roles
  return {
    isAuthenticated: true,
  };
}