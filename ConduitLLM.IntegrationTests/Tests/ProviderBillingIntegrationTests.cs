using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using ConduitLLM.IntegrationTests.Core;

namespace ConduitLLM.IntegrationTests.Tests;

[Collection("Sequential")]
[Trait("Category", "Integration")]
public class ProviderBillingIntegrationTests : ProviderIntegrationTestBase
{
    private readonly ITestOutputHelper _output;
    
    // Financial precision for billing verification (one-millionth of a dollar)
    private const decimal BillingTolerance = 0.000001m;

    public ProviderBillingIntegrationTests(TestFixture fixture, ITestOutputHelper output) 
        : base(fixture, GetProviderName())
    {
        _output = output;
    }
    
    protected override ILogger CreateLogger()
    {
        return _fixture.ServiceProvider.GetRequiredService<ILogger<ProviderBillingIntegrationTests>>();
    }
    
    private static string GetProviderName()
    {
        // Get provider from environment or default to groq
        var provider = Environment.GetEnvironmentVariable("TEST_PROVIDER")?.ToLower() ?? "groq";
        return provider;
    }

    [Theory]
    [InlineData("groq")]
    [InlineData("cerebras")]
    [InlineData("sambanova")]
    public async Task Provider_ChatAndBilling_ShouldWorkCorrectly(string providerName)
    {
        // Skip if provider not in active list
        if (!_config.ActiveProviders.Contains(providerName))
        {
            _output.WriteLine($"⊘ {providerName} - Not in active providers list");
            return;
        }

        _output.WriteLine($"\nTesting {providerName}...");
        
        // Reload provider config for this specific provider
        var providerConfig = ConfigurationLoader.LoadProviderConfig(providerName);
        var testRunId = $"TEST_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        _context.TestRunId = testRunId;
        
        try
        {
            // 1. Setup Provider Infrastructure (uses base class methods)
            _output.WriteLine($"  ✓ Provider configured with real API key");
            
            // Create provider
            await CreateProvider();
            var providerId = _context.ProviderId!.Value;
            
            // Create provider key  
            await CreateProviderKey();
            
            // Create model mapping
            await CreateModelMapping();
            
            // Create model cost
            await CreateModelCost();
            
            // 2. Create Virtual Key with Initial Credit
            await CreateVirtualKeyGroup();
            var initialCredit = _context.InitialCredit;
            var groupId = _context.VirtualKeyGroupId!.Value;
            
            await CreateVirtualKey();
            var virtualKey = _context.VirtualKey!;
            
            // 3. Make Chat Request
            var testCase = providerConfig.TestCases.BasicChat;
            var chatRequest = new ChatCompletionRequest
            {
                Model = _context.ModelAlias!,
                Messages = new[]
                {
                    new ChatMessage 
                    { 
                        Role = "user", 
                        Content = testCase.Prompt 
                    }
                }.ToList(),
                Stream = false
            };

            var chatResponse = await _apiClient.CorePostAsync<ChatCompletionResponse>(
                "/v1/chat/completions", 
                chatRequest,
                virtualKey);

            chatResponse.Success.Should().BeTrue($"Chat request failed: {chatResponse.Error}");
            chatResponse.Data.Should().NotBeNull();
            
            var promptTokens = chatResponse.Data!.Usage?.PromptTokens ?? 0;
            var completionTokens = chatResponse.Data.Usage?.CompletionTokens ?? 0;
            
            _output.WriteLine($"  ✓ Chat completed: {promptTokens} input, {completionTokens} output tokens");
            
            // 4. Calculate Expected Cost
            var modelConfig = providerConfig.Models[0];
            var inputCost = (promptTokens / 1_000_000m) * modelConfig.Cost.InputPerMillion;
            var outputCost = (completionTokens / 1_000_000m) * modelConfig.Cost.OutputPerMillion;
            var expectedCost = inputCost + outputCost;
            
            _output.WriteLine($"  ✓ Cost calculation: ${expectedCost:F6} ({promptTokens}×${modelConfig.Cost.InputPerMillion:F2}/M + {completionTokens}×${modelConfig.Cost.OutputPerMillion:F2}/M)");
            
            // 5. Verify Billing - CRITICAL STEP
            // Trigger immediate flush of pending batch spending charges via Admin API
            // This is more deterministic than waiting for the scheduled flush interval
            _output.WriteLine($"  ⏳ Triggering batch spend flush via Admin API...");
            
            var flushResponse = await _apiClient.AdminPostAsync<object>(
                "/api/batch-spending/flush",
                new { reason = "Integration test billing verification", priority = "Normal" });
            
            if (!flushResponse.Success)
            {
                _output.WriteLine($"  ⚠️ Flush request failed: {flushResponse.Error}");
                _output.WriteLine($"  📋 Falling back to 3-second delay for batch service...");
                await Task.Delay(3000);
            }
            else
            {
                _output.WriteLine($"  ✓ Batch spend flush request submitted successfully");
                // Give a brief moment for the async flush operation to complete
                await Task.Delay(1000);
            }
            
            var balanceResponse = await _apiClient.AdminGetAsync<CreateVirtualKeyGroupResponse>(
                $"/api/VirtualKeyGroups/{groupId}");
            
            balanceResponse.Success.Should().BeTrue("Failed to fetch updated balance");
            balanceResponse.Data.Should().NotBeNull();
            
            var newBalance = balanceResponse.Data!.Balance;
            var actualDeduction = initialCredit - newBalance;
            
            _output.WriteLine($"  Debug: Initial=${initialCredit:F6}, New=${newBalance:F6}, Deducted=${actualDeduction:F6}");
            
            // Financial accuracy check
            Math.Abs(actualDeduction - expectedCost).Should().BeLessThan(BillingTolerance,
                $"Billing discrepancy: Expected ${expectedCost:F6}, Actually deducted ${actualDeduction:F6}");
            
            _output.WriteLine($"  ✓ Billing verified: Deducted ${actualDeduction:F6} from balance");
            _output.WriteLine($"    Before: ${initialCredit:F6}, After: ${newBalance:F6}");
            
            // 6. Validate Response Content
            var content = chatResponse.Data.Choices?.FirstOrDefault()?.Message?.Content ?? "";
            content.Should().NotBeNullOrWhiteSpace("Response should contain content");
            
            // Check required patterns
            var validation = testCase.Validation;
            promptTokens.Should().BeGreaterThanOrEqualTo(validation.MinInputTokens, 
                $"Input tokens should be at least {validation.MinInputTokens}");
            completionTokens.Should().BeGreaterThanOrEqualTo(validation.MinOutputTokens,
                $"Output tokens should be at least {validation.MinOutputTokens}");
            
            foreach (var pattern in validation.RequiredPatterns)
            {
                var regex = new System.Text.RegularExpressions.Regex(pattern);
                regex.IsMatch(content).Should().BeTrue($"Response should match pattern: {pattern}");
            }
            
            _output.WriteLine($"  ✓ Response validated: Contains required content");
            
            // Success summary
            _output.WriteLine($"\n{providerName.ToUpper()}: ✓ Chat works, billing accurate (${actualDeduction:F6})\n");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"  ✗ Test failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                _output.WriteLine($"    Inner: {ex.InnerException.Message}");
            }
            _output.WriteLine($"\n{providerName.ToUpper()}: ✗ FAILED\n");
            throw;
        }
    }
}