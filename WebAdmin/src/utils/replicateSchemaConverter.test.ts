import {
  convertReplicateSchemaToParameters,
  tryConvertReplicateSchema,
} from './replicateSchemaConverter';

describe('Replicate schema conversion', () => {
  const schema = {
    type: 'object',
    properties: {
      guidance: {
        type: 'number',
        title: 'Guidance',
        minimum: 1,
        maximum: 20,
        default: 7.5,
      },
      output_format: {
        type: 'string',
        enum: ['png', 'webp'],
        default: 'png',
      },
      safety_checker: {
        type: 'boolean',
        default: true,
      },
    },
  };

  it('emits the canonical DynamicParameter shapes', () => {
    const converted = convertReplicateSchemaToParameters(schema);

    expect(converted.guidance).toMatchObject({
      type: 'slider',
      label: 'Guidance',
      min: 1,
      max: 20,
      step: 0.1,
    });
    expect(converted.output_format).toMatchObject({
      type: 'select',
      options: [
        { value: 'png', label: 'png' },
        { value: 'webp', label: 'webp' },
      ],
    });
    expect(converted.safety_checker).toMatchObject({
      type: 'checkbox',
      default: true,
    });
  });

  it('serializes converted schemas for the parameter editor', () => {
    const converted = JSON.parse(
      tryConvertReplicateSchema(JSON.stringify(schema)),
    ) as Record<string, { type: string }>;

    expect(converted.guidance.type).toBe('slider');
    expect(converted.output_format.type).toBe('select');
  });
});
