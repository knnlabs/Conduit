using System;
using System.Collections.Generic;
using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Replicate
{
    public partial class ReplicateClient
    {
        private string ExtractTextFromPredictionOutput(object? output)
        {
            // Handle different output formats from different models
            if (output == null)
            {
                return string.Empty;
            }

            try
            {
                // String output (common for text generation models)
                if (output is string str)
                {
                    return str;
                }

                // List of strings (some models return this)
                if (output is JsonElement element)
                {
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        return element.GetString() ?? string.Empty;
                    }
                    else if (element.ValueKind == JsonValueKind.Array)
                    {
                        // Try to read as array of strings
                        var result = new System.Text.StringBuilder();
                        foreach (var item in element.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                            {
                                result.Append(item.GetString());
                            }
                        }
                        return result.ToString();
                    }
                }

                // Last resort: serialize to JSON and try to extract
                return JsonSerializer.Serialize(output);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error extracting text from prediction output");
                return string.Empty;
            }
        }

        private List<string> ExtractImageUrlsFromPredictionOutput(object? output)
        {
            var urls = new List<string>();

            // Handle different output formats from different models
            if (output == null)
            {
                return urls;
            }

            try
            {
                // String output (single image URL)
                if (output is string str)
                {
                    urls.Add(str);
                    return urls;
                }

                // Array of strings (multiple image URLs)
                if (output is JsonElement element)
                {
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        urls.Add(element.GetString() ?? string.Empty);
                        return urls;
                    }
                    else if (element.ValueKind == JsonValueKind.Array)
                    {
                        // Try to read as array of strings
                        foreach (var item in element.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                            {
                                string? url = item.GetString();
                                if (!string.IsNullOrEmpty(url))
                                {
                                    urls.Add(url);
                                }
                            }
                        }
                        return urls;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error extracting image URLs from prediction output");
            }

            return urls;
        }

        private List<string> ExtractVideoUrlsFromPredictionOutput(object? output)
        {
            // Video models typically return URLs in the same format as image models
            // This method is separate in case we need video-specific handling in the future
            return ExtractImageUrlsFromPredictionOutput(output);
        }

        private int EstimateTokenCount(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            // Very rough estimate: 4 characters per token (English text)
            return text.Length / 4;
        }
    }
}