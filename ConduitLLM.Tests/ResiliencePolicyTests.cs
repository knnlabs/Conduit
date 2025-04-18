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
}
