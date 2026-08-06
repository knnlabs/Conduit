jest.unmock('./videoSignalRClient');

import {
  VideoSignalRClient,
  createVideoSignalRClient,
  disconnectVideoSignalRClient,
} from './videoSignalRClient';

describe('task-scoped video SignalR clients', () => {
  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('disconnects one task without affecting another task client', async () => {
    const disconnect = jest
      .spyOn(VideoSignalRClient.prototype, 'disconnect')
      .mockResolvedValue(undefined);
    const firstClient = createVideoSignalRClient('task-one');
    const secondClient = createVideoSignalRClient('task-two');

    expect(firstClient).not.toBe(secondClient);

    await disconnectVideoSignalRClient('task-one');

    expect(disconnect).toHaveBeenCalledTimes(1);
    expect(disconnect.mock.instances[0]).toBe(firstClient);

    await disconnectVideoSignalRClient('task-two');
    expect(disconnect).toHaveBeenCalledTimes(2);
    expect(disconnect.mock.instances[1]).toBe(secondClient);
  });

  it('does not let stale cleanup disconnect a replacement for the same task', async () => {
    const disconnect = jest
      .spyOn(VideoSignalRClient.prototype, 'disconnect')
      .mockResolvedValue(undefined);
    const staleClient = createVideoSignalRClient('reused-task');
    const replacementClient = createVideoSignalRClient('reused-task');
    disconnect.mockClear();

    await disconnectVideoSignalRClient('reused-task', staleClient);

    expect(disconnect).toHaveBeenCalledTimes(1);
    expect(disconnect.mock.instances[0]).toBe(staleClient);

    await disconnectVideoSignalRClient('reused-task');
    expect(disconnect.mock.instances[1]).toBe(replacementClient);
  });
});
