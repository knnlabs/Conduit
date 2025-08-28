import {
  IconServer,
  IconKey,
  IconRobot,
  IconCurrencyDollar,
  IconDatabase
} from '@tabler/icons-react';
import { withAdminClient } from '@/lib/client/adminClient';
import { COST_THRESHOLDS, type CheckResult, type ConfigData } from './types';

// Pure functions for checks - no side effects
export function checkEnabledProviders(data: ConfigData): CheckResult {
  const enabledProviders = data.providers.filter(p => p.isEnabled);
  
  if (enabledProviders.length === 0) {
    return {
      id: 'enabled-providers',
      title: 'Enabled Providers',
      status: 'error',
      message: 'No providers are enabled',
      details: ['At least one provider must be enabled to process requests'],
      icon: IconServer
    };
  }

  return {
    id: 'enabled-providers',
    title: 'Enabled Providers',
    status: 'success',
    message: `${enabledProviders.length} provider(s) enabled`,
    details: enabledProviders.map(p => p.providerName ?? p.providerType?.toString() ?? 'Unknown'),
    icon: IconServer
  };
}

export function checkEnabledProviderKeys(data: ConfigData): CheckResult {
  const enabledKeys = data.allProviderKeys.filter(k => k.isEnabled);
  
  if (enabledKeys.length === 0) {
    return {
      id: 'enabled-keys',
      title: 'Enabled Provider Keys',
      status: 'error',
      message: 'No provider keys are enabled',
      details: ['At least one provider key must be enabled to authenticate with providers'],
      icon: IconKey
    };
  }

  return {
    id: 'enabled-keys',
    title: 'Enabled Provider Keys',
    status: 'success',
    message: `${enabledKeys.length} provider key(s) enabled`,
    icon: IconKey
  };
}

export function checkModelMappings(data: ConfigData): CheckResult {
  if (data.modelMappings.length === 0) {
    return {
      id: 'model-mappings',
      title: 'Model Mappings',
      status: 'error',
      message: 'No models are mapped',
      details: ['At least one model must be mapped to handle requests'],
      icon: IconRobot
    };
  }

  const enabledMappings = data.modelMappings.filter(m => m.isEnabled);
  if (enabledMappings.length === 0) {
    return {
      id: 'model-mappings',
      title: 'Model Mappings',
      status: 'error',
      message: 'No enabled model mappings',
      details: ['At least one model mapping must be enabled'],
      icon: IconRobot
    };
  }

  return {
    id: 'model-mappings',
    title: 'Model Mappings',
    status: 'success',
    message: `${enabledMappings.length} model(s) mapped and enabled`,
    icon: IconRobot
  };
}

export function checkModelCosts(data: ConfigData): CheckResult {
  if (data.modelCosts.length === 0) {
    return {
      id: 'model-costs',
      title: 'Model Cost Configuration',
      status: 'error',
      message: 'No model costs configured',
      details: ['All models must have cost records for proper billing tracking'],
      icon: IconCurrencyDollar
    };
  }

  return {
    id: 'model-costs',
    title: 'Model Cost Configuration',
    status: 'success',
    message: `${data.modelCosts.length} cost configuration(s) defined`,
    icon: IconCurrencyDollar
  };
}

export function checkModelCategoriesMapping(data: ConfigData): CheckResult {
  const enabledMappings = data.modelMappings.filter(m => m.isEnabled);
  
  const categories = {
    chat: enabledMappings.filter(m => 
      m.modelAlias.toLowerCase().includes('chat') || 
      m.modelAlias.toLowerCase().includes('gpt') ||
      m.modelAlias.toLowerCase().includes('claude') ||
      m.modelAlias.toLowerCase().includes('llama')
    ),
    image: enabledMappings.filter(m => 
      m.modelAlias.toLowerCase().includes('dall') ||
      m.modelAlias.toLowerCase().includes('image') ||
      m.modelAlias.toLowerCase().includes('midjourney') ||
      m.modelAlias.toLowerCase().includes('stable')
    ),
    video: enabledMappings.filter(m => 
      m.modelAlias.toLowerCase().includes('video') ||
      m.modelAlias.toLowerCase().includes('sora')
    )
  };

  const missingCategories = [];
  if (categories.chat.length === 0) missingCategories.push('Chat');
  if (categories.image.length === 0) missingCategories.push('Image');
  if (categories.video.length === 0) missingCategories.push('Video');

  if (missingCategories.length > 0) {
    return {
      id: 'model-categories',
      title: 'Model Category Coverage',
      status: 'warning',
      message: `Missing models for: ${missingCategories.join(', ')}`,
      details: [
        'Consider adding models for each category to provide full service coverage',
        ...missingCategories.map(cat => `• No ${cat.toLowerCase()} models configured`)
      ],
      icon: IconRobot
    };
  }

  return {
    id: 'model-categories',
    title: 'Model Category Coverage',
    status: 'success',
    message: 'All major model categories covered',
    details: [
      `Chat: ${categories.chat.length} model(s)`,
      `Image: ${categories.image.length} model(s)`,
      `Video: ${categories.video.length} model(s)`
    ],
    icon: IconRobot
  };
}

export function checkCheapModelCosts(data: ConfigData): CheckResult {
  const cheapCosts = data.modelCosts.filter(cost => 
    cost.inputCostPerMillionTokens < COST_THRESHOLDS.INPUT_FLOOR ||
    cost.outputCostPerMillionTokens < COST_THRESHOLDS.OUTPUT_FLOOR
  );

  if (cheapCosts.length > 0) {
    return {
      id: 'cheap-costs',
      title: 'Model Cost Validation',
      status: 'warning',
      message: `${cheapCosts.length} model(s) have unusually low costs`,
      details: [
        `Expected minimums: $${COST_THRESHOLDS.INPUT_FLOOR}/1M input, $${COST_THRESHOLDS.OUTPUT_FLOOR}/1M output`,
        'Please verify these costs are accurate:',
        ...cheapCosts.map(cost => 
          `• ${cost.costName}: $${cost.inputCostPerMillionTokens}/1M input, $${cost.outputCostPerMillionTokens}/1M output`
        )
      ],
      icon: IconCurrencyDollar
    };
  }

  return {
    id: 'cheap-costs',
    title: 'Model Cost Validation',
    status: 'success',
    message: 'All model costs appear reasonable',
    icon: IconCurrencyDollar
  };
}

// Standalone async function for S3 check
export async function checkS3Configuration(): Promise<CheckResult> {
  try {
    const stats = await withAdminClient(async (client) => {
      return client.media.getMediaStats('overall');
    });

    return {
      id: 's3-config',
      title: 'S3 Storage Configuration',
      status: 'success',
      message: 'S3 storage is working',
      details: [
        'Successfully connected to media storage service',
        `Total files: ${stats.totalFiles}`,
        `Total size: ${Math.round(stats.totalSizeBytes / 1024 / 1024)} MB`
      ],
      icon: IconDatabase
    };
  } catch (error) {
    return {
      id: 's3-config',
      title: 'S3 Storage Configuration', 
      status: 'warning',
      message: 'Unable to verify S3 storage status',
      details: [
        'Could not test media storage connectivity',
        'S3 may be configured but not accessible',
        'Check S3 credentials and bucket permissions',
        `Error: ${error instanceof Error ? error.message : 'Unknown error'}`
      ],
      icon: IconDatabase
    };
  }
}