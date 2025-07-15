/**
 * Authentication configuration service
 * Centralizes auth-related configuration to avoid direct process.env access in routes
 */

import { getAdminPasswordStrength } from './validation';

interface AuthConfig {
  adminPassword: string;
  apiToApiAuthKey: string;
}

class AuthConfigService {
  private static instance: AuthConfigService;
  private config: AuthConfig;

  private constructor() {
    // Initialize configuration from environment variables
    this.config = {
      adminPassword: process.env.CONDUIT_ADMIN_LOGIN_PASSWORD || '',
      apiToApiAuthKey: process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY || ''
    };

    // Validate required configuration
    if (!this.config.adminPassword) {
      console.warn('⚠️  CONDUIT_ADMIN_LOGIN_PASSWORD not set - admin login will not work');
    } else {
      // Check password strength and warn if weak, but still allow app to start
      const strength = getAdminPasswordStrength(this.config.adminPassword);
      
      if (strength.score < 50) {
        console.warn('⚠️  WEAK ADMIN PASSWORD DETECTED');
        console.warn(`   Password strength: ${strength.label} (${strength.score}/100)`);
        console.warn('   This is a security risk in production environments.');
        
        if (strength.suggestions.length > 0) {
          console.warn('   Suggestions to improve security:');
          strength.suggestions.forEach(suggestion => {
            console.warn(`   • ${suggestion}`);
          });
        }
        
        console.warn('   Consider using a stronger password by updating CONDUIT_ADMIN_LOGIN_PASSWORD');
        console.warn('   The WebUI will continue to start, but please address this security concern.');
      } else if (strength.score < 70) {
        console.info(`ℹ️  Admin password strength: ${strength.label} (${strength.score}/100) - Consider strengthening for production`);
      } else {
        console.info(`✅ Admin password strength: ${strength.label} (${strength.score}/100)`);
      }
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