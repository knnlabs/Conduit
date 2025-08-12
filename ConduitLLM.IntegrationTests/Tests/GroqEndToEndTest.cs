using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using ConduitLLM.IntegrationTests.Core;

namespace ConduitLLM.IntegrationTests.Tests;

[Collection("Sequential")]
[Trait("Category", "Integration")]
public class GroqEndToEndTest : IClassFixture<TestFixture>
{
    private readonly TestFixture _fixture;
    private readonly ILogger<GroqEndToEndTest> _logger;
    private readonly ConduitApiClient _apiClient;
    private readonly TestConfiguration _config;
    private readonly ProviderConfig _providerConfig;
    
    public GroqEndToEndTest(TestFixture fixture)
    {
        _fixture = fixture;
        _logger = _fixture.ServiceProvider.GetRequiredService<ILogger<GroqEndToEndTest>>();
        _apiClient = _fixture.ServiceProvider.GetRequiredService<ConduitApiClient>();
        _config = _fixture.Configuration;
        _providerConfig = ConfigurationLoader.LoadProviderConfig("groq");
    }
    
    [Fact]
    public async Task GroqProvider_CompleteEndToEndFlow_ShouldWork()
    {
        // Initialize test context
        var context = new TestContext();
        _logger.LogInformation("Starting Groq integration test: {TestRunId}", context.TestRunId);
        
        // Wait for services to be healthy
        var servicesReady = await TestHelpers.HealthChecks.WaitForServicesAsync(_config, _logger);
        servicesReady.Should().BeTrue("All services should be ready before starting tests");
        
        try
        {
            // ========================================
            // Step 1: Create Provider
            // ========================================
            _logger.LogInformation("Step 1: Creating Groq provider");
            
            var providerTypeEnum = _providerConfig.Provider.Type.ToLower() switch
            {
                "groq" => 2,
                _ => throw new InvalidOperationException($"Unknown provider type: {_providerConfig.Provider.Type}")
            };
            
            var createProviderRequest = new CreateProviderRequest
            {
                ProviderName = $"{_config.Defaults.TestPrefix}{_providerConfig.Provider.Name}_{context.TestRunId}",
                ProviderType = providerTypeEnum,
                BaseUrl = _providerConfig.Provider.BaseUrl,
                IsEnabled = true
            };
            
            var providerResponse = await _apiClient.AdminPostAsync<CreateProviderResponse>("/api/ProviderCredentials", createProviderRequest);
            providerResponse.Success.Should().BeTrue($"Provider creation should succeed: {providerResponse.Error}");
            providerResponse.Data.Should().NotBeNull();
            context.ProviderId = providerResponse.Data!.Id;
            _logger.LogInformation("✓ Provider created with ID: {ProviderId}", context.ProviderId);
            
            // ========================================
            // Step 2: Create Provider Key
            // ========================================
            _logger.LogInformation("Step 2: Creating provider API key");
            
            var createKeyRequest = new CreateProviderKeyRequest
            {
                ApiKey = _providerConfig.Provider.ApiKey,
                KeyName = $"{_config.Defaults.TestPrefix}Key_{context.TestRunId}",
                IsPrimary = true
            };
            
            var keyResponse = await _apiClient.AdminPostAsync<CreateProviderKeyResponse>(
                $"/api/ProviderCredentials/{context.ProviderId}/keys", 
                createKeyRequest);
            
            keyResponse.Success.Should().BeTrue($"Key creation should succeed: {keyResponse.Error}");
            keyResponse.Data.Should().NotBeNull();
            keyResponse.Data!.IsPrimary.Should().BeTrue("Key should be primary");
            context.ProviderKeyId = keyResponse.Data.Id.ToString();
            _logger.LogInformation("✓ Provider key created: {KeyId}", context.ProviderKeyId);
            
            // ========================================
            // Step 3: Create Model Mapping
            // ========================================
            _logger.LogInformation("Step 3: Creating model mapping");
            
            var modelConfig = _providerConfig.Models[0];
            var createMappingRequest = new CreateModelMappingRequest
            {
                ModelId = $"{modelConfig.Alias}_{context.TestRunId}",
                ProviderId = context.ProviderId!.Value,
                ProviderModelId = modelConfig.Actual,
                SupportsChat = modelConfig.Capabilities.Chat,
                SupportsStreaming = modelConfig.Capabilities.Streaming
            };
            
            var mappingResponse = await _apiClient.AdminPostAsync<CreateModelMappingResponse>(
                "/api/ModelProviderMapping", 
                createMappingRequest);
            
            mappingResponse.Success.Should().BeTrue($"Model mapping should succeed: {mappingResponse.Error}");
            mappingResponse.Data.Should().NotBeNull();
            context.ModelMappingId = mappingResponse.Data!.Id;
            context.ModelAlias = $"{modelConfig.Alias}_{context.TestRunId}";
            _logger.LogInformation("✓ Model mapping created: {ModelAlias} -> {ActualModel}", 
                context.ModelAlias, modelConfig.Actual);
            
            // ========================================
            // Step 4: Create Model Cost Configuration
            // ========================================
            _logger.LogInformation("Step 4: Creating model cost configuration");
            
            var createCostRequest = new CreateModelCostRequest
            {
                CostName = $"{context.ModelAlias}_cost",
                ModelProviderMappingIds = new List<int> { context.ModelMappingId!.Value },
                InputCostPerMillionTokens = modelConfig.Cost.InputPerMillion,
                OutputCostPerMillionTokens = modelConfig.Cost.OutputPerMillion
            };
            
            var costResponse = await _apiClient.AdminPostAsync<CreateModelCostResponse>(
                "/api/ModelCosts", 
                createCostRequest);
            
            costResponse.Success.Should().BeTrue($"Model cost creation should succeed: {costResponse.Error}");
            costResponse.Data.Should().NotBeNull();
            context.ModelCostId = costResponse.Data!.Id;
            _logger.LogInformation("✓ Model cost created: Input=${Input}/M, Output=${Output}/M", 
                modelConfig.Cost.InputPerMillion, modelConfig.Cost.OutputPerMillion);
            
            // ========================================
            // Step 5: Create Virtual Key Group
            // ========================================
            _logger.LogInformation("Step 5: Creating virtual key group with ${Credit} credit", 
                _config.Defaults.VirtualKeyCredit);
            
            var createGroupRequest = new CreateVirtualKeyGroupRequest
            {
                GroupName = $"{_config.Defaults.TestPrefix}Group_{context.TestRunId}",
                InitialBalance = _config.Defaults.VirtualKeyCredit
            };
            
            var groupResponse = await _apiClient.AdminPostAsync<CreateVirtualKeyGroupResponse>(
                "/api/VirtualKeyGroups", 
                createGroupRequest);
            
            groupResponse.Success.Should().BeTrue($"Virtual key group creation should succeed: {groupResponse.Error}");
            groupResponse.Data.Should().NotBeNull();
            context.VirtualKeyGroupId = groupResponse.Data!.Id;
            
            // Verify the balance was set correctly
            groupResponse.Data.Balance.Should().Be(_config.Defaults.VirtualKeyCredit, 
                "Group balance should match the requested initial balance");
            
            context.InitialCredit = groupResponse.Data.Balance;
            context.RemainingCredit = groupResponse.Data.Balance;
            _logger.LogInformation("✓ Virtual key group created with ID: {Id}", context.VirtualKeyGroupId);
            
            // ========================================
            // Step 6: Create Virtual Key
            // ========================================
            _logger.LogInformation("Step 6: Creating virtual key");
            
            var createVKeyRequest = new CreateVirtualKeyRequest
            {
                KeyName = $"{_config.Defaults.TestPrefix}VKey_{context.TestRunId}",
                VirtualKeyGroupId = context.VirtualKeyGroupId!.Value
            };
            
            var vkeyResponse = await _apiClient.AdminPostAsync<CreateVirtualKeyResponse>(
                "/api/VirtualKeys", 
                createVKeyRequest);
            
            vkeyResponse.Success.Should().BeTrue($"Virtual key creation should succeed: {vkeyResponse.Error}");
            vkeyResponse.Data.Should().NotBeNull();
            vkeyResponse.Data!.VirtualKey.Should().NotBeNullOrEmpty();
            context.VirtualKey = vkeyResponse.Data.VirtualKey;
            _logger.LogInformation("✓ Virtual key created: {VirtualKey}", context.VirtualKey);
            
            // ========================================
            // Step 7: Send Chat Request
            // ========================================
            _logger.LogInformation("Step 7: Sending chat request to Groq");
            
            // Note: Cannot change timeout after HttpClient has been used
            // Using default timeout from configuration
            
            var testCase = _providerConfig.TestCases.BasicChat;
            var chatRequest = new ChatCompletionRequest
            {
                Model = context.ModelAlias!,
                Messages = new List<ChatMessage>
                {
                    new() { Role = "user", Content = testCase.Prompt }
                },
                Stream = false
            };
            
            var chatResponse = await _apiClient.CorePostAsync<ChatCompletionResponse>(
                "/v1/chat/completions", 
                chatRequest, 
                context.VirtualKey);
            
            chatResponse.Success.Should().BeTrue($"Chat request should succeed: {chatResponse.Error}");
            chatResponse.Data.Should().NotBeNull();
            
            // Validate response
            TestHelpers.Assertions.ValidateChatResponse(
                chatResponse.Data!, 
                testCase.Validation, 
                _logger);
            
            // Store response data
            context.LastChatResponse = new ChatResponse
            {
                Id = chatResponse.Data!.Id,
                Model = chatResponse.Data.Model,
                PromptTokens = chatResponse.Data.Usage?.PromptTokens,
                CompletionTokens = chatResponse.Data.Usage?.CompletionTokens,
                TotalTokens = chatResponse.Data.Usage?.TotalTokens,
                Content = chatResponse.Data.Choices[0].Message.Content
            };
            
            _logger.LogInformation("✓ Chat response received: {Tokens} tokens", 
                context.LastChatResponse.TotalTokens);
            
            // ========================================
            // Step 8: Verify Token Tracking
            // ========================================
            _logger.LogInformation("Step 8: Verifying token tracking accuracy");
            
            context.LastChatResponse.PromptTokens.Should().NotBeNull();
            context.LastChatResponse.CompletionTokens.Should().NotBeNull();
            
            var inputCost = (context.LastChatResponse.PromptTokens!.Value / 1_000_000m) * modelConfig.Cost.InputPerMillion;
            var outputCost = (context.LastChatResponse.CompletionTokens!.Value / 1_000_000m) * modelConfig.Cost.OutputPerMillion;
            var totalCost = inputCost + outputCost;
            
            context.LastChatResponse.InputCost = inputCost;
            context.LastChatResponse.OutputCost = outputCost;
            context.LastChatResponse.TotalCost = totalCost;
            
            _logger.LogInformation("✓ Token costs: Input=${InputCost:F6}, Output=${OutputCost:F6}, Total=${TotalCost:F6}",
                inputCost, outputCost, totalCost);
            
            inputCost.Should().BeGreaterThan(0, "Input cost should be greater than zero");
            outputCost.Should().BeGreaterThan(0, "Output cost should be greater than zero");
            
            // ========================================
            // Step 9: Generate Report
            // ========================================
            _logger.LogInformation("Step 9: Generating test report");
            
            // Save context for report generation
            context.SaveToFile();
            
            await TestHelpers.ReportGenerator.GenerateMarkdownReport(
                context, 
                _config, 
                _providerConfig, 
                _logger);
            
            // Report file uses timestamp without TEST_ prefix
            var reportFiles = Directory.GetFiles(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports"),
                "test_run_*.md")
                .OrderByDescending(f => new FileInfo(f).CreationTime)
                .ToList();
            
            reportFiles.Should().NotBeEmpty("At least one report should be generated");
            var reportPath = reportFiles.First();
            _logger.LogInformation("✓ Report generated: {ReportPath}", reportPath);
            
            // ========================================
            // Summary
            // ========================================
            _logger.LogInformation(@"
=====================================
✅ GROQ INTEGRATION TEST SUCCESSFUL
=====================================
Provider ID: {ProviderId}
Model: {Model}
Tokens Used: {Tokens}
Total Cost: ${Cost:F6}
Report: {Report}
=====================================",
                context.ProviderId,
                context.ModelAlias,
                context.LastChatResponse.TotalTokens,
                context.LastChatResponse.TotalCost,
                reportPath);
                
            // Verify no errors occurred
            context.Errors.Should().BeEmpty("No errors should have occurred during the test");
        }
        catch (Exception ex)
        {
            context.Errors.Add($"Test failed: {ex.Message}");
            context.SaveToFile();
            
            // Generate failure report
            try
            {
                await TestHelpers.ReportGenerator.GenerateMarkdownReport(
                    context, 
                    _config, 
                    _providerConfig, 
                    _logger);
                _logger.LogInformation("Failure report generated for Groq test");
            }
            catch (Exception reportEx)
            {
                _logger.LogError(reportEx, "Failed to generate failure report");
            }
            
            _logger.LogError(ex, "Test failed at step");
            throw;
        }
    }
}

