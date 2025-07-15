export interface AuthUser {
  adminPassword: string;
  virtualKey?: string;
  isAuthenticated: boolean;
  loginTime: Date;
}

export interface AuthState {
  user: AuthUser | null;
  isLoading: boolean;
  error: string | null;
  
  // Actions
  login: (adminPassword: string, rememberMe?: boolean) => Promise<boolean>;
  logout: () => void;
  checkAuth: () => Promise<boolean>;
  clearError: () => void;
}

export interface LoginCredentials {
  adminPassword: string;
  rememberMe: boolean;
}

export interface AuthError {
  message: string;
  code?: string;
}