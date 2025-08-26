'use client';

import { useState, useEffect, useCallback } from 'react';
import {
  Card,
  Stack,
  Title,
  Text,
  Group,
  Badge,
  ThemeIcon,
  Collapse,
  Button,
  Loader,
  Alert,
  List
} from '@mantine/core';
import {
  IconCheck,
  IconX,
  IconAlertTriangle,
  IconChevronDown,
  IconChevronRight,
  IconServer,
  IconKey,
  IconRobot,
  IconCurrencyDollar,
  IconDatabase
} from '@tabler/icons-react';
import { useAdminClient } from '@/lib/client/adminClient';
import type {
  ProviderDto,
  ProviderKeyCredentialDto,
  ModelProviderMappingDto,
  ModelCostDto,
  GlobalSettingDto
} from '@knn_labs/conduit-admin-client';

interface CheckResult {
  id: string;
  title: string;
  status: 'success' | 'error' | 'warning';
  message: string;
  details?: string[];
  icon: React.ComponentType<{ size?: number }>;
}

interface ConfigData {
  providers: ProviderDto[];
  allProviderKeys: ProviderKeyCredentialDto[];
  modelMappings: ModelProviderMappingDto[];
  modelCosts: ModelCostDto[];
  settings: GlobalSettingDto[];
}

// Cost thresholds based on GPT-OSS 120B pricing
const COST_THRESHOLDS = {
  INPUT_FLOOR: 0.15,  // $0.15 per 1M tokens
  OUTPUT_FLOOR: 0.60  // $0.60 per 1M tokens
};

