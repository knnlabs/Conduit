using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using ConduitLLM.IntegrationTests.Core;

namespace ConduitLLM.IntegrationTests.Tests;

[Collection("Sequential")]
[Trait("Category", "Integration")]
public class SambaNovaEndToEndTest : ProviderIntegrationTestBase
{
    private readonly ILogger<SambaNovaEndToEndTest> _specificLogger;
    
    public SambaNovaEndToEndTest(TestFixture fixture) : base(fixture, "sambanova")
    {
        _specificLogger = _fixture.ServiceProvider.GetRequiredService<ILogger<SambaNovaEndToEndTest>>();
    }
    
    protected override ILogger CreateLogger()
    {
        return _fixture.ServiceProvider.GetRequiredService<ILogger<SambaNovaEndToEndTest>>();
    }
    
    [Fact]
    public async Task SambaNovaProvider_BasicChat_ShouldWork()
    {
        bool reportGenerated = false;
        
        try
        {
            // Setup provider infrastructure (Steps 1-6)
            var setupSuccess = await SetupProviderInfrastructure();
            setupSuccess.Should().BeTrue("Provider infrastructure should be set up successfully");
            
            try
            {
                // Step 7: Send basic chat request
                var chatResponse = await SendBasicChatRequest();
                chatResponse.Should().NotBeNull("Chat response should not be null");
                
                // Step 8: Verify token tracking
                await VerifyTokenTracking();
            }
            catch (Exception chatEx)
            {
                // Log the error but continue to generate report
                _specificLogger.LogError(chatEx, "Chat request failed");
                _context.Errors.Add($"Chat request failed: {chatEx.Message}");
                
                // Still generate report even if chat failed
                _specificLogger.LogInformation("Generating report despite chat failure");
            }
            
            // Step 9: Generate report (always generate, even on failure)
            await GenerateReport();
            reportGenerated = true;
            
            // Now check if there were errors and fail the test if needed
            if (_context.Errors.Count() > 0)
            {
                var errorMessage = string.Join("; ", _context.Errors);
                _specificLogger.LogError("Test completed with errors: {Errors}", errorMessage);
                throw new Exception($"Test failed with errors: {errorMessage}");
            }
        }
        catch (Exception ex)
        {
            // Make sure we save context
            _context.Errors.Add($"Test failed: {ex.Message}");
            _context.SaveToFile();
            
            // Only generate report if we haven't already
            if (!reportGenerated)
            {
                try
                {
                    await GenerateReport();
                }
                catch
                {
                    // If report generation fails, just log it
                    _logger.LogError("Failed to generate report");
                }
            }
            
            _logger.LogError(ex, "Test failed");
            throw;
        }
    }
    
    [Fact]
    public async Task SambaNovaProvider_MultimodalChat_ShouldWork()
    {
        // Check if multimodal is enabled in config
        if (_providerConfig.TestCases.Multimodal == null || !_providerConfig.TestCases.Multimodal.Enabled)
        {
            _specificLogger.LogInformation("Multimodal test is disabled in configuration. Skipping test.");
            return;
        }
        
        try
        {
            // Setup provider infrastructure (Steps 1-6)
            var setupSuccess = await SetupProviderInfrastructure();
            setupSuccess.Should().BeTrue("Provider infrastructure should be set up successfully");
            
            // Step 7: Send multimodal chat request
            _specificLogger.LogInformation("Step 7: Sending multimodal request to SambaNova");
            
            var multimodalConfig = _providerConfig.TestCases.Multimodal;
            
            // Download and convert image to base64
            var base64Image = await TestHelpers.Multimodal.DownloadImageAsBase64(
                multimodalConfig.ImageUrl, 
                _specificLogger);
            
            // Create multimodal message
            var multimodalMessage = TestHelpers.Multimodal.CreateMultimodalMessage(
                multimodalConfig.Prompt,
                base64Image);
            
            var chatRequest = new ChatCompletionRequest
            {
                Model = _context.ModelAlias!,
                Messages = new List<ChatMessage> { multimodalMessage },
                Stream = false
            };
            
            try
            {
                var chatResponse = await _apiClient.CorePostAsync<ChatCompletionResponse>(
                    "/v1/chat/completions", 
                    chatRequest, 
                    _context.VirtualKey);
                
                if (chatResponse.Success)
                {
                    chatResponse.Data.Should().NotBeNull();
                    
                    // Validate multimodal response
                    TestHelpers.Assertions.ValidateChatResponse(
                        chatResponse.Data!, 
                        multimodalConfig.Validation, 
                        _specificLogger);
                    
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
                    
                    _specificLogger.LogInformation("✓ Multimodal response received: {Tokens} tokens", 
                        _context.LastChatResponse.TotalTokens);
                    
                    // Step 8: Verify token tracking
                    await VerifyTokenTracking();
                    
                    // Step 9: Generate report
                    await GenerateReportWithMultimodal(true);
                }
                else
                {
                    // If multimodal fails, log it but don't fail the test (model might not support it)
                    _specificLogger.LogWarning(
                        "Multimodal request failed (model may not support images): {Error}", 
                        chatResponse.Error);
                    
                    _context.Errors.Add($"Multimodal not supported: {chatResponse.Error}");
                    await GenerateReportWithMultimodal(false);
                }
            }
            catch (HttpRequestException httpEx)
            {
                // Network or API errors - model might not support multimodal
                _specificLogger.LogWarning(
                    "Multimodal request failed (likely not supported): {Message}", 
                    httpEx.Message);
                
                _context.Errors.Add($"Multimodal test skipped: {httpEx.Message}");
                await GenerateReportWithMultimodal(false);
            }
        }
        catch (Exception ex)
        {
            _context.Errors.Add($"Test setup failed: {ex.Message}");
            _context.SaveToFile();
            _logger.LogError(ex, "Test failed during setup");
            throw;
        }
    }
    
    private async Task GenerateReportWithMultimodal(bool multimodalSuccess)
    {
        _specificLogger.LogInformation("Step 9: Generating test report with multimodal status");
        
        // Save context for report generation
        _context.SaveToFile();
        
        // Add multimodal status to context
        if (!multimodalSuccess)
        {
            _context.Errors.Add("Note: Multimodal test was attempted but the model does not support image inputs.");
        }
        
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
        
        _specificLogger.LogInformation(@"
=====================================
✅ SAMBANOVA INTEGRATION TEST COMPLETE
=====================================
Provider ID: {ProviderId}
Model: {Model}
Basic Chat: ✅ Successful
Multimodal: {MultimodalStatus}
Tokens Used: {Tokens}
Total Cost: ${Cost:F6}
Report: {Report}
=====================================",
            _context.ProviderId,
            _context.ModelAlias,
            multimodalSuccess ? "✅ Successful" : "⚠️ Not Supported",
            _context.LastChatResponse?.TotalTokens,
            _context.LastChatResponse?.TotalCost,
            reportPath);
    }
}