'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useAuthStore } from '@/stores/useAuthStore';
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
  const { login } = useAuthStore();

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
          adminKey: adminKey.trim()
        }),
        credentials: 'include',
      });

      if (response.ok) {
        // Update the auth store to match the cookie state
        const success = await login(adminKey.trim(), rememberMe);
        
        if (success) {
          router.push('/');
        } else {
          setError('Unable to update authentication state');
        }
      } else {
        const errorData = await response.json().catch(() => ({ error: 'Login failed' }));
        setError(errorData.error || 'Invalid admin key');
      }
    } catch (error) {
      console.error('Login error:', error);
      setError('Unable to connect to server');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ minHeight: '100vh', position: 'relative', overflow: 'auto' }}>
      <div
        style={{ 
          position: 'absolute', 
          top: 0, 
          left: 0, 
          right: 0, 
          bottom: 0, 
          backgroundImage: "url(\"data:image/svg+xml,%3Csvg width='60' height='60' viewBox='0 0 60 60' xmlns='http://www.w3.org/2000/svg'%3E%3Cg fill='none' fill-rule='evenodd'%3E%3Cg fill='%23000000' fill-opacity='0.03'%3E%3Cpath d='m36 34v-4h-2v4h-4v2h4v4h2v-4h4v-2h-4zm0-30V0h-2v4h-4v2h4v4h2V6h4V4h-4zM6 34v-4H4v4H0v2h4v4h2v-4h4v-2H6zM6 4V0H4v4H0v2h4v4h2V6h4V4H6z'/%3E%3C/g%3E%3C/g%3E%3C/svg%3E\")",
          backgroundRepeat: 'repeat',
          zIndex: 0 
        }}
      />
      <div style={{ position: 'absolute', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(0, 0, 0, 0.1)', zIndex: 1 }} />
      <Container size={420} style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', position: 'relative', zIndex: 2 }}>
        <Paper withBorder shadow="lg" p={40} radius="md" w="100%" style={{ position: 'relative' }}>
          <Center mb="xl">
            <ThemeIcon size={60} radius="md" variant="gradient" gradient={{ from: 'blue', to: 'cyan' }}>
              <IconServer size={30} />
            </ThemeIcon>
          </Center>

          <Title ta="center" mb="md">
            Conduit Admin Access
          </Title>
          <Text c="dimmed" size="sm" ta="center" mb="xl">
            AI Gateway & Management Platform
          </Text>

          <Alert icon={<IconKey size={16} />} color="blue" mb="md" variant="light">
            <Text size="sm">
              <strong>Admin Key Required:</strong><br />
              Enter the administrator key to access the dashboard
            </Text>
          </Alert>

          <form onSubmit={handleSubmit}>
            <Stack gap="md">
              {error && (
                <Alert icon={<IconAlertCircle size={16} />} color="red" variant="light">
                  <Text size="sm">{error}</Text>
                </Alert>
              )}
              
              <PasswordInput
                label="Admin Key"
                placeholder="Enter your admin key"
                description="Contact your administrator for access credentials"
                required
                leftSection={<IconShield size={16} />}
                value={adminKey}
                onChange={(event) => setAdminKey(event.currentTarget.value)}
                disabled={loading}
                autoComplete="off"
                data-testid="admin-key-input"
              />

              <Group justify="space-between" mt="md">
                <Checkbox
                  label="Remember me"
                  description="Keep me signed in for 7 days"
                  checked={rememberMe}
                  onChange={(event) => setRememberMe(event.currentTarget.checked)}
                  disabled={loading}
                />
              </Group>

              <Button
                type="submit"
                fullWidth
                mt="md"
                loading={loading}
                leftSection={<IconKey size={16} />}
                size="md"
                disabled={loading}
                data-testid="submit-button"
              >
                {loading ? 'Authenticating...' : 'Access Dashboard'}
              </Button>
            </Stack>
          </form>

          <Alert icon={<IconAlertCircle size={16} />} color="orange" mt="xl" variant="light">
            <Text size="xs">
              <strong>Security Notice:</strong><br />
              This admin key grants access to the Conduit WebUI. It controls access to virtual key management, 
              provider configuration, and system monitoring.
            </Text>
          </Alert>

          <Text c="dimmed" size="xs" ta="center" mt="md">
            Conduit AI Gateway v2.0 - Secure access required
          </Text>
        </Paper>
      </Container>
    </div>
  );
}