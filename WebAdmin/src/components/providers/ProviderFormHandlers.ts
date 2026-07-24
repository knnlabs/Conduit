import { useRouter } from 'next/navigation';
import { notify } from '@/lib/notifications';
import { withAdminClient } from '@/lib/client/adminClient';
import { ApiKeyTestResult, PROVIDER_CONFIG_REQUIREMENTS, type ProviderType } from '@/lib/admin-api';
import type { ProviderFormData, ProviderFormLogicResult } from './ProviderFormLogic';

interface UseProviderFormHandlersParams {
  mode: 'add' | 'edit';
  providerId?: number;
  logic: ProviderFormLogicResult;
}

/** The declared structured-setting fields for the selected provider type. */
function getSettingFields(providerType: string) {
  return PROVIDER_CONFIG_REQUIREMENTS[providerType as unknown as ProviderType]?.settings ?? [];
}

/** Collects non-empty declared settings into the map sent to the backend, or undefined when none. */
function collectSettings(values: ProviderFormData): Record<string, string> | undefined {
  const result: Record<string, string> = {};
  for (const field of getSettingFields(values.providerType)) {
    const raw = values.settings?.[field.key]?.trim() ?? '';
    if (raw) {
      result[field.key] = raw;
    }
  }
  return Object.keys(result).length > 0 ? result : undefined;
}

/** Validates required/format for declared settings; returns a map of form-path -> error message. */
function validateSettings(values: ProviderFormData): Record<string, string> {
  const errors: Record<string, string> = {};
  for (const field of getSettingFields(values.providerType)) {
    const raw = values.settings?.[field.key]?.trim() ?? '';
    if (field.required && !raw) {
      errors[`settings.${field.key}`] = `${field.label} is required`;
      continue;
    }
    if (raw && field.validationRegexSource) {
      try {
        if (!new RegExp(field.validationRegexSource).test(raw)) {
          errors[`settings.${field.key}`] = `${field.label} is not in the expected format`;
        }
      } catch {
        // Ignore a malformed regex source rather than blocking submission.
      }
    }
  }
  return errors;
}

export function useProviderFormHandlers({ mode, providerId, logic }: UseProviderFormHandlersParams) {
  const router = useRouter();
  const {
    form,
    setIsSubmitting,
    setIsTesting,
    setTestResult,
    availableProviders,
  } = logic;

  const handleSubmit = async (values: ProviderFormData) => {
    setIsSubmitting(true);
    try {
      if (mode === 'add') {
        // Validate declared structured settings (for example a required Cloudflare account ID).
        const settingsErrors = validateSettings(values);
        if (Object.keys(settingsErrors).length > 0) {
          Object.entries(settingsErrors).forEach(([path, message]) => form.setFieldError(path, message));
          return;
        }

        let providerName = values.providerName.trim();
        if (!providerName) {
          const selectedProvider = availableProviders.find(p => p.value === values.providerType);
          providerName = selectedProvider?.label ?? 'Unknown Provider';
        }

        // First create the provider without the API key
        const providerPayload = {
          providerType: values.providerType as ProviderType,
          providerName: providerName,
          baseUrl: values.apiEndpoint ?? undefined,
          settings: collectSettings(values),
          isEnabled: values.isEnabled,
          trustProviderReportedCosts: values.trustProviderReportedCosts,
          providerCostMarkupMultiplier: values.providerCostMarkupMultiplier,
        };

        const createdProvider = await withAdminClient(client => 
          client.providers.create(providerPayload)
        );

        // Then add the API key to the created provider
        if (values.apiKey) {
          try {
            await withAdminClient(client =>
              client.providers.createKey(createdProvider.id, {
                apiKey: values.apiKey,
                keyName: 'Primary Key',
                organization: values.organizationId ?? undefined,
                isPrimary: true,
                isEnabled: true,
              })
            );
          } catch (keyError) {
            // If key creation fails, we should inform the user
            console.warn('Failed to create API key:', keyError);
            notify.warning('Provider created but failed to save API key. Please add it manually in the provider settings.');
            router.push('/llm-providers');
            return;
          }
        }

        notify.success('Provider and API key created successfully');
      } else {
        // Edit mode - Note: API keys cannot be updated here, only through the keys management page
        const payload = {
          providerName: values.providerName ?? undefined,
          baseUrl: values.apiEndpoint ?? undefined,
          organization: values.organizationId ?? undefined,
          isEnabled: values.isEnabled,
          trustProviderReportedCosts: values.trustProviderReportedCosts,
          providerCostMarkupMultiplier: values.providerCostMarkupMultiplier,
        };

        await withAdminClient(client => 
          client.providers.update(providerId as number, payload)
        );

        notify.success('Provider updated successfully');
      }
      
      router.push('/llm-providers');
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : `Failed to ${mode} provider`;
      
      if (mode === 'add' && errorMessage.includes('already exists')) {
        notify.warning(`A provider of type "${values.providerType}" already exists. Please edit the existing provider or delete it first.`, 'Provider Already Exists');
      } else {
        notify.error(errorMessage);
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleTestConnection = async () => {
    const validation = form.validate();
    if (validation.hasErrors) {
      return;
    }

    // Validate declared structured settings before hitting the provider.
    const settingsErrors = validateSettings(form.values);
    if (Object.keys(settingsErrors).length > 0) {
      Object.entries(settingsErrors).forEach(([path, message]) => form.setFieldError(path, message));
      return;
    }

    setIsTesting(true);
    setTestResult(null);

    try {
      let result;

      if (mode === 'add') {
        result = await withAdminClient(client =>
          client.providers.testConfig({
            providerType: form.values.providerType as ProviderType,
            apiKey: form.values.apiKey,
            baseUrl: form.values.apiEndpoint ?? undefined,
            organizationId: form.values.organizationId ?? undefined,
            settings: collectSettings(form.values),
          })
        );
      } else {
        result = await withAdminClient(client => 
          client.providers.testConnectionById(providerId as number)
        );
      }
      
      // Handle response format with proper enum comparison
      const isSuccess = result.result === ApiKeyTestResult.SUCCESS;

      setTestResult({
        success: isSuccess,
        message: result.message ?? (isSuccess ? 'Connection successful' : 'Connection failed'),
      });
    } catch (error) {
      console.error('Connection test error:', error);
      setTestResult({
        success: false,
        message: 'Failed to test connection',
      });
    } finally {
      setIsTesting(false);
    }
  };

  const handleBack = () => {
    router.push('/llm-providers');
  };

  const handleCancel = () => {
    router.push('/llm-providers');
  };

  return {
    handleSubmit,
    handleTestConnection,
    handleBack,
    handleCancel,
  };
}
