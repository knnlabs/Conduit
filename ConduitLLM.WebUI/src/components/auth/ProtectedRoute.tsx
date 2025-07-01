'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { LoadingOverlay, Container } from '@mantine/core';
import { useAuthStore } from '@/stores/useAuthStore';

interface ProtectedRouteProps {
  children: React.ReactNode;
  fallback?: React.ReactNode;
}

export function ProtectedRoute({ children, fallback }: ProtectedRouteProps) {
  const router = useRouter();
  const { user, checkAuth } = useAuthStore();
  const [isChecking, setIsChecking] = useState(true);

  useEffect(() => {
    const checkAuthentication = () => {
      try {
        const isAuthenticated = checkAuth();
        
        if (!isAuthenticated) {
          router.replace('/login');
          return;
        }
        
        setIsChecking(false);
      } catch (error) {
        console.error('Auth check error:', error);
        router.replace('/login');
      }
    };

    checkAuthentication();
  }, [checkAuth, router]);

  // Show loading while checking authentication
  if (isChecking) {
    return (
      fallback || (
        <Container size="xl" h="100vh" style={{ position: 'relative' }}>
          <LoadingOverlay 
            visible={true} 
            overlayProps={{ radius: 'sm', blur: 2 }}
            loaderProps={{ color: 'blue', type: 'dots' }}
          />
        </Container>
      )
    );
  }

  // Don't render children if not authenticated
  if (!user?.isAuthenticated) {
    return null;
  }

  return <>{children}</>;
}

// Higher-order component version
export function withProtectedRoute<P extends object>(
  Component: React.ComponentType<P>
) {
  return function ProtectedComponent(props: P) {
    return (
      <ProtectedRoute>
        <Component {...props} />
      </ProtectedRoute>
    );
  };
}