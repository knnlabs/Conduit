using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ConduitLLM.IntegrationTests.Core;

public abstract class ProviderIntegrationTestBase : IClassFixture<TestFixture>
{
    protected readonly TestFixture _fixture;
    protected readonly ILogger _logger;
    protected readonly ConduitApiClient _apiClient;
    protected readonly TestConfiguration _config;
    protected readonly ProviderConfig _providerConfig;
    protected readonly TestContext _context;
    
    protected ProviderIntegrationTestBase(TestFixture fixture, string providerName)
    {
        _fixture = fixture;
        _logger = CreateLogger();
        _apiClient = _fixture.ServiceProvider.GetRequiredService<ConduitApiClient>();
        _config = _fixture.Configuration;
        _providerConfig = ConfigurationLoader.LoadProviderConfig(providerName);
        _context = new TestContext();
    }
    
    protected abstract ILogger CreateLogger();
    
    protected async Task<bool> SetupProviderInfrastructure()
    {
        try
        {
            _logger.LogInformation("Starting {Provider} integration test: {TestRunId}", 
                _providerConfig.Provider.Type, _context.TestRunId);
            
            // Wait for services to be healthy
            var servicesReady = await TestHelpers.HealthChecks.WaitForServicesAsync(_config, _logger);
            servicesReady.Should().BeTrue("All services should be ready before starting tests");
            
            // Step 1: Create Provider
            await CreateProvider();
            
            // Step 2: Create Provider Key
            await CreateProviderKey();
            
            // Step 3: Create Model Mapping
            await CreateModelMapping();
            
            // Step 4: Create Model Cost Configuration
            await CreateModelCost();
            
            // Step 5: Create Virtual Key Group
            await CreateVirtualKeyGroup();
            
            // Step 6: Create Virtual Key
            await CreateVirtualKey();
            
            return true;
        }
        catch (Exception ex)
        {
            _context.Errors.Add($"Setup failed: {ex.Message}");
            _context.SaveToFile();
            _logger.LogError(ex, "Provider setup failed");
            return false;
        }
    }
    
    protected async Task CreateProvider()
    {
        _logger.LogInformation("Step 1: Creating {Provider} provider", _providerConfig.Provider.Type);
        
        var providerTypeEnum = GetProviderTypeEnum(_providerConfig.Provider.Type);
        
        var createProviderRequest = new CreateProviderRequest
        {
            ProviderName = $"{_config.Defaults.TestPrefix}{_providerConfig.Provider.Name}_{_context.TestRunId}",
            ProviderType = providerTypeEnum,
            BaseUrl = _providerConfig.Provider.BaseUrl,
            IsEnabled = true
        };
        
        var providerResponse = await _apiClient.AdminPostAsync<CreateProviderResponse>("/api/ProviderCredentials", createProviderRequest);
        providerResponse.Success.Should().BeTrue($"Provider creation should succeed: {providerResponse.Error}");
        providerResponse.Data.Should().NotBeNull();
        _context.ProviderId = providerResponse.Data!.Id;
        _logger.LogInformation("✓ Provider created with ID: {ProviderId}", _context.ProviderId);
    }
    
    protected async Task CreateProviderKey()
    {
        _logger.LogInformation("Step 2: Creating provider API key");
        
        var createKeyRequest = new CreateProviderKeyRequest
        {
            ApiKey = _providerConfig.Provider.ApiKey,
            KeyName = $"{_config.Defaults.TestPrefix}Key_{_context.TestRunId}",
            IsPrimary = true
        };
        
        var keyResponse = await _apiClient.AdminPostAsync<CreateProviderKeyResponse>(
            $"/api/ProviderCredentials/{_context.ProviderId}/keys", 
            createKeyRequest);
        
        keyResponse.Success.Should().BeTrue($"Key creation should succeed: {keyResponse.Error}");
        keyResponse.Data.Should().NotBeNull();
        keyResponse.Data!.IsPrimary.Should().BeTrue("Key should be primary");
        _context.ProviderKeyId = keyResponse.Data.Id.ToString();
        _logger.LogInformation("✓ Provider key created: {KeyId}", _context.ProviderKeyId);
    }
    
