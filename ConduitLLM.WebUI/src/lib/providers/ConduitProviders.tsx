'use client';

import React from 'react';
import type { QueryClient } from '@tanstack/react-query';

interface ConduitProvidersProps {
  children: React.ReactNode;
  queryClient: QueryClient;
}

// NOTE: SDK React Query providers are temporarily disabled due to module resolution issues
// The SDKs need to be properly published to npm or the build system needs to be configured
// to handle local file dependencies with peer dependencies correctly
export function ConduitProviders({ children }: ConduitProvidersProps) {
  // For now, just pass through children
  // TODO: Re-enable SDK providers once module resolution is fixed
  return <>{children}</>;
}