'use client';

import { Select, JsonInput, Stack, Text, Alert } from '@mantine/core';
import { IconInfoCircle } from '@tabler/icons-react';
import { PricingModel } from '@knn_labs/conduit-admin-client';

interface PricingModelSelectorProps {
  pricingModel: PricingModel;
  pricingConfiguration: string;
  onPricingModelChange: (model: PricingModel) => void;
  onConfigurationChange: (config: string) => void;
}

const PRICING_MODEL_OPTIONS = [
  { value: String(PricingModel.Standard), label: 'Standard (Per Token)' },
  { value: String(PricingModel.PerVideo), label: 'Per Video (Flat Rate)' },
  { value: String(PricingModel.PerSecondVideo), label: 'Per Second Video' },
  { value: String(PricingModel.InferenceSteps), label: 'Inference Steps' },
  { value: String(PricingModel.TieredTokens), label: 'Tiered Tokens' },
  { value: String(PricingModel.PerImage), label: 'Per Image' },
  { value: String(PricingModel.PerMinuteAudio), label: 'Per Minute Audio' },
  { value: String(PricingModel.PerThousandCharacters), label: 'Per Thousand Characters' },
];

const getDefaultConfiguration = (model: PricingModel): string => {
  switch (model) {
    case PricingModel.PerVideo:
      return JSON.stringify({
        rates: {
          resolution512p6s: 0.10,
          resolution768p6s: 0.28,
          resolution1080p6s: 0.49
        }
      }, null, 2);
    
    case PricingModel.PerSecondVideo:
      return JSON.stringify({
        baseRate: 0.09,
        resolutionMultipliers: {
          res480p: 0.5,
          res720p: 1.0,
          res1080p: 1.5
        }
      }, null, 2);
    
    case PricingModel.InferenceSteps:
      return JSON.stringify({
        costPerStep: 0.00035,
        defaultSteps: 20,
        modelSteps: {
          modelFast: 10,
          modelQuality: 30
        }
      }, null, 2);
    
    case PricingModel.TieredTokens:
      return JSON.stringify({
        tiers: [
          { maxContext: 200000, inputCost: 400, outputCost: 2200 },
          { maxContext: null, inputCost: 1300, outputCost: 2200 }
        ]
      }, null, 2);
    
    case PricingModel.PerImage:
      return JSON.stringify({
        baseRate: 0.04,
        qualityMultipliers: {
          standard: 1.0,
          hd: 1.5
        },
        resolutionMultipliers: {
          res1024x1024: 1.0,
          res1792x1024: 1.5
        }
      }, null, 2);
    
    default:
      return '';
  }
};

const getPricingModelDescription = (model: PricingModel): string => {
  switch (model) {
    case PricingModel.Standard:
      return 'Standard per-token pricing for text models. Most common for chat and completion models.';
    case PricingModel.PerVideo:
      return 'Flat rate pricing based on video resolution and duration combinations (e.g., MiniMax).';
    case PricingModel.PerSecondVideo:
      return 'Per-second video pricing with optional resolution multipliers (e.g., Replicate).';
    case PricingModel.InferenceSteps:
      return 'Pricing based on denoising/inference steps for image generation (e.g., Fireworks).';
    case PricingModel.TieredTokens:
      return 'Different token rates based on context length tiers (e.g., MiniMax M1).';
    case PricingModel.PerImage:
      return 'Per-image pricing with quality and resolution multipliers.';
    case PricingModel.PerMinuteAudio:
      return 'Audio pricing based on minutes of audio processed.';
    case PricingModel.PerThousandCharacters:
      return 'Text-to-speech pricing based on character count.';
    default:
      return '';
  }
};

export function PricingModelSelector({
  pricingModel,
  pricingConfiguration,
  onPricingModelChange,
  onConfigurationChange
}: PricingModelSelectorProps) {
  const handleModelChange = (value: string | null) => {
    if (value) {
      const model = Number(value) as PricingModel;
      onPricingModelChange(model);
      
      // Set default configuration for the selected model
      if (model !== PricingModel.Standard) {
        onConfigurationChange(getDefaultConfiguration(model));
      } else {
        onConfigurationChange('');
      }
    }
  };

  const requiresConfiguration = pricingModel !== PricingModel.Standard;
  const description = getPricingModelDescription(pricingModel);

  return (
    <Stack gap="md">
      <Select
        label="Pricing Model"
        description="Select how costs are calculated for this model"
        value={String(pricingModel)}
        onChange={handleModelChange}
        data={PRICING_MODEL_OPTIONS}
        required
      />

      {description && (
        <Alert icon={<IconInfoCircle size={16} />} variant="light">
          <Text size="sm">{description}</Text>
        </Alert>
      )}

      {requiresConfiguration && (
        <JsonInput
          label="Pricing Configuration"
          description="JSON configuration for the selected pricing model"
          value={pricingConfiguration}
          onChange={onConfigurationChange}
          autosize
          minRows={4}
          maxRows={15}
          formatOnBlur
          required={requiresConfiguration}
          error={(() => {
            if (!pricingConfiguration && requiresConfiguration) {
              return 'Configuration is required for this pricing model';
            }
            try {
              if (pricingConfiguration) {
                JSON.parse(pricingConfiguration);
              }
              return null;
            } catch {
              return 'Invalid JSON format';
            }
          })()}
        />
      )}

      {pricingModel === PricingModel.PerVideo && (
        <Alert icon={<IconInfoCircle size={16} />} color="yellow" variant="light">
          <Stack gap="xs">
            <Text size="sm" fw={500}>Per-Video Pricing Notes:</Text>
            <Text size="xs">
              • Define flat rates for specific resolution + duration combinations
            </Text>
            <Text size="xs">
              • Format: &quot;resolution_duration&quot; (e.g., &quot;768p_6&quot; for 768p 6-second video)
            </Text>
            <Text size="xs">
              • Strict validation: exact match required for resolution and duration
            </Text>
          </Stack>
        </Alert>
      )}

      {pricingModel === PricingModel.TieredTokens && (
        <Alert icon={<IconInfoCircle size={16} />} color="blue" variant="light">
          <Stack gap="xs">
            <Text size="sm" fw={500}>Tiered Token Pricing Notes:</Text>
            <Text size="xs">
              • Define different rates based on total context length
            </Text>
            <Text size="xs">
              • Set maxContext to null for the final tier (unlimited)
            </Text>
            <Text size="xs">
              • Costs are per million tokens in USD
            </Text>
          </Stack>
        </Alert>
      )}
    </Stack>
  );
}