import { fireEvent, render, screen } from '@testing-library/react';
import { MantineProvider } from '@mantine/core';

import { Sidebar } from './Sidebar';

const mockPush = jest.fn();

jest.mock('next/navigation', () => ({
  usePathname: () => '/models',
  useRouter: () => ({ push: mockPush }),
}));

describe('Sidebar', () => {
  beforeEach(() => {
    mockPush.mockClear();
  });

  it('links to the restored model pricing page', () => {
    render(
      <MantineProvider>
        <Sidebar />
      </MantineProvider>,
    );

    fireEvent.click(screen.getByText('Model Pricing'));

    expect(mockPush).toHaveBeenCalledWith('/model-costs');
  });
});
