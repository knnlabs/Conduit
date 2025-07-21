# Conduit SDK Migration Guide

This guide shows real before/after examples from the WebUI codebase, demonstrating how to migrate from raw fetch calls to the new SDK patterns.

## Table of Contents

1. [Provider Management](#provider-management)
2. [Virtual Keys CRUD](#virtual-keys-crud)
3. [Model Mappings](#model-mappings)
4. [File Uploads](#file-uploads)
5. [Error Handling](#error-handling)
6. [Loading States](#loading-states)
7. [Form Submissions](#form-submissions)
8. [Real-time Updates](#real-time-updates)
9. [Pagination](#pagination)
10. [Authentication](#authentication)

## Provider Management

### Listing Providers

**Before: 68 lines with manual state management**

```typescript
// ConduitLLM.WebUI/app/hooks/useProviders.ts (OLD)
import { useState, useEffect } from 'react';

interface Provider {
  id: string;
  name: string;
  type: string;
  isEnabled: boolean;
}

export function useProviders() {
  const [providers, setProviders] = useState<Provider[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchProviders = async () => {
      try {
        setLoading(true);
        setError(null);
        
        const response = await fetch('/api/admin/providers', {
          headers: {
            'Content-Type': 'application/json',
          },
        });

        if (!response.ok) {
          if (response.status === 401) {
            throw new Error('Unauthorized. Please check your admin key.');
          }
          if (response.status === 500) {
            throw new Error('Server error. Please try again later.');
          }
          throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        
        // Handle different response formats
        if (Array.isArray(data)) {
          setProviders(data);
        } else if (data.items && Array.isArray(data.items)) {
          setProviders(data.items);
        } else if (data.providers && Array.isArray(data.providers)) {
          setProviders(data.providers);
        } else {
          throw new Error('Unexpected response format');
        }
      } catch (err) {
        console.error('Error fetching providers:', err);
        setError(err instanceof Error ? err.message : 'An error occurred');
      } finally {
        setLoading(false);
      }
    };

    fetchProviders();
  }, []);

  const refetch = () => {
    setLoading(true);
    fetchProviders();
  };

  return { providers, loading, error, refetch };
}
```

**After: 1 line with built-in everything**

```typescript
// ConduitLLM.WebUI/app/hooks/useProviders.ts (NEW)
import { useProviders } from '@conduit/admin-client/react';
export { useProviders };
```

### Updating Provider

**Before: 45 lines with manual error handling**

```typescript
// ConduitLLM.WebUI/app/components/providers/ProviderSettings.tsx (OLD)
async function updateProvider(providerId: string, data: any) {
  const [updating, setUpdating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  
  const handleUpdate = async () => {
    try {
      setUpdating(true);
      setError(null);
      
      const response = await fetch(`/api/admin/providers/${providerId}`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(data),
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => null);
        throw new Error(
          errorData?.message || 
          errorData?.error || 
          `Failed to update provider: ${response.status}`
        );
      }

      const updated = await response.json();
      
      // Manually update cache
      queryClient.setQueryData(['providers'], (old: any) => {
        if (!old) return old;
        return old.map((p: any) => p.id === providerId ? updated : p);
      });
      
      toast.success('Provider updated successfully');
      return updated;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Update failed';
      setError(message);
      toast.error(message);
      throw err;
    } finally {
      setUpdating(false);
    }
  };
  
  return { handleUpdate, updating, error };
}
```

**After: 3 lines with automatic cache updates**

```typescript
// ConduitLLM.WebUI/app/components/providers/ProviderSettings.tsx (NEW)
import { useUpdateProvider } from '@conduit/admin-client/react';

const updateProvider = useUpdateProvider();
await updateProvider.mutateAsync({ providerId, data });
```

## Virtual Keys CRUD

### Creating Virtual Key

**Before: 52 lines with form handling**

```typescript
// ConduitLLM.WebUI/app/components/virtual-keys/CreateKeyDialog.tsx (OLD)
export function CreateKeyDialog({ onSuccess }: { onSuccess: () => void }) {
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  
  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setLoading(true);
    setError(null);
    
    const formData = new FormData(event.currentTarget);
    const data = {
      name: formData.get('name') as string,
      providers: formData.getAll('providers') as string[],
      maxRequestsPerMinute: formData.get('rateLimit') 
        ? parseInt(formData.get('rateLimit') as string) 
        : undefined,
      metadata: formData.get('metadata') 
        ? JSON.parse(formData.get('metadata') as string) 
        : undefined,
    };
    
    try {
      const response = await fetch('/api/admin/virtual-keys', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(data),
      });
      
      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.message || 'Failed to create virtual key');
      }
      
      const created = await response.json();
      
      // Show success message with key
      toast.success(`Virtual key created: ${created.key}`);
      
      setOpen(false);
      onSuccess();
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Creation failed';
      setError(message);
      toast.error(message);
    } finally {
      setLoading(false);
    }
  };
  
  // ... form JSX
}
```

**After: 8 lines with automatic error handling**

```typescript
// ConduitLLM.WebUI/app/components/virtual-keys/CreateKeyDialog.tsx (NEW)
import { useCreateVirtualKey } from '@conduit/admin-client/react';

export function CreateKeyDialog({ onSuccess }: { onSuccess: () => void }) {
  const createKey = useCreateVirtualKey();
  
  const handleSubmit = async (data: any) => {
    const result = await createKey.mutateAsync(data);
    toast.success(`Virtual key created: ${result.key}`);
    onSuccess();
  };
  
  // ... form JSX using createKey.isPending for loading state
}
```

### Deleting Virtual Key with Confirmation

**Before: 38 lines with confirmation dialog**

```typescript
// ConduitLLM.WebUI/app/components/virtual-keys/DeleteKeyButton.tsx (OLD)
export function DeleteKeyButton({ keyId, keyName }: Props) {
  const [showConfirm, setShowConfirm] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const router = useRouter();
  
  const handleDelete = async () => {
    try {
      setDeleting(true);
      
      const response = await fetch(`/api/admin/virtual-keys/${keyId}`, {
        method: 'DELETE',
      });
      
      if (!response.ok) {
        if (response.status === 404) {
          throw new Error('Virtual key not found');
        }
        throw new Error('Failed to delete virtual key');
      }
      
      toast.success('Virtual key deleted successfully');
      
      // Invalidate cache
      queryClient.invalidateQueries({ queryKey: ['virtualKeys'] });
      
      // Redirect to list
      router.push('/virtual-keys');
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Delete failed');
    } finally {
      setDeleting(false);
      setShowConfirm(false);
    }
  };
  
  return (
    <>
      <Button onClick={() => setShowConfirm(true)} variant="destructive">
        Delete
      </Button>
      {/* Confirmation dialog */}
    </>
  );
}
```

**After: 12 lines with optimistic updates**

```typescript
// ConduitLLM.WebUI/app/components/virtual-keys/DeleteKeyButton.tsx (NEW)
import { useDeleteVirtualKey } from '@conduit/admin-client/react';

export function DeleteKeyButton({ keyId, keyName }: Props) {
  const deleteKey = useDeleteVirtualKey();
  const router = useRouter();
  
  const handleDelete = async () => {
    await deleteKey.mutateAsync(keyId);
    toast.success('Virtual key deleted successfully');
    router.push('/virtual-keys');
  };
  
  return (
    <ConfirmDialog
      onConfirm={handleDelete}
      loading={deleteKey.isPending}
    />
  );
}
```

## Model Mappings

### Full CRUD for Model Mappings

**Before: 120+ lines across multiple files**

```typescript
// ConduitLLM.WebUI/app/api/admin/providers/[id]/model-mappings/route.ts (OLD)
export async function GET(
  request: Request,
  { params }: { params: { id: string } }
) {
  try {
    const mappings = await getMappingsFromDatabase(params.id);
    return NextResponse.json(mappings);
  } catch (error) {
    return NextResponse.json(
      { error: 'Failed to fetch mappings' },
      { status: 500 }
    );
  }
}

export async function POST(
  request: Request,
  { params }: { params: { id: string } }
) {
  try {
    const body = await request.json();
    
    // Validate input
    if (!body.requestModel || !body.actualModel) {
      return NextResponse.json(
        { error: 'Missing required fields' },
        { status: 400 }
      );
    }
    
    const mapping = await createMapping(params.id, body);
    return NextResponse.json(mapping, { status: 201 });
  } catch (error) {
    return NextResponse.json(
      { error: 'Failed to create mapping' },
      { status: 500 }
    );
  }
}

// Plus useModelMappings hook with 50+ lines
// Plus MappingForm component with 40+ lines
// Plus delete confirmation with 30+ lines
```

**After: 25 lines total with all CRUD operations**

```typescript
// ConduitLLM.WebUI/app/components/model-mappings/MappingsManager.tsx (NEW)
import {
  useModelMappings,
  useCreateModelMapping,
  useUpdateModelMapping,
  useDeleteModelMapping
} from '@conduit/admin-client/react';

export function MappingsManager({ providerId }: { providerId: string }) {
  const { data: mappings } = useModelMappings(providerId);
  const createMapping = useCreateModelMapping();
  const updateMapping = useUpdateModelMapping();
  const deleteMapping = useDeleteModelMapping();
  
  return (
    <div>
      <MappingsList
        mappings={mappings}
        onUpdate={(id, data) => updateMapping.mutate({ providerId, id, data })}
        onDelete={(id) => deleteMapping.mutate({ providerId, id })}
      />
      <CreateMappingForm
        onSubmit={(data) => createMapping.mutate({ providerId, ...data })}
        isLoading={createMapping.isPending}
      />
    </div>
  );
}
```

## File Uploads

### Image Upload with Progress

**Before: 85 lines with manual progress tracking**

```typescript
// ConduitLLM.WebUI/app/components/media/ImageUpload.tsx (OLD)
export function ImageUpload({ onUpload }: { onUpload: (url: string) => void }) {
  const [uploading, setUploading] = useState(false);
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState<string | null>(null);
  
  const handleUpload = async (file: File) => {
    if (!file.type.startsWith('image/')) {
      setError('Please select an image file');
      return;
    }
    
    if (file.size > 10 * 1024 * 1024) {
      setError('File size must be less than 10MB');
      return;
    }
    
    setUploading(true);
    setError(null);
    setProgress(0);
    
    const formData = new FormData();
    formData.append('file', file);
    
    try {
      const xhr = new XMLHttpRequest();
      
      xhr.upload.addEventListener('progress', (e) => {
        if (e.lengthComputable) {
          const percentComplete = (e.loaded / e.total) * 100;
          setProgress(Math.round(percentComplete));
        }
      });
      
      xhr.addEventListener('load', () => {
        if (xhr.status === 200) {
          const response = JSON.parse(xhr.responseText);
          onUpload(response.url);
          toast.success('Image uploaded successfully');
        } else {
          throw new Error(`Upload failed with status ${xhr.status}`);
        }
      });
      
      xhr.addEventListener('error', () => {
        throw new Error('Network error during upload');
      });
      
      xhr.open('POST', '/api/admin/media/upload');
      xhr.send(formData);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Upload failed';
      setError(message);
      toast.error(message);
    } finally {
      setUploading(false);
      setProgress(0);
    }
  };
  
  return (
    <div>
      <input
        type="file"
        accept="image/*"
        onChange={(e) => e.target.files?.[0] && handleUpload(e.target.files[0])}
        disabled={uploading}
      />
      {uploading && <ProgressBar value={progress} />}
      {error && <div className="error">{error}</div>}
    </div>
  );
}
```

**After: 20 lines with built-in progress**

```typescript
// ConduitLLM.WebUI/app/components/media/ImageUpload.tsx (NEW)
import { useUploadMedia } from '@conduit/admin-client/react';

export function ImageUpload({ onUpload }: { onUpload: (url: string) => void }) {
  const uploadMedia = useUploadMedia();
  
  const handleUpload = async (file: File) => {
    const result = await uploadMedia.mutateAsync({
      file,
      onProgress: (progress) => {
        // Progress is automatically tracked by the mutation
      }
    });
    
    onUpload(result.url);
    toast.success('Image uploaded successfully');
  };
  
  return (
    <div>
      <input
        type="file"
        accept="image/*"
        onChange={(e) => e.target.files?.[0] && handleUpload(e.target.files[0])}
        disabled={uploadMedia.isPending}
      />
      {uploadMedia.isPending && <ProgressBar value={uploadMedia.progress} />}
      {uploadMedia.error && <div className="error">{uploadMedia.error.message}</div>}
    </div>
  );
}
```

## Error Handling

### Global Error Handling

**Before: Error handling scattered across components**

```typescript
// ConduitLLM.WebUI/app/components/ErrorBoundary.tsx (OLD)
// Multiple try-catch blocks in every component
// Inconsistent error messages
// No centralized error logging

// Example from VirtualKeysList component:
try {
  const response = await fetch('/api/admin/virtual-keys');
  if (!response.ok) {
    if (response.status === 401) {
      setError('Unauthorized. Please login again.');
      router.push('/login');
    } else if (response.status === 403) {
      setError('You do not have permission to view this resource.');
    } else if (response.status === 500) {
      setError('Server error. Please try again later.');
    } else {
      setError(`Error: ${response.status}`);
    }
  }
} catch (err) {
  if (err instanceof TypeError && err.message.includes('fetch')) {
    setError('Network error. Please check your connection.');
  } else {
    setError('An unexpected error occurred.');
  }
  console.error('Fetch error:', err);
}
```

**After: Centralized error handling with React Query**

```typescript
// ConduitLLM.WebUI/app/providers.tsx (NEW)
import { QueryClient } from '@tanstack/react-query';

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: (failureCount, error: any) => {
        // Don't retry on 4xx errors
        if (error?.status >= 400 && error?.status < 500) {
          return false;
        }
        return failureCount < 3;
      },
    },
    mutations: {
      onError: (error: any) => {
        // Global error handling
        if (error?.status === 401) {
          toast.error('Session expired. Please login again.');
          router.push('/login');
        } else if (error?.status === 403) {
          toast.error('Access denied');
        } else if (error?.message) {
          toast.error(error.message);
        } else {
          toast.error('An unexpected error occurred');
        }
      },
    },
  },
});

// Components now just use the hooks without error handling boilerplate
const { data, error } = useVirtualKeys(); // Error handling is automatic
```

## Loading States

### Complex Loading States

**Before: Manual loading state management**

```typescript
// ConduitLLM.WebUI/app/components/dashboard/Dashboard.tsx (OLD)
export function Dashboard() {
  const [loadingProviders, setLoadingProviders] = useState(true);
  const [loadingKeys, setLoadingKeys] = useState(true);
  const [loadingStats, setLoadingStats] = useState(true);
  const [providers, setProviders] = useState([]);
  const [keys, setKeys] = useState([]);
  const [stats, setStats] = useState(null);
  
  useEffect(() => {
    // Fetch providers
    fetch('/api/admin/providers')
      .then(res => res.json())
      .then(data => {
        setProviders(data);
        setLoadingProviders(false);
      });
    
    // Fetch keys
    fetch('/api/admin/virtual-keys')
      .then(res => res.json())
      .then(data => {
        setKeys(data);
        setLoadingKeys(false);
      });
    
    // Fetch stats
    fetch('/api/admin/stats')
      .then(res => res.json())
      .then(data => {
        setStats(data);
        setLoadingStats(false);
      });
  }, []);
  
  const isLoading = loadingProviders || loadingKeys || loadingStats;
  
  if (isLoading) {
    return <DashboardSkeleton />;
  }
  
  return (
    <div>
      <ProvidersList providers={providers} />
      <KeysList keys={keys} />
      <StatsChart stats={stats} />
    </div>
  );
}
```

**After: Declarative loading states with Suspense**

```typescript
// ConduitLLM.WebUI/app/components/dashboard/Dashboard.tsx (NEW)
import { Suspense } from 'react';
import { useProviders, useVirtualKeys, useStats } from '@conduit/admin-client/react';

function DashboardContent() {
  const { data: providers } = useProviders({ suspense: true });
  const { data: keys } = useVirtualKeys({ suspense: true });
  const { data: stats } = useStats({ suspense: true });
  
  return (
    <div>
      <ProvidersList providers={providers} />
      <KeysList keys={keys} />
      <StatsChart stats={stats} />
    </div>
  );
}

export function Dashboard() {
  return (
    <Suspense fallback={<DashboardSkeleton />}>
      <DashboardContent />
    </Suspense>
  );
}
```

## Form Submissions

### Complex Form with Validation

**Before: 95 lines with manual validation**

```typescript
// ConduitLLM.WebUI/app/components/providers/ProviderConfigForm.tsx (OLD)
export function ProviderConfigForm({ provider, onSave }: Props) {
  const [formData, setFormData] = useState({
    name: provider?.name || '',
    apiKey: '',
    baseUrl: provider?.baseUrl || '',
    maxRetries: provider?.maxRetries || 3,
    timeout: provider?.timeout || 30000,
  });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);
  
  const validate = () => {
    const newErrors: Record<string, string> = {};
    
    if (!formData.name.trim()) {
      newErrors.name = 'Name is required';
    }
    
    if (!formData.apiKey && !provider) {
      newErrors.apiKey = 'API key is required for new providers';
    }
    
    if (formData.baseUrl && !isValidUrl(formData.baseUrl)) {
      newErrors.baseUrl = 'Invalid URL format';
    }
    
    if (formData.maxRetries < 0 || formData.maxRetries > 10) {
      newErrors.maxRetries = 'Max retries must be between 0 and 10';
    }
    
    if (formData.timeout < 1000 || formData.timeout > 300000) {
      newErrors.timeout = 'Timeout must be between 1 and 300 seconds';
    }
    
    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };
  
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!validate()) {
      return;
    }
    
    setSaving(true);
    
    try {
      const url = provider 
        ? `/api/admin/providers/${provider.id}`
        : '/api/admin/providers';
      
      const response = await fetch(url, {
        method: provider ? 'PUT' : 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(formData),
      });
      
      if (!response.ok) {
        throw new Error('Failed to save provider');
      }
      
      const saved = await response.json();
      toast.success('Provider saved successfully');
      onSave(saved);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Save failed');
    } finally {
      setSaving(false);
    }
  };
  
  // ... form JSX with error display
}
```

**After: 30 lines with react-hook-form integration**

```typescript
// ConduitLLM.WebUI/app/components/providers/ProviderConfigForm.tsx (NEW)
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { providerSchema } from '@conduit/admin-client/schemas';
import { useCreateProvider, useUpdateProvider } from '@conduit/admin-client/react';

export function ProviderConfigForm({ provider, onSave }: Props) {
  const createProvider = useCreateProvider();
  const updateProvider = useUpdateProvider();
  
  const form = useForm({
    resolver: zodResolver(providerSchema),
    defaultValues: provider || {
      name: '',
      apiKey: '',
      baseUrl: '',
      maxRetries: 3,
      timeout: 30000,
    },
  });
  
  const onSubmit = async (data: any) => {
    const mutation = provider ? updateProvider : createProvider;
    const result = await mutation.mutateAsync(
      provider ? { id: provider.id, data } : data
    );
    toast.success('Provider saved successfully');
    onSave(result);
  };
  
  return (
    <Form form={form} onSubmit={onSubmit}>
      {/* Auto-generated form fields with validation */}
    </Form>
  );
}
```

## Real-time Updates

### SignalR Integration

**Before: 75 lines of SignalR boilerplate**

```typescript
// ConduitLLM.WebUI/app/hooks/useNavigationState.ts (OLD)
import * as signalR from '@microsoft/signalr';

export function useNavigationState(virtualKey: string) {
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
  const [connected, setConnected] = useState(false);
  const [state, setState] = useState(null);
  const [error, setError] = useState<string | null>(null);
  
  useEffect(() => {
    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/navigation-state')
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          if (retryContext.elapsedMilliseconds < 60000) {
            return Math.random() * 10000;
          } else {
            return null;
          }
        }
      })
      .configureLogging(signalR.LogLevel.Warning)
      .build();
    
    newConnection.onreconnecting((error) => {
      setConnected(false);
      setError('Connection lost. Reconnecting...');
    });
    
    newConnection.onreconnected((connectionId) => {
      setConnected(true);
      setError(null);
      // Re-join groups
      newConnection.invoke('JoinGroup', virtualKey);
    });
    
    newConnection.on('StateUpdate', (newState) => {
      setState(newState);
    });
    
    newConnection.start()
      .then(() => {
        setConnected(true);
        setConnection(newConnection);
        return newConnection.invoke('JoinGroup', virtualKey);
      })
      .catch((err) => {
        setError(err.toString());
      });
    
    return () => {
      if (newConnection.state === signalR.HubConnectionState.Connected) {
        newConnection.invoke('LeaveGroup', virtualKey)
          .finally(() => newConnection.stop());
      }
    };
  }, [virtualKey]);
  
  return { state, connected, error };
}
```

**After: 10 lines with built-in reconnection**

```typescript
// ConduitLLM.WebUI/app/hooks/useNavigationState.ts (NEW)
import { useRealtimeConnection } from '@conduit/core-client/react';

export function useNavigationState(virtualKey: string) {
  const { data: state, isConnected, error } = useRealtimeConnection({
    hub: 'navigation-state',
    group: virtualKey,
    events: {
      StateUpdate: (newState) => newState,
    },
  });
  
  return { state, connected: isConnected, error };
}
```

## Pagination

### Infinite Scroll Implementation

**Before: 90 lines with manual pagination**

```typescript
// ConduitLLM.WebUI/app/components/logs/LogsList.tsx (OLD)
export function LogsList() {
  const [logs, setLogs] = useState<Log[]>([]);
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(true);
  const [loading, setLoading] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const observerRef = useRef<IntersectionObserver | null>(null);
  const lastElementRef = useRef<HTMLDivElement | null>(null);
  
  const fetchLogs = async (pageNum: number, append = false) => {
    if (append) {
      setLoadingMore(true);
    } else {
      setLoading(true);
    }
    
    try {
      const response = await fetch(`/api/admin/logs?page=${pageNum}&limit=50`);
      const data = await response.json();
      
      if (append) {
        setLogs(prev => [...prev, ...data.items]);
      } else {
        setLogs(data.items);
      }
      
      setHasMore(data.hasMore);
      setPage(pageNum);
    } catch (err) {
      console.error('Failed to fetch logs:', err);
    } finally {
      setLoading(false);
      setLoadingMore(false);
    }
  };
  
  useEffect(() => {
    fetchLogs(1);
  }, []);
  
  useEffect(() => {
    if (loading || loadingMore || !hasMore) return;
    
    const callback = (entries: IntersectionObserverEntry[]) => {
      if (entries[0].isIntersecting && hasMore) {
        fetchLogs(page + 1, true);
      }
    };
    
    observerRef.current = new IntersectionObserver(callback);
    
    if (lastElementRef.current) {
      observerRef.current.observe(lastElementRef.current);
    }
    
    return () => {
      if (observerRef.current) {
        observerRef.current.disconnect();
      }
    };
  }, [loading, loadingMore, hasMore, page]);
  
  if (loading) return <div>Loading...</div>;
  
  return (
    <div>
      {logs.map((log, index) => (
        <div 
          key={log.id}
          ref={index === logs.length - 1 ? lastElementRef : null}
        >
          {log.message}
        </div>
      ))}
      {loadingMore && <div>Loading more...</div>}
    </div>
  );
}
```

**After: 20 lines with infinite query**

```typescript
// ConduitLLM.WebUI/app/components/logs/LogsList.tsx (NEW)
import { useInfiniteLogs } from '@conduit/admin-client/react';
import { useInView } from 'react-intersection-observer';

export function LogsList() {
  const {
    data,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
    isLoading,
  } = useInfiniteLogs({ limit: 50 });
  
  const { ref } = useInView({
    onChange: (inView) => {
      if (inView && hasNextPage) {
        fetchNextPage();
      }
    },
  });
  
  if (isLoading) return <div>Loading...</div>;
  
  const logs = data?.pages.flatMap(page => page.items) ?? [];
  
  return (
    <div>
      {logs.map((log) => (
        <div key={log.id}>{log.message}</div>
      ))}
      <div ref={ref}>
        {isFetchingNextPage && <div>Loading more...</div>}
      </div>
    </div>
  );
}
```

## Authentication

### Protected Routes

**Before: 45 lines per protected page**

```typescript
// ConduitLLM.WebUI/app/admin/page.tsx (OLD)
export default function AdminPage() {
  const [authenticated, setAuthenticated] = useState(false);
  const [loading, setLoading] = useState(true);
  const router = useRouter();
  
  useEffect(() => {
    const checkAuth = async () => {
      try {
        const response = await fetch('/api/auth/verify', {
          credentials: 'include',
        });
        
        if (!response.ok) {
          throw new Error('Not authenticated');
        }
        
        const data = await response.json();
        if (data.role !== 'admin') {
          throw new Error('Insufficient permissions');
        }
        
        setAuthenticated(true);
      } catch (err) {
        toast.error('Please login to continue');
        router.push('/login?redirect=/admin');
      } finally {
        setLoading(false);
      }
    };
    
    checkAuth();
  }, [router]);
  
  if (loading) {
    return <div>Checking authentication...</div>;
  }
  
  if (!authenticated) {
    return null;
  }
  
  return <AdminDashboard />;
}
```

**After: 5 lines with middleware**

```typescript
// ConduitLLM.WebUI/app/admin/page.tsx (NEW)
import { requireAuth } from '@conduit/admin-client/nextjs';

export default requireAuth(function AdminPage() {
  return <AdminDashboard />;
}, { role: 'admin' });
```

## Summary

### Key Improvements

1. **Code Reduction**: 80-95% less code for common operations
2. **Type Safety**: Full TypeScript support with generated types
3. **Error Handling**: Centralized and consistent
4. **Caching**: Automatic with React Query
5. **Loading States**: Built-in with Suspense support
6. **Real-time**: Simplified SignalR integration
7. **Validation**: Integrated with react-hook-form and Zod
8. **Security**: Clear separation of admin/core concerns

### Migration Checklist

- [ ] Replace fetch calls with SDK hooks
- [ ] Remove manual error handling
- [ ] Delete custom loading state management
- [ ] Use built-in pagination/infinite scroll
- [ ] Migrate forms to react-hook-form
- [ ] Implement proper error boundaries
- [ ] Add Suspense for loading states
- [ ] Use middleware for auth protection

### Next Steps

1. Start with simple list views (providers, virtual keys)
2. Migrate CRUD operations next
3. Add real-time features last
4. Test thoroughly at each step

---

Last updated: 2025-01-08