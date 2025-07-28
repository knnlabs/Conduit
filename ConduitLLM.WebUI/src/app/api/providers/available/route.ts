import { NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { PROVIDER_DISPLAY_NAMES, PROVIDER_CATEGORIES, ProviderCategory } from '@/lib/constants/providers';

// GET /api/providers/available - Get available provider types that can be added
export async function GET(request: Request) {
  try {
    const adminClient = getServerAdminClient();
    const providersService = adminClient.providers;
    
    if (!providersService || typeof providersService.getAvailableProviderTypes !== 'function') {
      throw new Error('Providers service not available');
    }
    
    // Get URL parameters to filter by category if needed
    const { searchParams } = new URL(request.url);
    const categoryFilter = searchParams.get('category') as ProviderCategory | null;
    
    // Get available provider types from SDK
    let availableTypes = await providersService.getAvailableProviderTypes();
    
    // Filter by category if requested
    if (categoryFilter) {
      availableTypes = availableTypes.filter(type => {
        const categories = PROVIDER_CATEGORIES[type];
        return categories?.includes(categoryFilter);
      });
    }
    
    // For LLM providers modal, filter to only chat/embedding providers
    const llmOnly = searchParams.get('llmOnly') === 'true';
    if (llmOnly) {
      availableTypes = availableTypes.filter(type => {
        const categories = PROVIDER_CATEGORIES[type];
        return categories?.includes(ProviderCategory.Chat) || 
               categories?.includes(ProviderCategory.Embedding);
      });
    }
    
    // Convert to select options format
    const availableOptions = availableTypes.map(type => ({
      value: type.toString(),
      label: PROVIDER_DISPLAY_NAMES[type] || `Provider ${type}`,
    }));
    
    return NextResponse.json(availableOptions);
  } catch (error) {
    console.error('Error fetching available providers:', error);
    return handleSDKError(error);
  }
}