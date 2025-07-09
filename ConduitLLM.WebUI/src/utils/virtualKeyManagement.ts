import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';
import { CreateVirtualKeyRequest, VirtualKeyDto } from '@knn_labs/conduit-admin-client';

const WEBUI_VIRTUAL_KEY_NAME = 'WebUI Internal Key';
const WEBUI_VIRTUAL_KEY_SETTING = 'WebUI_VirtualKey';

export interface EnsureWebUIVirtualKeyResult {
  key: string;
  isNew: boolean;
}

/**
 * Ensures that a WebUI virtual key exists and returns it.
 * If the key doesn't exist, it creates one automatically.
 */
export async function ensureWebUIVirtualKey(
  adminClient: ConduitAdminClient
): Promise<EnsureWebUIVirtualKeyResult> {
  try {
    // Check global settings for existing key
    const existingKeyValue = await adminClient.settings.getGlobalSetting(WEBUI_VIRTUAL_KEY_SETTING);
    
    if (existingKeyValue && existingKeyValue.value) {
      // Verify the key still exists in virtual keys
      try {
        const virtualKeys = await adminClient.virtualKeys.list();
        const keyExists = virtualKeys.some(k => 
          k.keyName === WEBUI_VIRTUAL_KEY_NAME && k.isEnabled
        );
        
        if (keyExists) {
          return { key: existingKeyValue.value, isNew: false };
        }
      } catch (error) {
        console.warn('Failed to verify WebUI virtual key existence:', error);
      }
    }
    
    // Check if "WebUI Internal Key" exists but value not in settings
    const virtualKeys = await adminClient.virtualKeys.list();
    const existingWebUIKey = virtualKeys.find(k => k.keyName === WEBUI_VIRTUAL_KEY_NAME);
    
    if (existingWebUIKey) {
      // Key exists but value not in settings - this is an error state
      throw new Error(
        'WebUI Internal Key exists but value not stored in settings. ' +
        'Please delete the existing key and try again.'
      );
    }
    
    // Create new virtual key
    const createRequest: CreateVirtualKeyRequest = {
      keyName: WEBUI_VIRTUAL_KEY_NAME,
      allowedModels: '*', // Allow all models for admin operations
      maxBudget: 0, // No budget limit for internal use
      budgetDuration: 'Total',
      metadata: JSON.stringify({
        createdBy: 'WebUI',
        purpose: 'Core API operations',
        createdAt: new Date().toISOString(),
        version: '1.0'
      }),
      rateLimitRpm: 1000, // High rate limit for admin operations
      rateLimitRpd: 100000, // High daily limit
    };
    
    const newKeyResponse = await adminClient.virtualKeys.create(createRequest);
    
    // Store the key in global settings
    await adminClient.settings.createGlobalSetting({
      key: WEBUI_VIRTUAL_KEY_SETTING,
      value: newKeyResponse.virtualKey,
      description: 'Virtual key for WebUI Core API access',
      isSecret: true,
      category: 'Security'
    });
    
    return { key: newKeyResponse.virtualKey, isNew: true };
    
  } catch (error) {
    console.error('Failed to ensure WebUI virtual key:', error);
    throw new Error('Failed to create or retrieve WebUI virtual key');
  }
}

/**
 * Retrieves the WebUI virtual key from settings
 */
export async function getWebUIVirtualKey(
  adminClient: ConduitAdminClient
): Promise<string | null> {
  try {
    const setting = await adminClient.settings.getGlobalSetting(WEBUI_VIRTUAL_KEY_SETTING);
    return setting?.value || null;
  } catch (error) {
    console.error('Failed to retrieve WebUI virtual key:', error);
    return null;
  }
}

/**
 * Rotates the WebUI virtual key by creating a new one and deleting the old one
 */
export async function rotateWebUIVirtualKey(
  adminClient: ConduitAdminClient
): Promise<string> {
  try {
    // Get the current key ID if it exists
    const virtualKeys = await adminClient.virtualKeys.list();
    const existingKey = virtualKeys.find(k => k.keyName === WEBUI_VIRTUAL_KEY_NAME);
    
    // Delete the old key if it exists
    if (existingKey) {
      await adminClient.virtualKeys.deleteById(existingKey.id);
    }
    
    // Delete the setting
    try {
      await adminClient.settings.deleteGlobalSetting(WEBUI_VIRTUAL_KEY_SETTING);
    } catch (error) {
      // Setting might not exist, that's okay
      console.warn('Failed to delete old key setting:', error);
    }
    
    // Create a new key
    const result = await ensureWebUIVirtualKey(adminClient);
    
    return result.key;
  } catch (error) {
    console.error('Failed to rotate WebUI virtual key:', error);
    throw new Error('Failed to rotate WebUI virtual key');
  }
}