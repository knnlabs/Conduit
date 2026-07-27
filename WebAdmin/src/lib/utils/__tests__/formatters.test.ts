import {
  formatCost,
  formatDuration,
  formatRelativeTime,
  formatters,
} from '../formatters';

describe('shared UI formatters', () => {
  it('uses one duration formatter across request and function views', () => {
    expect(formatDuration(1250)).toBe(formatters.responseTime(1250));
  });

  it('formats missing, zero, and non-zero costs consistently', () => {
    expect(formatCost(undefined)).toBe('—');
    expect(formatCost(Number.NaN)).toBe('—');
    expect(formatCost(Number.POSITIVE_INFINITY)).toBe('—');
    expect(formatCost(0)).toBe('$0.00');
    expect(formatCost(1.25)).toBe(formatters.currency(1.25));
  });

  it('formats recent timestamps relative to a stable clock', () => {
    const now = Date.UTC(2026, 6, 26, 12);

    expect(formatRelativeTime(new Date(now - 15_000), now)).toBe('Just now');
    expect(formatRelativeTime(new Date(now - 120_000), now)).toBe(
      '2 minutes ago',
    );
    expect(formatRelativeTime(new Date(now - 3_600_000), now)).toBe(
      '1 hour ago',
    );
    expect(formatRelativeTime(new Date(now - 2 * 86_400_000), now)).toBe(
      '2 days ago',
    );
  });
});
