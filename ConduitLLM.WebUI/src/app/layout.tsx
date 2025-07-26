import type { Metadata, Viewport } from 'next';
import { ColorSchemeScript } from '@mantine/core';
import { ClerkProvider } from '@clerk/nextjs';
import { MantineProvider } from '@/lib/providers/MantineProvider';
import { QueryProvider } from '@/lib/providers/QueryProvider';
import { ThemeProvider } from '@/contexts/ThemeContext';
import { ErrorBoundary } from '@/components/error/ErrorBoundary';
import { ErrorHandlerInitializer } from '@/components/error/ErrorHandlerInitializer';
import { EnvironmentValidator } from '@/components/core/EnvironmentValidator';
import { ConditionalLayout } from '@/components/layout/ConditionalLayout';
import './globals.css';

// Force dynamic rendering for all pages since we use authentication
export const dynamic = 'force-dynamic';

export const metadata: Metadata = {
  title: 'Conduit WebUI',
  description: 'Next.js WebUI for Conduit LLM Platform',
};

export const viewport: Viewport = {
  width: 'device-width',
  initialScale: 1,
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en">
      <head>
        <ColorSchemeScript defaultColorScheme="auto" />
      </head>
      <body>
        <ClerkProvider>
          <QueryProvider>
            <ThemeProvider>
              <MantineProvider>
                <ErrorBoundary>
                  <ErrorHandlerInitializer />
                  <EnvironmentValidator />
                  <ConditionalLayout>
                    {children}
                  </ConditionalLayout>
                </ErrorBoundary>
              </MantineProvider>
            </ThemeProvider>
          </QueryProvider>
        </ClerkProvider>
      </body>
    </html>
  );
}