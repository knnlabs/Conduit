import { useStatusIndicator } from './useSystemStatus';

describe('useStatusIndicator', () => {
  it('returns the existing display configuration for named statuses', () => {
    expect(useStatusIndicator('degraded')).toEqual({
      type: 'degraded',
      color: 'yellow',
      label: 'Degraded',
      icon: 'alert-triangle',
      priority: 'medium',
      description: 'Some services experiencing issues',
    });
  });

  it.each([
    [true, { color: 'green', label: 'Active', type: 'enabled' }],
    [false, { color: 'red', label: 'Inactive', type: 'disabled' }],
  ])('preserves boolean status formatting for %s', (status, expected) => {
    expect(useStatusIndicator(status)).toEqual(expected);
  });
});
