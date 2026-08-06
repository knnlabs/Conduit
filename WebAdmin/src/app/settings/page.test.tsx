import { fireEvent, render, screen } from '@testing-library/react';
import { MantineProvider } from '@mantine/core';

import { withAdminClient } from '@/lib/client/adminClient';

import SettingsPage from './page';

const mockGetGlobalSettings = jest.fn();
const mockGetDefinitions = jest.fn();
const mockGetRoutingDefaults = jest.fn();
const mockUpdateGlobalSetting = jest.fn();

jest.mock('@/lib/client/adminClient', () => ({
  withAdminClient: jest.fn(),
}));
jest.mock('@/lib/notifications', () => ({
  notify: {
    success: jest.fn(),
    error: jest.fn(),
  },
}));
jest.mock('@mantine/modals', () => ({
  modals: { openConfirmModal: jest.fn() },
}));

const mockedWithAdminClient = jest.mocked(withAdminClient);

describe('SettingsPage', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGetGlobalSettings.mockResolvedValue({
      settings: [
        {
          id: 1,
          key: 'Agentic.MinIterations',
          value: '6',
          createdAt: '2026-07-23T17:00:00Z',
          updatedAt: '2026-07-23T17:00:00Z',
        },
        {
          id: 2,
          key: 'Agentic.MaxIterations',
          value: '5',
          createdAt: '2026-07-23T17:00:00Z',
          updatedAt: '2026-07-23T17:00:00Z',
        },
        {
          id: 3,
          key: 'Custom.Policy',
          value: '{"enabled":true}',
          createdAt: '2026-07-23T17:00:00Z',
          updatedAt: '2026-07-23T17:00:00Z',
        },
        {
          id: 4,
          key: 'WebAdmin_VirtualKey',
          value: 'must-not-render',
          createdAt: '2026-07-23T17:00:00Z',
          updatedAt: '2026-07-23T17:00:00Z',
        },
      ],
      categories: [],
      lastModified: '2026-07-23T17:00:00Z',
    });
    mockGetDefinitions.mockResolvedValue([
      {
        key: 'Agentic.MinIterations',
        displayName: 'Minimum agentic iterations',
        description: 'Minimum loop count',
        type: 'integer',
        category: 'Agentic',
        defaultValue: '1',
        minimum: 1,
        maximum: 100,
        isFeatureOwned: false,
      },
      {
        key: 'Agentic.MaxIterations',
        displayName: 'Maximum agentic iterations',
        description: 'Maximum loop count',
        type: 'integer',
        category: 'Agentic',
        defaultValue: '5',
        minimum: 1,
        maximum: 100,
        isFeatureOwned: false,
      },
      {
        key: 'PromptCaching.Config',
        displayName: 'Prompt caching policy',
        description: 'Feature settings',
        type: 'json',
        category: 'Feature-owned',
        defaultValue: '{}',
        featureRoute: '/prompt-caching',
        isFeatureOwned: true,
      },
    ]);
    mockGetRoutingDefaults.mockResolvedValue({
      chatRoutingEnabled: true,
      costWeight: 0.4,
      speedWeight: 0.3,
      qualityWeight: 0.3,
    });
    mockUpdateGlobalSetting.mockResolvedValue(undefined);
    mockedWithAdminClient.mockImplementation(callback => callback({
      settings: {
        getGlobalSettings: mockGetGlobalSettings,
        getDefinitions: mockGetDefinitions,
        updateGlobalSetting: mockUpdateGlobalSetting,
      },
      configuration: {
        getRoutingDefaults: mockGetRoutingDefaults,
      },
    } as never));
  });

  it('renders typed, feature-owned, and advanced settings without the protected key', async () => {
    render(
      <MantineProvider>
        <SettingsPage />
      </MantineProvider>,
    );

    expect(await screen.findByText('Minimum agentic iterations')).toBeInTheDocument();
    expect(screen.getByText('Prompt caching policy')).toBeInTheDocument();
    expect(screen.getByText('Custom.Policy')).toBeInTheDocument();
    expect(screen.getByText('JSON')).toBeInTheDocument();
    expect(screen.queryByText('WebAdmin_VirtualKey')).not.toBeInTheDocument();
    expect(screen.queryByText('must-not-render')).not.toBeInTheDocument();
  });

  it('blocks an invalid minimum/maximum relationship before saving', async () => {
    render(
      <MantineProvider>
        <SettingsPage />
      </MantineProvider>,
    );
    await screen.findByText('Minimum agentic iterations');

    fireEvent.click(screen.getAllByRole('button', { name: 'Save' })[0]);

    expect(await screen.findByText(
      'Minimum agentic iterations cannot be greater than the maximum.',
    )).toBeInTheDocument();
    expect(mockUpdateGlobalSetting).not.toHaveBeenCalled();
  });
});
