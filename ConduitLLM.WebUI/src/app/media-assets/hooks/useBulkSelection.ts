import { useState, useCallback } from 'react';
import { MediaRecord } from '../types';

export function useBulkSelection(media: MediaRecord[]) {
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  const toggleSelection = useCallback((id: string) => {
    setSelectedIds(prev => {
      const newSet = new Set(prev);
      if (newSet.has(id)) {
        newSet.delete(id);
      } else {
        newSet.add(id);
      }
      return newSet;
    });
  }, []);

  const selectAll = useCallback(() => {
    setSelectedIds(new Set(media.map(m => m.id)));
  }, [media]);

  const deselectAll = useCallback(() => {
    setSelectedIds(new Set());
  }, []);

  const isSelected = useCallback((id: string) => {
    return selectedIds.has(id);
  }, [selectedIds]);

  const getSelectedMedia = useCallback(() => {
    return media.filter(m => selectedIds.has(m.id));
  }, [media, selectedIds]);

  return {
    selectedIds,
    selectedCount: selectedIds.size,
    toggleSelection,
    selectAll,
    deselectAll,
    isSelected,
    getSelectedMedia,
  };
}