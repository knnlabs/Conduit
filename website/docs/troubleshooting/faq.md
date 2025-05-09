---
sidebar_position: 2
title: FAQ
description: Frequently asked questions about Conduit
---

# Frequently Asked Questions

## General Questions

### What is Conduit?

Conduit is a unified API gateway for Large Language Models (LLMs) that simplifies the integration of various AI services into your applications. It provides a consistent interface, allowing you to switch between different LLM providers seamlessly without changing your application code.

### Is Conduit open source?

Yes, Conduit is an open-source project. The source code is available on [GitHub](https://github.com/knnlabs/conduit) under the MIT license.

### What providers does Conduit support?

Conduit supports many providers including:
- OpenAI
- Anthropic
- Azure OpenAI
- Google Gemini
- Cohere
- Mistral
- AWS Bedrock
- Groq
- Replicate
- HuggingFace
- Ollama
- And more

### What are the system requirements for running Conduit?

Minimum requirements:
- Docker and Docker Compose (for containerized deployment)
- 2GB RAM
- 20GB disk space
- Internet connectivity to reach LLM providers

Recommended:
- 4GB+ RAM
- 50GB+ SSD storage
- Multi-core CPU

### Does Conduit work with my existing OpenAI code?

Yes, Conduit provides an OpenAI-compatible API, allowing you to use it with existing code that works with the OpenAI API. You typically only need to change the base URL and API key.

## Setup and Configuration

### How do I set up Conduit for the first time?

1. Clone the repository: `git clone https://github.com/knnlabs/conduit.git`
2. Create a `.env` file with your configuration
3. Run `docker compose up -d`
4. Access the Web UI at `http://localhost:5001`
5. Log in with your master key
6. Add provider credentials and configure model mappings

For detailed instructions, see the [Installation Guide](../getting-started/installation).

### How do I add a new provider?

1. Navigate to **Configuration > Provider Credentials** in the Web UI
2. Click **Add Provider Credential**
3. Select the provider type
4. Enter your API key and other required credentials
5. Save the configuration

### How do I create a virtual key?

1. Navigate to **Virtual Keys** in the Web UI
2. Click **Create New Key**
3. Provide a name and description
4. Set permissions and rate limits
5. Click **Create**
6. Copy the generated key (it will only be shown once)

### Can I use Conduit without Docker?

Yes, you can run Conduit directly on your system:
1. Install .NET 8 SDK
2. Clone the repository
3. Build the solution: `dotnet build`
4. Run the API: `dotnet run --project ConduitLLM.Http`
5. Run the Web UI: `dotnet run --project ConduitLLM.WebUI`

### How do I upgrade Conduit to a newer version?

For Docker deployments:
1. Pull the latest code: `git pull`
2. Rebuild and restart containers: `docker compose down && docker compose up -d --build`

For direct deployments:
1. Pull the latest code: `git pull`
2. Rebuild the solution: `dotnet build`
3. Restart the services

### How do I reset my master key?

1. Stop Conduit: `docker compose down`
2. Update the `CONDUIT_MASTER_KEY` in your `.env` file
3. Restart Conduit: `docker compose up -d`

## Usage

### How do I switch between different LLM providers?

There are several ways to switch providers:
1. **Model Mappings**: Change the provider in your model mapping configuration
2. **Routing Strategies**: Set up routing rules to automatically select providers
3. **Request-Level**: Specify a provider override in individual requests

### How does Conduit handle provider failures?

Conduit includes fallback mechanisms that can automatically route requests to alternative providers when a primary provider fails. This behavior is configurable through the routing settings.

### Can I use Conduit with local models?

Yes, Conduit supports local LLM deployments through:
- Ollama integration
- Custom provider configuration for local API endpoints
- Direct integration with local model servers

### How does caching work in Conduit?

Conduit can cache responses from LLM providers to improve performance and reduce costs:
1. Identical requests generate a cache key
2. If a response exists in the cache, it's returned immediately
3. Otherwise, the request is sent to the provider and the response is cached
4. Cache TTL (time-to-live) controls how long responses are stored

### What is the maximum number of virtual keys I can create?

There is no hard limit on the number of virtual keys. However, for performance reasons, we recommend keeping the number of active keys under a few thousand.

## Cost and Performance

### Does Conduit add latency to requests?

Conduit adds minimal latency (typically 10-50ms) to requests. This overhead is usually negligible compared to the response time of LLM providers (often 500ms-5s). Enabling caching can significantly reduce latency for repeated requests.

### How does Conduit help with cost management?

Conduit provides several cost management features:
1. **Budget Limits**: Set spending caps for virtual keys
2. **Cost Tracking**: Monitor usage across providers
3. **Least Cost Routing**: Automatically select the most economical provider
4. **Caching**: Avoid paying for repeated identical requests
5. **Usage Analytics**: Identify opportunities for optimization

### How accurate is the token counting?

Conduit's token counting is highly accurate for most models, typically within 1-2% of the provider's count. For OpenAI models, Conduit uses the same tokenizer (tiktoken) that OpenAI uses.

### What database does Conduit use?

By default, Conduit uses SQLite for simplicity. For production deployments with high throughput, you can configure it to use PostgreSQL.

### Can Conduit handle high traffic?

Yes, Conduit is designed to handle high traffic loads. For improved performance in high-traffic scenarios:
1. Use Redis for caching
2. Configure PostgreSQL for the database
3. Set up multiple API instances behind a load balancer
4. Optimize rate limits and routing strategies

## Security

### Is communication between Conduit and providers encrypted?

Yes, all communication between Conduit and LLM providers uses HTTPS/TLS encryption.

### How are provider credentials stored?

Provider credentials are stored in the database with appropriate security measures. For additional security, you can use environment variables instead of storing credentials in the database.

### Can I restrict virtual keys to specific IP addresses?

Yes, you can configure IP restrictions for virtual keys in the Web UI under the key's advanced settings.

### Does Conduit support rate limiting?

Yes, Conduit supports rate limiting at both the global level and per virtual key. You can configure limits based on requests per minute, hour, or day.

### Can I audit usage of virtual keys?

Yes, Conduit provides detailed logging of all requests, including which virtual key was used, the requested model, and usage statistics. These logs are accessible through the Web UI.

## Troubleshooting

### Why am I getting "Model not found" errors?

This usually happens because:
1. The model name in your request doesn't match any configured model mapping
2. The virtual key doesn't have permission to access the model
3. The provider for the model is not properly configured

Check your model mappings in the Web UI under **Configuration > Model Mappings**.

### Why are my requests timing out?

Request timeouts can occur due to:
1. Provider service issues
2. Network connectivity problems
3. Request complexity (very large prompts)
4. Insufficient timeout configuration

Try increasing the timeout settings or using a fallback configuration.

### How do I fix "Rate limit exceeded" errors?

If you're encountering rate limit errors:
1. Check if the virtual key has hit its configured rate limit
2. Verify if the provider itself is rate limiting your account
3. Implement request batching or throttling in your application
4. Consider using multiple provider accounts

### Why isn't caching working?

If caching isn't working as expected:
1. Verify that caching is enabled in **Configuration > Caching**
2. Check the cache provider configuration (Redis or in-memory)
3. Ensure the cache key generation is working correctly
4. Check if requests include the `no_cache` parameter

### How do I fix database migration errors?

For database migration issues:
1. Backup your data
2. Check database permissions
3. Try manually running migrations:
   ```bash
   dotnet ef database update --project ConduitLLM.Configuration
   ```
4. For persistent issues, consider recreating the database

## Extensions and Customization

### Can I add custom providers to Conduit?

Yes, you can add custom providers by:
1. Implementing the `ILLMClient` interface
2. Registering your provider in the `LLMClientFactory`
3. Adding appropriate configuration options

### Can I modify the routing algorithm?

Yes, you can create custom routing strategies by:
1. Implementing the `IModelSelectionStrategy` interface
2. Registering your strategy in the `ModelSelectionStrategyFactory`
3. Selecting your strategy in the routing configuration

### How can I contribute to Conduit?

You can contribute to Conduit by:
1. Submitting bug reports and feature requests on GitHub
2. Creating pull requests for bug fixes or new features
3. Improving documentation
4. Sharing your experiences and use cases

See the [Contributing Guide](https://github.com/knnlabs/conduit/blob/main/CONTRIBUTING.md) for details.

### Are there any webhooks or events I can subscribe to?

Conduit supports webhook notifications for various events:
1. Provider health status changes
2. Budget alerts
3. Error notifications
4. Request logging (optional)

Configure webhooks in the Web UI under **Configuration > Notifications**.

### Can I use Conduit in a microservices architecture?

Yes, Conduit works well in a microservices architecture:
1. Deploy Conduit as a standalone service
2. Configure multiple instances for high availability
3. Use a shared Redis cache for consistency
4. Implement authentication and routing as needed