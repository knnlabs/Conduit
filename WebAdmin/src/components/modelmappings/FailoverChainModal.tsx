'use client';

import { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Badge,
  Button,
  Group,
  Modal,
  Paper,
  Stack,
  Text,
} from '@mantine/core';
import { DragDropContext, Draggable, Droppable, type DropResult } from '@hello-pangea/dnd';
import { IconAlertTriangle, IconGripVertical } from '@tabler/icons-react';
import type { ModelProviderMappingDto } from '@knn_labs/conduit-admin-client';
import { useReorderFailoverChain } from '@/hooks/useModelMappingsApi';

interface FailoverChainModalProps {
  opened: boolean;
  onClose: () => void;
  modelAlias: string;
  mappings: ModelProviderMappingDto[];
}

/**
 * Per-alias failover chain editor: lists every provider mapped to the alias in priority
 * order with drag-to-reorder. Saving writes priority = list index. Requests are routed to
 * the top provider; provider-level failover walks the rest in order.
 */
export function FailoverChainModal({ opened, onClose, modelAlias, mappings }: FailoverChainModalProps) {
  const chainMappings = useMemo(
    () =>
      mappings
        .filter(m => m.modelAlias === modelAlias)
        .sort((a, b) => a.priority - b.priority || a.id - b.id),
    [mappings, modelAlias]
  );

  const [ordered, setOrdered] = useState<ModelProviderMappingDto[]>(chainMappings);
  const reorderMutation = useReorderFailoverChain();

  useEffect(() => {
    setOrdered(chainMappings);
  }, [chainMappings]);

  const distinctModelIds = useMemo(() => {
    const ids = new Set(
      chainMappings.map(m => m.modelId).filter((id): id is number => id !== null && id !== undefined)
    );
    return ids.size;
  }, [chainMappings]);

  const isDirty = ordered.some((m, index) => m.priority !== index);

  const handleDragEnd = (result: DropResult) => {
    if (!result.destination || result.destination.index === result.source.index) return;
    const next = [...ordered];
    const [moved] = next.splice(result.source.index, 1);
    next.splice(result.destination.index, 0, moved);
    setOrdered(next);
  };

  const handleSave = () => {
    reorderMutation.mutate(ordered, { onSuccess: onClose });
  };

  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={`Failover chain — ${modelAlias}`}
      size="lg"
    >
      <Stack gap="md">
        <Text size="sm" c="dimmed">
          Requests for this alias go to the top provider. When provider-level failover is
          enabled, lower entries are tried in order if the one above fails. Drag to reorder.
        </Text>

        {distinctModelIds > 1 && (
          <Alert color="yellow" icon={<IconAlertTriangle size={16} />} title="Different underlying models">
            The providers in this chain resolve to different canonical models. During
            failover, requests will be silently served by a different model than the primary.
          </Alert>
        )}

        {ordered.length < 2 && (
          <Alert color="blue" variant="light">
            Only one provider is mapped to this alias. Add another mapping with the same
            alias and a different provider to build a failover chain.
          </Alert>
        )}

        <DragDropContext onDragEnd={handleDragEnd}>
          <Droppable droppableId="failover-chain">
            {(droppable) => (
              <Stack gap="xs" ref={droppable.innerRef} {...droppable.droppableProps}>
                {ordered.map((mapping, index) => (
                  <Draggable key={mapping.id} draggableId={String(mapping.id)} index={index}>
                    {(draggable, snapshot) => (
                      <Paper
                        ref={draggable.innerRef}
                        {...draggable.draggableProps}
                        withBorder
                        p="sm"
                        shadow={snapshot.isDragging ? 'md' : undefined}
                        style={draggable.draggableProps.style}
                      >
                        <Group gap="sm" wrap="nowrap">
                          <div {...draggable.dragHandleProps} style={{ display: 'flex', cursor: 'grab' }}>
                            <IconGripVertical size={18} />
                          </div>
                          <Badge variant={index === 0 ? 'filled' : 'light'} color={index === 0 ? 'blue' : 'gray'}>
                            {index === 0 ? 'Primary' : `Fallback ${index}`}
                          </Badge>
                          <Stack gap={0} style={{ flexGrow: 1, minWidth: 0 }}>
                            <Text size="sm" fw={500} truncate>
                              {mapping.provider?.displayName ?? `Provider #${mapping.providerId}`}
                            </Text>
                            <Text size="xs" c="dimmed" truncate>
                              {mapping.providerModelId}
                              {mapping.modelName ? ` · ${mapping.modelName}` : ''}
                            </Text>
                          </Stack>
                          {!mapping.isEnabled && (
                            <Badge color="red" variant="light">Disabled</Badge>
                          )}
                        </Group>
                      </Paper>
                    )}
                  </Draggable>
                ))}
                {droppable.placeholder}
              </Stack>
            )}
          </Droppable>
        </DragDropContext>

        <Group justify="flex-end" gap="sm">
          <Button variant="default" onClick={onClose}>
            Cancel
          </Button>
          <Button
            onClick={handleSave}
            loading={reorderMutation.isPending}
            disabled={!isDirty}
          >
            Save order
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
