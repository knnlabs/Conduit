/**
 * Authentication configuration service
 * Centralizes auth-related configuration to avoid direct process.env access in routes
 */

interface AuthConfig {
  adminPassword: string;
  sessionSecret: string;
  sessionDuration: number;
  apiToApiAuthKey: string;
  authType: 'password' | 'clerk';
}

class AuthConfigService {
  private static instance: AuthConfigService;
  private config: AuthConfig;

  private constructor() {
    // Initialize configuration from environment variables
    const envAuthType = (process.env.CONDUIT_AUTH_TYPE || 'password').toLowerCase();
    if (!['password', 'clerk'].includes(envAuthType)) {
      throw new Error(`Invalid CONDUIT_AUTH_TYPE: ${envAuthType}. Expected 'password' or 'clerk'.`);
    }
    this.config = {
      adminPassword: process.env.CONDUIT_ADMIN_LOGIN_PASSWORD || '',
      sessionSecret: process.env.CONDUIT_SESSION_SECRET || 'default-session-secret',
      sessionDuration: parseInt(process.env.CONDUIT_SESSION_DURATION || '86400000', 10), // 24 hours default
      apiToApiAuthKey: process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY || '',
      authType: envAuthType as 'password' | 'clerk',
    };

    // Validate required configuration
    if (!this.config.adminPassword) {
      console.warn('CONDUIT_ADMIN_LOGIN_PASSWORD not set - admin login will not work');
    }
  }

  static getInstance(): AuthConfigService {
    if (!AuthConfigService.instance) {
      AuthConfigService.instance = new AuthConfigService();
    }
    return AuthConfigService.instance;
  }

  /**
   * Verify admin password
   */
  verifyAdminPassword(password: string): boolean {
    if (!this.config.adminPassword) {
      return false;
    }
    return password === this.config.adminPassword;
  }

  /**
   * Get session configuration
   */
  getSessionConfig() {
    return {
      secret: this.config.sessionSecret,
      duration: this.config.sessionDuration
    };
  }

  /**
   * Verify API-to-API authentication key
   */
  verifyApiKey(key: string): boolean {
    if (!this.config.apiToApiAuthKey) {
      return false;
    }
    return key === this.config.apiToApiAuthKey;
  }

  /**
   * Check if auth is properly configured
   */
  isConfigured(): boolean {
    if (this.config.authType === 'password') {
      return !!this.config.adminPassword;
    }
    // clerk mode – assume Clerk env vars handled separately
    return true;
  }

  getAuthType() {
    return this.config.authType;
  }

  isClerk() {
    return this.config.authType === 'clerk';
  }

  isPassword() {
    return this.config.authType === 'password';
  }
}

// Export singleton instance
export const authConfig = AuthConfigService.getInstance();