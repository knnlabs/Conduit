using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Configuration;
using ConduitLLM.Providers;
using ConduitLLM.Providers.Configuration;
using ConduitLLM.Providers.Extensions;
using Moq;
using Moq.Protected;
using Xunit;
using Polly.Timeout;

namespace ConduitLLM.Tests;

public class ResiliencePolicyTests
{
    [Fact]
    public async Task OpenAIClient_ShouldRetryOnTransientErrors()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var callCount = 0;

        // Setup the mock handler to return a 503 twice and then succeed
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                Moq.Protected.ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken token) => 
            {
                callCount++;
                if (callCount <= 2)
                {
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                }
                return new HttpResponseMessage(HttpStatusCode.OK) 
                { 
                    Content = new StringContent(@"{
                        ""id"": ""test"",
                        ""object"": ""chat.completion"",
                        ""created"": 1677858242,
                        ""model"": ""gpt-4"",
                        ""choices"": [
                            {
                                ""index"": 0,
                                ""message"": {
                                    ""role"": ""assistant"",
                                    ""content"": ""Hello""
                                },
                                ""finish_reason"": ""stop""
                            }
                        ],
                        ""usage"": {
                            ""prompt_tokens"": 10,
                            ""completion_tokens"": 20,
                            ""total_tokens"": 30
                        }
                    }")
                };
            });

        // Create services with retry policy
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<RetryOptions>().Configure(options => 
        {
            options.MaxRetries = 3;
            options.InitialDelaySeconds = 0; // Set to 0 for faster test execution
            options.MaxDelaySeconds = 1;
        });

        // Setup HttpClient factory to use our mock handler
        services.AddHttpClient<OpenAIClient>()
            .ConfigurePrimaryHttpMessageHandler(() => mockHandler.Object)
            .AddPolicyHandler((provider, _) => 
            {
                var logger = provider.GetService<ILogger<OpenAIClient>>();
                var options = provider.GetService<IOptions<RetryOptions>>()?.Value ?? new RetryOptions();
                return ResiliencePolicies.GetRetryPolicy(
                    options.MaxRetries,
                    TimeSpan.FromSeconds(options.InitialDelaySeconds),
                    TimeSpan.FromSeconds(options.MaxDelaySeconds),
                    logger);
            });

        // Create client dependencies
        services.AddSingleton(new ProviderCredentials { ProviderName = "openai", ApiKey = "test-key" });

        var serviceProvider = services.BuildServiceProvider();
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(nameof(OpenAIClient));
        var logger = serviceProvider.GetRequiredService<ILogger<OpenAIClient>>();
        var credentials = serviceProvider.GetRequiredService<ProviderCredentials>();

        // Create the client using the HttpClient with retry policy
        var openAIClient = new OpenAIClient(credentials, "gpt-4", logger, httpClient: httpClient);

        // Act
        var request = new ConduitLLM.Core.Models.ChatCompletionRequest
        {
            Model = "gpt-4", 
            Messages = new List<ConduitLLM.Core.Models.Message>
            {
                new ConduitLLM.Core.Models.Message { Role = "user", Content = "Hello" }
            }
        };

        var result = await openAIClient.CreateChatCompletionAsync(request);

        // Assert
        Assert.Equal(3, callCount); // Verify we tried 3 times (2 failures + 1 success)
        Assert.NotNull(result);
        Assert.Equal("test", result.Id);
        Assert.Equal(30, result?.Usage?.TotalTokens); // Verify usage data was properly parsed
    }

    [Fact]
    public async Task OpenAIClient_ShouldTimeoutWhenRequestTakesTooLong()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();

        // Setup the mock handler to delay longer than the timeout
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                Moq.Protected.ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken token) => 
            {
                // Simulate a long-running request that exceeds the timeout
                await Task.Delay(2000, token);
                
                return new HttpResponseMessage(HttpStatusCode.OK) 
                { 
                    Content = new StringContent(@"{
                        ""id"": ""test"",
                        ""object"": ""chat.completion"",
                        ""created"": 1677858242,
                        ""model"": ""gpt-4"",
                        ""choices"": [
                            {
                                ""index"": 0,
                                ""message"": {
                                    ""role"": ""assistant"",
                                    ""content"": ""Hello""
                                },
                                ""finish_reason"": ""stop""
                            }
                        ],
                        ""usage"": {
                            ""prompt_tokens"": 10,
                            ""completion_tokens"": 20,
                            ""total_tokens"": 30
                        }
                    }")
                };
            });

        // Create services with timeout policy
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<TimeoutOptions>().Configure(options => 
        {
            options.TimeoutSeconds = 1; // Set a very short timeout for the test
            options.EnableTimeoutLogging = true;
        });

        // Setup HttpClient factory to use our mock handler with a timeout policy
        services.AddHttpClient<OpenAIClient>()
            .ConfigurePrimaryHttpMessageHandler(() => mockHandler.Object)
            .AddPolicyHandler((provider, _) => 
            {
                var logger = provider.GetService<ILogger<OpenAIClient>>();
                var options = provider.GetService<IOptions<TimeoutOptions>>()?.Value ?? new TimeoutOptions();
                return ResiliencePolicies.GetTimeoutPolicy(
                    TimeSpan.FromSeconds(options.TimeoutSeconds),
                    logger);
            });

        // Create client dependencies
        services.AddSingleton(new ProviderCredentials { ProviderName = "openai", ApiKey = "test-key" });

        var serviceProvider = services.BuildServiceProvider();
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(nameof(OpenAIClient));
        var logger = serviceProvider.GetRequiredService<ILogger<OpenAIClient>>();
        var credentials = serviceProvider.GetRequiredService<ProviderCredentials>();

        // Create the client using the HttpClient with timeout policy
        var openAIClient = new OpenAIClient(credentials, "gpt-4", logger, httpClient: httpClient);

        // Act & Assert
        var request = new ConduitLLM.Core.Models.ChatCompletionRequest
        {
            Model = "gpt-4", 
            Messages = new List<ConduitLLM.Core.Models.Message>
            {
                new ConduitLLM.Core.Models.Message { Role = "user", Content = "Hello" }
            }
        };

        // The request should time out and throw a TimeoutRejectedException or similar
        var exception = await Assert.ThrowsAsync<TaskCanceledException>(async () => 
            await openAIClient.CreateChatCompletionAsync(request)
        );

        // Verify it was a timeout that caused the TaskCanceledException
        Assert.Contains(exception.InnerException?.GetType().Name ?? string.Empty, 
            new[] { "TimeoutRejectedException", "TimeoutException" }, 
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpenAIClient_ShouldApplyBothTimeoutAndRetryPolicies()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var callCount = 0;

        // Setup the mock handler to return a 503 once, then delay for a response
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                Moq.Protected.ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken token) => 
            {
                callCount++;
                if (callCount == 1)
                {
                    // First attempt: Return 503 Service Unavailable (should trigger retry)
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                }
                else
                {
                    // Second attempt: Delay longer than timeout (should trigger timeout)
                    await Task.Delay(2000, token);
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }
            });

        // Create services with both retry and timeout policies
        var services = new ServiceCollection();
        services.AddLogging();
        
        // Configure retry options
        services.AddOptions<RetryOptions>().Configure(options => 
        {
            options.MaxRetries = 3;
            options.InitialDelaySeconds = 0; // Set to 0 for faster test execution
            options.MaxDelaySeconds = 1;
        });
        
        // Configure timeout options
        services.AddOptions<TimeoutOptions>().Configure(options => 
        {
            options.TimeoutSeconds = 1; // Set a very short timeout for the test
            options.EnableTimeoutLogging = true;
        });

        // Setup HttpClient factory to use our mock handler with both timeout and retry policies
        services.AddHttpClient<OpenAIClient>()
            .ConfigurePrimaryHttpMessageHandler(() => mockHandler.Object)
            .AddPolicyHandler((provider, _) => 
            {
                // Outer policy: Timeout
                var logger = provider.GetService<ILogger<OpenAIClient>>();
                var options = provider.GetService<IOptions<TimeoutOptions>>()?.Value ?? new TimeoutOptions();
                return ResiliencePolicies.GetTimeoutPolicy(
                    TimeSpan.FromSeconds(options.TimeoutSeconds),
                    logger);
            })
            .AddPolicyHandler((provider, _) => 
            {
                // Inner policy: Retry
                var logger = provider.GetService<ILogger<OpenAIClient>>();
                var options = provider.GetService<IOptions<RetryOptions>>()?.Value ?? new RetryOptions();
                return ResiliencePolicies.GetRetryPolicy(
                    options.MaxRetries,
                    TimeSpan.FromSeconds(options.InitialDelaySeconds),
                    TimeSpan.FromSeconds(options.MaxDelaySeconds),
                    logger);
            });

        // Create client dependencies
        services.AddSingleton(new ProviderCredentials { ProviderName = "openai", ApiKey = "test-key" });

        var serviceProvider = services.BuildServiceProvider();
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(nameof(OpenAIClient));
        var logger = serviceProvider.GetRequiredService<ILogger<OpenAIClient>>();
        var credentials = serviceProvider.GetRequiredService<ProviderCredentials>();

        // Create the client using the HttpClient with both policies
        var openAIClient = new OpenAIClient(credentials, "gpt-4", logger, httpClient: httpClient);

        // Act & Assert
        var request = new ConduitLLM.Core.Models.ChatCompletionRequest
        {
            Model = "gpt-4", 
            Messages = new List<ConduitLLM.Core.Models.Message>
            {
                new ConduitLLM.Core.Models.Message { Role = "user", Content = "Hello" }
            }
        };

        // First call should fail with 503, retry, then time out on the second attempt
        var exception = await Assert.ThrowsAsync<TaskCanceledException>(async () => 
            await openAIClient.CreateChatCompletionAsync(request)
        );

        // Verify it was a timeout after a retry
        Assert.Equal(2, callCount); // Verify we tried twice (1 failure + 1 timeout)
        Assert.Contains(exception.InnerException?.GetType().Name ?? string.Empty, 
            new[] { "TimeoutRejectedException", "TimeoutException" }, 
            StringComparer.OrdinalIgnoreCase);
    }
}
