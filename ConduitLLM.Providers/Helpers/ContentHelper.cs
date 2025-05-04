using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace ConduitLLM.Providers.Helpers
{
    /// <summary>
    /// Helper class for handling multimodal message content.
    /// </summary>
    public static class ContentHelper
    {
        /// <summary>
        /// Extracts multimodal content as a list of text content parts.
        /// </summary>
        /// <param name="content">The message content (can be string or content parts)</param>
        /// <returns>List of string content parts</returns>
        public static List<string> ExtractMultimodalContent(object? content)
        {
            var textParts = new List<string>();
            
            if (content == null)
                return textParts;
                
            if (content is string textContent)
            {
                textParts.Add(textContent);
                return textParts;
            }
            
            // Handle JSON Element or list of content parts
            if (content is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    textParts.Add(jsonElement.GetString() ?? string.Empty);
                    return textParts;
                }
                
                if (jsonElement.ValueKind == JsonValueKind.Array)
                {
                    // Extract all text content parts
                    foreach (var element in jsonElement.EnumerateArray())
                    {
                        if (element.TryGetProperty("type", out var typeElement) && 
                            typeElement.GetString() == "text" &&
                            element.TryGetProperty("text", out var textElement))
                        {
                            textParts.Add(textElement.GetString() ?? string.Empty);
                        }
                    }
                    return textParts;
                }
            }
            
            // Try to serialize and then extract text parts (for collections or other objects)
            try
            {
                var json = JsonSerializer.Serialize(content);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                if (root.ValueKind == JsonValueKind.Array)
                {
                    // It's likely content parts
                    foreach (var element in root.EnumerateArray())
                    {
                        if (element.TryGetProperty("type", out var typeElement) && 
                            typeElement.GetString() == "text" &&
                            element.TryGetProperty("text", out var textElement))
                        {
                            textParts.Add(textElement.GetString() ?? string.Empty);
                        }
                    }
                    return textParts;
                }
            }
            catch (Exception)
            {
                // If we can't process it properly, just use the string representation
            }
            
            // Fallback: Just add the string representation
            textParts.Add(content.ToString() ?? string.Empty);
            return textParts;
        }
        /// <summary>
        /// Converts message content (which could be a string or content parts) to a simple string.
        /// </summary>
        /// <param name="content">The message content (can be string or content parts)</param>
        /// <returns>String representation of the content</returns>
        public static string GetContentAsString(object? content)
        {
            if (content == null)
                return string.Empty;
            
            if (content is string textContent)
                return textContent;
            
            // Handle JSON Element or list of content parts
            if (content is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.String)
                    return jsonElement.GetString() ?? string.Empty;
                
                if (jsonElement.ValueKind == JsonValueKind.Array)
                {
                    // Combine all text content parts
                    var sb = new StringBuilder();
                    foreach (var element in jsonElement.EnumerateArray())
                    {
                        if (element.TryGetProperty("type", out var typeElement) && 
                            typeElement.GetString() == "text" &&
                            element.TryGetProperty("text", out var textElement))
                        {
                            sb.AppendLine(textElement.GetString());
                        }
                        // For now, we ignore image_url parts in providers that don't support them
                    }
                    return sb.ToString();
                }
            }
            
            // Try to serialize and then extract text parts (for collections or other objects)
            try
            {
                var json = JsonSerializer.Serialize(content);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                if (root.ValueKind == JsonValueKind.Array)
                {
                    // It's likely content parts
                    var sb = new StringBuilder();
                    foreach (var element in root.EnumerateArray())
                    {
                        if (element.TryGetProperty("type", out var typeElement) && 
                            typeElement.GetString() == "text" &&
                            element.TryGetProperty("text", out var textElement))
                        {
                            sb.AppendLine(textElement.GetString());
                        }
                    }
                    return sb.ToString();
                }
            }
            catch (Exception)
            {
                // If we can't process it properly, just return the string representation
            }
            
            // Fallback: Just return the string representation
            return content.ToString() ?? string.Empty;
        }
        
        /// <summary>
        /// Determines if the content contains only text (no images).
        /// </summary>
        /// <param name="content">The message content</param>
        /// <returns>True if the content is text-only, false if it contains images</returns>
        public static bool IsTextOnly(object? content)
        {
            if (content == null || content is string)
                return true;
            
            if (content is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.String)
                    return true;
                
                if (jsonElement.ValueKind == JsonValueKind.Array)
                {
                    // Check each element in the array
                    foreach (var element in jsonElement.EnumerateArray())
                    {
                        if (element.TryGetProperty("type", out var typeElement) && 
                            typeElement.GetString() == "image_url")
                        {
                            return false; // Found an image
                        }
                    }
                    return true; // No images found
                }
            }
            
            // Try to serialize and check parts
            try
            {
                var json = JsonSerializer.Serialize(content);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in root.EnumerateArray())
                    {
                        if (element.TryGetProperty("type", out var typeElement) && 
                            typeElement.GetString() == "image_url")
                        {
                            return false; // Found an image
                        }
                    }
                }
                
                return true; // No images found
            }
            catch
            {
                // If we can't process it, assume it's text-only
                return true;
            }
        }
    }
}
