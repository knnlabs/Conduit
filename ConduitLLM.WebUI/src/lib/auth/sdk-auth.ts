import { NextRequest } from 'next/server';
import { getServerAdminClient, getServerCoreClient } from '@/lib/clients/server';
import { logger } from '@/lib/utils/logging';

// Enhanced session data with SDK context
export interface SDKSessionData {
  isAuthenticated: boolean;
  expiresAt: string;
  masterKeyHash?: string;  // For admin operations
  virtualKey?: string;      // For core operations
  permissions?: string[];   // Future: granular permissions
  roles?: string[];         // User roles
  user?: {                  // User information
    id: string;
    email?: string;
    name?: string;
  };
}

// Validation result with SDK client context
export interface SDKAuthResult {
  isValid: boolean;
  error?: string;
  session?: SDKSessionData;
  adminClient?: ReturnType<typeof getServerAdminClient>;
  coreClient?: ReturnType<typeof getServerCoreClient>;
}

// Enhanced authentication context
export interface SDKAuthContext {
  adminClient?: ReturnType<typeof getServerAdminClient>;
  coreClient?: ReturnType<typeof getServerCoreClient>;
  request: NextRequest;
  session: SDKSessionData;
  user?: SDKSessionData['user'];
}

// Authentication options
export interface SDKAuthOptions {
  requireAdmin?: boolean;           // Require admin access
  requireCore?: boolean;            // Require core client access
  requireVirtualKey?: boolean;      // Require virtual key
  requireSpecificRole?: string[];   // Require specific roles
  requirePermissions?: string[];    // Require specific permissions
  allowServiceAccounts?: boolean;   // Allow service account access
}

// Extract session data from request
export function extractSessionData(request: NextRequest): SDKSessionData | null {
  try {
    // Check for session cookie (primary)
    const sessionCookie = request.cookies.get('conduit_session');
    if (sessionCookie) {
      try {
        return JSON.parse(sessionCookie.value);
      } catch {
        logger.warn('Invalid session cookie format');
      }
    }

    // Fallback to Authorization header for API clients
    const authHeader = request.headers.get('authorization');
    if (authHeader && authHeader.startsWith('Bearer ')) {
      const token = authHeader.substring(7);
      try {
        return JSON.parse(atob(token));
      } catch {
        logger.warn('Invalid session token format');
      }
    }

    return null;
  } catch (error) {
    logger.error('Error extracting session data', { error });
    return null;
  }
}

// Validate session for Admin SDK operations
export async function validateAdminSession(request: NextRequest): Promise<SDKAuthResult> {
  try {
    const sessionData = extractSessionData(request);
    
    if (!sessionData) {
      return { isValid: false, error: 'No session found' };
    }

    if (!sessionData.isAuthenticated) {
      return { isValid: false, error: 'Session not authenticated' };
    }

    // Check session expiration
    if (sessionData.expiresAt && new Date(sessionData.expiresAt) < new Date()) {
      return { isValid: false, error: 'Session expired' };
    }

    // Validate master key access
    if (!sessionData.masterKeyHash) {
      return { isValid: false, error: 'Admin access not authorized' };
    }

    // Create admin client
    try {
      const adminClient = getServerAdminClient();
      
      // Optional: Validate the client by making a lightweight API call
      if (process.env.CONDUIT_VALIDATE_SDK_AUTH === 'true') {
        await adminClient.system.getHealth();
      }

      return {
        isValid: true,
        session: sessionData,
        adminClient,
      };
    } catch (error) {
      logger.error('Failed to create admin client', { error });
      return { isValid: false, error: 'Admin client initialization failed' };
    }
  } catch (error) {
    logger.error('Admin session validation error', { error });
    return { isValid: false, error: 'Session validation failed' };
  }
}

