import {
  ExecutionState,
  formatExecutionCost,
  formatExecutionDuration,
  getExecutionStateBadgeColor,
} from '.';

describe('function execution presentation', () => {
  it('formats missing and non-finite values without leaking NaN', () => {
    expect(formatExecutionCost(null)).toBe('-');
    expect(formatExecutionCost(Number.NaN)).toBe('-');
    expect(formatExecutionDuration(undefined)).toBe('-');
    expect(formatExecutionDuration(Number.POSITIVE_INFINITY)).toBe('-');
  });

  it('uses one complete state-to-color mapping for API and gateway values', () => {
    expect(getExecutionStateBadgeColor(ExecutionState.Cancelled)).toBe('gray');
    expect(getExecutionStateBadgeColor(ExecutionState.TimedOut)).toBe('orange');
    expect(getExecutionStateBadgeColor('timed_out')).toBe('orange');
  });
});
