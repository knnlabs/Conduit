import { create } from 'zustand';
import { AuthState, AuthUser } from '@/types/auth';
import { authStorage, StoredAuth } from '@/lib/auth/storage';
import { sanitizeAdminPassword } from '@/lib/auth/client-validation';
import { validateAdminPassword } from '@/lib/auth/validation';

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  isLoading: false,
  error: null,

  login: async (adminPassword: string, rememberMe: boolean = false): Promise<boolean> => {
    set({ isLoading: true, error: null });

    try {
      const sanitizedPassword = sanitizeAdminPassword(adminPassword);
      
      if (!sanitizedPassword) {
        set({ error: 'Admin password is required', isLoading: false });
        return false;
      }

      // Validate the admin password with the admin API
      const validationResult = await validateAdminPassword(sanitizedPassword);
      
      if (!validationResult.isValid) {
        set({ error: 'Invalid admin password', isLoading: false });
        return false;
      }

      // Create user object
      const user: AuthUser = {
        adminPassword: sanitizedPassword,
        virtualKey: validationResult.virtualKey,
        isAuthenticated: true,
        loginTime: new Date(),
      };

      // Save to storage
      const storedAuth: StoredAuth = {
        masterKey: sanitizedPassword,
        virtualKey: validationResult.virtualKey,
        isAuthenticated: true,
        loginTime: user.loginTime.toISOString(),
        rememberMe,
      };
      
      await authStorage.save(storedAuth);

      set({ user, isLoading: false, error: null });
      return true;
    } catch (error: unknown) {
      console.error('Login error:', error);
      const errorMessage = error instanceof Error ? error.message : 'Login failed. Please try again.';
      set({ 
        error: errorMessage, 
        isLoading: false 
      });
      return false;
    }
  },

  logout: (): void => {
    authStorage.clear();
    set({ user: null, error: null });
  },

  checkAuth: async (): Promise<boolean> => {
    try {
      const stored = await authStorage.load();
      
      if (!stored || !stored.isAuthenticated) {
        return false;
      }

      const user: AuthUser = {
        adminPassword: stored.masterKey,
        virtualKey: stored.virtualKey,
        isAuthenticated: true,
        loginTime: new Date(stored.loginTime),
      };

      set({ user, error: null });
      return true;
    } catch (error) {
      console.error('Auth check error:', error);
      authStorage.clear();
      set({ user: null, error: null });
      return false;
    }
  },

  clearError: (): void => {
    set({ error: null });
  },
}));

// Initialize auth state on store creation
if (typeof window !== 'undefined') {
  useAuthStore.getState().checkAuth().catch(console.error);
}