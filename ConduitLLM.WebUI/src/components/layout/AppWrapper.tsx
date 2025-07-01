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

const PUBLIC_ROUTES = ['/login'];

export function AppWrapper({ children }: AppWrapperProps) {
  const pathname = usePathname();
  
  // Initialize SignalR connections for authenticated pages
  const isPublicRoute = PUBLIC_ROUTES.includes(pathname);
  
  // Connect to real-time hubs for live updates
  useNavigationStateHub();
  useSpendTracking();
  useVirtualKeyHub();
  useProviderHub();
  useModelMappingHub();

  // For public routes (like login), render children without layout
  if (isPublicRoute) {
    return <>{children}</>;
  }

  // For protected routes, wrap with main layout
  return (
    <MainLayout>
      {children}
    </MainLayout>
  );
}