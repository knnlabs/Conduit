/**
 * Access Denied Page
 * 
 * This page is shown when a user without admin privileges attempts to access the admin WebUI.
 * 
 * Feature: External Redirect
 * If the ACCESS_DENIED_REDIRECT environment variable is set, users will be automatically
 * redirected to the specified URL instead of seeing this access denied page.
 * 
 * Configuration:
 * - Set ACCESS_DENIED_REDIRECT in your environment with a valid URL
 * - Example: ACCESS_DENIED_REDIRECT="https://your-main-site.com"
 * - Can be configured in docker-compose.yml or .env files
 */

'use client';

import { Container, Title, Text, Button, Group, Paper } from '@mantine/core';
import { IconLock } from '@tabler/icons-react';
import { SignOutButton } from '@clerk/nextjs';
import { useAuth } from '@/contexts/AuthContext';
import { useRouter } from 'next/navigation';

export default function AccessDeniedPage() {
  const { isAuthDisabled } = useAuth();
  const router = useRouter();
  
  return (
    <Container size="sm" style={{ paddingTop: 100 }}>
      <Paper shadow="md" p="xl" withBorder>
        <Group justify="center" mb="xl">
          <IconLock size={64} style={{ color: 'var(--mantine-color-red-6)' }} />
        </Group>
        
        <Title order={2} ta="center" mb="md">
          Access Denied
        </Title>
        
        <Text ta="center" mb="xl" c="dimmed">
          You don&apos;t have permission to access this application. 
          Only administrators with proper authorization can use the Conduit WebUI.
        </Text>
        
        <Text ta="center" mb="xl" size="sm" c="dimmed">
          If you believe this is an error, please contact your system administrator.
        </Text>
        
        <Group justify="center">
          {isAuthDisabled ? (
            <Button variant="light" onClick={() => router.push('/')}>
              Go to Home
            </Button>
          ) : (
            <SignOutButton>
              <Button variant="light">Sign Out</Button>
            </SignOutButton>
          )}
        </Group>
      </Paper>
    </Container>
  );
}