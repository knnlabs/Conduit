using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Examples;

class Program
{
    static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var conduit = host.Services.GetRequiredService<Conduit>(); // Get Conduit instance

        logger.LogInformation("ConduitLLM Example Starting...");

        // --- Example Chat Completion Request ---
        var request = new ChatCompletionRequest
        {
            // *** IMPORTANT: Replace "your-model-alias" with an alias defined in your appsettings.json ***
            // Example aliases: "openai-gpt4o", "anthropic-claude3-sonnet", "gemini-1.5-flash", "cohere-command-r"
            Model = "openai-gpt4o", // CHANGE THIS to a configured model alias
            Messages = new List<Message>
            {
                new Message { Role = MessageRole.System, Content = "You are a helpful assistant." },
                new Message { Role = MessageRole.User, Content = "Tell me a short story about a curious cat exploring a garden." }
            },
            MaxTokens = 150,
            Temperature = 0.7f
        };

        // --- Non-Streaming Example (Optional) ---
        // try
        // {
        //     logger.LogInformation("Sending non-streaming request to model: {ModelAlias}", request.Model);
        //     var response = await conduit.CreateChatCompletionAsync(request);
        //     logger.LogInformation("Received non-streaming response:");
        //     Console.WriteLine($"Response Text: {response.Choices.FirstOrDefault()?.Message.Content}");
        //     logger.LogInformation("Usage: Prompt={PromptTokens}, Completion={CompletionTokens}, Total={TotalTokens}",
        //         response.Usage?.PromptTokens, response.Usage?.CompletionTokens, response.Usage?.TotalTokens);
        // }
        // catch (Exception ex)
        // {
        //     logger.LogError(ex, "Error during non-streaming chat completion");
        // }

        Console.WriteLine("\n---\n");

        // --- Streaming Example ---
        try
        {
            logger.LogInformation("Sending streaming request to model: {ModelAlias}", request.Model);
            Console.WriteLine("Streaming Response:");

            string? finalFinishReason = null;
            await foreach (var chunk in conduit.StreamChatCompletionAsync(request))
            {
                if (chunk.Choices != null && chunk.Choices.Count > 0)
                {
                    var choice = chunk.Choices[0];
                    if (choice.Delta?.Content != null)
                    {
                        Console.Write(choice.Delta.Content); // Write content delta directly
                    }
                    if (choice.FinishReason != null)
                    {
                        finalFinishReason = choice.FinishReason;
                        // Optionally print finish reason here or after the loop
                    }
                }
            }
            Console.WriteLine(); // Newline after stream finishes
            logger.LogInformation("Stream finished. Final Finish Reason: {FinishReason}", finalFinishReason ?? "N/A");

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during streaming chat completion");
            Console.WriteLine($"\nSTREAMING ERROR: {ex.Message}");
        }

        logger.LogInformation("ConduitLLM Example Finished.");
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                // Load configuration from appsettings.json
                config.SetBasePath(Directory.GetCurrentDirectory())
                      .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                      .AddEnvironmentVariables(); // Allow overriding with environment variables
            })
            .ConfigureServices((context, services) =>
            {
                // Configure logging
                services.AddLogging(configure => configure.AddConsole());

                // Configure Options pattern for ConduitSettings
                services.Configure<ConduitSettings>(context.Configuration.GetSection("Conduit"));

                // Register the LLMClientFactory and Conduit orchestrator
                services.AddSingleton<ILLMClientFactory, LLMClientFactory>();
                services.AddSingleton<Conduit>(); // Register Conduit itself
            });
}