// Validate session for Core SDK operations
export async function validateCoreSession(
  request: NextRequest,
  options?: { 
    requireVirtualKey?: boolean;
    virtualKeyHeader?: string;
  }
): Promise<SDKAuthResult> {
  const { requireVirtualKey = true, virtualKeyHeader = 'x-virtual-key' } = options || {};

  try {
    const sessionData = extractSessionData(request);
    
    if (!sessionData) {
      return { isValid: false, error: 'No session found' };
    }

    if (!sessionData.isAuthenticated) {
      return { isValid: false, error: 'Session not authenticated' };
    }

    // Check session expiration
    if (sessionData.expiresAt && new Date(sessionData.expiresAt) < new Date()) {
      return { isValid: false, error: 'Session expired' };
    }

    // Get virtual key from session or header
    let virtualKey = sessionData.virtualKey;
    
    // Allow override from header (for API testing)
    const headerKey = request.headers.get(virtualKeyHeader);
    if (headerKey) {
      virtualKey = headerKey;
    }

    if (requireVirtualKey && !virtualKey) {
      return { isValid: false, error: 'Virtual key required' };
    }

    // Create core client if virtual key is available
    let coreClient;
    if (virtualKey) {
      try {
        coreClient = getServerCoreClient(virtualKey);
        
        // TODO: SDK does not yet support health checks
        // Optional: Validate the client
        // if (process.env.CONDUIT_VALIDATE_SDK_AUTH === 'true') {
        //   await coreClient.health.check();
        // }
      } catch (error) {
        logger.error('Failed to create core client', { error });
        return { isValid: false, error: 'Core client initialization failed' };
      }
    }

    return {
      isValid: true,
      session: sessionData,
      coreClient,
    };
  } catch (error) {
    logger.error('Core session validation error', { error });
    return { isValid: false, error: 'Session validation failed' };
  }
}

// Validate either admin or core session
export async function validateSDKSession(
  request: NextRequest,
  options?: {
    requireAdmin?: boolean;
    requireCore?: boolean;
    requireVirtualKey?: boolean;
  }
): Promise<SDKAuthResult> {
  const { requireAdmin = false, requireCore = false, requireVirtualKey = false } = options || {};

  // If admin is required, validate admin session
  if (requireAdmin) {
    return validateAdminSession(request);
  }

  // If core is required, validate core session
  if (requireCore) {
    return validateCoreSession(request, { requireVirtualKey });
  }

  // Otherwise, validate basic session
  const sessionData = extractSessionData(request);
  
  if (!sessionData) {
    return { isValid: false, error: 'No session found' };
  }

  if (!sessionData.isAuthenticated) {
    return { isValid: false, error: 'Session not authenticated' };
  }

  if (sessionData.expiresAt && new Date(sessionData.expiresAt) < new Date()) {
    return { isValid: false, error: 'Session expired' };
  }

  return {
    isValid: true,
    session: sessionData,
  };
}

// Helper to extract virtual key from various sources
export function extractVirtualKey(request: NextRequest): string | null {
  // Check custom header
  const headerKey = request.headers.get('x-virtual-key') || 
                   request.headers.get('x-api-key');
  if (headerKey) return headerKey;

  // Check Authorization header (Bearer token)
  const authHeader = request.headers.get('authorization');
  if (authHeader && authHeader.startsWith('Bearer vk_')) {
    return authHeader.substring(7);
  }

  // Check session
  const sessionData = extractSessionData(request);
  if (sessionData?.virtualKey) {
    return sessionData.virtualKey;
  }

  // Check query parameter (least secure, only for specific endpoints)
  const url = new URL(request.url);
  const queryKey = url.searchParams.get('api_key');
  if (queryKey && queryKey.startsWith('vk_')) {
    return queryKey;
  }

  return null;
}

// Create session data for SDK operations
export function createSDKSession(options: {
  masterKeyAccess?: boolean;
  virtualKey?: string;
  expirationHours?: number;
  permissions?: string[];
}): SDKSessionData {
  const { 
    masterKeyAccess = false, 
    virtualKey, 
    expirationHours = 24,
    permissions = []
  } = options;

  const expiresAt = new Date();
  expiresAt.setHours(expiresAt.getHours() + expirationHours);

  return {
    isAuthenticated: true,
    expiresAt: expiresAt.toISOString(),
    ...(masterKeyAccess && { masterKeyHash: 'admin' }), // In production, use actual hash
    ...(virtualKey && { virtualKey }),
    ...(permissions.length > 0 && { permissions }),
  };
}

