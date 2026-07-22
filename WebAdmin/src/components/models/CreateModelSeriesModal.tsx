'use client';

import { useState, useEffect, useCallback } from 'react';
import { TextInput, Select, Switch, Textarea } from '@mantine/core';
import { CodeHighlight } from '@mantine/code-highlight';
import { useForm } from '@mantine/form';
import { withAdminClient } from '@/lib/client/adminClient';
import { useFormModal } from '@/hooks/useFormModal';
import { EntityFormModal } from '@/components/common/EntityFormModal';
import { JsonEditorField } from '@/components/common/JsonEditorField';
import { notify } from '@/lib/notifications';
import type { CreateModelSeriesDto, ModelAuthorDto } from '@/lib/admin-api';


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
  const [authors, setAuthors] = useState<ModelAuthorDto[]>([]);
  const [jsonValid, setJsonValid] = useState(true);

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
            return null;
          } catch {
            return 'Invalid JSON format';
          }
        }
        return null;
      }
    }
  });

  const submitAction = useCallback(
    async (values: CreateModelSeriesDto & { parameters?: string; displayName?: string; isActive?: boolean }) => {
      const dto: CreateModelSeriesDto = {
        name: values.name,
        authorId: values.authorId,
        description: values.description,
        parameters: values.parameters ?? null
      };
      await withAdminClient(client => client.modelSeries.create(dto));
    },
    []
  );

  const { loading, handleSubmit, handleClose: baseHandleClose } = useFormModal({
    form,
    onClose,
    onSuccess,
    submitAction,
    successMessage: 'Model series created successfully',
  });

  const handleClose = useCallback(() => {
    setJsonValid(true);
    baseHandleClose();
  }, [baseHandleClose]);

  useEffect(() => {
    if (isOpen) {
      void loadAuthors();
    }
  }, [isOpen]);

  const loadAuthors = async () => {
    try {
      const data = await withAdminClient(client => client.modelAuthors.list());
      setAuthors(data);
    } catch (error) {
      notify.error(error, 'Failed to load authors');
    }
  };

  const authorOptions = authors
    .filter(a => a.id !== undefined)
    .map(a => ({
      value: String(a.id),
      label: a.name ?? 'Unknown Author'
    }));

  return (
    <EntityFormModal
      opened={isOpen}
      onClose={handleClose}
      title="Create New Model Series"
      onSubmit={form.onSubmit(handleSubmit)}
      loading={loading}
      submitLabel="Create Series"
      submitDisabled={!jsonValid}
    >
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

      <JsonEditorField
        label="Parameters (JSON)"
        value={form.values.parameters ?? ''}
        onChange={(v) => form.setFieldValue('parameters', v)}
        onValidityChange={setJsonValid}
        placeholder="JSON parameters for UI generation..."
        collapsiblePreview
        renderPreview={(v) => (
          <CodeHighlight code={v} language="json" withCopyButton={false} />
        )}
      />

      <Switch
        label="Active"
        {...form.getInputProps('isActive', { type: 'checkbox' })}
      />
    </EntityFormModal>
  );
}
