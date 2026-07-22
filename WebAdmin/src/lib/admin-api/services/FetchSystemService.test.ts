import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import { FetchSystemService } from './FetchSystemService';

const mockGetGlobalSetting = jest.fn();
const mockCreateGlobalSetting = jest.fn();
const mockValidateVirtualKey = jest.fn();
const mockCreateVirtualKey = jest.fn();
const mockDeleteVirtualKey = jest.fn();
const mockListGroups = jest.fn();
const mockCreateGroup = jest.fn();
const mockDeleteGroup = jest.fn();

jest.mock('./FetchSettingsService', () => ({
  FetchSettingsService: jest.fn(() => ({
    getGlobalSetting: mockGetGlobalSetting,
    createGlobalSetting: mockCreateGlobalSetting,
  })),
}));

jest.mock('./FetchVirtualKeyService', () => ({
  FetchVirtualKeyService: jest.fn(() => ({
    validate: mockValidateVirtualKey,
    create: mockCreateVirtualKey,
    delete: mockDeleteVirtualKey,
  })),
}));

jest.mock('./FetchVirtualKeyGroupService', () => ({
  FetchVirtualKeyGroupService: jest.fn(() => ({
    list: mockListGroups,
    create: mockCreateGroup,
    delete: mockDeleteGroup,
  })),
}));

describe('FetchSystemService.getWebAdminVirtualKey', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGetGlobalSetting.mockRejectedValue(new Error('not found'));
    mockListGroups.mockResolvedValue({ items: [], totalPages: 1 });
    mockCreateGroup.mockResolvedValue({ id: 7, externalGroupId: 'webadmin-internal' });
    mockCreateVirtualKey.mockResolvedValue({
      virtualKey: 'vk_webadmin',
      keyInfo: { id: 11 },
    });
    mockCreateGlobalSetting.mockResolvedValue({ key: 'WebAdmin_VirtualKey' });
  });

  it('shares one cold-start bootstrap across concurrent service instances', async () => {
    const client = {} as FetchBaseApiClient;
    const firstService = new FetchSystemService(client);
    const secondService = new FetchSystemService(client);

    const [firstKey, secondKey] = await Promise.all([
      firstService.getWebAdminVirtualKey(),
      secondService.getWebAdminVirtualKey(),
    ]);

    expect(firstKey).toBe('vk_webadmin');
    expect(secondKey).toBe('vk_webadmin');
    expect(mockCreateGroup).toHaveBeenCalledTimes(1);
    expect(mockCreateVirtualKey).toHaveBeenCalledTimes(1);
    expect(mockCreateGlobalSetting).toHaveBeenCalledTimes(1);
  });

  it('reuses an existing internal group instead of funding another one', async () => {
    mockListGroups.mockResolvedValue({
      items: [{ id: 42, externalGroupId: 'webadmin-internal' }],
      totalPages: 1,
    });
    const service = new FetchSystemService({} as FetchBaseApiClient);

    await service.getWebAdminVirtualKey();

    expect(mockCreateGroup).not.toHaveBeenCalled();
    expect(mockCreateVirtualKey).toHaveBeenCalledWith(
      expect.objectContaining({ virtualKeyGroupId: 42 }),
      undefined
    );
  });

  it('loads the winning key and cleans up resources after a settings race', async () => {
    mockGetGlobalSetting
      .mockRejectedValueOnce(new Error('not found'))
      .mockResolvedValueOnce({ value: 'vk_winner' });
    mockCreateGlobalSetting.mockRejectedValueOnce(new Error('setting already exists'));
    mockValidateVirtualKey.mockResolvedValueOnce({ isValid: true });
    const service = new FetchSystemService({} as FetchBaseApiClient);

    const key = await service.getWebAdminVirtualKey();

    expect(key).toBe('vk_winner');
    expect(mockDeleteVirtualKey).toHaveBeenCalledWith('11', undefined);
    expect(mockDeleteGroup).toHaveBeenCalledWith(7, undefined);
  });
});
