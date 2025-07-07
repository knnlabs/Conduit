/**
 * Utilities for transforming between PascalCase and camelCase
 * Used to handle C# API responses that use PascalCase by default
 */

/**
 * Convert a PascalCase string to camelCase
 */
export function pascalToCamel(str: string): string {
  if (!str || str.length === 0) return str;
  return str.charAt(0).toLowerCase() + str.slice(1);
}

/**
 * Convert a camelCase string to PascalCase
 */
export function camelToPascal(str: string): string {
  if (!str || str.length === 0) return str;
  return str.charAt(0).toUpperCase() + str.slice(1);
}

/**
 * Recursively transform object keys from PascalCase to camelCase
 */
export function transformPascalToCamel<T = any>(obj: any): T {
  if (obj === null || obj === undefined) {
    return obj;
  }

  // Handle arrays
  if (Array.isArray(obj)) {
    return obj.map(item => transformPascalToCamel(item)) as any;
  }

  // Handle dates (don't transform)
  if (obj instanceof Date) {
    return obj as any;
  }

  // Handle objects
  if (typeof obj === 'object') {
    const transformed: any = {};
    
    for (const key in obj) {
      if (obj.hasOwnProperty(key)) {
        const camelKey = pascalToCamel(key);
        transformed[camelKey] = transformPascalToCamel(obj[key]);
      }
    }
    
    return transformed;
  }

  // Return primitives as-is
  return obj;
}

/**
 * Recursively transform object keys from camelCase to PascalCase
 * Used for request payloads sent to C# API
 */
export function transformCamelToPascal<T = any>(obj: any): T {
  if (obj === null || obj === undefined) {
    return obj;
  }

  // Handle arrays
  if (Array.isArray(obj)) {
    return obj.map(item => transformCamelToPascal(item)) as any;
  }

  // Handle dates (don't transform)
  if (obj instanceof Date) {
    return obj as any;
  }

  // Handle objects
  if (typeof obj === 'object') {
    const transformed: any = {};
    
    for (const key in obj) {
      if (obj.hasOwnProperty(key)) {
        const pascalKey = camelToPascal(key);
        transformed[pascalKey] = transformCamelToPascal(obj[key]);
      }
    }
    
    return transformed;
  }

  // Return primitives as-is
  return obj;
}

/**
 * Check if an object appears to use PascalCase
 * This is a heuristic check - returns true if any top-level keys start with uppercase
 */
export function isPascalCased(obj: any): boolean {
  if (!obj || typeof obj !== 'object' || Array.isArray(obj)) {
    return false;
  }

  for (const key in obj) {
    if (obj.hasOwnProperty(key) && key.length > 0) {
      // If we find any key starting with uppercase, assume PascalCase
      if (key[0] === key[0].toUpperCase() && key[0] !== key[0].toLowerCase()) {
        return true;
      }
    }
  }

  return false;
}