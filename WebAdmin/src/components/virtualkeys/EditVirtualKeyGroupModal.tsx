'use client';

import {
  Alert,
  Divider,
  Group,
  Text,
  TextInput,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconAlertCircle, IconLayersLinked } from '@tabler/icons-react';
import { useEffect, useRef, useState } from 'react';

import type { VirtualKeyGroupDto } from '@/lib/admin-api';
import { withAdminClient } from '@/lib/client/adminClient';
import { useFormModal } from '@/hooks/useFormModal';
import { EntityFormModal } from '@/components/common/EntityFormModal';
import { RateLimitFields } from './RateLimitFields';

interface EditVirtualKeyGroupModalProps {
  opened: boolean;
  onClose: () => void;
  group: VirtualKeyGroupDto | null;
  onSuccess?: () => void;
}

interface EditVirtualKeyGroupForm {
  groupName: string;
  externalGroupId?: string;
  rateLimitRpm?: number;
  rateLimitRpd?: number;
  rateLimitTpm?: number;
  maxParallelRequests?: number;
}

const emptyForm: EditVirtualKeyGroupForm = {
  groupName: '',
  externalGroupId: '',
  rateLimitRpm: undefined,
  rateLimitRpd: undefined,
  rateLimitTpm: undefined,
  maxParallelRequests: undefined,
};

/**
 * Edits a group's name and the ceilings shared by every key inside it.
 */
export function EditVirtualKeyGroupModal({ opened, onClose, group, onSuccess }: EditVirtualKeyGroupModalProps) {
  const [initialValues, setInitialValues] = useState<EditVirtualKeyGroupForm>(emptyForm);
  const lastGroupId = useRef<number | undefined>(undefined);

  const form = useForm<EditVirtualKeyGroupForm>({
    initialValues,
    validate: {
      groupName: (value) => (!value?.trim() ? 'Group name is required' : null),
    },
  });

  const { loading, handleSubmit, handleClose } = useFormModal({
    form,
    onClose,
    onSuccess,
    submitAction: (values) => {
      if (!group) return Promise.resolve();

      return withAdminClient(client =>
        client.virtualKeyGroups.update(group.id, {
          groupName: values.groupName.trim(),
          externalGroupId: values.externalGroupId?.trim() ? values.externalGroupId.trim() : undefined,
          rateLimitRpm: values.rateLimitRpm ?? undefined,
          rateLimitRpd: values.rateLimitRpd ?? undefined,
          rateLimitTpm: values.rateLimitTpm ?? undefined,
          maxParallelRequests: values.maxParallelRequests ?? undefined,
        })
      );
    },
    successMessage: 'Virtual key group updated successfully',
  });

  useEffect(() => {
    if (!opened) {
      lastGroupId.current = undefined;
    }
  }, [opened]);

  useEffect(() => {
    if (!group || lastGroupId.current === group.id) return;
    lastGroupId.current = group.id;

    const next: EditVirtualKeyGroupForm = {
      groupName: group.groupName,
      externalGroupId: group.externalGroupId ?? '',
      rateLimitRpm: group.rateLimitRpm ?? undefined,
      rateLimitRpd: group.rateLimitRpd ?? undefined,
      rateLimitTpm: group.rateLimitTpm ?? undefined,
      maxParallelRequests: group.maxParallelRequests ?? undefined,
    };

    setInitialValues(next);
    form.setValues(next);
    form.resetDirty();
  }, [group, form]);

  if (!group) {
    return null;
  }

  return (
    <EntityFormModal
      opened={opened}
      onClose={handleClose}
      title={
        <Group gap="sm">
          <IconLayersLinked size={20} />
          <Text fw={500}>Edit Virtual Key Group</Text>
        </Group>
      }
      size="lg"
      onSubmit={form.onSubmit(handleSubmit)}
      loading={loading}
      submitLabel="Save Changes"
    >
      <TextInput
        label="Group Name"
        placeholder="Enter a name for this group"
        required
        {...form.getInputProps('groupName')}
      />

      <TextInput
        label="External Group ID"
        placeholder="Optional external identifier"
        {...form.getInputProps('externalGroupId')}
      />

      <Divider label="Rate limits" labelPosition="left" />

      <RateLimitFields
        scope="group"
        values={{
          rateLimitRpm: form.values.rateLimitRpm,
          rateLimitRpd: form.values.rateLimitRpd,
          rateLimitTpm: form.values.rateLimitTpm,
          maxParallelRequests: form.values.maxParallelRequests,
        }}
        onChange={(field, value) => form.setFieldValue(field, value)}
      />

      <Alert icon={<IconAlertCircle size={16} />} color="blue">
        <Text size="sm">
          Group limits apply on top of each key&apos;s own. A request must satisfy both, so the
          tighter of the two is what a caller actually experiences.
        </Text>
      </Alert>
    </EntityFormModal>
  );
}
