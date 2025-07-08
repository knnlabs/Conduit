import { GET as createGET, POST as createPOST } from '@knn_labs/conduit-admin-client/nextjs';

export const GET = createGET(async ({ client, searchParams }) => {
  // List all provider metadata
  const result = await client.providers.list();

  // Convert to array and apply filters
  const resultArray = Array.from(result);
  const providerName = searchParams.get('providerName');
  
  const filteredResult = resultArray.filter(provider => {
    if (providerName) {
      return provider.providerName.toLowerCase().includes(providerName.toLowerCase());
    }
    return true;
  });

  // Return the filtered result directly
  return filteredResult;
});

export const POST = createPOST(async ({ client, body }) => {
  // Create provider credential (not provider metadata)
  const providerData: any = {
    providerName: body.providerName,
    apiKey: body.apiKey,
    organizationId: body.organizationId,
    additionalConfig: body.additionalSettings ? JSON.stringify(body.additionalSettings) : body.additionalConfig,
    isEnabled: body.isEnabled ?? true,
  };
  
  // Only add apiEndpoint if it has a value
  const endpoint = body.apiUrl || body.apiEndpoint;
  if (endpoint) {
    providerData.apiEndpoint = endpoint;
  }
  
  const result = await client.providers.create(providerData);

  // Test connection if requested
  if (body.testConnection) {
    try {
      const testData: any = {
        providerName: body.providerName,
        apiKey: body.apiKey,
        organizationId: body.organizationId,
        additionalConfig: body.additionalSettings ? JSON.stringify(body.additionalSettings) : body.additionalConfig,
      };
      
      // Only add apiEndpoint if it has a value
      if (endpoint) {
        testData.apiEndpoint = endpoint;
      }
      
      await client.providers.testConnection(testData);
    } catch (testError) {
      // Log test failure but still return created provider
      console.warn('Provider credential created but connection test failed:', testError);
    }
  }

  // Return the SDK response with 201 status
  return new Response(JSON.stringify(result), {
    status: 201,
    headers: { 'Content-Type': 'application/json' },
  });
});