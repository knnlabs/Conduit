import { useForm } from '@mantine/form';
import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { notifications } from '@mantine/notifications';
import { 
  type ProviderDto
} from '@knn_labs/conduit-admin-client';
import { withAdminClient } from '@/lib/client/adminClient';
import { getProviderTypeFromDto, getProviderDisplayName } from '@/lib/utils/providerTypeUtils';
import { validators } from '@/lib/utils/form-validators';

export interface ProviderFormData {
  providerType: string;
  providerName: string;
  apiKey: string;
  apiEndpoint?: string;
  organizationId?: string;
  isEnabled: boolean;
}

export interface ProviderOption {
  value: string;
  label: string;
}

export interface ProviderFormLogicResult {
  form: ReturnType<typeof useForm<ProviderFormData>>;
  isSubmitting: boolean;
  setIsSubmitting: (value: boolean) => void;
  isTesting: boolean;
  setIsTesting: (value: boolean) => void;
  testResult: { success: boolean; message: string } | null;
  setTestResult: (value: { success: boolean; message: string } | null) => void;
  availableProviders: ProviderOption[];
  setAvailableProviders: (value: ProviderOption[]) => void;
  isLoadingProviders: boolean;
  setIsLoadingProviders: (value: boolean) => void;
  existingProvider: ProviderDto | null;
  setExistingProvider: (value: ProviderDto | null) => void;
  isLoadingProvider: boolean;
  setIsLoadingProvider: (value: boolean) => void;
  initialFormValues: ProviderFormData;
  setInitialFormValues: (value: ProviderFormData) => void;
  providerDisplayName: string;
  isLoading: boolean;
}

export function useProviderFormLogic(
  mode: 'add' | 'edit', 
  providerId?: number
): ProviderFormLogicResult {
  const router = useRouter();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isTesting, setIsTesting] = useState(false);
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);
  const [availableProviders, setAvailableProviders] = useState<ProviderOption[]>([]);
  const [isLoadingProviders, setIsLoadingProviders] = useState(mode === 'add');
  const [existingProvider, setExistingProvider] = useState<ProviderDto | null>(null);
  const [isLoadingProvider, setIsLoadingProvider] = useState(mode === 'edit');
  const [initialFormValues, setInitialFormValues] = useState<ProviderFormData>(() => ({
    providerType: '',
    providerName: '',
    apiKey: '',
    apiEndpoint: '',
    organizationId: '',
    isEnabled: true,
  }));

  const form = useForm<ProviderFormData>({
    initialValues: initialFormValues,
    validate: {
      providerType: (value) => (mode === 'add' && !value ? 'Provider type is required' : null),
      providerName: (value) => {
        if (mode === 'edit' && !value) {
          return 'Provider name is required';
        }
        return null;
      },
      apiKey: (value) => (mode === 'add' && !value ? 'API key is required' : null),
      apiEndpoint: (value) => {
        if (value && !validators.url(value)) {
          return 'Please enter a valid URL';
        }
        return null;
      },
    },
  });

  // Fetch available providers for add mode
  useEffect(() => {
    if (mode === 'add') {
      const loadProviders = async () => {
        setIsLoadingProviders(true);
        try {
          const providerTypes = await withAdminClient(client => 
            client.providers.getAvailableProviderTypes()
          );
          
          const providers: ProviderOption[] = providerTypes.map(type => ({
            value: type.toString(),
            label: getProviderDisplayName(type)
          }));
          
          setAvailableProviders(providers);
          
          if (providers.length === 0) {
            notifications.show({
              title: 'No Providers Available',
              message: 'All provider types have already been configured.',
              color: 'orange',
            });
            router.push('/llm-providers');
          }
        } catch (error) {
          console.error('Error fetching available providers:', error);
          notifications.show({
            title: 'Error',
            message: 'Failed to load available providers',
            color: 'red',
          });
        } finally {
          setIsLoadingProviders(false);
        }
      };
      
      void loadProviders();
    }
  }, [mode, router]);

  // Fetch existing provider for edit mode and reinitialize form
  useEffect(() => {
    if (mode === 'edit' && providerId) {
      const loadProvider = async () => {
        setIsLoadingProvider(true);
        try {
          const provider = await withAdminClient(client => 
            client.providers.getById(providerId)
          );
          setExistingProvider(provider);
          
          const apiProvider = provider;
          
          // Create new form values
          const newFormValues: ProviderFormData = {
            providerType: provider.providerType?.toString() ?? '',
            providerName: typeof apiProvider.providerName === 'string' ? apiProvider.providerName : '',
            apiKey: '', // Don't show existing key for security
            apiEndpoint: apiProvider.baseUrl ?? '',
            organizationId: (provider as { organization?: string; organizationId?: string }).organization ?? 
                          (provider as { organization?: string; organizationId?: string }).organizationId ?? '',
            isEnabled: provider.isEnabled === true,
          };
          
          // Update initial values - form will reinitialize via key prop
          setInitialFormValues(newFormValues);
        } catch (error) {
          console.error('Error fetching provider:', error);
          notifications.show({
            title: 'Error',
            message: 'Failed to load provider',
            color: 'red',
          });
          router.push('/llm-providers');
        } finally {
          setIsLoadingProvider(false);
        }
      };
      
      void loadProvider();
    }
  }, [mode, providerId, router]);

  let providerDisplayName = 'Unknown Provider';
  if (mode === 'edit' && existingProvider) {
    try {
      const providerType = getProviderTypeFromDto(existingProvider);
      providerDisplayName = getProviderDisplayName(providerType);
    } catch {
      // Fallback to provider name if available
      const apiProvider = existingProvider;
      providerDisplayName = typeof apiProvider.providerName === 'string' ? apiProvider.providerName : 'Unknown Provider';
    }
  }

  const isLoading = isLoadingProviders || isLoadingProvider;

  return {
    form,
    isSubmitting,
    setIsSubmitting,
    isTesting,
    setIsTesting,
    testResult,
    setTestResult,
    availableProviders,
    setAvailableProviders,
    isLoadingProviders,
    setIsLoadingProviders,
    existingProvider,
    setExistingProvider,
    isLoadingProvider,
    setIsLoadingProvider,
    initialFormValues,
    setInitialFormValues,
    providerDisplayName,
    isLoading,
  };
}