import { useRouter } from 'next/navigation';
import { notify } from '@/lib/notifications';
import { withAdminClient } from '@/lib/client/adminClient';
import {
  ApiKeyTestResult,
  type ProviderConfigurationDefinition,
  type ProviderSettingField,
  type ProviderType
} from '@/lib/admin-api';
import type { ProviderFormData, ProviderFormLogicResult } from './ProviderFormLogic';

interface UseProviderFormHandlersParams {
  mode: 'add' | 'edit';
  providerId?: number;
  logic: ProviderFormLogicResult;
}

/**
 * Collects the structured settings map sent to the backend, or undefined to leave the stored value
 * untouched (the update endpoint treats null as "no change" and any supplied map as a wholesale
 * replace).
 *
 * Edit mode seeds the map from the values loaded off the provider rather than building it from
 * scratch, so keys the running backend does not declare survive that wholesale replace.
 * Clearing a declared field removes its key, which is how a value gets unset.
 */
function collectSettings(
  values: ProviderFormData,
  fields: ProviderSettingField[],
  mode: 'add' | 'edit'
): Record<string, string> | undefined {
  const result: Record<string, string> = mode === 'edit' ? { ...(values.settings ?? {}) } : {};

  for (const field of fields) {
    const raw = values.settings?.[field.key]?.trim() ?? '';
    if (raw) {
      result[field.key] = raw;
    } else {
      delete result[field.key];
    }
  }

  if (mode === 'edit') {
    // Only manage settings for provider types that declare them; otherwise send nothing so an
    // unrecognized type's stored settings are left alone.
    return fields.length > 0 ? result : undefined;
  }
  return Object.keys(result).length > 0 ? result : undefined;
}

/**
 * Validates declared settings; returns a map of form-path -> error message.
 *
 * Required-ness is only enforced on create. An existing provider may legitimately carry no settings
 * because its identifier is embedded in a raw base URL (the dual-read path), so blocking an edit on
 * a missing value would be stricter than the backend, which decides by re-resolving the base URL.
 */
function validateSettings(
  values: ProviderFormData,
  fields: ProviderSettingField[],
  mode: 'add' | 'edit'
): Record<string, string> {
  const errors: Record<string, string> = {};
  for (const field of fields) {
    const raw = values.settings?.[field.key]?.trim() ?? '';
    if (!raw) {
      if (field.required && mode === 'add') {
        errors[`settings.${field.key}`] = `${field.label} is required`;
      }
      continue;
    }
    if (field.validationRegexSource) {
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

function validateProviderConfiguration(
  values: ProviderFormData,
  configuration: ProviderConfigurationDefinition | undefined,
  mode: 'add' | 'edit'
): Record<string, string> {
  const errors: Record<string, string> = {};
  if (mode === 'add' && configuration?.requiresApiKey !== false && !values.apiKey.trim()) {
    errors.apiKey = 'API key is required';
  }
  if (configuration?.requiresEndpoint && !values.apiEndpoint?.trim()) {
    errors.apiEndpoint = 'API endpoint is required';
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
    settingFields,
    providerConfiguration,
  } = logic;

  const handleSubmit = async (values: ProviderFormData) => {
    setIsSubmitting(true);
    try {
      const errors = {
        ...validateProviderConfiguration(values, providerConfiguration, mode),
        ...validateSettings(values, settingFields, mode),
      };
      if (Object.keys(errors).length > 0) {
        Object.entries(errors).forEach(([path, message]) => form.setFieldError(path, message));
        return;
      }

      if (mode === 'add') {
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
          settings: collectSettings(values, settingFields, 'add'),
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
          settings: collectSettings(values, settingFields, 'edit'),
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

    const errors = {
      ...validateProviderConfiguration(form.values, providerConfiguration, mode),
      ...validateSettings(form.values, settingFields, mode),
    };
    if (Object.keys(errors).length > 0) {
      Object.entries(errors).forEach(([path, message]) => form.setFieldError(path, message));
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
            settings: collectSettings(form.values, settingFields, 'add'),
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
