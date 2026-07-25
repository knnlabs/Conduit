import { useForm } from '@mantine/form';
import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { notify } from '@/lib/notifications';
import { 
  type ProviderDto
} from '@/lib/admin-api';
import { withAdminClient } from '@/lib/client/adminClient';
import { getProviderTypeFromDto, getProviderDisplayName } from '@/lib/utils/providerTypeUtils';
import { validators } from '@/lib/utils/form-validators';

export interface ProviderFormData {
  providerType: string;
  providerName: string;
  apiKey: string;
  apiEndpoint?: string;
  organizationId?: string;
  /** Structured, provider-scoped settings (for example a Cloudflare account ID), keyed by setting key. */
  settings: Record<string, string>;
  isEnabled: boolean;
  trustProviderReportedCosts: boolean;
  providerCostMarkupMultiplier: number;
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
    settings: {},
    isEnabled: true,
    trustProviderReportedCosts: false,
    providerCostMarkupMultiplier: 1,
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
            notify.warning('All provider types have already been configured.', 'No Providers Available');
            router.push('/llm-providers');
          }
        } catch (error) {
          console.error('Error fetching available providers:', error);
          notify.error('Failed to load available providers');
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
            settings: { ...(provider.settings ?? {}) },
            isEnabled: provider.isEnabled === true,
            trustProviderReportedCosts: provider.trustProviderReportedCosts === true,
            providerCostMarkupMultiplier: provider.providerCostMarkupMultiplier ?? 1,
          };
          
          // Hydrate the form with the loaded provider. useForm captures initialValues on its first
          // render, so updating the state alone would never reach the inputs; initialize() sets both
          // the values and the dirty baseline, and is a no-op if it somehow runs twice.
          setInitialFormValues(newFormValues);
          form.initialize(newFormValues);
        } catch (error) {
          console.error('Error fetching provider:', error);
          notify.error('Failed to load provider');
          router.push('/llm-providers');
        } finally {
          setIsLoadingProvider(false);
        }
      };
      
      void loadProvider();
    }
    // 'form' is intentionally omitted: useForm returns a new object every render, so including it
    // would refetch the provider on each render. The initialize() call inside is self-guarding.
    // eslint-disable-next-line react-hooks/exhaustive-deps
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