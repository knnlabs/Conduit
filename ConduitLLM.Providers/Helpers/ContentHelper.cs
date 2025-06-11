using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

using ConduitLLM.Core.Models;

namespace ConduitLLM.Providers.Helpers
{
    /// <summary>
    /// Helper class for handling multimodal message content, providing utilities for
    /// working with text and image content parts in messages.
    /// </summary>
    public static class ContentHelper
    {
        /// <summary>
        /// Extracts multimodal content as a list of text content parts,
        /// filtering out non-text content like images.
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

            // Handle ContentParts from direct API usage
            if (content is IEnumerable<object> contentList)
            {
                foreach (var part in contentList)
                {
                    if (part is TextContentPart textPart)
                    {
                        textParts.Add(textPart.Text);
                    }
                }

                if (textParts.Any())
                {
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
        /// Useful for providers that don't support multimodal inputs.
        /// </summary>
        /// <param name="content">The message content (can be string or content parts)</param>
        /// <returns>String representation of the content, omitting non-text parts</returns>
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
                            string? text = textElement.GetString();
                            if (!string.IsNullOrEmpty(text))
                            {
                                if (sb.Length > 0)
                                    sb.AppendLine();

                                sb.Append(text);
                            }
                        }
                        // Image content is omitted for providers that don't support them
                    }
                    return sb.ToString();
                }
            }

            // Handle ContentParts from direct API usage
            if (content is IEnumerable<object> contentList)
            {
                var sb = new StringBuilder();
                foreach (var part in contentList)
                {
                    if (part is TextContentPart textPart)
                    {
                        if (!string.IsNullOrEmpty(textPart.Text))
                        {
                            if (sb.Length > 0)
                                sb.AppendLine();

                            sb.Append(textPart.Text);
                        }
                    }
                }

                if (sb.Length > 0)
                {
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
                            string? text = textElement.GetString();
                            if (!string.IsNullOrEmpty(text))
                            {
                                if (sb.Length > 0)
                                    sb.AppendLine();

                                sb.Append(text);
                            }
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

            // Handle ContentParts from direct API usage
            if (content is IEnumerable<object> contentList)
            {
                foreach (var part in contentList)
                {
                    if (part is ImageUrlContentPart)
                        return false;

                    // Check for type property dynamically for custom implementations
                    var type = part.GetType().GetProperty("Type")?.GetValue(part)?.ToString();
                    if (type == "image_url")
                        return false;
                }

                return true;
            }

            // Handle JSON Element
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

        /// <summary>
        /// Extracts image URLs from multimodal content.
        /// </summary>
        /// <param name="content">The message content (can be string or content parts)</param>
        /// <returns>List of image URLs found in the content</returns>
        public static List<ImageUrl> ExtractImageUrls(object? content)
        {
            var imageUrls = new List<ImageUrl>();

            if (content == null)
                return imageUrls;

            if (content is string)
                return imageUrls; // Plain strings don't contain images

            // Handle ContentParts from direct API usage
            if (content is IEnumerable<object> contentList)
            {
                foreach (var part in contentList)
                {
                    if (part is ImageUrlContentPart imagePart && imagePart.ImageUrl != null)
                    {
                        imageUrls.Add(imagePart.ImageUrl);
                    }
                }

                if (imageUrls.Any())
                {
                    return imageUrls;
                }
            }

            // Handle JSON Element
            if (content is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in jsonElement.EnumerateArray())
                {
                    if (element.TryGetProperty("type", out var typeElement) &&
                        typeElement.GetString() == "image_url" &&
                        element.TryGetProperty("image_url", out var imageUrlElement))
                    {
                        string? url = null;
                        string? detail = null;

                        if (imageUrlElement.TryGetProperty("url", out var urlElement))
                        {
                            url = urlElement.GetString();
                        }

                        if (imageUrlElement.TryGetProperty("detail", out var detailElement))
                        {
                            detail = detailElement.GetString();
                        }

                        if (!string.IsNullOrEmpty(url))
                        {
                            imageUrls.Add(new ImageUrl
                            {
                                Url = url,
                                Detail = detail
                            });
                        }
                    }
                }
            }

            return imageUrls;
        }

        /// <summary>
        /// Creates a standard multimodal content list combining text and image parts.
        /// </summary>
        /// <param name="text">Text content to include</param>
        /// <param name="imageUrls">Optional collection of image URLs to include</param>
        /// <returns>A list of content parts (TextContentPart and ImageUrlContentPart objects)</returns>
        public static List<object> CreateMultimodalContent(string text, IEnumerable<ImageUrl>? imageUrls = null)
        {
            var content = new List<object>();

            // Add text content if present
            if (!string.IsNullOrEmpty(text))
            {
                content.Add(new TextContentPart { Text = text });
            }

            // Add images if present
            if (imageUrls != null)
            {
                foreach (var imageUrl in imageUrls)
                {
                    content.Add(new ImageUrlContentPart { ImageUrl = imageUrl });
                }
            }

            return content;
        }

        /// <summary>
        /// Creates a string description of any multimodal content (useful for logging).
        /// </summary>
        /// <param name="content">The message content to describe</param>
        /// <returns>A string description of the content</returns>
        public static string DescribeContent(object? content)
        {
            if (content == null)
                return "[null]";

            if (content is string textContent)
                return $"Text: {(textContent.Length > 50 ? textContent.Substring(0, 47) + "..." : textContent)}";

            var textParts = ExtractMultimodalContent(content);
            var imageUrls = ExtractImageUrls(content);

            var sb = new StringBuilder();

            if (textParts.Any())
            {
                var combinedText = string.Join(" ", textParts);
                sb.Append($"Text parts: {textParts.Count} ({(combinedText.Length > 50 ? combinedText.Substring(0, 47) + "..." : combinedText)})");
            }

            if (imageUrls.Any())
            {
                if (sb.Length > 0)
                    sb.Append(", ");

                sb.Append($"Image parts: {imageUrls.Count}");
            }

            if (sb.Length == 0)
                return "[unknown content format]";

            return sb.ToString();
        }
    }
}
