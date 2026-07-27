import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MantineProvider } from '@mantine/core';

import { withAdminClient } from '@/lib/client/adminClient';

import SystemInfoPage from './page';

const mockGetSystemInfo = jest.fn();
const mockGetServiceHealth = jest.fn();
const mockCreateObjectUrl = jest.fn(() => 'blob:diagnostics');
const mockAnchorClick = jest.fn();

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

const systemInfo = {
  version: {
    appVersion: '3.0.0',
    commitSha: 'abcdef1234567890',
    buildTimestamp: '2026-07-23T17:00:00Z',
  },
  operatingSystem: { description: 'Linux', architecture: 'X64' },
  database: {
    provider: 'PostgreSQL',
    version: '17',
    connected: true,
    location: 'server',
    size: '10 MB',
    tableCount: 12,
  },
  runtime: { runtimeVersion: '.NET 10', uptime: '01:00:00', customerMode: 'Internal' },
  recordCounts: { virtualKeys: 2, settings: 3, providers: 4, modelMappings: 5 },
};

const health = {
  timestamp: '2026-07-23T18:00:00Z',
  overallStatus: 'degraded',
  summary: { healthy: 5, degraded: 1, unhealthy: 0, unknown: 0, total: 6 },
  services: [
    {
      id: 'core-api',
      name: 'Gateway API',
      status: 'degraded',
      instances: [
        {
          instanceId: 'gateway-a',
          status: 'healthy',
          version: '3.0.0',
          commitSha: 'abc123',
          uptime: '01:00:00',
          heartbeatAgeSeconds: 5,
          lastHeartbeat: '2026-07-23T17:59:55Z',
        },
        {
          instanceId: 'gateway-stale',
          status: 'degraded',
          version: '3.0.0',
          commitSha: 'abc123',
          uptime: '01:00:00',
          heartbeatAgeSeconds: 95,
          lastHeartbeat: '2026-07-23T17:58:25Z',
        },
      ],
    },
    {
      id: 'admin-api',
      name: 'Admin API',
      status: 'healthy',
      instances: [{
        instanceId: 'admin-a',
        status: 'healthy',
        version: '3.0.0',
        heartbeatAgeSeconds: 4,
      }],
    },
    { id: 'database', name: 'PostgreSQL Database', status: 'healthy', version: '17' },
    { id: 'redis', name: 'Redis', status: 'healthy', version: '8.0' },
    { id: 'messaging', name: 'Messaging', status: 'healthy' },
    { id: 'media-storage', name: 'Media Storage', status: 'degraded' },
  ],
};

function renderPage() {
  return render(
    <MantineProvider>
      <SystemInfoPage />
    </MantineProvider>,
  );
}

describe('SystemInfoPage', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGetSystemInfo.mockResolvedValue(systemInfo);
    mockGetServiceHealth.mockResolvedValue(health);
    mockedWithAdminClient.mockImplementation(callback => callback({
      system: {
        getSystemInfo: mockGetSystemInfo,
        getServiceHealth: mockGetServiceHealth,
      },
    } as never));
    Object.defineProperty(URL, 'createObjectURL', {
      configurable: true,
      value: mockCreateObjectUrl,
    });
    Object.defineProperty(URL, 'revokeObjectURL', {
      configurable: true,
      value: jest.fn(),
    });
    HTMLAnchorElement.prototype.click = mockAnchorClick;
  });

  it('renders degraded cluster status, every instance, and real dependency cards', async () => {
    renderPage();

    await screen.findByText('1/2 healthy');
    expect(screen.getByText('gateway-a')).toBeInTheDocument();
    expect(screen.getByText('gateway-stale')).toBeInTheDocument();
    expect(screen.getByText('admin-a')).toBeInTheDocument();
    expect(screen.getByText('PostgreSQL Database')).toBeInTheDocument();
    expect(screen.getByText('Redis')).toBeInTheDocument();
    expect(screen.getByText('Messaging')).toBeInTheDocument();
    expect(screen.getByText('Media Storage')).toBeInTheDocument();
    expect(screen.getByText('Customer mode')).toBeInTheDocument();
    expect(screen.getByText('Internal')).toBeInTheDocument();
    expect(screen.queryByText('Cache Hit Rate')).not.toBeInTheDocument();
  });

  it('keeps health visible when metadata fails', async () => {
    mockGetSystemInfo.mockRejectedValueOnce(new Error('metadata offline'));

    renderPage();

    expect(await screen.findByText(/Runtime metadata could not be refreshed: metadata offline/))
      .toBeInTheDocument();
    expect(screen.getByText('1/2 healthy')).toBeInTheDocument();
    expect(screen.getByText('gateway-a')).toBeInTheDocument();
  });

  it('refreshes both sources together and exports a redacted bundle', async () => {
    renderPage();
    await screen.findByText('1/2 healthy');

    fireEvent.click(screen.getByRole('button', { name: 'Refresh' }));
    await waitFor(() => {
      expect(mockGetSystemInfo).toHaveBeenCalledTimes(2);
      expect(mockGetServiceHealth).toHaveBeenCalledTimes(2);
    });

    fireEvent.click(screen.getByRole('button', { name: 'Export diagnostics' }));
    expect(mockCreateObjectUrl).toHaveBeenCalledTimes(1);
    expect(mockAnchorClick).toHaveBeenCalledTimes(1);
  });
});
