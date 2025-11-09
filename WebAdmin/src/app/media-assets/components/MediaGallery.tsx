'use client';

import { SimpleGrid, Center, Text, Loader } from '@mantine/core';
import { MediaRecord } from '../types';
import MediaCard from './MediaCard';

interface MediaGalleryProps {
  media: MediaRecord[];
  loading: boolean;
  selectedIds: Set<string>;
  onSelectMedia: (id: string) => void;
  onViewMedia: (media: MediaRecord) => void;
  onDeleteMedia: (id: string) => void;
}

export default function MediaGallery({
  media,
  loading,
  selectedIds,
  onSelectMedia,
  onViewMedia,
  onDeleteMedia,
}: MediaGalleryProps) {
  if (loading) {
    return (
      <Center h={400}>
        <Loader size="lg" />
      </Center>
    );
  }

  if (media.length === 0) {
    return (
      <Center h={400}>
        <Text c="dimmed">No media found matching your filters</Text>
      </Center>
    );
  }

  return (
    <SimpleGrid cols={{ base: 1, sm: 2, md: 3, lg: 4 }} spacing="lg">
      {media.map((item) => (
        <MediaCard
          key={item.id}
          media={item}
          selected={selectedIds.has(item.id)}
          onSelect={onSelectMedia}
          onView={onViewMedia}
          onDelete={onDeleteMedia}
        />
      ))}
    </SimpleGrid>
  );
}