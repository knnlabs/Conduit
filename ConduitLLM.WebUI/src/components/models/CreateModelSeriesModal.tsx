'use client';

import { useState, useEffect } from 'react';
import { Modal, TextInput, Select, Switch, Button, Stack, Group, Textarea, Alert, Text } from '@mantine/core';
import { CodeHighlight } from '@mantine/code-highlight';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { IconAlertCircle } from '@tabler/icons-react';
import { useAdminClient } from '@/lib/client/adminClient';
import type { CreateModelSeriesDto, ModelAuthorDto } from '@knn_labs/conduit-admin-client';


interface CreateModelSeriesModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

const DEFAULT_PARAMETERS = JSON.stringify({
  temperature: {
    min: 0,
    max: 2,
    default: 1,
    step: 0.1
  },
  maxTokens: {
    min: 1,
    max: 4096,
    default: 2048
  }
}, null, 2);

export function CreateModelSeriesModal({ isOpen, onClose, onSuccess }: CreateModelSeriesModalProps) {
  const [loading, setLoading] = useState(false);
  const [authors, setAuthors] = useState<ModelAuthorDto[]>([]);
  const [jsonError, setJsonError] = useState<string | null>(null);
  const [showJsonPreview, setShowJsonPreview] = useState(false);
  const { executeWithAdmin } = useAdminClient();

  const form = useForm<CreateModelSeriesDto & { parameters?: string }>({
    initialValues: {
      name: '',
      // displayName field doesn't exist in CreateModelSeriesDto
      authorId: 0,
      description: '',
      parameters: DEFAULT_PARAMETERS,
      // isActive field doesn't exist in CreateModelSeriesDto
    },
    validate: {
      name: (value) => !value ? 'Name is required' : null,
      authorId: (value) => !value || value === 0 ? 'Author is required' : null,
      parameters: (value) => {
        if (value) {
          try {
            JSON.parse(value);
            setJsonError(null);
            return null;
          } catch {
            const error = 'Invalid JSON format';
            setJsonError(error);
            return error;
          }
        }
        return null;
      }
    }
  });

  useEffect(() => {
    if (isOpen) {
      void loadAuthors();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen]);

  const loadAuthors = async () => {
    try {
      const data = await executeWithAdmin(client => client.modelAuthors.list());
      setAuthors(data);
    } catch (error) {
      console.error('Failed to load authors:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to load authors',
        color: 'red',
      });
    }
  };

  const handleSubmit = async (values: CreateModelSeriesDto & { parameters?: string; displayName?: string; isActive?: boolean }) => {
    try {
      setLoading(true);
      const dto: CreateModelSeriesDto = {
        name: values.name,
        authorId: values.authorId,
        description: values.description,
        parameters: values.parameters ?? null
      };
      
      await executeWithAdmin(client => client.modelSeries.create(dto));
      notifications.show({
        title: 'Success',
        message: 'Model series created successfully',
        color: 'green',
      });
      form.reset();
      onSuccess();
    } catch (error) {
      console.error('Failed to create model series:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to create model series',
        color: 'red',
      });
    } finally {
      setLoading(false);
    }
  };

  const authorOptions = authors
    .filter(a => a.id !== undefined)
    .map(a => ({
      value: String(a.id),
      label: a.name ?? 'Unknown Author'
    }));

  const validateJson = (value: string) => {
    try {
      if (value) {
        JSON.parse(value);
        setJsonError(null);
      }
    } catch {
      setJsonError('Invalid JSON format');
    }
  };

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title="Create New Model Series"
      size="lg"
    >
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack>
          <TextInput
            label="Series Name"
            placeholder="e.g., GPT-4"
            required
            {...form.getInputProps('name')}
          />

          <TextInput
            label="Display Name"
            placeholder="e.g., GPT-4 Series"
            {...form.getInputProps('displayName')}
          />

          <Select
            label="Author"
            required
            data={authorOptions}
            placeholder="Select an author"
            value={form.values.authorId?.toString()}
            onChange={(value) => form.setFieldValue('authorId', value ? parseInt(value) : 0)}
          />

          <Textarea
            label="Description"
            placeholder="Description of the model series..."
            rows={3}
            {...form.getInputProps('description')}
          />

          <Stack gap="xs">
            <Group justify="space-between">
              <Text size="sm" fw={500}>
                Parameters (JSON)
              </Text>
              <Button
                size="xs"
                variant="subtle"
                onClick={() => setShowJsonPreview(!showJsonPreview)}
              >
                {showJsonPreview ? 'Hide' : 'Show'} Preview
              </Button>
            </Group>
            
            <Textarea
              placeholder="JSON parameters for UI generation..."
              rows={8}
              style={{ fontFamily: 'monospace' }}
              {...form.getInputProps('parameters')}
              onChange={(e) => {
                form.setFieldValue('parameters', e.currentTarget.value);
                validateJson(e.currentTarget.value);
              }}
              error={jsonError}
            />

            {showJsonPreview && form.values.parameters && !jsonError && (
              <CodeHighlight
                code={form.values.parameters}
                language="json"
                withCopyButton={false}
              />
            )}

            {jsonError && (
              <Alert icon={<IconAlertCircle size={16} />} color="red" variant="light">
                {jsonError}
              </Alert>
            )}
          </Stack>

          <Switch
            label="Active"
            {...form.getInputProps('isActive', { type: 'checkbox' })}
          />

          <Group justify="flex-end">
            <Button variant="subtle" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={loading} disabled={!!jsonError}>
              Create Series
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}