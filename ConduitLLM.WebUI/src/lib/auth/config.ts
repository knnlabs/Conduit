/**
 * Authentication configuration service
 * Centralizes auth-related configuration to avoid direct process.env access in routes
 */

interface AuthConfig {
  adminPassword: string;
  sessionSecret: string;
  sessionDuration: number;
  apiToApiAuthKey: string;
}

class AuthConfigService {
  private static instance: AuthConfigService;
  private config: AuthConfig;

  private constructor() {
    // Initialize configuration from environment variables
    this.config = {
      adminPassword: process.env.CONDUIT_ADMIN_LOGIN_PASSWORD || '',
      sessionSecret: process.env.CONDUIT_SESSION_SECRET || 'default-session-secret',
      sessionDuration: parseInt(process.env.CONDUIT_SESSION_DURATION || '86400000', 10), // 24 hours default
      apiToApiAuthKey: process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY || ''
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
    return !!(this.config.adminPassword && this.config.apiToApiAuthKey);
  }
}

// Export singleton instance
export const authConfig = AuthConfigService.getInstance();