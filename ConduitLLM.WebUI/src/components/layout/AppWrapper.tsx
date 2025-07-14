'use client';

import { usePathname } from 'next/navigation';
import { MainLayout } from './MainLayout';

interface AppWrapperProps {
  children: React.ReactNode;
}

interface AuthenticatedLayoutProps {
  children: React.ReactNode;
}

const PUBLIC_ROUTES = ['/login', '/sign-in', '/sign-up'];

function AuthenticatedLayout({ children }: AuthenticatedLayoutProps) {
  return (
    <MainLayout>
      {children}
    </MainLayout>
  );
}

export function AppWrapper({ children }: AppWrapperProps) {
  const pathname = usePathname();
  
  // Check if this is a public route
  const isPublicRoute = PUBLIC_ROUTES.includes(pathname);
  
  // For public routes (like login), render children without layout
  if (isPublicRoute) {
    return <>{children}</>;
  }

  // For protected routes, wrap with authenticated layout
  return (
    <AuthenticatedLayout>
      {children}
    </AuthenticatedLayout>
  );
}