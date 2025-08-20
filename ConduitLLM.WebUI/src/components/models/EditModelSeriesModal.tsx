'use client';

import { useState, useEffect } from 'react';
import { Modal, TextInput, Button, Stack, Group, Textarea, Alert, Text } from '@mantine/core';
import { CodeHighlight } from '@mantine/code-highlight';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { IconAlertCircle } from '@tabler/icons-react';
import { useAdminClient } from '@/lib/client/adminClient';
import type { ModelSeriesDto, UpdateModelSeriesDto } from '@knn_labs/conduit-admin-client';


interface EditModelSeriesModalProps {
  isOpen: boolean;
  series: ModelSeriesDto;
  onClose: () => void;
  onSuccess: () => void;
}

export function EditModelSeriesModal({ isOpen, series, onClose, onSuccess }: EditModelSeriesModalProps) {
  const [loading, setLoading] = useState(false);
  const [jsonError, setJsonError] = useState<string | null>(null);
  const [showJsonPreview, setShowJsonPreview] = useState(false);
  const { executeWithAdmin } = useAdminClient();

  const form = useForm<UpdateModelSeriesDto & { parameters?: string }>({
    initialValues: {
      name: series?.name ?? '',
      description: series?.description ?? '',
      parameters: series?.parameters ?? ''
    },
    validate: {
      name: (value) => !value ? 'Name is required' : null,
      // authorId validation removed - field might not be in form
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
    if (series) {
      form.setValues({
        name: series.name ?? '',
        description: series.description ?? '',
        parameters: series.parameters ?? ''
      });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [series]);


  const handleSubmit = async (values: typeof form.values) => {
    try {
      setLoading(true);
      const dto: UpdateModelSeriesDto = {
        name: values.name,
        description: values.description,
        parameters: values.parameters ?? null
      };
      
      if (!series.id) throw new Error('Series ID is required');
      await executeWithAdmin(client => client.modelSeries.update(series.id as number, dto));
      notifications.show({
        title: 'Success',
        message: 'Model series updated successfully',
        color: 'green',
      });
      onSuccess();
    } catch (error) {
      console.error('Failed to update model series:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to update model series',
        color: 'red',
      });
    } finally {
      setLoading(false);
    }
  };

  // Removed authorOptions as it's no longer used

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
      title="Edit Model Series"
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


          <Group justify="flex-end">
            <Button variant="subtle" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={loading} disabled={!!jsonError}>
              Update Series
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}