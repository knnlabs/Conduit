import { Container, Title, Text, Button, Group, Paper } from '@mantine/core';
import { IconLock } from '@tabler/icons-react';
import Link from 'next/link';
import { SignOutButton } from '@clerk/nextjs';

export default function AccessDeniedPage() {
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
          You don't have permission to access this application. 
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