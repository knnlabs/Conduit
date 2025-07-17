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

import { Container, Title, Text, Button, Group, Paper } from '@mantine/core';
import { IconLock } from '@tabler/icons-react';
import { SignOutButton } from '@clerk/nextjs';
import { redirect } from 'next/navigation';

export default function AccessDeniedPage() {
  // Check for redirect URL in environment variable
  const redirectUrl = process.env.ACCESS_DENIED_REDIRECT;
  
  // If a redirect URL is configured, use it
  if (redirectUrl) {
    // You can add URL validation here if needed
    // For example, to only allow specific domains
    try {
      // Basic URL validation
      new URL(redirectUrl);
      redirect(redirectUrl);
    } catch (_error) {
      console.error('Invalid redirect URL:', redirectUrl);
      // Continue to the access denied page if URL is invalid
    }
  }
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
          <SignOutButton>
            <Button variant="light">Sign Out</Button>
          </SignOutButton>
        </Group>
      </Paper>
    </Container>
  );
}