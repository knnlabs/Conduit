import { render, screen } from '@testing-library/react';
import { MantineProvider } from '@mantine/core';

import { ProviderTypeList } from './ProviderTypeList';

describe('ProviderTypeList', () => {
  it('deep-links an associated cost through the model pricing page', () => {
    render(
      <MantineProvider>
        <ProviderTypeList
          associations={[{
            id: 7,
            identifier: 'test-model',
            provider: 1,
            isPrimary: true,
            modelCostId: 42,
            normalizedProvider: null,
            providerName: 'Test Provider',
          }]}
          loading={false}
          onAdd={jest.fn()}
          onEdit={jest.fn()}
          onDelete={jest.fn()}
        />
      </MantineProvider>,
    );

    expect(screen.getByRole('link', { name: /View Cost/i }))
      .toHaveAttribute('href', '/model-costs?view=42');
  });
});
