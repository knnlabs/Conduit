export interface AuthUser {
  masterKey: string;
  isAuthenticated: boolean;
  loginTime: Date;
}

export interface AuthState {
  user: AuthUser | null;
  isLoading: boolean;
  error: string | null;
  
  // Actions
  login: (masterKey: string, rememberMe?: boolean) => Promise<boolean>;
  logout: () => void;
  checkAuth: () => Promise<boolean>;
  clearError: () => void;
}

export interface LoginCredentials {
  masterKey: string;
  rememberMe: boolean;
}

export interface AuthError {
  message: string;
  code?: string;
}