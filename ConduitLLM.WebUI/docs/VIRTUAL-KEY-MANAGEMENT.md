# Virtual Key Management

## Overview

The WebUI automatically manages a virtual key for Core API operations. This key is created on first login and reused for all subsequent sessions.

## How It Works

### 1. Initial Setup
When an admin first logs into the WebUI:
- System checks for existing "WebUI Admin Access" virtual key
- If not found, creates a new virtual key automatically
- Key is stored in encrypted storage when available

### 2. Key Configuration
The WebUI virtual key is configured with:
```typescript
{
  name: "WebUI Admin Access",
  description: "Automatically managed virtual key for WebUI Core API access",
  providers: ["*"], // Access to all providers
  rateLimits: {
    requestsPerMinute: 100,
    requestsPerHour: 2000,
    tokensPerMinute: 100000,
    tokensPerHour: 2000000
  },
  metadata: {
    source: "WebUI",
    autoCreated: true,
    createdAt: new Date().toISOString()
  }
}
```

### 3. Storage and Retrieval

#### Client-Side Storage
```typescript
// Stored in auth store (Zustand)
interface AuthState {
  virtualKey: string | null;
  // ... other auth state
}

// Encrypted in session/local storage when available
const storedAuth = {
  virtualKey: "vk_abc123...",
  encryptedMasterKey: "...",
  expiresAt: "2024-01-01T00:00:00Z"
};
```

#### Server-Side Management
```typescript
// utils/virtualKeyManagement.ts
export async function ensureWebUIVirtualKey(adminClient: ConduitAdminClient) {
  // Check for existing key
  const existing = await getWebUIVirtualKey(adminClient);
  if (existing) return { key: existing, isNew: false };
  
  // Create new key
  const newKey = await createWebUIVirtualKey(adminClient);
  return { key: newKey.key, isNew: true };
}
```

## Security Model

### Access Control
- Virtual key only provides access to Core API operations
- Cannot perform admin operations (provider management, etc.)
- Rate limited to prevent abuse
- Automatically cleared on logout

### Key Visibility
- Virtual keys are visible in browser DevTools
- This is acceptable for admin tools
- Not suitable for public-facing applications
- Consider the security implications for your use case

## Usage in Components

### Direct SDK Usage
```typescript
// Automatically uses virtual key from auth store
import { useChatCompletion } from '@knn_labs/conduit-core-client/react-query';

function ChatComponent() {
  const { mutate } = useChatCompletion();
  
  // Virtual key is automatically included by ConduitProvider
  const handleSend = (messages) => {
    mutate({ messages });
  };
}
```

### Manual Key Access
```typescript
import { useAuthStore } from '@/stores/useAuthStore';

function MyComponent() {
  const virtualKey = useAuthStore(state => state.virtualKey);
  
  if (!virtualKey) {
    return <div>Not authenticated</div>;
  }
  
  // Use virtual key for custom operations
}
```

## Key Rotation

### Manual Rotation
If you need to rotate the virtual key:

1. Delete the existing key through the Virtual Keys UI
2. Log out and log back in
3. A new key will be automatically created

### Programmatic Rotation
```typescript
// In a server-side API route
export async function rotateWebUIVirtualKey() {
  const adminClient = getServerAdminClient();
  
  // Delete existing key
  const existing = await getWebUIVirtualKey(adminClient);
  if (existing) {
    await adminClient.virtualKeys.delete(existing.id);
  }
  
  // Create new key
  const newKey = await createWebUIVirtualKey(adminClient);
  return newKey;
}
```

## Monitoring and Debugging

### Check Virtual Key Status
```typescript
// In browser console
const authStore = window.__ZUSTAND_DEVTOOLS__.stores.get('authStore');
console.log('Virtual Key:', authStore.getState().virtualKey);
```

### View Key in Admin UI
1. Navigate to Virtual Keys page
2. Look for "WebUI Admin Access" key
3. Check usage statistics and rate limit status

### Common Issues

#### Key Not Found
- **Symptom**: "No virtual key available" errors
- **Solution**: Log out and log back in to recreate

#### Rate Limit Exceeded
- **Symptom**: 429 errors from API
- **Solution**: Check rate limit configuration, adjust if needed

#### Key Deleted Manually
- **Symptom**: API calls fail with 401
- **Solution**: Log out and log back in to recreate

## Best Practices

1. **Don't Share Keys**: Virtual keys are tied to the WebUI instance
2. **Monitor Usage**: Regularly check key usage in the admin panel
3. **Rotate Periodically**: Consider rotating keys monthly
4. **Handle Errors**: Implement proper error handling for key failures
5. **Secure Storage**: Ensure HTTPS in production

## Configuration Options

### Environment Variables
```bash
# Optional: Custom rate limits (defaults shown)
WEBUI_VIRTUAL_KEY_RPM=100
WEBUI_VIRTUAL_KEY_RPH=2000
WEBUI_VIRTUAL_KEY_TPM=100000
WEBUI_VIRTUAL_KEY_TPH=2000000
```

### Custom Key Name
```typescript
// In virtualKeyManagement.ts
const WEBUI_VIRTUAL_KEY_NAME = process.env.WEBUI_KEY_NAME || "WebUI Admin Access";
```

## Future Enhancements

1. **Automatic Rotation**: Rotate keys on a schedule
2. **Multiple Keys**: Support for different permission levels
3. **Key Metrics**: Dashboard for key usage analytics
4. **Audit Logging**: Track all key operations
5. **Backup Keys**: Fallback keys for high availability