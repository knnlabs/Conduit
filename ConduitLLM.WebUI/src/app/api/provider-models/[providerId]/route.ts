import { NextRequest, NextResponse } from 'next/server';
import { getServerCoreClient } from '@/lib/server/coreClient';

interface ProviderModel {
  id: string;
  name: string;
  capabilities?: string[];
}

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ providerId: string }> }
) {
  try {
    const { providerId } = await params;
    const forceRefresh = request.nextUrl.searchParams.get('forceRefresh') === 'true';
    
    // Convert string providerId to number as expected by SDK
    const providerIdNum = parseInt(providerId, 10);
    if (isNaN(providerIdNum)) {
      return NextResponse.json(
        { error: 'Invalid provider ID' },
        { status: 400 }
      );
    }

    // Use the Core SDK to fetch provider models
    const coreClient = await getServerCoreClient();
    const modelIds = await coreClient.providerModels.getProviderModels(
      providerIdNum,
      forceRefresh
    );
    
    // Transform to the format expected by the frontend
    const models: ProviderModel[] = modelIds.map(id => ({
      id,
      name: id, // Use the ID as the display name
      capabilities: [], // Backend doesn't provide capabilities in this endpoint
    }));

    return NextResponse.json(models);
  } catch (error) {
    // Extract error message from SDK errors
    let errorMessage = 'Failed to fetch provider models';
    let statusCode = 500;
    
    if (error instanceof Error) {
      errorMessage = error.message;
      
      // Try to parse SDK error response for better error handling
      if ('response' in error && typeof error.response === 'object' && error.response !== null) {
        const response = error.response as { status?: number; data?: { error?: string; message?: string } };
        statusCode = response.status ?? 500;
        if (response.data) {
          errorMessage = response.data.error ?? response.data.message ?? errorMessage;
        }
      }
    }
    
    console.error('Error fetching provider models:', error);
    return NextResponse.json(
      { error: errorMessage },
      { status: statusCode }
    );
  }
}