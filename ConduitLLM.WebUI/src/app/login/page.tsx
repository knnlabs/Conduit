'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { getAuthMode } from '@/lib/auth/auth-mode';
import {
  Stack,
  Title,
  Text,
  Group,
  Button,
  PasswordInput,
  Checkbox,
  Alert,
  Center,
  Container,
  Paper,
  ThemeIcon,
} from '@mantine/core';
import {
  IconAlertCircle,
  IconServer,
  IconKey,
  IconShield,
} from '@tabler/icons-react';

export default function LoginPage() {
  const [adminKey, setAdminKey] = useState('');
  const [rememberMe, setRememberMe] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const router = useRouter();
  
  useEffect(() => {
    // If Clerk is enabled, redirect to Clerk sign-in
    if (getAuthMode() === 'clerk') {
      router.replace('/sign-in');
    }
  }, [router]);
  
  // If Clerk is enabled, don't render the Conduit login form
  if (getAuthMode() === 'clerk') {
    return null;
  }

  const handleSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    
    if (!adminKey.trim()) {
      setError('Admin key is required');
      return;
    }

    setLoading(true);
    setError(null);
    
    try {
      // Call the Next.js API route which uses the SDK
      const response = await fetch('/api/auth/validate', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ 
          password: adminKey,
          rememberMe,
        }),
        credentials: 'include',
      });
      
      if (response.ok) {
        const data = await response.json();
        
        // Login is handled by the API endpoint which sets the session cookie
        // The auth state will be updated automatically via AuthProvider
        
        // Redirect to home page
        router.replace('/');
      } else {
        const error = await response.json();
        setError(error.message || 'Authentication failed');
      }
    } catch (err) {
      console.error('Login error:', err);
      setError('An error occurred during login');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Container size={420} style={{ minHeight: '100vh', display: 'flex', alignItems: 'center' }}>
      <Paper shadow="lg" p="xl" radius="md" w="100%">
        <Stack gap="md">
          <Center>
            <ThemeIcon size={60} radius="xl" variant="gradient" gradient={{ from: 'cyan', to: 'blue' }}>
              <IconServer size={30} />
            </ThemeIcon>
          </Center>
          
          <Stack gap={0} align="center">
            <Title order={2}>Conduit Admin</Title>
            <Text c="dimmed" size="sm">Enter your admin password to continue</Text>
          </Stack>

          {error && (
            <Alert
              icon={<IconAlertCircle size={16} />}
              title="Authentication Failed"
              color="red"
              variant="light"
            >
              {error}
            </Alert>
          )}

          <form onSubmit={handleSubmit}>
            <Stack gap="md">
              <PasswordInput
                label="Admin Password"
                placeholder="Enter admin password"
                required
                value={adminKey}
                onChange={(e) => setAdminKey(e.currentTarget.value)}
                leftSection={<IconKey size={16} />}
                disabled={loading}
                autoFocus
              />

              <Checkbox
                label="Remember me for 30 days"
                checked={rememberMe}
                onChange={(e) => setRememberMe(e.currentTarget.checked)}
                disabled={loading}
              />

              <Button
                type="submit"
                fullWidth
                loading={loading}
                leftSection={!loading && <IconShield size={16} />}
              >
                Sign In
              </Button>
            </Stack>
          </form>

          <Text size="xs" c="dimmed" ta="center">
            This is the administrative interface for Conduit. Only authorized personnel should access this system.
          </Text>
        </Stack>
      </Paper>
    </Container>
  );
}