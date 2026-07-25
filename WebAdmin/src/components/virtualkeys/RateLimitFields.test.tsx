import { fireEvent, render, screen } from '@testing-library/react';
import { MantineProvider } from '@mantine/core';

import { RateLimitFields, type RateLimitFormValues } from './RateLimitFields';

function renderFields(values: RateLimitFormValues, onChange = jest.fn()) {
  render(
    <MantineProvider>
      <RateLimitFields values={values} onChange={onChange} />
    </MantineProvider>
  );
  return onChange;
}

describe('RateLimitFields', () => {
  it('offers every limit dimension the gateway enforces', () => {
    renderFields({});

    expect(screen.getByLabelText('Requests per minute')).toBeInTheDocument();
    expect(screen.getByLabelText('Requests per day')).toBeInTheDocument();
    expect(screen.getByLabelText('Tokens per minute')).toBeInTheDocument();
    expect(screen.getByLabelText('Max parallel requests')).toBeInTheDocument();
  });

  it('shows blank as unlimited rather than as zero', () => {
    // A zero here would read as "no requests allowed"; the API treats absent as no ceiling.
    renderFields({});

    expect(screen.getAllByPlaceholderText('Unlimited')).toHaveLength(4);
  });

  it('round-trips existing values', () => {
    renderFields({ rateLimitRpm: 600, rateLimitRpd: 10000, rateLimitTpm: 250000, maxParallelRequests: 8 });

    expect(screen.getByLabelText('Requests per minute')).toHaveValue('600');
    expect(screen.getByLabelText('Requests per day')).toHaveValue('10000');
    expect(screen.getByLabelText('Tokens per minute')).toHaveValue('250000');
    expect(screen.getByLabelText('Max parallel requests')).toHaveValue('8');
  });

  it('reports a cleared field as undefined, not zero', () => {
    const onChange = renderFields({ rateLimitRpm: 600 });

    fireEvent.change(screen.getByLabelText('Requests per minute'), { target: { value: '' } });

    expect(onChange).toHaveBeenCalledWith('rateLimitRpm', undefined);
  });

  it('warns when the daily ceiling is below the per-minute one', () => {
    // 600/min against 100/day spends the whole day in ten seconds — almost certainly transposed.
    renderFields({ rateLimitRpm: 600, rateLimitRpd: 100 });

    expect(screen.getByText(/daily allowance would be spent in under a minute/i)).toBeInTheDocument();
  });

  it('does not warn on a sane pair', () => {
    renderFields({ rateLimitRpm: 600, rateLimitRpd: 100000 });

    expect(screen.queryByText(/daily allowance would be spent/i)).not.toBeInTheDocument();
  });

  it('describes group limits as shared across the group', () => {
    render(
      <MantineProvider>
        <RateLimitFields values={{}} onChange={jest.fn()} scope="group" />
      </MantineProvider>
    );

    expect(screen.getByText(/every key in this group/i)).toBeInTheDocument();
  });
});
