import { act, renderHook } from '@testing-library/react';
import { useBulkSelection } from '../useBulkSelection';

interface Item {
  id: number;
  selectable?: boolean;
}

const getKey = (item: Item) => item.id;
const isSelectable = (item: Item) => item.selectable !== false;

describe('useBulkSelection', () => {
  it('tracks membership and select-all state with a Set', () => {
    const items: Item[] = [{ id: 1 }, { id: 2 }, { id: 3 }];
    const { result } = renderHook(() =>
      useBulkSelection({ items, getKey }),
    );

    act(() => result.current.toggleOne(2));
    expect(result.current.selectedKeys.has(2)).toBe(true);
    expect(result.current.isIndeterminate).toBe(true);

    act(() => result.current.toggleAll());
    expect(result.current.selectedKeys).toEqual(new Set([1, 2, 3]));
    expect(result.current.isAllSelected).toBe(true);
  });

  it('ignores non-selectable rows and prunes keys when data changes', () => {
    const initial: Item[] = [
      { id: 1 },
      { id: 2, selectable: false },
      { id: 3 },
    ];
    const { result, rerender } = renderHook(
      ({ items }: { items: Item[] }) =>
        useBulkSelection({ items, getKey, isSelectable }),
      { initialProps: { items: initial } },
    );

    act(() => result.current.toggleAll());
    expect(result.current.selectedKeys).toEqual(new Set([1, 3]));

    rerender({ items: [{ id: 3 }] });
    expect(result.current.selectedKeys).toEqual(new Set([3]));
  });
});
