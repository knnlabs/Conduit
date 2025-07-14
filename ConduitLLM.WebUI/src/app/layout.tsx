import type { Metadata, Viewport } from 'next';
import { ColorSchemeScript } from '@mantine/core';
import { MantineProvider } from '@/lib/providers/MantineProvider';
import { QueryProvider } from '@/lib/providers/QueryProvider';
import { ConditionalAuthProvider } from '@/lib/providers/ConditionalAuthProvider';
import { ThemeProvider } from '@/contexts/ThemeContext';
import { AppWrapper } from '@/components/layout/AppWrapper';
import { ErrorBoundary } from '@/components/error/ErrorBoundary';
import { ErrorHandlerInitializer } from '@/components/error/ErrorHandlerInitializer';
import { EnvironmentValidator } from '@/components/core/EnvironmentValidator';
import { SessionRefreshProvider } from '@/lib/providers/SessionRefreshProvider';
import './globals.css';

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
        <QueryProvider>
          <ThemeProvider>
            <MantineProvider>
              <ConditionalAuthProvider>
                <SessionRefreshProvider>
                  <ErrorBoundary>
                    <ErrorHandlerInitializer />
                    <EnvironmentValidator />
                    <AppWrapper>
                      {children}
                    </AppWrapper>
                  </ErrorBoundary>
                </SessionRefreshProvider>
              </ConditionalAuthProvider>
            </MantineProvider>
          </ThemeProvider>
        </QueryProvider>
      </body>
    </html>
  );
}