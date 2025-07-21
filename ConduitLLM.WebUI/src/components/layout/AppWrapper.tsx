'use client';

import { MainLayout } from './MainLayout';

interface AppWrapperProps {
  children: React.ReactNode;
}

export function AppWrapper({ children }: AppWrapperProps) {
  // No auth logic needed - middleware handles everything
  return (
    <MainLayout>
      {children}
    </MainLayout>
  );
}