// Check if user has required roles
async function checkUserRole(session: SDKSessionData, requiredRoles: string[]): Promise<boolean> {
  if (!session.roles || session.roles.length === 0) {
    return false;
  }
  return requiredRoles.some(role => session.roles?.includes(role));
}

// Check if user has required permissions
async function checkUserPermissions(session: SDKSessionData, requiredPermissions: string[]): Promise<boolean> {
  if (!session.permissions || session.permissions.length === 0) {
    return false;
  }
  return requiredPermissions.every(permission => session.permissions?.includes(permission));
}

// Create unauthorized response
export function createUnauthorizedResponse(error?: string): Response {
  return new Response(
    JSON.stringify({ error: error || 'Unauthorized' }),
    { 
      status: 401, 
      headers: { 'Content-Type': 'application/json' }
    }
  );
}

// Create forbidden response
export function createForbiddenResponse(error?: string): Response {
  return new Response(
    JSON.stringify({ error: error || 'Forbidden' }),
    { 
      status: 403, 
      headers: { 'Content-Type': 'application/json' }
    }
  );
}

// Enhanced middleware helper for route handlers
export function withSDKAuth<T extends SDKAuthOptions = SDKAuthOptions>(
  handler: (
    request: NextRequest,
    context: SDKAuthContext
  ) => Promise<Response>,
  options?: T
) {
  return async (request: NextRequest): Promise<Response> => {
    try {
      // 1. Validate session based on requirements
      const auth = await validateSDKSession(request, {
        requireAdmin: options?.requireAdmin,
        requireCore: options?.requireCore,
        requireVirtualKey: options?.requireVirtualKey,
      });
      
      if (!auth.isValid) {
        return createUnauthorizedResponse(auth.error);
      }

      if (!auth.session) {
        return createUnauthorizedResponse('Invalid session');
      }

      // 2. Check role requirements
      if (options?.requireSpecificRole && options.requireSpecificRole.length > 0) {
        const hasRole = await checkUserRole(auth.session, options.requireSpecificRole);
        if (!hasRole) {
          return createForbiddenResponse('Insufficient role permissions');
        }
      }

      // 3. Check permission requirements
      if (options?.requirePermissions && options.requirePermissions.length > 0) {
        const hasPermissions = await checkUserPermissions(auth.session, options.requirePermissions);
        if (!hasPermissions) {
          return createForbiddenResponse('Insufficient permissions');
        }
      }

      // 4. Check service account restrictions
      if (options?.allowServiceAccounts === false && auth.session.user?.id?.startsWith('service_')) {
        return createForbiddenResponse('Service accounts not allowed');
      }

      // 5. Create context object
      const context: SDKAuthContext = {
        adminClient: auth.adminClient,
        coreClient: auth.coreClient,
        request,
        session: auth.session,
        user: auth.session.user,
      };

      // 6. Call handler with context
      return await handler(request, context);

    } catch (error) {
      logger.error('Authentication middleware error', { error });
      return new Response(
        JSON.stringify({ error: 'Internal authentication error' }),
        { 
          status: 500, 
          headers: { 'Content-Type': 'application/json' }
        }
      );
    }
  };
}

// Legacy compatibility wrapper - maps old API to new API
export function withSDKAuthLegacy(
  handler: (
    request: NextRequest,
    context: { auth: SDKAuthResult }
  ) => Promise<Response>,
  options?: {
    requireAdmin?: boolean;
    requireCore?: boolean;
    requireVirtualKey?: boolean;
  }
) {
  return withSDKAuth(
    async (request, context) => {
      // Map new context to legacy format
      const legacyAuth: SDKAuthResult = {
        isValid: true,
        session: context.session,
        adminClient: context.adminClient,
        coreClient: context.coreClient,
      };
      return handler(request, { auth: legacyAuth });
    },
    options
  );
}