import type { ParametersSchema, ParameterDefinition } from '@/types/parameters';

interface ReplicateProperty {
  type: string;
  title?: string;
  description?: string;
  default?: unknown;
  minimum?: number;
  maximum?: number;
  enum?: string[];
  format?: string;
  nullable?: boolean;
  items?: { type: string };
  xOrder?: number; // Use camelCase internally
}

interface ReplicateSchema {
  type: string;
  title?: string;
  required?: string[];
  properties: Record<string, ReplicateProperty>;
}

/**
 * Converts a Replicate model schema to Conduit parameters format
 */
export function convertReplicateSchemaToParameters(replicateSchema: string | object): ParametersSchema {
  let schema: ReplicateSchema;
  
  // Parse if string, otherwise use as-is
  if (typeof replicateSchema === 'string') {
    try {
      schema = JSON.parse(replicateSchema) as ReplicateSchema;
    } catch {
      throw new Error('Invalid JSON schema provided');
    }
  } else {
    schema = replicateSchema as ReplicateSchema;
  }

  // Validate basic structure
  if (!schema.properties || typeof schema.properties !== 'object') {
    throw new Error('Schema must have a properties object');
  }

  const parameters: ParametersSchema = {};
  const requiredFields = new Set(schema.required ?? []);

  // Sort by x-order if available
  const sortedEntries = Object.entries(schema.properties).sort(([, a], [, b]) => {
    // Access x-order property from raw JSON
    const rawA = a as unknown as Record<string, unknown>;
    const rawB = b as unknown as Record<string, unknown>;
    const orderA = (rawA['x-order'] as number | undefined) ?? 999;
    const orderB = (rawB['x-order'] as number | undefined) ?? 999;
    return orderA - orderB;
  });

  for (const [key, prop] of sortedEntries) {
    const parameter = convertProperty(key, prop, requiredFields.has(key));
    if (parameter) {
      parameters[key] = parameter;
    }
  }

  return parameters;
}

function convertProperty(key: string, prop: ReplicateProperty, isRequired: boolean): ParameterDefinition | null {
  // Skip properties we can't handle
  if (!prop.type) return null;

  const baseProps = {
    label: prop.title ?? formatLabel(key),
    description: prop.description,
    required: isRequired,
    default: prop.default,
  };

  // Handle enum types -> select
  if (prop.enum && prop.enum.length > 0) {
    // Convert default value safely
    let defaultValue: string | undefined;
    if (prop.default !== undefined && typeof prop.default === 'string') {
      defaultValue = prop.enum.includes(prop.default) ? prop.default : undefined;
    }
    
    return {
      ...baseProps,
      type: 'select',
      options: prop.enum.map(value => ({
        value: String(value), // Ensure value is always a string
        label: String(value),
      })),
      default: defaultValue,
    };
  }

  // Handle different property types
  switch (prop.type) {
    case 'boolean':
      return {
        ...baseProps,
        type: 'checkbox',
        default: prop.default as boolean | undefined,
      };

    case 'integer':
    case 'number':
      // Decide between slider and number input
      if (prop.minimum !== undefined && prop.maximum !== undefined) {
        const range = prop.maximum - prop.minimum;
        // Use slider for reasonable ranges
        if (range <= 1000 && range > 0) {
          return {
            ...baseProps,
            type: 'slider',
            min: prop.minimum,
            max: prop.maximum,
            step: prop.type === 'integer' ? 1 : 0.1,
            default: prop.default as number | undefined,
          };
        }
      }
      // Use number input for large ranges or when bounds aren't specified
      return {
        ...baseProps,
        type: 'number',
        min: prop.minimum,
        max: prop.maximum,
        step: prop.type === 'integer' ? 1 : 0.1,
        default: prop.default as number | undefined,
      };

    case 'string':
      // Check for special formats
      if (prop.format === 'uri') {
        return {
          ...baseProps,
          type: 'input',
          placeholder: prop.description?.includes('image') ? 'Image URL' : 'URL',
          default: prop.default as string | undefined,
        };
      }
      
      // Use textarea for prompts and longer text fields
      if (key.toLowerCase().includes('prompt') || key.toLowerCase().includes('description')) {
        return {
          ...baseProps,
          type: 'textarea',
          rows: 4,
          default: prop.default as string | undefined,
        };
      }

      // Default to input for other strings
      return {
        ...baseProps,
        type: 'input',
        default: prop.default as string | undefined,
      };

    default:
      // For array or object types, we can't handle them directly
      // Return null to skip
      return null;
  }
}

/**
 * Format a property key into a human-readable label
 */
function formatLabel(key: string): string {
  // Convert snake_case or camelCase to Title Case
  return key
    .replace(/_/g, ' ')
    .replace(/([A-Z])/g, ' $1')
    .trim()
    .split(' ')
    .map(word => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
    .join(' ');
}

/**
 * Validates if a string is valid Replicate schema JSON
 */
export function isValidReplicateSchema(text: string): boolean {
  try {
    const parsed = JSON.parse(text) as unknown;
    return parsed !== null && 
           typeof parsed === 'object' && 
           'properties' in parsed &&
           typeof (parsed as Record<string, unknown>).properties === 'object';
  } catch {
    return false;
  }
}

/**
 * Attempts to convert text in the parameters field
 * Returns the converted parameters or the original text if not valid Replicate schema
 */
export function tryConvertReplicateSchema(text: string): string {
  if (!text.trim()) return text;
  
  try {
    // Check if it's already in our format (has a property with 'type' field that matches our types)
    const parsed = JSON.parse(text) as unknown;
    if (typeof parsed === 'object' && parsed !== null) {
      const obj = parsed as Record<string, unknown>;
      const firstKey = Object.keys(obj)[0];
      if (firstKey && typeof obj[firstKey] === 'object' && obj[firstKey] !== null) {
        const firstProp = obj[firstKey] as Record<string, unknown>;
        if ('type' in firstProp && typeof firstProp.type === 'string' &&
            ['slider', 'select', 'input', 'number', 'checkbox', 'textarea'].includes(firstProp.type)) {
          // Already in our format, return as-is
          return text;
        }
      }
    }
    
    // Check if it looks like Replicate schema
    if (isValidReplicateSchema(text)) {
      const converted = convertReplicateSchemaToParameters(text);
      return JSON.stringify(converted, null, 2);
    }
    
    return text;
  } catch {
    // Not valid JSON or conversion failed, return original
    return text;
  }
}