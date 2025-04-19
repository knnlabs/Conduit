using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Examples
{
    /// <summary>
    /// Example demonstrating how to use ConduitLLM with multimodal (vision) capabilities.
    /// </summary>
    public class VisionExample
    {
        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        /// <summary>
        /// Send a multimodal request to Conduit with text and image content.
        /// </summary>
        public static async Task RunVisionExample(string conduitApiUrl, string apiKey)
        {
            Console.WriteLine("Running Vision Example...");
            Console.WriteLine("------------------------");

            // Create a message with an image and text
            var messages = new List<Message>
            {
                new Message
                {
                    Role = "user",
                    // Using multimodal content format with both text and image
                    Content = new List<object>
                    {
                        new TextContentPart
                        {
                            Text = "What's in this image? Describe it in detail."
                        },
                        new ImageUrlContentPart
                        {
                            ImageUrl = new ImageUrl
                            {
                                // For demo, using a public image URL
                                // In production, you could use a data URL with base64 encoded image
                                Url = "https://images.unsplash.com/photo-1579353977828-2a4eab540b9a",
                                Detail = "high" // Request high detail analysis
                            }
                        }
                    }
                }
            };

            // Create the request
            var request = new ChatCompletionRequest
            {
                Model = "gpt-4-vision", // Use a model with vision capabilities
                Messages = messages,
                MaxTokens = 1000,
                Temperature = 0.7,
            };

            // Send the request to Conduit
            var response = await SendRequestAsync(conduitApiUrl, request, apiKey);

            // Display the response
            if (response != null)
            {
                Console.WriteLine("\nResponse:");
                Console.WriteLine("---------");
                Console.WriteLine($"Model: {response.Model}");
                
                if (response.Choices.Count > 0)
                {
                    var content = response.Choices[0].Message.Content;
                    Console.WriteLine($"Content: {content}");
                }
                
                if (response.Usage != null)
                {
                    Console.WriteLine("\nToken Usage:");
                    Console.WriteLine($"  Prompt tokens: {response.Usage.PromptTokens}");
                    Console.WriteLine($"  Completion tokens: {response.Usage.CompletionTokens}");
                    Console.WriteLine($"  Total tokens: {response.Usage.TotalTokens}");
                }
            }
            else
            {
                Console.WriteLine("No response received.");
            }
        }

        /// <summary>
        /// Sends a request to the Conduit API.
        /// </summary>
        private static async Task<ChatCompletionResponse?> SendRequestAsync(
            string apiUrl, 
            ChatCompletionRequest request, 
            string apiKey)
        {
            try
            {
                var requestUri = $"{apiUrl.TrimEnd('/')}/v1/chat/completions";
                Console.WriteLine($"Sending request to: {requestUri}");

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri);
                httpRequest.Content = JsonContent.Create(request, options: _jsonOptions);
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                // Send the request
                using var response = await _httpClient.SendAsync(httpRequest);
                
                // Check if the request was successful
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error: {response.StatusCode}");
                    Console.WriteLine(error);
                    return null;
                }

                // Parse the response
                return await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(_jsonOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                return null;
            }
        }
    }
}
