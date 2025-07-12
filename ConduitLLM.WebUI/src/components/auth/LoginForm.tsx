'use client';

import { useState } from 'react';
import {
  Paper,
  PasswordInput,
  Button,
  Title,
  Text,
  Container,
  Stack,
  Checkbox,
  Alert,
  Group,
  ThemeIcon,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconKey, IconAlertCircle } from '@tabler/icons-react';
import { useAuthStore } from '@/stores/useAuthStore';
import { LoginCredentials } from '@/types/auth';
import { validateMasterKeyFormat } from '@/lib/auth/client-validation';

interface LoginFormProps {
  onSuccess?: () => void;
}

export function LoginForm({ onSuccess }: LoginFormProps) {
  const { login, isLoading, error, clearError } = useAuthStore();
  const [isSubmitting, setIsSubmitting] = useState(false);

  const form = useForm<LoginCredentials>({
    initialValues: {
      masterKey: '',
      rememberMe: false,
    },
    validate: {
      masterKey: (value) => validateMasterKeyFormat(value),
    },
  });


  const handleSubmit = async (values: LoginCredentials) => {
    setIsSubmitting(true);
    clearError();

    try {
      const success = await login(values.masterKey, values.rememberMe);
      
      if (success) {
        onSuccess?.();
      }
    } catch (error: unknown) {
      console.error('Login submission error:', error);
    } finally {
      setIsSubmitting(false);
    }
  };

  const loading = isLoading || isSubmitting;

  return (
    <Container size={420} my={40}>
      <Stack gap="xl">
        <Paper p="xl" shadow="md" radius="md" withBorder>
          <Stack gap="lg">
            <Group justify="center">
              <ThemeIcon size="xl" variant="light" color="blue">
                <IconKey size={24} />
              </ThemeIcon>
            </Group>

            <div style={{ textAlign: 'center' }}>
              <Title order={2} mb="xs">
                Welcome to Conduit
              </Title>
              <Text size="sm" c="dimmed">
                Enter your master key to access the admin interface
              </Text>
            </div>

            {error && (
              <Alert 
                icon={<IconAlertCircle size={16} />} 
                color="red" 
                variant="light"
                onClose={clearError}
                withCloseButton
              >
                {error}
              </Alert>
            )}

            <form onSubmit={form.onSubmit(handleSubmit)}>
              <Stack gap="md">
                <Stack gap="xs">
                  <PasswordInput
                    label="Master Key"
                    placeholder="Enter your master key"
                    required
                    disabled={loading}
                    {...form.getInputProps('masterKey')}
                  />
                </Stack>

                <Checkbox
                  label="Remember me for 30 days"
                  disabled={loading}
                  {...form.getInputProps('rememberMe', { type: 'checkbox' })}
                />

                <Button
                  type="submit"
                  fullWidth
                  loading={loading}
                  leftSection={<IconKey size={16} />}
                >
                  {loading ? 'Signing in...' : 'Sign In'}
                </Button>
              </Stack>
            </form>
          </Stack>
        </Paper>

        <Text size="xs" c="dimmed" ta="center">
          Conduit WebUI - Secure access to your LLM infrastructure
        </Text>
      </Stack>
    </Container>
  );
}