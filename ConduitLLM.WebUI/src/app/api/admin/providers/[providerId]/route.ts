import { GET as createGET, PUT as createPUT, DELETE as createDELETE } from '@knn_labs/conduit-admin-client/nextjs';
import { NextResponse } from 'next/server';

export const GET = createGET(async ({ client, params }) => {
  const providerId = Array.isArray(params.providerId) ? params.providerId[0] : params.providerId;
  
  // Get all providers and find the specific one
  const providers = await client.providers.list();
  
  // Find the specific provider by name (providerId is actually the provider name)
  const provider = Array.from(providers).find(p => p.providerName === providerId);
  
  if (!provider) {
    return NextResponse.json(
      { error: `Provider ${providerId} not found` },
      { status: 404 }
    );
  }
  
  return provider;
});

export const PUT = createPUT(async ({ client, params, body }) => {
  const providerId = Array.isArray(params.providerId) ? params.providerId[0] : params.providerId;
  
  // Convert string ID to number for the SDK
  const numericId = parseInt(providerId, 10);
  if (isNaN(numericId)) {
    throw new Error('Invalid provider ID: must be a number');
  }
  
  // Update provider using the admin client
  await client.providers.update(numericId, body);

  return { success: true };
});

export const DELETE = createDELETE(async ({ client, params }) => {
  const providerId = Array.isArray(params.providerId) ? params.providerId[0] : params.providerId;
  
  // Delete provider using the admin client
  await client.providers.deleteById(parseInt(providerId));

  return { success: true };
});