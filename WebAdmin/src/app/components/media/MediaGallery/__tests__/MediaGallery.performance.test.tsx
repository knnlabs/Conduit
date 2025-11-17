/**
 * Performance profiling tests for MediaGallery component
 * Tests rendering performance with various item counts and virtualization settings
 */
import React from 'react';
import { render, screen } from '@/app/test-utils';
import '@testing-library/jest-dom';
import { MediaGallery } from '../MediaGallery';

// Mock @tanstack/react-virtual
jest.mock('@tanstack/react-virtual', () => ({
  useVirtualizer: jest.fn(() => ({
    getTotalSize: () => 10000,
    getVirtualItems: () => [],
    scrollToIndex: jest.fn()
  }))
}));

interface TestItem {
  id: string;
  name: string;
}

describe('MediaGallery Performance Tests', () => {
  const createTestItems = (count: number): TestItem[] => {
    return Array.from({ length: count }, (item, i) => ({
      id: `item-${i}`,
      name: `Test Item ${i}`
    }));
  };

  const renderTestCard = (item: TestItem) => (
    <div key={item.id} data-testid={`card-${item.id}`}>
      {item.name}
    </div>
  );

  describe('Rendering Performance', () => {
    it('should render small galleries (10 items) without virtualization', () => {
      const items = createTestItems(10);
      const startTime = performance.now();

      render(
        <MediaGallery
          items={items}
          renderCard={renderTestCard}
        />
      );

      const endTime = performance.now();
      const renderTime = endTime - startTime;

      expect(screen.getByTestId('card-item-0')).toBeInTheDocument();
      expect(screen.getByTestId('card-item-9')).toBeInTheDocument();

      // Expect render to complete in under 100ms for 10 items
      expect(renderTime).toBeLessThan(100);

      console.warn(`Rendered 10 items in ${renderTime.toFixed(2)}ms`);
    });

    it('should render medium galleries (50 items) efficiently', () => {
      const items = createTestItems(50);
      const startTime = performance.now();

      render(
        <MediaGallery
          items={items}
          renderCard={renderTestCard}
        />
      );

      const endTime = performance.now();
      const renderTime = endTime - startTime;

      // All items should be rendered without virtualization at threshold
      expect(screen.getByTestId('card-item-0')).toBeInTheDocument();
      expect(screen.getByTestId('card-item-49')).toBeInTheDocument();

      // Expect render to complete in under 200ms for 50 items
      expect(renderTime).toBeLessThan(200);

      console.warn(`Rendered 50 items in ${renderTime.toFixed(2)}ms`);
    });

    it('should enable virtualization for large galleries (100+ items)', () => {
      const items = createTestItems(100);

      const { container } = render(
        <MediaGallery
          items={items}
          renderCard={renderTestCard}
        />
      );

      // With virtualization, should render a scrollable container
      const scrollContainer = container.querySelector('[style*="overflow"]');
      expect(scrollContainer).toBeInTheDocument();

      console.warn('Virtualization enabled for 100+ items');
    });

    it('should respect custom virtualization threshold', () => {
      const items = createTestItems(30);

      const { container } = render(
        <MediaGallery
          items={items}
          renderCard={renderTestCard}
          virtualizationThreshold={25}
        />
      );

      // Should enable virtualization since 30 > 25
      const scrollContainer = container.querySelector('[style*="overflow"]');
      expect(scrollContainer).toBeInTheDocument();

      console.warn('Custom virtualization threshold respected');
    });

    it('should allow disabling virtualization explicitly', () => {
      const items = createTestItems(100);

      render(
        <MediaGallery
          items={items}
          renderCard={renderTestCard}
          enableVirtualization={false}
        />
      );

      // All items should be rendered
      expect(screen.getByTestId('card-item-0')).toBeInTheDocument();
      expect(screen.getByTestId('card-item-99')).toBeInTheDocument();

      console.warn('Virtualization disabled, rendered all 100 items');
    });
  });

  describe('Memory Efficiency', () => {
    it('should not create excessive DOM nodes with virtualization', () => {
      const items = createTestItems(100);

      const { container } = render(
        <MediaGallery
          items={items}
          renderCard={renderTestCard}
        />
      );

      // With virtualization, should only render visible items
      // The mock returns empty array, so no cards should be rendered
      const cards = container.querySelectorAll('[data-testid^="card-"]');

      // In a real scenario with virtualization, this would be ~10-20 items
      // With our mock, it should be 0
      expect(cards.length).toBeLessThan(100);

      console.warn(`Virtualized gallery rendered ${cards.length} DOM nodes for 100 items`);
    });
  });

  describe('Re-render Performance', () => {
    it('should handle item updates efficiently', () => {
      const items = createTestItems(20);

      const { rerender } = render(
        <MediaGallery
          items={items}
          renderCard={renderTestCard}
        />
      );

      // Measure re-render time
      const startTime = performance.now();

      const updatedItems = createTestItems(20);
      rerender(
        <MediaGallery
          items={updatedItems}
          renderCard={renderTestCard}
        />
      );

      const endTime = performance.now();
      const rerenderTime = endTime - startTime;

      // Re-render should be fast (under 50ms)
      expect(rerenderTime).toBeLessThan(50);

      console.warn(`Re-rendered 20 items in ${rerenderTime.toFixed(2)}ms`);
    });
  });

  describe('Empty State Performance', () => {
    it('should render empty state quickly', () => {
      const startTime = performance.now();

      render(
        <MediaGallery
          items={[]}
          renderCard={renderTestCard}
          emptyTitle="No items"
          emptyMessage="Add some items to get started"
        />
      );

      const endTime = performance.now();
      const renderTime = endTime - startTime;

      expect(screen.getByText('No items')).toBeInTheDocument();
      expect(renderTime).toBeLessThan(50);

      console.warn(`Rendered empty state in ${renderTime.toFixed(2)}ms`);
    });
  });
});
