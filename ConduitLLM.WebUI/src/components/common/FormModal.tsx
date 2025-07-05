import { Modal, Stack, Group, Button } from '@mantine/core';
import { UseFormReturnType } from '@mantine/form';
import { UseMutationResult } from '@tanstack/react-query';
import { notifications } from '@mantine/notifications';
import { useEffect } from 'react';

export interface FormModalProps<TForm, TData = unknown, TError = unknown, TVariables = TForm> {
  // Modal props
  opened: boolean;
  onClose: () => void;
  title: string;
  size?: string;
  
  // Form props
  form: UseFormReturnType<TForm>;
  initialValues?: TForm;
  
  // Mutation props
  mutation: UseMutationResult<TData, TError, TVariables>;
  
  // Content
  children: (form: UseFormReturnType<TForm>) => React.ReactNode;
  
  // Configuration
  entityType: string;
  isEdit?: boolean;
  submitText?: string;
  cancelText?: string;
  
  // Callbacks
  onSuccess?: (data: unknown) => void;
  onError?: (error: Error) => void;
}

export function FormModal<TForm, TData = unknown, TError = unknown, TVariables = TForm>({
  opened,
  onClose,
  title,
  size = "lg",
  form,
  initialValues,
  mutation,
  children,
  entityType,
  isEdit = false,
  submitText,
  cancelText = "Cancel",
  onSuccess,
  onError,
}: FormModalProps<TForm, TData, TError, TVariables>) {
  
  // Handle form reset and population
  useEffect(() => {
    if (opened && initialValues) {
      form.setValues(initialValues);
    } else if (opened) {
      form.reset();
    }
  }, [opened, initialValues, form]);

  // Handle form submission
  const handleSubmit = (values: TForm) => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    mutation.mutate(values as any, {
      onSuccess: (data) => {
        notifications.show({
          title: 'Success',
          message: `${entityType} ${isEdit ? 'updated' : 'created'} successfully`,
          color: 'green',
        });
        onClose();
        form.reset();
        onSuccess?.(data);
      },
      onError: (error) => {
        notifications.show({
          title: 'Error',
          message: `Failed to ${isEdit ? 'update' : 'create'} ${entityType}`,
          color: 'red',
        });
        onError?.(error as Error);
      },
    });
  };

  const defaultSubmitText = submitText || (isEdit ? 'Update' : 'Create');

  return (
    <Modal opened={opened} onClose={onClose} title={title} size={size}>
      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack gap="md">
          {children(form)}
          <Group justify="flex-end" mt="md">
            <Button variant="subtle" onClick={onClose}>
              {cancelText}
            </Button>
            <Button 
              type="submit" 
              loading={mutation.isPending}
              disabled={!form.isValid()}
            >
              {defaultSubmitText}
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}