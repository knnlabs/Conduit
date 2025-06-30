'use client';

import { useEffect } from 'react';
import { usePathname, useRouter } from 'next/navigation';
import { useAuthStore } from '@/stores/useAuthStore';

interface AuthProviderProps {
  children: React.ReactNode;
}

const PUBLIC_ROUTES = ['/login'];

export function AuthProvider({ children }: AuthProviderProps) {
  const pathname = usePathname();
  const router = useRouter();
  const { user, checkAuth } = useAuthStore();

  useEffect(() => {
    // Skip auth check for public routes
    if (PUBLIC_ROUTES.includes(pathname)) {
      return;
    }

    // Check authentication on app load and route changes
    const checkAuthentication = async () => {
      const isAuthenticated = await checkAuth();

      if (!isAuthenticated) {
        router.replace('/login');
      }
    };

    checkAuthentication().catch(console.error);
  }, [pathname, router, checkAuth]);

  // For public routes, always render children
  if (PUBLIC_ROUTES.includes(pathname)) {
    return <>{children}</>;
  }

  // For protected routes, only render if authenticated
  if (!user?.isAuthenticated) {
    return null;
  }

  return <>{children}</>;
}