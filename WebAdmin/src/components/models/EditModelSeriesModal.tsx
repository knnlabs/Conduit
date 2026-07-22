'use client';

import { useState, useEffect, useCallback } from 'react';
import { TextInput, Textarea } from '@mantine/core';
import { useForm } from '@mantine/form';
import { withAdminClient } from '@/lib/client/adminClient';
import { useFormModal } from '@/hooks/useFormModal';
import { EntityFormModal } from '@/components/common/EntityFormModal';
import { JsonEditorField } from '@/components/common/JsonEditorField';
import { ParameterPreview } from '@/components/parameters/ParameterPreview';
import type { ModelSeriesDto, UpdateModelSeriesDto } from '@/lib/admin-api';


interface EditModelSeriesModalProps {
  isOpen: boolean;
  series: ModelSeriesDto;
  onClose: () => void;
  onSuccess: () => void;
}

export function EditModelSeriesModal({ isOpen, series, onClose, onSuccess }: EditModelSeriesModalProps) {
  const [jsonValid, setJsonValid] = useState(true);

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
            return null;
          } catch {
            return 'Invalid JSON format';
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


  const submitAction = useCallback(
    async (values: UpdateModelSeriesDto & { parameters?: string }) => {
      if (!series.id) throw new Error('Series ID is required');
      const dto: UpdateModelSeriesDto = {
        name: values.name,
        description: values.description,
        parameters: values.parameters ?? null
      };
      await withAdminClient(client => client.modelSeries.update(series.id as number, dto));
    },
    [series.id]
  );

  const { loading, handleSubmit, handleClose: baseHandleClose } = useFormModal({
    form,
    onClose,
    onSuccess,
    submitAction,
    successMessage: 'Model series updated successfully',
  });

  const handleClose = useCallback(() => {
    setJsonValid(true);
    baseHandleClose();
  }, [baseHandleClose]);

  return (
    <EntityFormModal
      opened={isOpen}
      onClose={handleClose}
      title="Edit Model Series"
      onSubmit={form.onSubmit(handleSubmit)}
      loading={loading}
      submitLabel="Update Series"
      submitDisabled={!jsonValid}
    >
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

      <JsonEditorField
        label="Parameters (JSON)"
        value={form.values.parameters ?? ''}
        onChange={(v) => form.setFieldValue('parameters', v)}
        onValidityChange={setJsonValid}
        placeholder="JSON parameters for UI generation..."
        previewPosition="above"
        renderPreview={(v) => (
          <ParameterPreview
            parametersJson={v}
            context="chat"
            label="Preview UI Components"
            maxHeight={300}
          />
        )}
      />
    </EntityFormModal>
  );
}