    protected async Task CreateModelMapping()
    {
        _logger.LogInformation("Step 3: Creating model mapping");
        
        var modelConfig = _providerConfig.Models[0];
        var createMappingRequest = new CreateModelMappingRequest
        {
            ModelId = $"{modelConfig.Alias}_{_context.TestRunId}",
            ProviderId = _context.ProviderId!.Value,
            ProviderModelId = modelConfig.Actual,
            SupportsChat = modelConfig.Capabilities.Chat,
            SupportsStreaming = modelConfig.Capabilities.Streaming
        };
        
        var mappingResponse = await _apiClient.AdminPostAsync<CreateModelMappingResponse>(
            "/api/ModelProviderMapping", 
            createMappingRequest);
        
        mappingResponse.Success.Should().BeTrue($"Model mapping should succeed: {mappingResponse.Error}");
        mappingResponse.Data.Should().NotBeNull();
        _context.ModelMappingId = mappingResponse.Data!.Id;
        _context.ModelAlias = $"{modelConfig.Alias}_{_context.TestRunId}";
        _logger.LogInformation("✓ Model mapping created: {ModelAlias} -> {ActualModel}", 
            _context.ModelAlias, modelConfig.Actual);
    }
    
    protected async Task CreateModelCost()
    {
        _logger.LogInformation("Step 4: Creating model cost configuration");
        
        var modelConfig = _providerConfig.Models[0];
        var createCostRequest = new CreateModelCostRequest
        {
            CostName = $"{_context.ModelAlias}_cost",
            ModelProviderMappingIds = new List<int> { _context.ModelMappingId!.Value },
            InputCostPerMillionTokens = modelConfig.Cost.InputPerMillion,
            OutputCostPerMillionTokens = modelConfig.Cost.OutputPerMillion
        };
        
        var costResponse = await _apiClient.AdminPostAsync<CreateModelCostResponse>(
            "/api/ModelCosts", 
            createCostRequest);
        
        costResponse.Success.Should().BeTrue($"Model cost creation should succeed: {costResponse.Error}");
        costResponse.Data.Should().NotBeNull();
        _context.ModelCostId = costResponse.Data!.Id;
        _logger.LogInformation("✓ Model cost created: Input=${Input}/M, Output=${Output}/M", 
            modelConfig.Cost.InputPerMillion, modelConfig.Cost.OutputPerMillion);
    }
    
    protected async Task CreateVirtualKeyGroup()
    {
        _logger.LogInformation("Step 5: Creating virtual key group with ${Credit} credit", 
            _config.Defaults.VirtualKeyCredit);
        
        var createGroupRequest = new CreateVirtualKeyGroupRequest
        {
            GroupName = $"{_config.Defaults.TestPrefix}Group_{_context.TestRunId}",
            InitialBalance = _config.Defaults.VirtualKeyCredit
        };
        
        var groupResponse = await _apiClient.AdminPostAsync<CreateVirtualKeyGroupResponse>(
            "/api/VirtualKeyGroups", 
            createGroupRequest);
        
        groupResponse.Success.Should().BeTrue($"Virtual key group creation should succeed: {groupResponse.Error}");
        groupResponse.Data.Should().NotBeNull();
        _context.VirtualKeyGroupId = groupResponse.Data!.Id;
        
        groupResponse.Data.Balance.Should().Be(_config.Defaults.VirtualKeyCredit, 
            "Group balance should match the requested initial balance");
        
        _context.InitialCredit = groupResponse.Data.Balance;
        _context.RemainingCredit = groupResponse.Data.Balance;
        _logger.LogInformation("✓ Virtual key group created with ID: {Id}", _context.VirtualKeyGroupId);
    }
    
    protected async Task CreateVirtualKey()
    {
        _logger.LogInformation("Step 6: Creating virtual key");
        
        var createVKeyRequest = new CreateVirtualKeyRequest
        {
            KeyName = $"{_config.Defaults.TestPrefix}VKey_{_context.TestRunId}",
            VirtualKeyGroupId = _context.VirtualKeyGroupId!.Value
        };
        
        var vkeyResponse = await _apiClient.AdminPostAsync<CreateVirtualKeyResponse>(
            "/api/VirtualKeys", 
            createVKeyRequest);
        
        vkeyResponse.Success.Should().BeTrue($"Virtual key creation should succeed: {vkeyResponse.Error}");
        vkeyResponse.Data.Should().NotBeNull();
        vkeyResponse.Data!.VirtualKey.Should().NotBeNullOrEmpty();
        _context.VirtualKey = vkeyResponse.Data.VirtualKey;
        _logger.LogInformation("✓ Virtual key created: {VirtualKey}", _context.VirtualKey);
    }
    
