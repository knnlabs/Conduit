import {
  ProviderType,
  isValidProviderType,
} from './providers';

describe('isValidProviderType', () => {
  it('accepts canonical names and positive legacy numeric IDs', () => {
    expect(isValidProviderType(1)).toBe(true);
    expect(isValidProviderType(12)).toBe(true);
    expect(isValidProviderType(ProviderType.OpenAI)).toBe(true);
  });

  it('rejects unknown, empty, invalid numeric, and unrelated values', () => {
    expect(isValidProviderType(ProviderType.Unknown)).toBe(false);
    expect(isValidProviderType('')).toBe(false);
    expect(isValidProviderType(0)).toBe(false);
    expect(isValidProviderType({ provider: 'OpenAI' })).toBe(false);
  });
});
