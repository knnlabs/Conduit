import { EncryptionService } from './encryption';

const AUTH_STORAGE_KEY = 'conduit-auth';
const AUTH_REMEMBER_KEY = 'conduit-remember';

export interface StoredAuth {
  masterKey: string;
  virtualKey?: string;
  isAuthenticated: boolean;
  loginTime: string;
  rememberMe: boolean;
}

interface EncryptedStoredAuth {
  encryptedMasterKey: string;
  virtualKey?: string;
  isAuthenticated: boolean;
  loginTime: string;
  rememberMe: boolean;
  encrypted: true;
}

export const authStorage = {
  save: async (auth: StoredAuth): Promise<void> => {
    try {
      if (typeof window === 'undefined') return;
      
      // Encrypt the master key if encryption is available
      let dataToStore: string;
      
      if (EncryptionService.isAvailable()) {
        const encryptedMasterKey = await EncryptionService.encrypt(auth.masterKey);
        const encryptedAuth: EncryptedStoredAuth = {
          encryptedMasterKey,
          virtualKey: auth.virtualKey,
          isAuthenticated: auth.isAuthenticated,
          loginTime: auth.loginTime,
          rememberMe: auth.rememberMe,
          encrypted: true,
        };
        dataToStore = JSON.stringify(encryptedAuth);
      } else {
        // Fallback to plaintext with warning (for development/unsupported browsers)
        console.warn('Encryption not available, storing master key in plaintext');
        dataToStore = JSON.stringify(auth);
      }
      
      if (auth.rememberMe) {
        localStorage.setItem(AUTH_STORAGE_KEY, dataToStore);
        localStorage.setItem(AUTH_REMEMBER_KEY, 'true');
      } else {
        sessionStorage.setItem(AUTH_STORAGE_KEY, dataToStore);
        localStorage.removeItem(AUTH_REMEMBER_KEY);
      }
    } catch (error) {
      console.error('Failed to save auth to storage:', error);
      throw new Error('Failed to save authentication data');
    }
  },

  load: async (): Promise<StoredAuth | null> => {
    try {
      if (typeof window === 'undefined') return null;
      
      const rememberMe = localStorage.getItem(AUTH_REMEMBER_KEY) === 'true';
      const storage = rememberMe ? localStorage : sessionStorage;
      const stored = storage.getItem(AUTH_STORAGE_KEY);
      
      if (!stored) return null;
      
      const parsedData = JSON.parse(stored);
      
      // Check if this is encrypted data
      let auth: StoredAuth;
      if (parsedData.encrypted && parsedData.encryptedMasterKey) {
        // Decrypt the master key
        try {
          const decryptedMasterKey = await EncryptionService.decrypt(parsedData.encryptedMasterKey);
          auth = {
            masterKey: decryptedMasterKey,
            virtualKey: parsedData.virtualKey,
            isAuthenticated: parsedData.isAuthenticated,
            loginTime: parsedData.loginTime,
            rememberMe: parsedData.rememberMe,
          };
        } catch (decryptError) {
          console.error('Failed to decrypt master key:', decryptError);
          authStorage.clear();
          return null;
        }
      } else {
        // Legacy plaintext data
        auth = parsedData as StoredAuth;
      }
      
      // Check if session is expired (24 hours for session, 30 days for remember me)
      const loginTime = new Date(auth.loginTime);
      const now = new Date();
      const maxAge = auth.rememberMe ? 30 * 24 * 60 * 60 * 1000 : 24 * 60 * 60 * 1000;
      
      if (now.getTime() - loginTime.getTime() > maxAge) {
        authStorage.clear();
        return null;
      }
      
      return auth;
    } catch (error) {
      console.error('Failed to load auth from storage:', error);
      authStorage.clear();
      return null;
    }
  },

  clear: (): void => {
    try {
      if (typeof window === 'undefined') return;
      
      localStorage.removeItem(AUTH_STORAGE_KEY);
      localStorage.removeItem(AUTH_REMEMBER_KEY);
      sessionStorage.removeItem(AUTH_STORAGE_KEY);
    } catch (error) {
      console.warn('Failed to clear auth storage:', error);
    }
  },

  isRemembered: (): boolean => {
    try {
      if (typeof window === 'undefined') return false;
      return localStorage.getItem(AUTH_REMEMBER_KEY) === 'true';
    } catch {
      return false;
    }
  },
};