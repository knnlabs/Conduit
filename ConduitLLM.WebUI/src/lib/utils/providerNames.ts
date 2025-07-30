// Provider name storage utilities
// Since the backend doesn't support storing custom provider names,
// we'll store them in localStorage as a workaround

const PROVIDER_NAMES_KEY = 'conduit_provider_names';

interface ProviderNameMap {
  [providerId: string]: string;
}

export function getStoredProviderNames(): ProviderNameMap {
  if (typeof window === 'undefined') return {};
  
  try {
    const stored = localStorage.getItem(PROVIDER_NAMES_KEY);
    return stored ? JSON.parse(stored) as ProviderNameMap : {};
  } catch {
    return {};
  }
}

export function saveProviderName(providerId: number | string, name: string): void {
  if (typeof window === 'undefined') return;
  
  const names = getStoredProviderNames();
  names[String(providerId)] = name;
  
  try {
    localStorage.setItem(PROVIDER_NAMES_KEY, JSON.stringify(names));
  } catch (error) {
    console.error('Failed to save provider name:', error);
  }
}

export function getProviderName(providerId: number | string): string | undefined {
  const names = getStoredProviderNames();
  return names[String(providerId)];
}

export function deleteProviderName(providerId: number | string): void {
  if (typeof window === 'undefined') return;
  
  const names = getStoredProviderNames();
  delete names[String(providerId)];
  
  try {
    localStorage.setItem(PROVIDER_NAMES_KEY, JSON.stringify(names));
  } catch (error) {
    console.error('Failed to delete provider name:', error);
  }
}