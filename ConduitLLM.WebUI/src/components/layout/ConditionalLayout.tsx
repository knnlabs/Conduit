'use client';

import { usePathname } from 'next/navigation';
import { AppWrapper } from './AppWrapper';

interface ConditionalLayoutProps {
  children: React.ReactNode;
}

export function ConditionalLayout({ children }: ConditionalLayoutProps) {
  const pathname = usePathname();
  
  // Pages that should NOT have the main layout
  const noLayoutPages = ['/access-denied'];
  
  if (noLayoutPages.includes(pathname)) {
    return children;
  }
  
  return <AppWrapper>{children}</AppWrapper>;
}