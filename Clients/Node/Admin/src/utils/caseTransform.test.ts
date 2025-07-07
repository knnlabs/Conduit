import { pascalToCamel, camelToPascal, transformPascalToCamel, transformCamelToPascal, isPascalCased } from './caseTransform';

describe('caseTransform', () => {
  describe('pascalToCamel', () => {
    it('should convert PascalCase to camelCase', () => {
      expect(pascalToCamel('TestString')).toBe('testString');
      expect(pascalToCamel('ID')).toBe('iD');
      expect(pascalToCamel('HTTPSConnection')).toBe('hTTPSConnection');
    });

    it('should handle edge cases', () => {
      expect(pascalToCamel('')).toBe('');
      expect(pascalToCamel('a')).toBe('a');
      expect(pascalToCamel('A')).toBe('a');
    });
  });

  describe('camelToPascal', () => {
    it('should convert camelCase to PascalCase', () => {
      expect(camelToPascal('testString')).toBe('TestString');
      expect(camelToPascal('id')).toBe('Id');
      expect(camelToPascal('httpsConnection')).toBe('HttpsConnection');
    });

    it('should handle edge cases', () => {
      expect(camelToPascal('')).toBe('');
      expect(camelToPascal('a')).toBe('A');
      expect(camelToPascal('A')).toBe('A');
    });
  });

  describe('transformPascalToCamel', () => {
    it('should transform object keys from PascalCase to camelCase', () => {
      const input = {
        Success: true,
        Message: 'Test message',
        ErrorDetails: null,
        NestedObject: {
          PropertyOne: 'value1',
          PropertyTwo: 'value2'
        }
      };

      const expected = {
        success: true,
        message: 'Test message',
        errorDetails: null,
        nestedObject: {
          propertyOne: 'value1',
          propertyTwo: 'value2'
        }
      };

      expect(transformPascalToCamel(input)).toEqual(expected);
    });

    it('should handle arrays', () => {
      const input = {
        Items: [
          { Id: 1, Name: 'Item 1' },
          { Id: 2, Name: 'Item 2' }
        ]
      };

      const expected = {
        items: [
          { id: 1, name: 'Item 1' },
          { id: 2, name: 'Item 2' }
        ]
      };

      expect(transformPascalToCamel(input)).toEqual(expected);
    });

    it('should handle null and undefined', () => {
      expect(transformPascalToCamel(null)).toBeNull();
      expect(transformPascalToCamel(undefined)).toBeUndefined();
    });

    it('should handle primitives', () => {
      expect(transformPascalToCamel('string')).toBe('string');
      expect(transformPascalToCamel(123)).toBe(123);
      expect(transformPascalToCamel(true)).toBe(true);
    });

    it('should handle dates', () => {
      const date = new Date();
      expect(transformPascalToCamel(date)).toBe(date);
    });
  });

  describe('transformCamelToPascal', () => {
    it('should transform object keys from camelCase to PascalCase', () => {
      const input = {
        success: true,
        message: 'Test message',
        errorDetails: null,
        nestedObject: {
          propertyOne: 'value1',
          propertyTwo: 'value2'
        }
      };

      const expected = {
        Success: true,
        Message: 'Test message',
        ErrorDetails: null,
        NestedObject: {
          PropertyOne: 'value1',
          PropertyTwo: 'value2'
        }
      };

      expect(transformCamelToPascal(input)).toEqual(expected);
    });
  });

  describe('isPascalCased', () => {
    it('should detect PascalCase objects', () => {
      expect(isPascalCased({ Success: true, Message: 'test' })).toBe(true);
      expect(isPascalCased({ Id: 1, Name: 'test' })).toBe(true);
      expect(isPascalCased({ URL: 'http://example.com' })).toBe(true);
    });

    it('should detect non-PascalCase objects', () => {
      expect(isPascalCased({ success: true, message: 'test' })).toBe(false);
      expect(isPascalCased({ id: 1, name: 'test' })).toBe(false);
      expect(isPascalCased({})).toBe(false);
    });

    it('should handle edge cases', () => {
      expect(isPascalCased(null)).toBe(false);
      expect(isPascalCased(undefined)).toBe(false);
      expect(isPascalCased('string')).toBe(false);
      expect(isPascalCased(123)).toBe(false);
      expect(isPascalCased([])).toBe(false);
    });

    it('should detect mixed case objects as PascalCase', () => {
      // If any key is PascalCase, we treat the whole object as PascalCase
      expect(isPascalCased({ success: true, Message: 'test' })).toBe(true);
    });
  });
});