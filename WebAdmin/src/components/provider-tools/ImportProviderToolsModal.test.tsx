import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MantineProvider } from '@mantine/core';
import { ImportProviderToolsModal } from './ImportProviderToolsModal';
import { withAdminClient } from '@/lib/client/adminClient';

jest.mock('@/lib/client/adminClient', () => ({
  withAdminClient: jest.fn(),
}));

jest.mock('@/lib/notifications', () => ({
  notify: {
    success: jest.fn(),
    error: jest.fn(),
  },
}));

const mockedWithAdminClient = jest.mocked(withAdminClient);
const importProviderTools = jest.fn();

function renderModal(overrides?: Partial<React.ComponentProps<typeof ImportProviderToolsModal>>) {
  const props = {
    isOpen: true,
    onClose: jest.fn(),
    onSuccess: jest.fn(),
    ...overrides,
  };

  render(
    <MantineProvider>
      <ImportProviderToolsModal {...props} />
    </MantineProvider>,
  );

  return props;
}

function selectJson(contents: string) {
  const file = new File([contents], 'tools.json', { type: 'application/json' });
  Object.defineProperty(file, 'text', { value: jest.fn().mockResolvedValue(contents) });
  const input = document.querySelector<HTMLInputElement>('input[type="file"]');
  if (!input) throw new Error('File input was not rendered');
  fireEvent.change(input, { target: { files: [file] } });
}

describe('ImportProviderToolsModal', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockedWithAdminClient.mockImplementation(async operation =>
      operation({ providerTools: { importProviderTools } } as never),
    );
  });

  it('reports malformed JSON without submitting it', async () => {
    renderModal();
    selectJson('{not-json');

    expect(await screen.findByText('The selected file is not valid JSON.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Confirm import' })).toBeDisabled();
    expect(importProviderTools).not.toHaveBeenCalled();
  });

  it('rejects JSON that is not an array', async () => {
    renderModal();
    selectJson(JSON.stringify({ provider: 1, toolName: 'browser_search' }));

    expect(await screen.findByText('Provider tool imports must contain a JSON array.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Confirm import' })).toBeDisabled();
  });

  it('shows schema errors and excludes invalid records', async () => {
    renderModal();
    selectJson(JSON.stringify([{ provider: 1, costPerUnit: -1 }]));

    expect(await screen.findByText(/toolName must be a non-empty string/)).toBeInTheDocument();
    expect(screen.getByText(/costPerUnit must be a non-negative number/)).toBeInTheDocument();
    expect(screen.getByText('1 invalid')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Confirm import' })).toBeDisabled();
  });

  it('cancels without importing', async () => {
    const onClose = jest.fn();
    renderModal({ onClose });
    selectJson(JSON.stringify([{ provider: 1, toolName: 'browser_search' }]));

    await screen.findByRole('button', { name: 'Confirm import (1)' });
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(onClose).toHaveBeenCalledTimes(1);
    expect(importProviderTools).not.toHaveBeenCalled();
  });

  it('identifies duplicate provider and tool names before submission', async () => {
    renderModal();
    selectJson(JSON.stringify([
      { provider: 1, toolName: 'browser_search' },
      { provider: 1, toolName: 'BROWSER_SEARCH' },
    ]));

    expect(await screen.findByText('1 duplicates')).toBeInTheDocument();
    expect(screen.getByText(/Duplicate provider and toolName in this file/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Confirm import (1)' })).toBeEnabled();
  });

  it('renders imported, skipped, and error results distinctly for partial success', async () => {
    importProviderTools.mockResolvedValue({
      imported: 1,
      skipped: 1,
      total: 2,
      errors: ['Row 2: unsupported billing unit'],
    });
    const onSuccess = jest.fn();
    renderModal({ onSuccess });
    selectJson(JSON.stringify([
      { provider: 1, toolName: 'browser_search' },
      { provider: 2, toolName: 'code_execution' },
    ]));

    fireEvent.click(await screen.findByRole('button', { name: 'Confirm import (2)' }));

    expect(await screen.findByText('1 imported')).toBeInTheDocument();
    expect(screen.getByText('1 skipped')).toBeInTheDocument();
    expect(screen.getByText('Row 2: unsupported billing unit')).toBeInTheDocument();
    expect(onSuccess).toHaveBeenCalledTimes(1);
  });

  it('submits validated contract fields and reports success', async () => {
    importProviderTools.mockResolvedValue({ imported: 1, skipped: 0, total: 1, errors: [] });
    const onSuccess = jest.fn();
    renderModal({ onSuccess });
    selectJson(JSON.stringify([{
      id: 99,
      provider: 1,
      toolName: ' browser_search ',
      costPerUnit: 0.01,
      isActive: true,
      providerName: 'ignored export field',
    }]));

    fireEvent.click(await screen.findByRole('button', { name: 'Confirm import (1)' }));

    await waitFor(() => expect(importProviderTools).toHaveBeenCalledWith([{
      provider: 1,
      toolName: 'browser_search',
      toolParameters: null,
      costPerUnit: 0.01,
      billingUnit: null,
      costDescription: null,
      isActive: true,
    }]));
    expect(await screen.findByText('1 imported')).toBeInTheDocument();
    expect(screen.getByText('0 skipped')).toBeInTheDocument();
    expect(onSuccess).toHaveBeenCalledTimes(1);
  });
});
