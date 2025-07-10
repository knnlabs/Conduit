'use client';

import {
  Stack,
  TextInput,
  Select,
  Textarea,
  Text,
  NumberInput,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { useCreateSecurityEvent } from '@/hooks/api/useSecurityApi';
import { FormModal } from '@/components/common/FormModal';
import type { CreateSecurityEventDto } from '@knn_labs/conduit-admin-client';

interface CreateSecurityEventModalProps {
  opened: boolean;
  onClose: () => void;
}

const eventTypes = [
  { value: 'authentication_failure', label: 'Authentication Failure' },
  { value: 'rate_limit_exceeded', label: 'Rate Limit Exceeded' },
  { value: 'suspicious_activity', label: 'Suspicious Activity' },
  { value: 'invalid_api_key', label: 'Invalid API Key' },
];

const severityLevels = [
  { value: 'low', label: 'Low', color: 'blue' },
  { value: 'medium', label: 'Medium', color: 'yellow' },
  { value: 'high', label: 'High', color: 'orange' },
  { value: 'critical', label: 'Critical', color: 'red' },
];

export function CreateSecurityEventModal({ opened, onClose }: CreateSecurityEventModalProps) {
  const createEvent = useCreateSecurityEvent();

  const form = useForm<CreateSecurityEventDto & { detailsText?: string }>({
    initialValues: {
      type: 'suspicious_activity',
      severity: 'medium',
      source: '',
      virtualKeyId: '',
      ipAddress: '',
      details: {},
      detailsText: '',
      statusCode: undefined,
    },
    validate: {
      source: (value) => (value ? null : 'Source is required'),
      type: (value) => (value ? null : 'Event type is required'),
      severity: (value) => (value ? null : 'Severity is required'),
    },
  });

  // Create mutation wrapper for payload transformation
  const mutationWrapper = {
    ...createEvent,
    mutate: (values: CreateSecurityEventDto & { detailsText?: string }, options?: any) => {
      // Parse details from the text field
      let parsedDetails: Record<string, unknown> = {};
      if (values.detailsText && values.detailsText.trim()) {
        try {
          parsedDetails = JSON.parse(values.detailsText);
        } catch {
          parsedDetails = { message: values.detailsText };
        }
      } else {
        parsedDetails = { message: 'Manual security event' };
      }

      const payload = {
        ...values,
        details: parsedDetails,
        virtualKeyId: values.virtualKeyId || undefined,
        ipAddress: values.ipAddress || undefined,
        statusCode: values.statusCode || undefined,
      };

      createEvent.mutate(payload, options);
    },
  };

  return (
    <FormModal
      opened={opened}
      onClose={onClose}
      title="Create Security Event"
      size="md"
      form={form}
      mutation={mutationWrapper}
      entityType="security event"
      submitText="Create Event"
    >
      {(form) => (
        <Stack>
          <Select
            label="Event Type"
            placeholder="Select event type"
            data={eventTypes}
            required
            {...form.getInputProps('type')}
          />

          <Select
            label="Severity"
            placeholder="Select severity level"
            data={severityLevels}
            required
            {...form.getInputProps('severity')}
          />

          <TextInput
            label="Source"
            placeholder="e.g., api.example.com"
            required
            description="The source or origin of the security event"
            {...form.getInputProps('source')}
          />

          <TextInput
            label="Virtual Key ID"
            placeholder="e.g., vk_123456"
            description="Optional: Associated virtual key ID"
            {...form.getInputProps('virtualKeyId')}
          />

          <TextInput
            label="IP Address"
            placeholder="e.g., 192.168.1.1"
            description="Optional: Associated IP address"
            {...form.getInputProps('ipAddress')}
          />

          <NumberInput
            label="HTTP Status Code"
            placeholder="e.g., 403"
            description="Optional: Associated HTTP status code"
            min={100}
            max={599}
            {...form.getInputProps('statusCode')}
          />

          <Textarea
            label="Additional Details"
            placeholder='{"reason": "Multiple failed login attempts", "attempts": 5}'
            description="Optional: JSON object with additional event details"
            minRows={3}
            {...form.getInputProps('detailsText')}
          />

          <Text size="xs" c="dimmed">
            Note: If the backend endpoint is not yet implemented, this operation may fail with a 404 or 501 error.
          </Text>
        </Stack>
      )}
    </FormModal>
  );
}