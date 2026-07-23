import { act, renderHook, waitFor } from '@testing-library/react';
import { normalizeParameterValues, useParameterState } from './useParameterState';
import type { DynamicParameter } from '../types/parameters';

const imageParameters = (defaultQuality: string, options: string[]): Record<string, DynamicParameter> => ({
  quality: {
    type: 'select',
    label: 'Quality',
    default: defaultQuality,
    options: options.map(value => ({ value, label: value })),
  },
});

describe('useParameterState', () => {
  beforeEach(() => localStorage.clear());

  it('normalizes invalid values to the current server default and drops unknown fields', () => {
    expect(normalizeParameterValues(imageParameters('standard', ['standard', 'hd']), {
      quality: 'unsupported',
      stale: true,
    })).toEqual({ quality: 'standard' });
  });

  it('loads and validates the effective schema when the model changes', async () => {
    localStorage.setItem('parameters_image-model-b', JSON.stringify({ quality: 'invalid', stale: true }));
    const { result, rerender } = renderHook(
      ({ parameters, persistKey }) => useParameterState({ parameters, persistKey }),
      {
        initialProps: {
          parameters: imageParameters('standard', ['standard', 'hd']),
          persistKey: 'image-model-a',
        },
      },
    );

    act(() => result.current.updateValue('quality', 'hd'));
    expect(result.current.values).toEqual({ quality: 'hd' });

    rerender({
      parameters: imageParameters('auto', ['auto', 'high']),
      persistKey: 'image-model-b',
    });

    await waitFor(() => expect(result.current.values).toEqual({ quality: 'auto' }));
    expect(result.current.parameters.quality).toMatchObject({ default: 'auto' });
  });

  it('adopts a changed server default when the current value was the old default', async () => {
    const { result, rerender } = renderHook(
      ({ parameters }) => useParameterState({ parameters, persistKey: 'image-model' }),
      { initialProps: { parameters: imageParameters('standard', ['standard', 'hd']) } },
    );

    rerender({ parameters: imageParameters('auto', ['auto', 'hd']) });
    await waitFor(() => expect(result.current.values).toEqual({ quality: 'auto' }));
  });
});
