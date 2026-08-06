import {
  TOKENIZER_DISPLAY_NAMES,
  TOKENIZER_SELECT_OPTIONS,
  TOKENIZER_SHORT_NAMES,
  TOKENIZER_TYPE_VALUES,
  TokenizerType,
  getTokenizerDisplayName,
  isValidTokenizerType,
  parseTokenizerType,
} from './tokenizerTypes';

describe('tokenizerTypes', () => {
  it('exposes every contract value exactly once', () => {
    expect(new Set(TOKENIZER_TYPE_VALUES).size).toBe(TOKENIZER_TYPE_VALUES.length);
    expect(Object.keys(TOKENIZER_DISPLAY_NAMES).sort()).toEqual(
      [...TOKENIZER_TYPE_VALUES].sort(),
    );
    expect(Object.keys(TOKENIZER_SHORT_NAMES).sort()).toEqual(
      [...TOKENIZER_TYPE_VALUES].sort(),
    );
  });

  it('builds select options from the contract values', () => {
    expect(TOKENIZER_SELECT_OPTIONS).toHaveLength(TOKENIZER_TYPE_VALUES.length);
    expect(TOKENIZER_SELECT_OPTIONS[0]).toEqual({
      value: TokenizerType.None,
      label: TOKENIZER_DISPLAY_NAMES[TokenizerType.None],
    });
  });

  describe('parseTokenizerType', () => {
    it('round-trips wire strings', () => {
      for (const value of TOKENIZER_TYPE_VALUES) {
        expect(parseTokenizerType(value)).toBe(value);
      }
    });

    it('maps legacy numeric ordinals to the matching backend enum member', () => {
      // Ordinals mirror Shared/ConduitLLM.Configuration/Entities/TokenizerType.cs.
      expect(parseTokenizerType(0)).toBe(TokenizerType.None);
      expect(parseTokenizerType(1)).toBe(TokenizerType.Cl100KBase);
      expect(parseTokenizerType(5)).toBe(TokenizerType.O200KBase);
      expect(parseTokenizerType(15)).toBe(TokenizerType.O200KHarmony);
      expect(parseTokenizerType(23)).toBe(TokenizerType.Tiktoken);
      expect(parseTokenizerType('7')).toBe(TokenizerType.Claude3);
    });

    it('returns null rather than guessing for unknown input', () => {
      expect(parseTokenizerType(null)).toBeNull();
      expect(parseTokenizerType(undefined)).toBeNull();
      expect(parseTokenizerType('')).toBeNull();
      expect(parseTokenizerType('   ')).toBeNull();
      expect(parseTokenizerType('not-a-tokenizer')).toBeNull();
      expect(parseTokenizerType(1.5)).toBeNull();
      expect(parseTokenizerType(-1)).toBeNull();
      expect(parseTokenizerType(TOKENIZER_TYPE_VALUES.length)).toBeNull();
    });
  });

  describe('getTokenizerDisplayName', () => {
    it('returns long and short names', () => {
      expect(getTokenizerDisplayName(TokenizerType.Cl100KBase)).toBe(
        'cl100k_base (GPT-4, GPT-3.5-turbo)',
      );
      expect(getTokenizerDisplayName(TokenizerType.Cl100KBase, true)).toBe('cl100k_base');
    });

    it('surfaces the raw value when it cannot be resolved', () => {
      expect(getTokenizerDisplayName('mystery')).toBe('Unknown (mystery)');
    });
  });

  describe('isValidTokenizerType', () => {
    it('accepts contract values and rejects everything else', () => {
      expect(isValidTokenizerType(TokenizerType.Mistral)).toBe(true);
      expect(isValidTokenizerType('Mistral')).toBe(false);
      expect(isValidTokenizerType(13)).toBe(false);
      expect(isValidTokenizerType(null)).toBe(false);
    });
  });
});
