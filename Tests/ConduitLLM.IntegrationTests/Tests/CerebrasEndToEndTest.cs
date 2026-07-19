using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using ConduitLLM.IntegrationTests.Core;

namespace ConduitLLM.IntegrationTests.Tests;

[Collection("Sequential")]
[Trait("Category", "Integration")]
public class CerebrasEndToEndTest : ProviderIntegrationTestBase
{
    private readonly ILogger<CerebrasEndToEndTest> _specificLogger;
    
    public CerebrasEndToEndTest(TestFixture fixture) : base(fixture, "cerebras")
    {
        _specificLogger = _fixture.ServiceProvider.GetRequiredService<ILogger<CerebrasEndToEndTest>>();
    }
    
    protected override ILogger CreateLogger()
    {
        return _fixture.ServiceProvider.GetRequiredService<ILogger<CerebrasEndToEndTest>>();
    }
    
    [Fact(DisplayName = "Cerebras Provider - Basic Chat Test")]
    public async Task CerebrasProvider_BasicChat_ShouldWork()
    {
        // Check if using a placeholder API key
        if (_providerConfig.Provider.ApiKey.Contains("YOUR_CEREBRAS_API_KEY_HERE") || 
            _providerConfig.Provider.ApiKey.Length < 20)
        {
            // Generate a report indicating the test couldn't run
            _context.Errors.Add("Test cannot run: Placeholder API key detected");
            _context.Errors.Add("To fix: Configure a valid Cerebras API key in cerebras.yaml");
            _context.SaveToFile();
            
            // Fail the test with a clear message
            throw new InvalidOperationException(
                "Cerebras test FAILED: Placeholder API key detected. " +
                "Please configure a valid Cerebras API key in cerebras.yaml. " + 
                "Get your API key from https://cerebras.ai");
        }
        
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
            if (_context.Errors.Any())
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
    public async Task CerebrasProvider_MultimodalChat_ShouldSkip()
    {
        // Cerebras does not support multimodal, so this test should always skip
        _specificLogger.LogInformation("Cerebras does not support multimodal. Skipping multimodal test.");
        
        // Check if multimodal is enabled in config (it should be false)
        if (_providerConfig.TestCases.Multimodal != null && _providerConfig.TestCases.Multimodal.Enabled)
        {
            _specificLogger.LogWarning("Multimodal is incorrectly enabled in Cerebras config. It should be disabled.");
            _context.Errors.Add("Configuration error: Multimodal should be disabled for Cerebras");
        }
        
        // Generate report indicating multimodal is not supported
        _context.SaveToFile();
        await TestHelpers.ReportGenerator.GenerateMarkdownReport(
            _context, 
            _config, 
            _providerConfig, 
            _logger);
        
        _specificLogger.LogInformation(@"
=====================================
✅ CEREBRAS INTEGRATION TEST COMPLETE
=====================================
Provider: Cerebras
Multimodal: Not Supported (Text-only provider)
=====================================");
    }
}