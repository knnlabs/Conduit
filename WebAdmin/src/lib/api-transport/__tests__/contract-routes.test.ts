import { materializeContractPath } from '../contract-routes';

describe('contract route materialization', () => {
  it('encodes path parameters without changing the contract template registry', () => {
    expect(materializeContractPath('/v1/tasks/{taskId}', { taskId: 'task/with spaces' }))
      .toBe('/v1/tasks/task%2Fwith%20spaces');
  });

  it('rejects missing path parameters', () => {
    expect(() => materializeContractPath('/v1/tasks/{taskId}', {}))
      .toThrow('Missing path parameter: taskId');
  });
});
