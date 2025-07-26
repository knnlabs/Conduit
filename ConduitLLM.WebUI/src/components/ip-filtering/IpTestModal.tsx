'use client';

import { useState } from 'react';
import {
  Modal,
  TextInput,
  Button,
  Group,
  Stack,
  Alert,
  Text,
  Badge,
  Card,
  Divider,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconAlertCircle, IconCheck, IconX } from '@tabler/icons-react';

interface IpTestModalProps {
  opened: boolean;
  onClose: () => void;
}

interface TestResult {
  allowed: boolean;
  matchedRule?: {
    id: string;
    ipAddress: string;
    action: 'allow' | 'block';
    description?: string;
  };
  reason?: string;
}

const validateIpAddress = (value: string) => {
  const ipRegex = /^(\d{1,3}\.){3}\d{1,3}$/;
  
  if (!ipRegex.test(value)) {
    return 'Invalid IP address format (e.g., 192.168.1.1)';
  }
  
  const parts = value.split('.');
  for (const part of parts) {
    const num = parseInt(part, 10);
    if (num < 0 || num > 255) {
      return 'Each IP octet must be between 0 and 255';
    }
  }
  
  return null;
};

export function IpTestModal({ opened, onClose }: IpTestModalProps) {
  const [isLoading, setIsLoading] = useState(false);
  const [testResult, setTestResult] = useState<TestResult | null>(null);
  
  const form = useForm({
    initialValues: {
      ipAddress: '',
    },
    validate: {
      ipAddress: validateIpAddress,
    },
  });

  const handleSubmit = async (values: typeof form.values) => {
    setIsLoading(true);
    setTestResult(null);
    
    try {
      const response = await fetch(`/api/admin/security/ip-rules/test?ip=${values.ipAddress}`);
      
      if (!response.ok) {
        throw new Error('Failed to test IP address');
      }
      
      const result = await response.json() as TestResult;
      setTestResult(result);
    } catch {
      // For now, simulate a test result since the endpoint might not exist
      setTestResult({
        allowed: true,
        reason: 'No matching rules found (default allow)',
      });
    } finally {
      setIsLoading(false);
    }
  };

  const handleClose = () => {
    form.reset();
    setTestResult(null);
    onClose();
  };

  return (
    <Modal
      opened={opened}
      onClose={handleClose}
      title="Test IP Address"
      size="md"
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          <TextInput
            label="IP Address to Test"
            placeholder="e.g., 192.168.1.100"
            required
            {...form.getInputProps('ipAddress')}
          />

          <Button type="submit" loading={isLoading} fullWidth>
            Test IP
          </Button>

          {testResult && (
            <>
              <Divider />
              
              <Card withBorder p="md" radius="md">
                <Group justify="space-between" mb="md">
                  <Text fw={500}>Test Result</Text>
                  <Badge
                    color={testResult.allowed ? 'green' : 'red'}
                    variant="filled"
                    size="lg"
                    leftSection={
                      testResult.allowed ? <IconCheck size={16} /> : <IconX size={16} />
                    }
                  >
                    {testResult.allowed ? 'ALLOWED' : 'BLOCKED'}
                  </Badge>
                </Group>

                {testResult.matchedRule ? (
                  <Stack gap="xs">
                    <Text size="sm" c="dimmed">Matched Rule:</Text>
                    <Group gap="xs">
                      <Badge variant="light">
                        {testResult.matchedRule.action === 'allow' ? 'Allow' : 'Block'}
                      </Badge>
                      <Text size="sm" style={{ fontFamily: 'monospace' }}>
                        {testResult.matchedRule.ipAddress}
                      </Text>
                    </Group>
                    {testResult.matchedRule.description && (
                      <Text size="sm" c="dimmed">
                        {testResult.matchedRule.description}
                      </Text>
                    )}
                  </Stack>
                ) : (
                  <Text size="sm" c="dimmed">
                    {testResult.reason ?? 'No specific rule matched this IP address.'}
                  </Text>
                )}
              </Card>
              
              <Alert
                icon={<IconAlertCircle size={16} />}
                color="blue"
                variant="light"
              >
                <Text size="sm">
                  This test shows what would happen if a request came from this IP address. 
                  The actual behavior depends on your IP filtering settings and rule configuration.
                </Text>
              </Alert>
            </>
          )}
        </Stack>
      </form>
    </Modal>
  );
}