export function SystemConfigChecklist() {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [checks, setChecks] = useState<CheckResult[]>([]);
  const [expanded, setExpanded] = useState(false);

  const { executeWithAdmin } = useAdminClient();

  const checkS3Configuration = useCallback(async (): Promise<CheckResult> => {
    try {
      // Test S3 storage by calling the media stats endpoint via Admin SDK
      // This will fail if S3 is not properly configured or accessible
      
      const stats = await executeWithAdmin(async (client) => {
        // Try to get overall media stats - this touches the storage service
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
  }, [executeWithAdmin]);
  
  useEffect(() => {
    let isMounted = true;

    async function loadConfigurationChecks() {
      try {
        if (!isMounted) return;
        setLoading(true);
        setError(null);

        // Fetch all required data in parallel
        const [providersResponse, modelMappings, modelCostsResponse, settingsResponse] = await Promise.all([
          executeWithAdmin((client) => client.providers.list(1, 100)),
          executeWithAdmin((client) => client.modelMappings.list()),
          executeWithAdmin((client) => client.modelCosts.list()),
          executeWithAdmin((client) => client.settings.getGlobalSettings())
        ]);

        if (!isMounted) return;

        const providers = providersResponse.items;
        const modelCosts = modelCostsResponse.items || [];
        const settings = settingsResponse.settings;

        // Get all provider keys
        const allProviderKeys: ProviderKeyCredentialDto[] = [];
        for (const provider of providers) {
          if (!isMounted) return;
          try {
            const keys = await executeWithAdmin((client) => 
              client.providers.listKeys(provider.id)
            );
            allProviderKeys.push(...keys);
          } catch (err) {
            console.warn(`Failed to fetch keys for provider ${provider.id}:`, err);
          }
        }

        if (!isMounted) return;

        const configData: ConfigData = {
          providers,
          allProviderKeys,
          modelMappings,
          modelCosts,
          settings
        };

        // Run all checks
        const checkResults: CheckResult[] = [
          checkEnabledProviders(configData),
          checkEnabledProviderKeys(configData),
          checkModelMappings(configData),
          checkModelCosts(configData),
          checkModelCategoriesMapping(configData),
          checkCheapModelCosts(configData),
          await checkS3Configuration()
        ];

        if (!isMounted) return;
        setChecks(checkResults);

        // Auto-expand if there are any errors or warnings
        const hasIssues = checkResults.some(check => check.status === 'error' || check.status === 'warning');
        setExpanded(hasIssues);

      } catch (err) {
        if (!isMounted) return;
        console.error('Failed to perform configuration checks:', err);
        setError(err instanceof Error ? err.message : 'Failed to load configuration data');
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    void loadConfigurationChecks();

    return () => {
      isMounted = false;
    };
  }, [executeWithAdmin, checkS3Configuration]);

  async function handleRefresh() {
    try {
      setLoading(true);
      setError(null);

      // Fetch all required data in parallel
      const [providersResponse, modelMappings, modelCostsResponse, settingsResponse] = await Promise.all([
        executeWithAdmin((client) => client.providers.list(1, 100)),
        executeWithAdmin((client) => client.modelMappings.list()),
        executeWithAdmin((client) => client.modelCosts.list()),
        executeWithAdmin((client) => client.settings.getGlobalSettings())
      ]);

      const providers = providersResponse.items;
      const modelCosts = modelCostsResponse.items || [];
      const settings = settingsResponse.settings;

      // Get all provider keys
      const allProviderKeys: ProviderKeyCredentialDto[] = [];
      for (const provider of providers) {
        try {
          const keys = await executeWithAdmin((client) => 
            client.providers.listKeys(provider.id)
          );
          allProviderKeys.push(...keys);
        } catch (err) {
          console.warn(`Failed to fetch keys for provider ${provider.id}:`, err);
        }
      }

      const configData: ConfigData = {
        providers,
        allProviderKeys,
        modelMappings,
        modelCosts,
        settings
      };

      // Run all checks
      const checkResults: CheckResult[] = [
        checkEnabledProviders(configData),
        checkEnabledProviderKeys(configData),
        checkModelMappings(configData),
        checkModelCosts(configData),
        checkModelCategoriesMapping(configData),
        checkCheapModelCosts(configData),
        await checkS3Configuration()
      ];

      setChecks(checkResults);

      // Auto-expand if there are any errors or warnings
      const hasIssues = checkResults.some(check => check.status === 'error' || check.status === 'warning');
      setExpanded(hasIssues);

    } catch (err) {
      console.error('Failed to perform configuration checks:', err);
      setError(err instanceof Error ? err.message : 'Failed to load configuration data');
    } finally {
      setLoading(false);
    }
  }

  // Required Checks
  const checkEnabledProviders = (data: ConfigData): CheckResult => {
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
  };

  const checkEnabledProviderKeys = (data: ConfigData): CheckResult => {
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
  };

  const checkModelMappings = (data: ConfigData): CheckResult => {
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
  };

  const checkModelCosts = (data: ConfigData): CheckResult => {
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

    // This is a simplified check - in reality, we'd need to verify the mapping between models and costs
    return {
      id: 'model-costs',
      title: 'Model Cost Configuration',
      status: 'success',
      message: `${data.modelCosts.length} cost configuration(s) defined`,
      icon: IconCurrencyDollar
    };
  };

  // Warning Checks
  const checkModelCategoriesMapping = (data: ConfigData): CheckResult => {
    const enabledMappings = data.modelMappings.filter(m => m.isEnabled);
    
    // Group mappings by model type (we'd need to join with Model data to get actual types)
    // For now, we'll make assumptions based on model aliases
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
  };

  const checkCheapModelCosts = (data: ConfigData): CheckResult => {
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
  };

  const getStatusIcon = (status: CheckResult['status']) => {
    switch (status) {
      case 'success':
        return <IconCheck size={16} color="var(--mantine-color-green-6)" />;
      case 'error':
        return <IconX size={16} color="var(--mantine-color-red-6)" />;
      case 'warning':
        return <IconAlertTriangle size={16} color="var(--mantine-color-yellow-6)" />;
    }
  };

  const getStatusBadge = (status: CheckResult['status']) => {
    const config = {
      success: { color: 'green', label: 'OK' },
      error: { color: 'red', label: 'Error' },
      warning: { color: 'yellow', label: 'Warning' }
    };
    const { color, label } = config[status];
    return <Badge color={color} size="sm">{label}</Badge>;
  };

  const hasErrors = checks.some(check => check.status === 'error');
  const hasWarnings = checks.some(check => check.status === 'warning');

  const overallStatus: CheckResult['status'] = (() => {
    if (hasErrors) return 'error';
    if (hasWarnings) return 'warning';
    return 'success';
  })();
  
  const overallMessage = (() => {
    if (hasErrors) return 'System has configuration errors that require attention';
    if (hasWarnings) return 'System is functional but has some recommendations';
    return 'System is properly configured';
  })();

  if (loading) {
    return (
      <Card withBorder>
        <Group>
          <Loader size="sm" />
          <Text>Checking system configuration...</Text>
        </Group>
      </Card>
    );
  }

  if (error) {
    return (
      <Alert color="red" title="Configuration Check Failed">
        <Text size="sm">{error}</Text>
        <Button size="xs" mt="sm" onClick={() => { void handleRefresh(); }}>
          Retry
        </Button>
      </Alert>
    );
  }

  return (
    <Card withBorder>
      <Stack gap="md">
        <Group justify="space-between">
          <Group>
            <ThemeIcon 
              size="lg" 
              variant="light" 
              color={(() => {
                if (overallStatus === 'success') return 'green';
                if (overallStatus === 'error') return 'red';
                return 'yellow';
              })()}
            >
              {getStatusIcon(overallStatus)}
            </ThemeIcon>
            <div>
              <Title order={4}>System Configuration</Title>
              <Text size="sm" c="dimmed">{overallMessage}</Text>
            </div>
          </Group>
          <Group gap="xs">
            {getStatusBadge(overallStatus)}
            <Button 
              variant="subtle" 
              size="xs" 
              onClick={() => setExpanded(!expanded)}
              rightSection={expanded ? <IconChevronDown size={14} /> : <IconChevronRight size={14} />}
            >
              Details
            </Button>
          </Group>
        </Group>

        <Collapse in={expanded}>
          <Stack gap="sm">
            {checks.map((check) => (
              <Card key={check.id} withBorder padding="sm">
                <Group justify="space-between" align="flex-start">
                  <Group align="flex-start">
                    <ThemeIcon size="sm" variant="subtle" color="gray">
                      <check.icon size={14} />
                    </ThemeIcon>
                    <div style={{ flex: 1 }}>
                      <Group gap="xs" mb="xs">
                        <Text size="sm" fw={500}>{check.title}</Text>
                        {getStatusBadge(check.status)}
                      </Group>
                      <Text size="xs" c="dimmed" mb={check.details ? "xs" : 0}>
                        {check.message}
                      </Text>
                      {check.details && (
                        <List size="xs" spacing="xs">
                          {check.details.map((detail, idx) => (
                            <List.Item key={idx}>{detail}</List.Item>
                          ))}
                        </List>
                      )}
                    </div>
                  </Group>
                </Group>
              </Card>
            ))}
            
            <Group justify="center" mt="md">
              <Button size="xs" variant="light" onClick={() => { void handleRefresh(); }}>
                Refresh Checks
              </Button>
            </Group>
          </Stack>
        </Collapse>
      </Stack>
    </Card>
  );
}