import type { Metadata, Viewport } from 'next';
import { ColorSchemeScript } from '@mantine/core';
import { MantineProvider } from '@/lib/providers/MantineProvider';
import { QueryProvider } from '@/lib/providers/QueryProvider';
import { AuthProvider } from '@/lib/providers/AuthProvider';
import { AppWrapper } from '@/components/layout/AppWrapper';
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
        <ColorSchemeScript />
      </head>
      <body>
        <QueryProvider>
          <MantineProvider>
            {children}
          </MantineProvider>
        </QueryProvider>
      </body>
    </html>
  );
}