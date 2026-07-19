import { useRouter } from 'next/navigation';
import { notify } from '@/lib/notifications';
import { withAdminClient } from '@/lib/client/adminClient';
import { ApiKeyTestResult } from '@knn_labs/conduit-admin-client';
import type { ProviderFormData, ProviderFormLogicResult } from './ProviderFormLogic';

interface UseProviderFormHandlersParams {
  mode: 'add' | 'edit';
  providerId?: number;
  logic: ProviderFormLogicResult;
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
        let providerName = values.providerName.trim();
        if (!providerName) {
          const selectedProvider = availableProviders.find(p => p.value === values.providerType);
          providerName = selectedProvider?.label ?? 'Unknown Provider';
        }

        // First create the provider without the API key
        const providerPayload = {
          providerType: parseInt(values.providerType, 10),
          providerName: providerName,
          baseUrl: values.apiEndpoint ?? undefined,
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

    setIsTesting(true);
    setTestResult(null);

    try {
      let result;
      
      if (mode === 'add') {
        result = await withAdminClient(client => 
          client.providers.testConfig({
            providerType: parseInt(form.values.providerType, 10),
            apiKey: form.values.apiKey,
            baseUrl: form.values.apiEndpoint ?? undefined,
            organizationId: form.values.organizationId ?? undefined,
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