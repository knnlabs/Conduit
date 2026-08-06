import { useCallback, useEffect, useMemo, useState } from 'react';

type SelectionKey = string | number;

interface UseBulkSelectionOptions<T, K extends SelectionKey> {
  items: T[];
  getKey: (item: T) => K;
  isSelectable?: (item: T) => boolean;
}

export function useBulkSelection<T, K extends SelectionKey>({
  items,
  getKey,
  isSelectable,
}: UseBulkSelectionOptions<T, K>) {
  const [selectedKeys, setSelectedKeys] = useState<Set<K>>(new Set());
  const selectableItems = useMemo(
    () => (isSelectable ? items.filter(isSelectable) : items),
    [isSelectable, items],
  );
  const selectableKeys = useMemo(
    () => selectableItems.map(getKey),
    [getKey, selectableItems],
  );
  const selectableKeySet = useMemo(
    () => new Set(selectableKeys),
    [selectableKeys],
  );

  useEffect(() => {
    setSelectedKeys((previous) => {
      const next = new Set(
        Array.from(previous).filter((key) => selectableKeySet.has(key)),
      );
      return next.size === previous.size ? previous : next;
    });
  }, [selectableKeySet]);

  const selectedCount = useMemo(
    () => selectableKeys.filter((key) => selectedKeys.has(key)).length,
    [selectableKeys, selectedKeys],
  );
  const isAllSelected =
    selectableKeys.length > 0 && selectedCount === selectableKeys.length;
  const isIndeterminate = selectedCount > 0 && !isAllSelected;

  const toggleOne = useCallback((key: K) => {
    setSelectedKeys((previous) => {
      const next = new Set(previous);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }, []);

  const toggleAll = useCallback(() => {
    setSelectedKeys(isAllSelected ? new Set() : new Set(selectableKeys));
  }, [isAllSelected, selectableKeys]);

  const clearSelection = useCallback(() => setSelectedKeys(new Set()), []);
  const deselect = useCallback((key: K) => {
    setSelectedKeys((previous) => {
      if (!previous.has(key)) return previous;
      const next = new Set(previous);
      next.delete(key);
      return next;
    });
  }, []);
  const isSelected = useCallback(
    (key: K) => selectedKeys.has(key),
    [selectedKeys],
  );
  const getSelectedItems = useCallback(
    () => selectableItems.filter((item) => selectedKeys.has(getKey(item))),
    [getKey, selectableItems, selectedKeys],
  );

  return {
    selectedKeys,
    selectedCount,
    isAllSelected,
    isIndeterminate,
    toggleOne,
    toggleAll,
    clearSelection,
    deselect,
    isSelected,
    getSelectedItems,
  };
}
