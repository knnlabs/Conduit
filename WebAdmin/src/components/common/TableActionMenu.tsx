'use client';

import {
  Menu,
  ActionIcon,
  Text,
  rem,
} from '@mantine/core';
import {
  IconDots,
  IconEdit,
  IconTrash,
} from '@tabler/icons-react';
import { modals } from '@mantine/modals';
import type { ActionDef, DeleteConfirmation } from './BaseTable';

interface TableActionMenuProps<T> {
  item: T;
  actions: ActionDef<T>[];
  deleteConfirmation?: DeleteConfirmation<T>;
}

export function TableActionMenu<T extends Record<string, unknown>>({
  item,
  actions,
  deleteConfirmation,
}: TableActionMenuProps<T>) {
  
  const handleDelete = (item: T, deleteAction: ActionDef<T>) => {
    if (!deleteConfirmation) {
      deleteAction.onClick(item);
      return;
    }

    modals.openConfirmModal({
      title: deleteConfirmation.title,
      children: (
        <Text size="sm">
          {deleteConfirmation.message(item)}
        </Text>
      ),
      labels: { confirm: 'Delete', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => deleteAction.onClick(item),
    });
  };

  const handleEdit = (item: T, editAction: ActionDef<T>) => {
    editAction.onClick(item);
  };

  const handleCustomAction = (item: T, action: ActionDef<T>) => {
    if (action.disabled?.(item)) {
      return;
    }
    action.onClick(item);
  };

  // Filter and categorize actions
  const editActions = actions.filter(action => action.label === 'Edit');
  const deleteActions = actions.filter(action => action.label === 'Delete');
  const customActions = actions.filter(action => 
    action.label !== 'Edit' && action.label !== 'Delete'
  );

  // Don't render menu if no actions
  if (actions.length === 0) {
    return null;
  }

  return (
    <Menu shadow="md" width={200}>
      <Menu.Target>
        <ActionIcon variant="subtle" color="gray">
          <IconDots size={16} />
        </ActionIcon>
      </Menu.Target>

      <Menu.Dropdown>
        {/* Edit actions */}
        {editActions.map((action) => (
          <Menu.Item
            key={`edit-${action.label}-${action.onClick.toString().slice(0, 20)}`}
            leftSection={
              <IconEdit style={{ width: rem(14), height: rem(14) }} />
            }
            onClick={() => handleEdit(item, action)}
            disabled={action.disabled?.(item)}
          >
            {action.label}
          </Menu.Item>
        ))}

        {/* Custom actions */}
        {customActions.map((action) => (
          <Menu.Item
            key={`custom-${action.label}-${action.onClick.toString().slice(0, 20)}`}
            leftSection={
              action.icon ? (
                <action.icon size={14} />
              ) : undefined
            }
            onClick={() => handleCustomAction(item, action)}
            disabled={action.disabled?.(item)}
            color={action.color}
          >
            {action.label}
          </Menu.Item>
        ))}

        {/* Divider before delete if there are other actions */}
        {(editActions.length > 0 || customActions.length > 0) && deleteActions.length > 0 && (
          <Menu.Divider />
        )}

        {/* Delete actions */}
        {deleteActions.map((action) => (
          <Menu.Item
            key={`delete-${action.label}-${action.onClick.toString().slice(0, 20)}`}
            color="red"
            leftSection={
              <IconTrash style={{ width: rem(14), height: rem(14) }} />
            }
            onClick={() => handleDelete(item, action)}
            disabled={action.disabled?.(item)}
          >
            {action.label}
          </Menu.Item>
        ))}
      </Menu.Dropdown>
    </Menu>
  );
}