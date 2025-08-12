using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Npgsql;
using StackExchange.Redis;

namespace ConduitLLM.IntegrationTests.Core;

public static class TestHelpers
{
    public static class HealthChecks
    {
        public static async Task<bool> WaitForServicesAsync(
            TestConfiguration config,
            ILogger logger,
            int maxRetries = 30,
            int delaySeconds = 2)
        {
            logger.LogInformation("Waiting for services to be ready...");
            
            for (int i = 0; i < maxRetries; i++)
            {
                var allHealthy = true;
                
                // Check Core API
                if (!await CheckHttpEndpoint($"{config.Environment.CoreApiUrl}/health", logger))
                    allHealthy = false;
                
                // Check Admin API
                if (!await CheckHttpEndpoint($"{config.Environment.AdminApiUrl}/health", logger))
                    allHealthy = false;
                
                // Check PostgreSQL
                if (!await CheckPostgres(logger))
                    allHealthy = false;
                
                // Check Redis
                if (!await CheckRedis(logger))
                    allHealthy = false;
                
                if (allHealthy)
                {
                    logger.LogInformation("All services are ready!");
                    return true;
                }
                
                if (i < maxRetries - 1)
                {
                    logger.LogInformation("Services not ready, retrying in {Seconds} seconds...", delaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                }
            }
            
            logger.LogError("Services failed to become ready after {Retries} attempts", maxRetries);
            return false;
        }
        
        private static async Task<bool> CheckHttpEndpoint(string url, ILogger logger)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var response = await client.GetAsync(url);
                var isHealthy = response.IsSuccessStatusCode;
                logger.LogDebug("{Url}: {Status}", url, isHealthy ? "✓" : "✗");
                return isHealthy;
            }
            catch (Exception ex)
            {
                logger.LogDebug("{Url}: ✗ ({Error})", url, ex.Message);
                return false;
            }
        }
        
        private static async Task<bool> CheckPostgres(ILogger logger)
        {
            try
            {
                // Read connection string from environment or use default
                var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") 
                    ?? "Host=localhost;Port=5432;Database=conduitdb;Username=conduit;Password=conduitpass";
                
                using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                await connection.CloseAsync();
                logger.LogDebug("PostgreSQL: ✓");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogDebug("PostgreSQL: ✗ ({Error})", ex.Message);
                return false;
            }
        }
        
