'use client';

import { usePathname } from 'next/navigation';
import { MainLayout } from './MainLayout';
import { useNavigationStateHub } from '@/hooks/signalr/useNavigationStateHub';
import { useSpendTracking } from '@/hooks/signalr/useSpendTracking';
import { useVirtualKeyHub } from '@/hooks/signalr/useVirtualKeyHub';
import { useProviderHub } from '@/hooks/signalr/useProviderHub';
import { useModelMappingHub } from '@/hooks/signalr/useModelMappingHub';

interface AppWrapperProps {
  children: React.ReactNode;
}

interface AuthenticatedLayoutProps {
  children: React.ReactNode;
}

const PUBLIC_ROUTES = ['/login'];

function AuthenticatedLayout({ children }: AuthenticatedLayoutProps) {
  // Connect to real-time hubs for live updates
  useNavigationStateHub();
  useSpendTracking();
  useVirtualKeyHub();
  useProviderHub();
  useModelMappingHub();

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