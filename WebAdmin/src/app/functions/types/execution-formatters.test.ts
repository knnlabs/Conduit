import {
  ExecutionState,
  getExecutionStateBadgeColor,
} from '.';

describe('function execution presentation', () => {
  it('uses one complete state-to-color mapping for API and gateway values', () => {
    expect(getExecutionStateBadgeColor(ExecutionState.Cancelled)).toBe('gray');
    expect(getExecutionStateBadgeColor(ExecutionState.TimedOut)).toBe('orange');
    expect(getExecutionStateBadgeColor('timed_out')).toBe('orange');
  });
});
