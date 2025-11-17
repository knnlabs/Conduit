'use client';

import React, { useRef } from 'react';
import {
  SimpleGrid,
  Paper,
  Center,
  Stack,
  Title,
  Text,
  Group,
  Button,
  Modal,
  Box
} from '@mantine/core';
import { IconTrash } from '@tabler/icons-react';
import { useVirtualizer } from '@tanstack/react-virtual';

/**
 * Generic media gallery component that can handle any type of media items
 * @template T The type of media item to display
 */
export interface MediaGalleryProps<T> {
  items: T[];
  renderCard: (item: T, index: number) => React.ReactNode;
  onClearAll?: () => void;
  emptyTitle?: string;
  emptyMessage?: string;
  title?: string | ((count: number) => string);
  cols?: { base?: number; sm?: number; md?: number; lg?: number; xl?: number };
  spacing?: number | string;
  showClearButton?: boolean;
  clearButtonText?: string;
  // Modal props for preview functionality
  modalContent?: React.ReactNode;
  modalOpened?: boolean;
  onModalClose?: () => void;
  modalTitle?: string;
  modalSize?: string;
  // Virtualization props for performance with large lists
  enableVirtualization?: boolean;
  virtualizationThreshold?: number;
  estimatedItemHeight?: number;
}

export function MediaGallery<T>({
  items,
  renderCard,
  onClearAll,
  emptyTitle = 'No media generated yet',
  emptyMessage = 'Your generated media will appear here',
  title,
  cols = { base: 1, sm: 2, md: 3, lg: 4 },
  spacing = 'lg',
  showClearButton = true,
  clearButtonText = 'Clear All',
  modalContent,
  modalOpened = false,
  onModalClose,
  modalTitle = 'Media Details',
  modalSize = 'xl',
  enableVirtualization,
  virtualizationThreshold = 50,
  estimatedItemHeight = 350
}: MediaGalleryProps<T>) {
  const parentRef = useRef<HTMLDivElement>(null);

  // Determine if virtualization should be enabled
  const shouldVirtualize = enableVirtualization ?? items.length >= virtualizationThreshold;

  // Setup virtualizer for large lists
  const virtualizer = useVirtualizer({
    count: items.length,
    getScrollElement: () => parentRef.current,
    estimateSize: () => estimatedItemHeight,
    overscan: 5,
    enabled: shouldVirtualize
  });

  // Handle empty state
  if (items.length === 0) {
    return (
      <Paper p="xl" withBorder>
        <Center>
          <Stack align="center" gap="md">
            <Title order={3} c="dimmed">{emptyTitle}</Title>
            <Text c="dimmed">{emptyMessage}</Text>
          </Stack>
        </Center>
      </Paper>
    );
  }

  const titleText = typeof title === 'function' ? title(items.length) : title;

  return (
    <>
      <Stack gap="md">
        {/* Header with title and clear button */}
        {(titleText ?? (showClearButton && onClearAll)) && (
          <Group justify="space-between">
            {titleText && <Title order={3}>{titleText}</Title>}
            {showClearButton && onClearAll && (
              <Button
                onClick={onClearAll}
                variant="subtle"
                size="sm"
                leftSection={<IconTrash size={16} />}
              >
                {clearButtonText}
              </Button>
            )}
          </Group>
        )}

        {/* Grid of media cards - virtualized or standard */}
        {shouldVirtualize ? (
          <Box
            ref={parentRef}
            style={{
              height: '600px',
              overflow: 'auto',
              contain: 'strict'
            }}
          >
            <div
              style={{
                height: `${virtualizer.getTotalSize()}px`,
                width: '100%',
                position: 'relative'
              }}
            >
              {virtualizer.getVirtualItems().map((virtualItem) => (
                <div
                  key={virtualItem.key}
                  data-index={virtualItem.index}
                  style={{
                    position: 'absolute',
                    top: 0,
                    left: 0,
                    width: '100%',
                    height: `${virtualItem.size}px`,
                    transform: `translateY(${virtualItem.start}px)`
                  }}
                >
                  {renderCard(items[virtualItem.index], virtualItem.index)}
                </div>
              ))}
            </div>
          </Box>
        ) : (
          <SimpleGrid cols={cols} spacing={spacing}>
            {items.map((item, index) => renderCard(item, index))}
          </SimpleGrid>
        )}
      </Stack>

      {/* Optional modal for preview */}
      {onModalClose && (
        <Modal
          opened={modalOpened}
          onClose={onModalClose}
          size={modalSize}
          title={modalTitle}
          centered
        >
          {modalContent}
        </Modal>
      )}
    </>
  );
}

/**
 * Type-safe helper to create a MediaGallery for a specific type
 */
export function createMediaGallery<T>() {
  return function TypedMediaGallery(props: MediaGalleryProps<T>) {
    return <MediaGallery {...props} />;
  };
}