    protected async Task<ChatCompletionResponse?> SendBasicChatRequest()
    {
        _logger.LogInformation("Step 7: Sending chat request to {Provider}", _providerConfig.Provider.Type);
        
        var testCase = _providerConfig.TestCases.BasicChat;
        var chatRequest = new ChatCompletionRequest
        {
            Model = _context.ModelAlias!,
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = testCase.Prompt }
            },
            Stream = false
        };
        
        var chatResponse = await _apiClient.CorePostAsync<ChatCompletionResponse>(
            "/v1/chat/completions", 
            chatRequest, 
            _context.VirtualKey);
        
        chatResponse.Success.Should().BeTrue($"Chat request should succeed: {chatResponse.Error}");
        chatResponse.Data.Should().NotBeNull();
        
        // Validate response
        TestHelpers.Assertions.ValidateChatResponse(
            chatResponse.Data!, 
            testCase.Validation, 
            _logger);
        
        // Store response data
        _context.LastChatResponse = new ChatResponse
        {
            Id = chatResponse.Data!.Id,
            Model = chatResponse.Data.Model,
            PromptTokens = chatResponse.Data.Usage?.PromptTokens,
            CompletionTokens = chatResponse.Data.Usage?.CompletionTokens,
            TotalTokens = chatResponse.Data.Usage?.TotalTokens,
            Content = chatResponse.Data.Choices[0].Message.Content
        };
        
        _logger.LogInformation("✓ Chat response received: {Tokens} tokens", 
            _context.LastChatResponse.TotalTokens);
        
        return chatResponse.Data;
    }
    
    protected Task VerifyTokenTracking()
    {
        _logger.LogInformation("Step 8: Verifying token tracking accuracy");
        
        _context.LastChatResponse!.PromptTokens.Should().NotBeNull();
        _context.LastChatResponse.CompletionTokens.Should().NotBeNull();
        
        var modelConfig = _providerConfig.Models[0];
        var inputCost = (_context.LastChatResponse.PromptTokens!.Value / 1_000_000m) * modelConfig.Cost.InputPerMillion;
        var outputCost = (_context.LastChatResponse.CompletionTokens!.Value / 1_000_000m) * modelConfig.Cost.OutputPerMillion;
        var totalCost = inputCost + outputCost;
        
        _context.LastChatResponse.InputCost = inputCost;
        _context.LastChatResponse.OutputCost = outputCost;
        _context.LastChatResponse.TotalCost = totalCost;
        
        _logger.LogInformation("✓ Token costs: Input=${InputCost:F6}, Output=${OutputCost:F6}, Total=${TotalCost:F6}",
            inputCost, outputCost, totalCost);
        
        inputCost.Should().BeGreaterThan(0, "Input cost should be greater than zero");
        outputCost.Should().BeGreaterThan(0, "Output cost should be greater than zero");
        
        return Task.CompletedTask;
    }
    
    protected async Task GenerateReport()
    {
        _logger.LogInformation("Step 9: Generating test report");
        
        // Save context for report generation
        _context.SaveToFile();
        
        await TestHelpers.ReportGenerator.GenerateMarkdownReport(
            _context, 
            _config, 
            _providerConfig, 
            _logger);
        
        var reportFiles = Directory.GetFiles(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports"),
            "test_run_*.md")
            .OrderByDescending(f => new FileInfo(f).CreationTime)
            .ToList();
        
        reportFiles.Should().NotBeEmpty("At least one report should be generated");
        var reportPath = reportFiles.First();
        _logger.LogInformation("✓ Report generated: {ReportPath}", reportPath);
        
        LogTestSummary(reportPath);
    }
    
    protected void LogTestSummary(string reportPath)
    {
        _logger.LogInformation(@"
=====================================
✅ {Provider} INTEGRATION TEST SUCCESSFUL
=====================================
Provider ID: {ProviderId}
Model: {Model}
Tokens Used: {Tokens}
Total Cost: ${Cost:F6}
Report: {Report}
=====================================",
            _providerConfig.Provider.Type.ToUpper(),
            _context.ProviderId,
            _context.ModelAlias,
            _context.LastChatResponse?.TotalTokens,
            _context.LastChatResponse?.TotalCost,
            reportPath);
    }
    
    protected int GetProviderTypeEnum(string providerType)
    {
        return providerType.ToLower() switch
        {
            "openai" => 1,
            "groq" => 2,
            "replicate" => 3,
            "fireworks" => 4,
            "openaicompatible" => 5,
            "minimax" => 6,
            "ultravox" => 7,
            "elevenlabs" => 8,
            "cerebras" => 9,
            "sambanova" => 10,
            _ => throw new InvalidOperationException($"Unknown provider type: {providerType}")
        };
    }
}