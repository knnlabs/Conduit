import { create } from 'zustand';
import { AuthState, AuthUser } from '@/types/auth';
import { authStorage, StoredAuth } from '@/lib/auth/storage';
import { sanitizeMasterKey } from '@/lib/auth/client-validation';
import { validateMasterKey } from '@/lib/auth/validation';
import { getAuthMode } from '@/lib/auth/auth-mode';
import { isClerkAuthenticated } from '@/lib/auth/clerk-helpers';

interface UnifiedAuthState extends AuthState {
  authMode: 'clerk' | 'conduit';
  clerkUser: any | null;
}

export const useUnifiedAuthStore = create<UnifiedAuthState>((set, get) => ({
  user: null,
  isLoading: false,
  error: null,
  authMode: getAuthMode(),
  clerkUser: null,

  login: async (masterKey: string, rememberMe: boolean = false): Promise<boolean> => {
    const { authMode } = get();
    
    if (authMode === 'clerk') {
      // Clerk handles login through its own UI
      return false;
    }

    set({ isLoading: true, error: null });

    try {
      const sanitizedKey = sanitizeMasterKey(masterKey);
      
      if (!sanitizedKey) {
        set({ error: 'Master key is required', isLoading: false });
        return false;
      }

      // Validate the master key with the admin API
      const validationResult = await validateMasterKey(sanitizedKey);
      
      if (!validationResult.isValid) {
        set({ error: 'Invalid master key', isLoading: false });
        return false;
      }

      // Create user object
      const user: AuthUser = {
        masterKey: sanitizedKey,
        virtualKey: validationResult.virtualKey,
        isAuthenticated: true,
        loginTime: new Date(),
      };

      // Save to storage
      const storedAuth: StoredAuth = {
        masterKey: sanitizedKey,
        virtualKey: validationResult.virtualKey,
        isAuthenticated: true,
        loginTime: user.loginTime.toISOString(),
        rememberMe,
      };
      
      await authStorage.save(storedAuth);

      set({ user, isLoading: false, error: null });
      return true;
    } catch (error) {
      console.error('Login error:', error);
      set({ error: 'Login failed', isLoading: false });
      return false;
    }
  },

  logout: () => {
    const { authMode } = get();
    
    if (authMode === 'clerk') {
      // Clerk logout is handled by the Header component
      return;
    }

    // Clear Conduit auth
    set({ isLoading: true });

    // Call logout API endpoint
    fetch('/api/auth/logout', {
      method: 'POST',
      credentials: 'include',
    })
      .then(() => {
        // Clear storage
        return authStorage.clear();
      })
      .then(() => {
        set({ user: null, isLoading: false, error: null });
      })
      .catch((error) => {
        console.error('Logout error:', error);
        set({ error: 'Logout failed', isLoading: false });
      });
  },

  // Initialize auth state on app load
  initialize: async () => {
    const { authMode } = get();
    
    if (authMode === 'clerk') {
      // Clerk auth is handled by ClerkProvider
      return;
    }

    set({ isLoading: true });

    try {
      // Check if we have stored auth
      const storedAuth = await authStorage.load();
      
      if (storedAuth && storedAuth.isAuthenticated) {
        // Validate the stored auth is still valid
        const validationResult = await validateMasterKey(storedAuth.masterKey);
        
        if (validationResult.isValid) {
          const user: AuthUser = {
            masterKey: storedAuth.masterKey,
            virtualKey: validationResult.virtualKey || storedAuth.virtualKey,
            isAuthenticated: true,
            loginTime: new Date(storedAuth.loginTime),
          };
          
          set({ user, isLoading: false });
        } else {
          // Stored auth is no longer valid
          await authStorage.clear();
          set({ user: null, isLoading: false });
        }
      } else {
        set({ user: null, isLoading: false });
      }
    } catch (error) {
      console.error('Auth initialization error:', error);
      await authStorage.clear();
      set({ user: null, isLoading: false });
    }
  },

  updateClerkUser: (clerkUser: any) => {
    set({ clerkUser });
  },

  checkAuth: async (): Promise<boolean> => {
    const { authMode } = get();
    
    if (authMode === 'clerk') {
      // Clerk auth is handled automatically
      return await isClerkAuthenticated();
    }

    // For Conduit auth, check stored session
    set({ isLoading: true });

    try {
      const storedAuth = await authStorage.load();
      
      if (storedAuth && storedAuth.isAuthenticated) {
        // Validate the stored auth is still valid
        const validationResult = await validateMasterKey(storedAuth.masterKey);
        
        if (validationResult.isValid) {
          const user: AuthUser = {
            masterKey: storedAuth.masterKey,
            virtualKey: validationResult.virtualKey || storedAuth.virtualKey,
            isAuthenticated: true,
            loginTime: new Date(storedAuth.loginTime),
          };
          
          set({ user, isLoading: false });
          return true;
        }
      }
      
      await authStorage.clear();
      set({ user: null, isLoading: false });
      return false;
    } catch (error) {
      console.error('Auth check error:', error);
      set({ user: null, isLoading: false });
      return false;
    }
  },

  clearError: () => {
    set({ error: null });
  },
}));