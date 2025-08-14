'use client';

import { Modal, ModalProps, Stack, Text, Group, Button } from '@mantine/core';
import { ReactNode } from 'react';

export interface BaseModalProps extends Omit<ModalProps, 'children'> {
  /**
   * Modal content
   */
  children: ReactNode;
  
  /**
   * Whether to show action buttons (save/cancel)
   */
  showActions?: boolean;
  
  /**
   * Save button text
   */
  saveText?: string;
  
  /**
   * Cancel button text
   */
  cancelText?: string;
  
  /**
   * Whether save button is loading
   */
  saveLoading?: boolean;
  
  /**
   * Whether save button is disabled
   */
  saveDisabled?: boolean;
  
  /**
   * Save button handler
   */
  onSave?: () => void;
  
  /**
   * Cancel button handler
   */
  onCancel?: () => void;
  
  /**
   * Save button color
   */
  saveColor?: string;
  
  /**
   * Cancel button variant
   */
  cancelVariant?: 'light' | 'outline' | 'subtle';
}

/**
 * Base modal component with consistent styling and optional action buttons
 */
export function BaseModal({
  children,
  showActions = false,
  saveText = 'Save',
  cancelText = 'Cancel',
  saveLoading = false,
  saveDisabled = false,
  onSave,
  onCancel,
  onClose,
  saveColor = 'blue',
  cancelVariant = 'light',
  ...modalProps
}: BaseModalProps) {
  const handleCancel = () => {
    if (onCancel) {
      onCancel();
    } else {
      onClose();
    }
  };

  return (
    <Modal onClose={onClose} {...modalProps}>
      <Stack gap="md">
        {/* Modal Content */}
        {children}
        
        {/* Action Buttons */}
        {showActions && (
          <Group justify="flex-end" mt="md">
            <Button
              variant={cancelVariant}
              onClick={handleCancel}
              disabled={saveLoading}
            >
              {cancelText}
            </Button>
            <Button
              color={saveColor}
              onClick={onSave}
              loading={saveLoading}
              disabled={saveDisabled}
            >
              {saveText}
            </Button>
          </Group>
        )}
      </Stack>
    </Modal>
  );
}

/**
 * Confirmation modal variant with predefined styling
 */
export interface ConfirmationModalProps extends Omit<BaseModalProps, 'showActions' | 'saveColor'> {
  /**
   * Confirmation message
   */
  message: string;
  
  /**
   * Additional details or warning text
   */
  details?: string;
  
  /**
   * Confirmation action type
   */
  type?: 'danger' | 'warning' | 'info';
}

export function ConfirmationModal({
  message,
  details,
  type = 'info',
  saveText = 'Confirm',
  cancelText = 'Cancel',
  children,
  ...props
}: ConfirmationModalProps) {
  const getColor = () => {
    switch (type) {
      case 'danger':
        return 'red';
      case 'warning':
        return 'orange';
      default:
        return 'blue';
    }
  };

  return (
    <BaseModal
      {...props}
      showActions={true}
      saveText={saveText}
      cancelText={cancelText}
      saveColor={getColor()}
    >
      <Stack gap="sm">
        <Text>{message}</Text>
        {details && (
          <Text size="sm" c="dimmed">
            {details}
          </Text>
        )}
        {children}
      </Stack>
    </BaseModal>
  );
}