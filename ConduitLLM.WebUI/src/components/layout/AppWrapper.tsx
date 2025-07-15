'use client';

import { MainLayout } from './MainLayout';

interface AppWrapperProps {
  children: React.ReactNode;
}

export function AppWrapper({ children }: AppWrapperProps) {
  // All routes now use the main layout - no authentication required
  return (
    <MainLayout>
      {children}
    </MainLayout>
  );
}