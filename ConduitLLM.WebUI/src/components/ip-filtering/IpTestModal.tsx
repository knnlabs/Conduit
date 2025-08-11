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
import { withAdminClient } from '@/lib/client/adminClient';

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
      const result = await withAdminClient(client =>
        client.ipFilters.checkIp(values.ipAddress)
      );

      // Convert IpCheckResult to TestResult format
      const testResult: TestResult = {
        allowed: result.isAllowed,
        reason: result.deniedReason ?? (result.isAllowed ? 'IP address is allowed' : 'IP address is blocked'),
      };

      // If there's a matched filter, add rule details
      if (result.matchedFilter && result.matchedFilterId) {
        try {
          const filter = await withAdminClient(client =>
            client.ipFilters.getById(result.matchedFilterId as number)
          );
          
          testResult.matchedRule = {
            id: filter.id.toString(),
            ipAddress: filter.ipAddressOrCidr,
            action: filter.filterType === 'whitelist' ? 'allow' : 'block',
            description: filter.description,
          };
        } catch {
          // If we can't get the filter details, just use the basic info
          testResult.matchedRule = {
            id: result.matchedFilterId.toString(),
            ipAddress: result.matchedFilter,
            action: result.filterType === 'whitelist' ? 'allow' : 'block',
          };
        }
      }

      setTestResult(testResult);
    } catch (error) {
      // If the check fails, show an error message
      const message = error instanceof Error ? error.message : 'Failed to test IP address';
      setTestResult({
        allowed: false,
        reason: `Error: ${message}`,
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