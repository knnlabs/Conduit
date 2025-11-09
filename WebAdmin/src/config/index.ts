/**
 * Central configuration module
 * Re-exports all configuration utilities
 */

export { 
  config, 
  validateEnvironment, 
  getAdminApiUrl, 
  getCoreApiUrl,
  getSignalRUrl,
  type AppConfig 
} from './environment';