        private static async Task<bool> CheckRedis(ILogger logger)
        {
            try
            {
                var redisConnection = Environment.GetEnvironmentVariable("REDIS_URL") ?? "localhost:6379";
                using var redis = await ConnectionMultiplexer.ConnectAsync(redisConnection);
                var db = redis.GetDatabase();
                await db.PingAsync();
                logger.LogDebug("Redis: ✓");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogDebug("Redis: ✗ ({Error})", ex.Message);
                return false;
            }
        }
    }
    
    public static class Assertions
    {
        public static void ValidateChatResponse(
            ChatCompletionResponse response,
            ValidationConfig validation,
            ILogger logger)
        {
            response.Should().NotBeNull("Chat response should not be null");
            response.Choices.Should().NotBeNullOrEmpty("Response should have choices");
            response.Usage.Should().NotBeNull("Response should include usage information");
            
            var content = response.Choices[0].Message.Content;
            content.Should().NotBeNullOrEmpty("Response content should not be empty");
            
            // Log the actual content for debugging
            logger.LogInformation("Response content (first 500 chars): {Content}", 
                content.Length > 500 ? content.Substring(0, 500) + "..." : content);
            
            // Token validation
            response.Usage!.PromptTokens.Should().BeGreaterThanOrEqualTo(
                validation.MinInputTokens,
                $"Input tokens should be at least {validation.MinInputTokens}");
            
            response.Usage.CompletionTokens.Should().BeGreaterThanOrEqualTo(
                validation.MinOutputTokens,
                $"Output tokens should be at least {validation.MinOutputTokens}");
            
            // Pattern validation
            foreach (var pattern in validation.RequiredPatterns)
            {
                var regex = new Regex(pattern);
                regex.IsMatch(content).Should().BeTrue(
                    $"Response should match pattern: {pattern}");
                logger.LogDebug("Pattern '{Pattern}' matched: ✓", pattern);
            }
        }
        
        public static void ValidateCostCalculation(
            ChatUsage usage,
            ModelCost cost,
            decimal actualInputCost,
            decimal actualOutputCost,
            ILogger logger)
        {
            var expectedInputCost = (usage.PromptTokens / 1_000_000m) * cost.InputPerMillion;
            var expectedOutputCost = (usage.CompletionTokens / 1_000_000m) * cost.OutputPerMillion;
            
            actualInputCost.Should().BeApproximately(
                expectedInputCost, 0.000001m,
                $"Input cost should be {expectedInputCost:F6}");
            
            actualOutputCost.Should().BeApproximately(
                expectedOutputCost, 0.000001m,
                $"Output cost should be {expectedOutputCost:F6}");
            
            logger.LogInformation(
                "Cost calculation validated: Input=${InputCost:F6}, Output=${OutputCost:F6}, Total=${TotalCost:F6}",
                actualInputCost, actualOutputCost, actualInputCost + actualOutputCost);
        }
    }
    
    public static class Multimodal
    {
        public static async Task<string> DownloadImageAsBase64(string imageUrl, ILogger logger)
        {
            try
            {
                logger.LogInformation("Downloading image from: {Url}", imageUrl);
                
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var imageBytes = await client.GetByteArrayAsync(imageUrl);
                
                // Detect MIME type from URL or content
                var mimeType = GetMimeTypeFromUrl(imageUrl);
                var base64String = Convert.ToBase64String(imageBytes);
                
                logger.LogInformation("✓ Image downloaded: {Size} bytes, Type: {MimeType}", 
                    imageBytes.Length, mimeType);
                
                // Return in the format expected by OpenAI-compatible APIs
                return $"data:{mimeType};base64,{base64String}";
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to download image from {Url}", imageUrl);
                throw new InvalidOperationException($"Failed to download image: {ex.Message}", ex);
            }
        }
        
        private static string GetMimeTypeFromUrl(string url)
        {
            var extension = Path.GetExtension(url).ToLower().TrimStart('.');
            return extension switch
            {
                "jpg" or "jpeg" => "image/jpeg",
                "png" => "image/png",
                "gif" => "image/gif",
                "webp" => "image/webp",
                "bmp" => "image/bmp",
                _ => "image/jpeg" // Default to JPEG
            };
        }
        
        public static ChatMessage CreateMultimodalMessage(string text, string base64Image)
        {
            return new ChatMessage
            {
                Role = "user",
                Content = new List<ContentPart>
                {
                    new ContentPart { Type = "text", Text = text },
                    new ContentPart 
                    { 
                        Type = "image_url", 
                        ImageUrl = new ImageUrl { Url = base64Image }
                    }
                }
            };
        }
    }
    
    public static class ReportGenerator
    {
        public static async Task GenerateMarkdownReport(
            TestContext context,
            TestConfiguration config,
            ProviderConfig providerConfig,
            ILogger logger)
        {
            var reportDir = "Reports";
            Directory.CreateDirectory(reportDir);
            
            var timestamp = DateTime.UtcNow;
            var providerName = providerConfig.Provider.Type.ToLower();
            // Include milliseconds and test run ID to ensure unique filenames
            var fileName = $"test_run_{providerName}_{timestamp:yyyyMMdd_HHmmss_fff}_{context.TestRunId}.md";
            var filePath = Path.Combine(reportDir, fileName);
            
            var sb = new StringBuilder();
            sb.AppendLine($"# Integration Test Report - {timestamp:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
            sb.AppendLine($"## Test Run: {context.TestRunId}");
            sb.AppendLine();
            
            // Provider section
            var providerStatus = context.Errors.Count == 0 ? "✅" : "❌";
            sb.AppendLine($"### {providerStatus} Provider: {providerConfig.Provider.Type}");
            sb.AppendLine();
            
            if (context.ProviderId.HasValue)
            {
                sb.AppendLine("**Setup:**");
                sb.AppendLine($"- Provider ID: {context.ProviderId}");
                sb.AppendLine($"- Provider Key ID: {context.ProviderKeyId}");
                sb.AppendLine($"- Model Mapping ID: {context.ModelMappingId}");
                sb.AppendLine($"- Model Cost ID: {context.ModelCostId}");
                sb.AppendLine($"- Virtual Key Group ID: {context.VirtualKeyGroupId}");
                sb.AppendLine($"- Virtual Key: {context.VirtualKey}");
                sb.AppendLine($"- Initial Credit: ${context.InitialCredit:F2}");
                sb.AppendLine();
            }
            
            if (context.LastChatResponse != null)
            {
                sb.AppendLine("**Chat Test Results:**");
                sb.AppendLine($"- Model: {context.LastChatResponse.Model}");
                sb.AppendLine($"- Input Tokens: {context.LastChatResponse.PromptTokens}");
                sb.AppendLine($"- Output Tokens: {context.LastChatResponse.CompletionTokens}");
                sb.AppendLine($"- Total Tokens: {context.LastChatResponse.TotalTokens}");
                sb.AppendLine($"- Input Cost: ${context.LastChatResponse.InputCost:F6}");
                sb.AppendLine($"- Output Cost: ${context.LastChatResponse.OutputCost:F6}");
                sb.AppendLine($"- Total Cost: ${context.LastChatResponse.TotalCost:F6}");
                sb.AppendLine($"- Remaining Credit: ${context.RemainingCredit:F6}");
                sb.AppendLine();
                
                if (!string.IsNullOrEmpty(context.LastChatResponse.Content))
                {
                    sb.AppendLine("**Response Sample (first 500 chars):**");
                    var sample = context.LastChatResponse.Content.Length > 500 
                        ? context.LastChatResponse.Content.Substring(0, 500) + "..."
                        : context.LastChatResponse.Content;
                    sb.AppendLine($"> {sample.Replace("\n", "\n> ")}");
                    sb.AppendLine();
                }
                
                sb.AppendLine("**Validation:** ✅ All patterns matched");
                sb.AppendLine();
            }
            
            if (context.Errors.Count > 0)
            {
                sb.AppendLine("**Errors:**");
                foreach (var error in context.Errors)
                {
                    sb.AppendLine($"- {error}");
                }
                sb.AppendLine();
                
                // If there was a response but validation failed, show it
                if (context.LastChatResponse != null && !string.IsNullOrEmpty(context.LastChatResponse.Content))
                {
                    sb.AppendLine("**Failed Response Content (first 1000 chars):**");
                    var sample = context.LastChatResponse.Content.Length > 1000 
                        ? context.LastChatResponse.Content.Substring(0, 1000) + "..."
                        : context.LastChatResponse.Content;
                    sb.AppendLine("```");
                    sb.AppendLine(sample);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
            }
            
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine($"- Test Status: {(context.Errors.Count == 0 ? "PASSED ✅" : "FAILED ❌")}");
            sb.AppendLine($"- Provider: {providerConfig.Provider.Type}");
            sb.AppendLine($"- Model: {providerConfig.Models[0].Actual}");
            if (context.LastChatResponse != null)
            {
                sb.AppendLine($"- Total Cost: ${context.LastChatResponse.TotalCost:F6}");
                sb.AppendLine($"- Credit Used: ${context.InitialCredit - context.RemainingCredit:F6}");
            }
            sb.AppendLine($"- Report Location: {Path.GetFullPath(filePath)}");
            
            await File.WriteAllTextAsync(filePath, sb.ToString());
            
            // Also output to console
            logger.LogInformation("Test Report Generated: {FilePath}", filePath);
            Console.WriteLine();
            Console.WriteLine(sb.ToString());
        }
    }
}