// Simple fetch wrapper - kept for potential future use
// Currently all components use native fetch() directly with credentials: 'include'

export async function fetchWithCredentials(
  url: string,
  options: RequestInit = {}
): Promise<Response> {
  return fetch(url, {
    ...options,
    credentials: 'include',
  });
}

export async function fetchJSON<T = unknown>(
  url: string,
  options: RequestInit = {}
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

// Deprecated - use native fetch() instead
export const apiFetch = fetchJSON;