---
sidebar_position: 2
title: Virtual Keys
description: Learn how to use and manage virtual keys in Conduit
---

# Virtual Keys

Virtual keys are a core security feature in Conduit that provide controlled access to LLM providers through Conduit's API.

## What Are Virtual Keys?

Virtual keys function similarly to API keys but with enhanced capabilities for access control, rate limiting, and cost management. They allow you to:

- Create separate keys for different applications or users
- Restrict access to specific models or providers
- Set rate limits on API usage
- Implement budget controls for cost management
- Track usage and costs per key

## Creating Virtual Keys

Virtual keys can be created through the Conduit Web UI:

1. Navigate to **Virtual Keys** in the sidebar
2. Click **Create New Key**
3. Provide a name and description
4. Configure permissions and rate limits
5. Click **Create**

## Virtual Key Anatomy

A virtual key looks like this: `condt_abcdefg123456...`

All virtual keys begin with the `condt_` prefix, making them easily identifiable. The rest of the key is a secure random string that cannot be derived from any other information.

## Virtual Key Permissions

When creating a virtual key, you can set various permissions:

- **Model Access** - Limit which models the key can access
- **Operation Types** - Restrict to certain operations (Chat, Completions, Embeddings)
- **Rate Limits** - Set maximum requests per minute/hour/day
- **Budget Limits** - Set maximum spending amounts
- **IP Restrictions** - Limit usage to specific IP addresses (optional)

## Using Virtual Keys

To use a virtual key in API requests, include it in the Authorization header:

```bash
curl http://localhost:5000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer condt_your_virtual_key" \
  -d '{
    "model": "gpt-3.5-turbo",
    "messages": [{"role": "user", "content": "Hello!"}]
  }'
```

## Virtual Key Management

You can manage existing virtual keys through the Web UI:

- **View Usage** - See request and cost metrics
- **Edit Permissions** - Update access controls and rate limits
- **Regenerate** - Create a new key while maintaining the same settings
- **Disable/Enable** - Temporarily suspend access without deleting the key
- **Delete** - Permanently remove the key

## Best Practices

- Create separate keys for different applications or users
- Use descriptive names to easily identify keys
- Set appropriate rate limits to prevent accidental excessive usage
- Regularly review key usage and revoke unused keys
- Consider using short expiration times for sensitive use cases

## Next Steps

- Learn about [Budget Management](../guides/budget-management) for cost control
- Explore [Model Routing](model-routing) to understand how requests are directed to providers
- See the [API Reference](../api-reference/overview) for detailed endpoint documentation