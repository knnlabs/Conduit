/**
 * Fetch wrapper that automatically includes credentials for API calls
 * This ensures all requests to our /api routes include authentication cookies
 */

type FetchOptions = RequestInit & {
  // Allow overriding credentials if needed
  credentials?: RequestCredentials;
};

/**
 * Enhanced fetch that automatically includes credentials for /api routes
 * @param url - The URL to fetch
 * @param options - Fetch options
 * @returns Promise<Response>
 */
export async function fetchWithCredentials(
  url: string,
  options: FetchOptions = {}
): Promise<Response> {
  // For /api routes, always include credentials unless explicitly overridden
  if (url.startsWith('/api')) {
    options.credentials = options.credentials || 'include';
  }
  
  return fetch(url, options);
}

/**
 * Convenience wrapper for JSON API calls with automatic credential inclusion
 * @param url - The URL to fetch
 * @param options - Fetch options
 * @returns Promise<T> - Parsed JSON response
 */
export async function fetchJSON<T = any>(
  url: string,
  options: FetchOptions = {}
): Promise<T> {
  const response = await fetchWithCredentials(url, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options.headers,
    },
  });
  
  if (!response.ok) {
    const error = await response.text();
    throw new Error(error || `HTTP ${response.status}: ${response.statusText}`);
  }
  
  return response.json();
}

/**
 * Export the wrapper as the default fetch for /api routes
 * Usage: import { apiFetch } from '@/lib/utils/fetch-wrapper';
 */
export const apiFetch = fetchWithCredentials;