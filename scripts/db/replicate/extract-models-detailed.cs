#!/usr/bin/env -S dotnet run
#:package AngleSharp@1.1.2

using AngleSharp;
using AngleSharp.Html.Parser;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

// Parse command-line arguments
if (args.Length == 0)
{
    Console.WriteLine("Usage: ./extract-models-detailed.cs <model-type> [--output <format>] [--filename <path>]");
    Console.WriteLine("Model types: image, video, text");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --output <format>   Output format: console (default), sql");
    Console.WriteLine("  --filename <path>   Output filename (required for sql output)");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  ./extract-models-detailed.cs text");
    Console.WriteLine("  ./extract-models-detailed.cs text --output sql --filename replicate-text-models.sql");
    Console.WriteLine("  ./extract-models-detailed.cs image --output sql --filename replicate-image-models.sql");
    return;
}

var modelType = args[0].ToLower();
if (modelType != "image" && modelType != "video" && modelType != "text")
{
    Console.WriteLine($"Error: Invalid model type '{args[0]}'");
    Console.WriteLine("Valid types: image, video, text");
    return;
}

// Parse optional arguments
string outputFormat = "console";
string? outputFilename = null;

for (int i = 1; i < args.Length; i++)
{
    if (args[i] == "--output" && i + 1 < args.Length)
    {
        outputFormat = args[i + 1].ToLower();
        i++;
    }
    else if (args[i] == "--filename" && i + 1 < args.Length)
    {
        outputFilename = args[i + 1];
        i++;
    }
}

// Validate output format
if (outputFormat != "console" && outputFormat != "sql")
{
    Console.WriteLine($"Error: Invalid output format '{outputFormat}'");
    Console.WriteLine("Valid formats: console, sql");
    return;
}

// Validate filename for SQL output
if (outputFormat == "sql" && string.IsNullOrEmpty(outputFilename))
{
    Console.WriteLine("Error: --filename is required when --output sql is specified");
    return;
}

// Map model type to collection URL
var collectionUrl = modelType switch
{
    "image" => "https://replicate.com/collections/text-to-image",
    "video" => "https://replicate.com/collections/text-to-video",
    "text" => "https://replicate.com/collections/language-models",
    _ => throw new InvalidOperationException($"Unexpected model type: {modelType}")
};

var modelTypeLabel = modelType switch
{
    "image" => "Image",
    "video" => "Video",
    "text" => "Text/LLM",
    _ => modelType
};

Console.WriteLine($"=== Replicate {modelTypeLabel} Models Extraction ===");
Console.WriteLine($"Fetching models from: {collectionUrl}");
Console.WriteLine();

using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

// Step 1: Get list of models from collection page
var html = await httpClient.GetStringAsync(collectionUrl);
var models = ExtractModelsFromCollection(html);

Console.WriteLine($"Found {models.Count} models in collection");
Console.WriteLine();

// Step 2: Fetch detailed schema for each model
var detailedModels = new List<DetailedModel>();
int processed = 0;

foreach (var model in models)
{
    processed++;
    Console.WriteLine($"[{processed}/{models.Count}] Processing {model.Owner}/{model.Name}...");

    try
    {
        var schemaUrl = $"https://replicate.com/{model.Owner}/{model.Name}/api/schema";
        var schemaHtml = await httpClient.GetStringAsync(schemaUrl);

        var modelFamily = DetectModelFamily(model.Name);

        var capabilities = modelType switch
        {
            "image" => new ModelCapabilities
            {
                SupportsImageGeneration = true,
                SupportsVision = true,
                SupportsChat = false,
                SupportsEmbeddings = false,
                SupportsFunctionCalling = false,
                SupportsVideoGeneration = false,
                SupportsStreaming = false
            },
            "video" => new ModelCapabilities
            {
                SupportsImageGeneration = false,
                SupportsVision = true,  // Video models can take image inputs
                SupportsChat = false,
                SupportsEmbeddings = false,
                SupportsFunctionCalling = false,
                SupportsVideoGeneration = true,
                SupportsStreaming = false
            },
            "text" => new ModelCapabilities
            {
                SupportsImageGeneration = false,
                SupportsVision = false,  // Set based on model specifics if needed
                SupportsChat = true,
                SupportsEmbeddings = false,
                SupportsFunctionCalling = false,
                SupportsVideoGeneration = false,
                SupportsStreaming = true  // Most text models support streaming
            },
            _ => throw new InvalidOperationException($"Unexpected model type: {modelType}")
        };

        var (inputSchema, maxOutputTokens) = ExtractSchemaFromPage(schemaHtml, modelType);

        var detailedModel = new DetailedModel
        {
            Owner = model.Owner,
            ModelName = model.Name,
            ModelFamily = modelFamily,
            SeriesName = DetermineSeriesName(modelFamily, model.Owner, detailedModels),
            Url = $"https://replicate.com{model.Url}",
            ReplicateIdentifier = $"{model.Owner}/{model.Name}",
            SchemaUrl = schemaUrl,
            InputSchema = inputSchema,
            MaxOutputTokens = maxOutputTokens,
            MaxInputTokens = null,  // Requires manual lookup from official docs
            TokenizerType = "None", // Requires manual configuration per model family
            ModelType = modelType,
            Capabilities = capabilities
        };

        detailedModels.Add(detailedModel);

        // Small delay to be respectful of rate limits
        await Task.Delay(100);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ⚠️  Error: {ex.Message}");
    }
}

Console.WriteLine();
Console.WriteLine("=== Extraction Complete ===");
Console.WriteLine($"Successfully processed {detailedModels.Count} models");
Console.WriteLine();

// Load token limits reference file for text models SQL generation
Dictionary<string, TokenLimitReference>? tokenLimits = null;
if (outputFormat == "sql" && modelType == "text")
{
    try
    {
        var tokenLimitsPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".", "replicate-token-limits.json");
        if (!File.Exists(tokenLimitsPath))
        {
            // Try current directory
            tokenLimitsPath = "replicate-token-limits.json";
        }

        if (File.Exists(tokenLimitsPath))
        {
            var tokenLimitsJson = await File.ReadAllTextAsync(tokenLimitsPath);
            var tokenLimitsDoc = JsonDocument.Parse(tokenLimitsJson);
            var modelsNode = tokenLimitsDoc.RootElement.GetProperty("models");

            tokenLimits = new Dictionary<string, TokenLimitReference>();
            foreach (var model in modelsNode.EnumerateObject())
            {
                tokenLimits[model.Name] = new TokenLimitReference
                {
                    MaxInputTokens = model.Value.GetProperty("maxInputTokens").GetInt32(),
                    TokenizerType = model.Value.GetProperty("tokenizerType").GetString() ?? "None",
                    Notes = model.Value.TryGetProperty("notes", out var notes) ? notes.GetString() : null
                };
            }
            Console.WriteLine($"Loaded token limits for {tokenLimits.Count} models from {tokenLimitsPath}");
        }
        else
        {
            Console.WriteLine("⚠️  Warning: replicate-token-limits.json not found. MaxInputTokens will be null.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️  Warning: Failed to load token limits: {ex.Message}");
    }
    Console.WriteLine();
}
// Route to appropriate output handler
if (outputFormat == "sql")
{
    await GenerateSQLOutput(detailedModels, modelType, outputFilename!, tokenLimits);
}
else
{
    // Console output
    Console.WriteLine("=== Results ===");
    Console.WriteLine();

    // Output as simple structured format
    foreach (var model in detailedModels)
{
    Console.WriteLine($"Owner: {model.Owner}");
    Console.WriteLine($"Model Name: {model.ModelName}");
    Console.WriteLine($"Model Type: {model.ModelType}");
    Console.WriteLine($"Model Family: {model.ModelFamily}");
    Console.WriteLine($"Series Name: {model.SeriesName}");
    Console.WriteLine($"URL: {model.Url}");
    Console.WriteLine($"Replicate ID: {model.ReplicateIdentifier}");
    Console.WriteLine($"Schema URL: {model.SchemaUrl}");
    Console.WriteLine($"Capabilities:");

    // Output capabilities based on model type
    if (model.ModelType == "image")
    {
        Console.WriteLine($"  - Image Generation: {model.Capabilities.SupportsImageGeneration}");
        Console.WriteLine($"  - Vision: {model.Capabilities.SupportsVision}");
    }
    else if (model.ModelType == "video")
    {
        Console.WriteLine($"  - Video Generation: {model.Capabilities.SupportsVideoGeneration}");
        Console.WriteLine($"  - Vision: {model.Capabilities.SupportsVision}");
    }
    else if (model.ModelType == "text")
    {
        Console.WriteLine($"  - Chat: {model.Capabilities.SupportsChat}");
        Console.WriteLine($"  - Streaming: {model.Capabilities.SupportsStreaming}");
        Console.WriteLine($"  - Vision: {model.Capabilities.SupportsVision}");
    }

    // Output token limits for text models
    if (model.ModelType == "text")
    {
        Console.WriteLine("Token Limits:");
        if (model.MaxOutputTokens.HasValue)
        {
            Console.WriteLine($"  - Max Output Tokens: {model.MaxOutputTokens.Value} (extracted from schema)");
        }
        else
        {
            Console.WriteLine($"  - Max Output Tokens: (not found in schema)");
        }
        Console.WriteLine($"  - Max Input Tokens: null (⚠️  requires manual lookup from official docs)");
        Console.WriteLine($"TokenizerType: {model.TokenizerType} (⚠️  requires manual configuration)");
    }

    // Output Parameters JSON
    if (model.InputSchema != null && model.InputSchema.Count > 0)
    {
        Console.WriteLine($"Parameters: {FormatParametersJson(model.InputSchema)}");
    }
    else
    {
        Console.WriteLine("Parameters: (schema extraction failed)");
    }

    Console.WriteLine("---");
}

// Also output unique authors and families for summary
Console.WriteLine();
Console.WriteLine("=== Summary ===");
Console.WriteLine();
var uniqueOwners = detailedModels.Select(m => m.Owner).Distinct().OrderBy(o => o).ToList();
var uniqueFamilies = detailedModels.Select(m => m.ModelFamily).Distinct().OrderBy(f => f).ToList();

Console.WriteLine($"Unique Authors ({uniqueOwners.Count}):");
foreach (var owner in uniqueOwners)
{
    var count = detailedModels.Count(m => m.Owner == owner);
    Console.WriteLine($"  - {owner} ({count} models)");
}

Console.WriteLine();
Console.WriteLine($"Unique Model Families ({uniqueFamilies.Count}):");
foreach (var family in uniqueFamilies)
{
    var count = detailedModels.Count(m => m.ModelFamily == family);
    var owners = detailedModels.Where(m => m.ModelFamily == family).Select(m => m.Owner).Distinct().ToList();
    Console.WriteLine($"  - {family} ({count} models) - Authors: {string.Join(", ", owners)}");
}

Console.WriteLine();
var uniqueSeries = detailedModels.Select(m => new { m.SeriesName, m.Owner }).Distinct().OrderBy(s => s.SeriesName).ToList();
Console.WriteLine($"Unique Model Series ({uniqueSeries.Count}):");
foreach (var series in uniqueSeries)
{
    var count = detailedModels.Count(m => m.SeriesName == series.SeriesName && m.Owner == series.Owner);
    Console.WriteLine($"  - {series.SeriesName} ({series.Owner}) - {count} models");
}

// Add warning for text models about manual configuration needed
if (detailedModels.Any() && detailedModels.First().ModelType == "text")
{
    Console.WriteLine();
    Console.WriteLine("=== ⚠️  IMPORTANT: Text Model Configuration Required ===");
    Console.WriteLine();
    Console.WriteLine("The following fields require manual configuration:");
    Console.WriteLine("1. MaxInputTokens (context window) - must be looked up from official model documentation");
    Console.WriteLine("2. TokenizerType - must be determined based on model family");
    Console.WriteLine();
    Console.WriteLine("Token extraction summary:");
    var withMaxOutput = detailedModels.Count(m => m.MaxOutputTokens.HasValue);
    var withoutMaxOutput = detailedModels.Count(m => !m.MaxOutputTokens.HasValue);
    Console.WriteLine($"  - MaxOutputTokens extracted: {withMaxOutput} models");
    Console.WriteLine($"  - MaxOutputTokens not found: {withoutMaxOutput} models");
    Console.WriteLine();
    Console.WriteLine("Recommended: Create a reference file (e.g., replicate-token-limits.json) mapping");
    Console.WriteLine("model identifiers to their context windows and tokenizer types.");
}
} // End of console output else block

// Helper methods

static List<BasicModel> ExtractModelsFromCollection(string html)
{
    var models = new List<BasicModel>();
    var processedUrls = new HashSet<string>();

    var enqueuePattern = @"streamController\.enqueue\(""(.*?)""\);";
    var matches = Regex.Matches(html, enqueuePattern, RegexOptions.Singleline);

    foreach (Match match in matches)
    {
        var encodedJson = match.Groups[1].Value;
        var decodedJson = encodedJson
            .Replace(@"\""", "\"")
            .Replace(@"\\", @"\")
            .Replace(@"\/", "/");

        var urlPattern = @"""(/[a-zA-Z0-9_-]+/[a-zA-Z0-9._-]+)""";
        var urlMatches = Regex.Matches(decodedJson, urlPattern);

        foreach (Match urlMatch in urlMatches)
        {
            var modelUrl = urlMatch.Groups[1].Value;
            var parts = modelUrl.TrimStart('/').Split('/');

            if (parts.Length == 2 && !processedUrls.Contains(modelUrl))
            {
                processedUrls.Add(modelUrl);
                models.Add(new BasicModel
                {
                    Owner = parts[0],
                    Name = parts[1],
                    Url = modelUrl
                });
            }
        }
    }

    return models;
}

static string DetectModelFamily(string modelName)
{
    // Remove common suffixes that indicate variants, not families (in order - most specific first)
    var cleanName = modelName;
    var suffixesToRemove = new[] {
        "-dev-lora", "-controlnet-lora", "-multi-controlnet-lora",  // Composite suffixes first
        "-turbo", "-fast", "-pro", "-ultra", "-dev", "-full", "-lora",
        "-balanced", "-quality", "-max", "-lightning", "-emoji", "-svg",
        "-controlnet", "-multi", "-aesthetic", "-scribble", "-diffusion",
        "-schnell", "-flash", "-origin", "-maker", "-workflow", "-sprint"
    };

    // Remove all matching suffixes (handles cases like "flux-dev-lora" → "flux")
    bool changed = true;
    while (changed)
    {
        changed = false;
        foreach (var suffix in suffixesToRemove)
        {
            var newCleanName = Regex.Replace(cleanName, suffix + "$", "", RegexOptions.IgnoreCase);
            if (newCleanName != cleanName)
            {
                cleanName = newCleanName;
                changed = true;
                break;  // Start over to handle multiple suffixes
            }
        }
    }

    // Extract base family name (everything before version numbers)
    var parts = cleanName.Split('-');
    var familyParts = new List<string>();

    foreach (var part in parts)
    {
        // Stop at version numbers (v1, v2, v3, 1.1, 2.2, etc.)
        if (Regex.IsMatch(part, @"^v?\d+(\.\d+)*[a-z]*$"))
            break;
        // Skip very short parts (likely not meaningful family names)
        if (part.Length <= 1)
            continue;
        familyParts.Add(part);
    }

    if (familyParts.Count == 0)
    {
        // If no parts remain, use the first part of the original name
        var firstPart = modelName.Split('-')[0];
        familyParts.Add(firstPart);
    }

    var family = string.Join("-", familyParts);

    // Titlecase the result
    var textInfo = CultureInfo.CurrentCulture.TextInfo;
    return textInfo.ToTitleCase(family.ToLower());
}

static (Dictionary<string, object>? parameters, int? maxOutputTokens) ExtractSchemaFromPage(string html, string modelType)
{
    try
    {
        // Replicate embeds the OpenAPI schema in script tags
        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);

        // Look for script tags containing OpenAPI schema
        var scripts = document.QuerySelectorAll("script");

        foreach (var script in scripts)
        {
            var content = script.TextContent;

            // Look for components.schemas.Input schema
            if (content.Contains("\"components\"") && content.Contains("\"schemas\"") && content.Contains("\"Input\""))
            {
                try
                {
                    // Find "Input": {"type": "object"... pattern in components.schemas section
                    // Note: May have spaces after colons
                    var inputStart = content.IndexOf("\"Input\":");
                    if (inputStart == -1) continue;

                    // Verify it's followed by type object (allowing for spaces)
                    var typeCheck = content.Substring(inputStart, Math.Min(100, content.Length - inputStart));
                    if (!typeCheck.Contains("\"type\"") || !typeCheck.Contains("\"object\""))
                        continue;

                    // Find the properties field within this Input schema
                    var propertiesStart = content.IndexOf("\"properties\":", inputStart);
                    if (propertiesStart == -1) continue;

                    // Make sure this properties is within a reasonable distance (should be close to Input definition)
                    if (propertiesStart - inputStart > 500) continue;

                    // Skip to the opening brace of properties
                    var braceStart = content.IndexOf('{', propertiesStart + "\"properties\":".Length);
                    if (braceStart == -1) continue;

                    // Extract the JSON object by counting braces
                    var propertiesJson = ExtractJsonObject(content, braceStart);

                    if (!string.IsNullOrEmpty(propertiesJson))
                    {
                        // Parse the properties JSON
                        var inputProperties = JsonNode.Parse(propertiesJson);

                        if (inputProperties != null)
                        {
                            // Extract max output tokens for text models
                            int? maxOutputTokens = null;
                            if (modelType == "text")
                            {
                                maxOutputTokens = ExtractMaxOutputTokens(inputProperties);
                            }

                            // Convert Replicate schema to Conduit parameters format
                            var parameters = ConvertReplicateSchemaToConduitParameters(inputProperties, modelType);
                            return (parameters, maxOutputTokens);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ⚠️  Schema parsing error: {ex.Message}");
                    // Continue to next script
                }
            }
        }

        return (null, null);
    }
    catch
    {
        return (null, null);
    }
}

static int? ExtractMaxOutputTokens(JsonNode inputProperties)
{
    // Look for common token parameter names and extract their maximum value
    var tokenParamNames = new[] {
        "max_tokens",
        "max_output_tokens",
        "max_new_tokens",
        "num_tokens",
        "length"
    };

    foreach (var paramName in tokenParamNames)
    {
        var param = inputProperties[paramName];
        if (param == null) continue;

        // Try to get maximum from the schema
        var maximum = param["maximum"];
        if (maximum != null)
        {
            try
            {
                return maximum.GetValue<int>();
            }
            catch
            {
                // Try as long
                try
                {
                    return (int)maximum.GetValue<long>();
                }
                catch
                {
                    continue;
                }
            }
        }
    }

    return null;
}

static string ExtractJsonObject(string content, int startIndex)
{
    var sb = new StringBuilder();
    var braceCount = 0;
    var inString = false;
    var escapeNext = false;

    for (int i = startIndex; i < content.Length; i++)
    {
        var ch = content[i];
        sb.Append(ch);

        if (escapeNext)
        {
            escapeNext = false;
            continue;
        }

        if (ch == '\\')
        {
            escapeNext = true;
            continue;
        }

        if (ch == '"')
        {
            inString = !inString;
            continue;
        }

        if (inString) continue;

        if (ch == '{')
        {
            braceCount++;
        }
        else if (ch == '}')
        {
            braceCount--;
            if (braceCount == 0)
            {
                // Found the matching closing brace
                return sb.ToString();
            }
        }
    }

    return "";  // Unmatched braces
}

static Dictionary<string, object> ConvertReplicateSchemaToConduitParameters(JsonNode inputProperties, string modelType)
{
    var parameters = new Dictionary<string, object>();

    foreach (var property in inputProperties.AsObject())
    {
        var paramName = property.Key;
        var paramSchema = property.Value;

        if (paramSchema == null) continue;

        // Skip prompt fields (handled separately by UI)
        if (paramName.Contains("prompt", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        // Skip seed (handled separately for reproducibility)
        if (paramName.Equals("seed", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var conduitParam = ConvertParameter(paramName, paramSchema, modelType);
        if (conduitParam != null)
        {
            parameters[paramName] = conduitParam;
        }
    }

    return parameters;
}

static Dictionary<string, object>? ConvertParameter(string name, JsonNode schema, string modelType)
{
    var type = schema["type"]?.GetValue<string>();
    var title = schema["title"]?.GetValue<string>() ?? name;
    var description = schema["description"]?.GetValue<string>();
    var defaultValue = schema["default"];
    var enumValues = schema["enum"]?.AsArray();
    var format = schema["format"]?.GetValue<string>();
    var minimum = schema["minimum"];
    var maximum = schema["maximum"];

    // Determine Conduit parameter type
    if (type == "string" && enumValues != null && enumValues.Count > 0)
    {
        // String with enum → select
        return CreateSelectParameter(name, title, description, enumValues, defaultValue);
    }
    else if (type == "string" && format == "uri")
    {
        // URI → media-upload (determine accept type based on model type and field name)
        return CreateMediaUploadParameter(name, title, description, defaultValue, modelType);
    }
    else if (type == "integer" && minimum != null && maximum != null)
    {
        // Integer with range → slider
        return CreateSliderParameter(name, title, description, minimum, maximum, defaultValue);
    }
    else if (type == "boolean")
    {
        // Boolean → toggle
        return CreateToggleParameter(name, title, description, defaultValue);
    }
    else if (type == "string")
    {
        // Regular string → text
        return CreateTextParameter(name, title, description, defaultValue);
    }
    else if (type == "integer" || type == "number")
    {
        // Number without range → number control
        return CreateNumberParameter(name, title, description, minimum, maximum, defaultValue);
    }

    return null;
}

static Dictionary<string, object> CreateSelectParameter(string name, string title, string? description, JsonArray enumValues, JsonNode? defaultValue)
{
    var options = new List<Dictionary<string, string>>();

    foreach (var enumValue in enumValues)
    {
        var value = enumValue?.GetValue<string>() ?? "";
        var label = EnhanceLabel(value, name);

        options.Add(new Dictionary<string, string>
        {
            ["value"] = value,
            ["label"] = label
        });
    }

    var param = new Dictionary<string, object>
    {
        ["type"] = "select",
        ["name"] = name,
        ["label"] = title,
        ["options"] = options
    };

    if (!string.IsNullOrEmpty(description))
        param["description"] = description;

    if (defaultValue != null)
    {
        var defaultStr = defaultValue.GetValue<string>();
        param["default"] = defaultStr;
    }

    return param;
}

static Dictionary<string, object> CreateMediaUploadParameter(string name, string title, string? description, JsonNode? defaultValue, string modelType)
{
    // Determine accept type based on model type and field name
    var acceptType = "image/*";  // Default for image models

    if (modelType == "video")
    {
        // Video models may accept images (for first frame) or videos
        if (name.Contains("video", StringComparison.OrdinalIgnoreCase))
        {
            acceptType = "video/*";
        }
        else if (name.Contains("image", StringComparison.OrdinalIgnoreCase) ||
                 name.Contains("frame", StringComparison.OrdinalIgnoreCase))
        {
            acceptType = "image/*";
        }
        else
        {
            // Default to both for video models
            acceptType = "image/*,video/*";
        }
    }

    var param = new Dictionary<string, object>
    {
        ["type"] = "media-upload",
        ["name"] = name,
        ["label"] = title,
        ["accept"] = acceptType
    };

    if (!string.IsNullOrEmpty(description))
        param["description"] = description;

    if (defaultValue != null)
    {
        var defaultStr = defaultValue.GetValue<string>();
        param["default"] = defaultStr;
    }

    return param;
}

static Dictionary<string, object> CreateSliderParameter(string name, string title, string? description, JsonNode minimum, JsonNode maximum, JsonNode? defaultValue)
{
    var min = minimum.GetValue<int>();
    var max = maximum.GetValue<int>();

    // Determine step based on field name
    var step = (name.Contains("width", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("height", StringComparison.OrdinalIgnoreCase)) ? 32 : 1;

    var param = new Dictionary<string, object>
    {
        ["type"] = "slider",
        ["name"] = name,
        ["label"] = title,
        ["min"] = min,
        ["max"] = max,
        ["step"] = step
    };

    if (!string.IsNullOrEmpty(description))
        param["description"] = description;

    if (defaultValue != null)
    {
        try
        {
            var defaultInt = defaultValue.GetValue<int>();
            param["default"] = defaultInt;
        }
        catch
        {
            // Skip if default can't be parsed as int
        }
    }

    return param;
}

static Dictionary<string, object> CreateToggleParameter(string name, string title, string? description, JsonNode? defaultValue)
{
    var param = new Dictionary<string, object>
    {
        ["type"] = "toggle",
        ["name"] = name,
        ["label"] = title
    };

    if (!string.IsNullOrEmpty(description))
        param["description"] = description;

    if (defaultValue != null)
    {
        try
        {
            var defaultBool = defaultValue.GetValue<bool>();
            param["default"] = defaultBool;
        }
        catch
        {
            // Skip if default can't be parsed as bool
        }
    }

    return param;
}

static Dictionary<string, object> CreateTextParameter(string name, string title, string? description, JsonNode? defaultValue)
{
    var param = new Dictionary<string, object>
    {
        ["type"] = "text",
        ["name"] = name,
        ["label"] = title
    };

    if (!string.IsNullOrEmpty(description))
        param["description"] = description;

    if (defaultValue != null)
    {
        var defaultStr = defaultValue.GetValue<string>();
        param["default"] = defaultStr;
    }

    return param;
}

static Dictionary<string, object> CreateNumberParameter(string name, string title, string? description, JsonNode? minimum, JsonNode? maximum, JsonNode? defaultValue)
{
    var param = new Dictionary<string, object>
    {
        ["type"] = "number",
        ["name"] = name,
        ["label"] = title
    };

    if (minimum != null)
    {
        try
        {
            param["min"] = minimum.GetValue<int>();
        }
        catch
        {
            // Skip if can't parse
        }
    }

    if (maximum != null)
    {
        try
        {
            param["max"] = maximum.GetValue<int>();
        }
        catch
        {
            // Skip if can't parse
        }
    }

    if (!string.IsNullOrEmpty(description))
        param["description"] = description;

    if (defaultValue != null)
    {
        try
        {
            param["default"] = defaultValue.GetValue<int>();
        }
        catch
        {
            // Skip if can't parse as int
        }
    }

    return param;
}

static string EnhanceLabel(string value, string parameterName)
{
    // Enhance aspect ratio labels
    if (parameterName.Contains("aspect", StringComparison.OrdinalIgnoreCase) ||
        parameterName.Contains("ratio", StringComparison.OrdinalIgnoreCase))
    {
        return value switch
        {
            "1:1" => "1:1 (Square)",
            "16:9" => "16:9 (Landscape)",
            "9:16" => "9:16 (Portrait)",
            "4:3" => "4:3",
            "3:4" => "3:4",
            "3:2" => "3:2",
            "2:3" => "2:3",
            "5:4" => "5:4",
            "4:5" => "4:5",
            "custom" => "Custom",
            _ => value
        };
    }

    // Enhance format labels
    if (parameterName.Contains("format", StringComparison.OrdinalIgnoreCase))
    {
        return value switch
        {
            "webp" => "WebP",
            "jpg" => "JPEG",
            "jpeg" => "JPEG",
            "png" => "PNG",
            "gif" => "GIF",
            "mp4" => "MP4",
            "mov" => "MOV",
            _ => CapitalizeFirst(value)
        };
    }

    // Default: capitalize first letter
    return CapitalizeFirst(value);
}

static string CapitalizeFirst(string input)
{
    if (string.IsNullOrEmpty(input))
        return input;

    return char.ToUpper(input[0]) + input.Substring(1);
}

static string FormatParametersJson(Dictionary<string, object> parameters)
{
    if (parameters == null || parameters.Count == 0)
        return "{}";

    try
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("  {");

        var paramList = parameters.ToList();
        for (int i = 0; i < paramList.Count; i++)
        {
            var param = paramList[i];
            sb.Append($"    \"{param.Key}\": ");
            sb.Append(FormatObject(param.Value, 4));

            if (i < paramList.Count - 1)
                sb.AppendLine(",");
            else
                sb.AppendLine();
        }

        sb.Append("  }");
        return sb.ToString();
    }
    catch (Exception ex)
    {
        return $"(formatting error: {ex.Message})";
    }
}

static string FormatObject(object obj, int indent)
{
    var indentStr = new string(' ', indent);

    if (obj is Dictionary<string, object> dict)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");

        var items = dict.ToList();
        for (int i = 0; i < items.Count; i++)
        {
            sb.Append($"{indentStr}  \"{items[i].Key}\": ");
            sb.Append(FormatObject(items[i].Value, indent + 2));

            if (i < items.Count - 1)
                sb.AppendLine(",");
            else
                sb.AppendLine();
        }

        sb.Append($"{indentStr}}}");
        return sb.ToString();
    }
    else if (obj is List<Dictionary<string, string>> list)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[");

        for (int i = 0; i < list.Count; i++)
        {
            sb.Append($"{indentStr}  ");
            sb.Append(FormatObject(list[i], indent + 2));

            if (i < list.Count - 1)
                sb.AppendLine(",");
            else
                sb.AppendLine();
        }

        sb.Append($"{indentStr}]");
        return sb.ToString();
    }
    else if (obj is Dictionary<string, string> strDict)
    {
        var sb = new StringBuilder();
        sb.Append("{");

        var items = strDict.ToList();
        for (int i = 0; i < items.Count; i++)
        {
            sb.Append($"\"{items[i].Key}\": \"{EscapeJsonString(items[i].Value)}\"");

            if (i < items.Count - 1)
                sb.Append(", ");
        }

        sb.Append("}");
        return sb.ToString();
    }
    else if (obj is string str)
    {
        return $"\"{EscapeJsonString(str)}\"";
    }
    else if (obj is bool b)
    {
        return b ? "true" : "false";
    }
    else if (obj is int || obj is long || obj is double || obj is decimal)
    {
        return obj.ToString() ?? "null";
    }
    else
    {
        return "null";
    }
}

static string EscapeJsonString(string str)
{
    return str
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\n", "\\n")
        .Replace("\r", "\\r")
        .Replace("\t", "\\t");
}

static string DetermineSeriesName(string modelFamily, string owner, List<DetailedModel> existingModels)
{
    // Check if this is the first model in this family
    var existingInFamily = existingModels.Where(m => m.ModelFamily == modelFamily).ToList();

    if (existingInFamily.Count == 0)
    {
        // First model in this family - this is the "primary" series
        return $"{modelFamily} Series";
    }

    // Check if there are models from the same family but different owner
    var primaryOwner = existingInFamily.First().Owner;

    if (owner == primaryOwner)
    {
        // Same owner as the first model - use the primary series name
        return $"{modelFamily} Series";
    }

    // Different owner - determine variant descriptor based on owner or model characteristics
    var descriptor = DetermineVariantDescriptor(owner, modelFamily);
    return $"{modelFamily} Series ({descriptor})";
}

static string DetermineVariantDescriptor(string owner, string modelFamily)
{
    // Special cases based on known optimizers/fine-tuners
    return owner switch
    {
        "prunaai" => "Optimized",
        "fofr" => "Custom",
        "fermatresearch" => "ControlNet",
        "sdxl-based" => "Custom",
        "lucataco" => "Custom",
        "adirik" => "Custom",
        _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(owner)
    };
}

// SQL Generation

static async Task GenerateSQLOutput(List<DetailedModel> models, string modelType, string outputFilename, Dictionary<string, TokenLimitReference>? tokenLimits)
{
    var sql = new StringBuilder();

    sql.AppendLine("-- Auto-generated SQL for Replicate models");
    sql.AppendLine($"-- Model Type: {modelType}");
    sql.AppendLine($"-- Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
    sql.AppendLine($"-- Total Models: {models.Count}");
    sql.AppendLine();
    sql.AppendLine("-- NOTE: This script uses ON CONFLICT DO NOTHING to preserve manual changes.");
    sql.AppendLine("-- If you need to update existing records, remove the ON CONFLICT clauses.");
    sql.AppendLine();
    sql.AppendLine("BEGIN;");
    sql.AppendLine();

    foreach (var model in models)
    {
        // Get token limit data if available
        TokenLimitReference? tokenLimit = null;
        if (tokenLimits != null && tokenLimits.TryGetValue(model.ReplicateIdentifier, out var limit))
        {
            tokenLimit = limit;
        }

        var tokenizerTypeEnum = MapTokenizerTypeToEnum(tokenLimit?.TokenizerType ?? model.TokenizerType);
        var maxInputTokens = tokenLimit?.MaxInputTokens ?? model.MaxInputTokens;
        var maxOutputTokens = model.MaxOutputTokens ?? tokenLimit?.MaxInputTokens; // Fallback to input if output not available

        sql.AppendLine($"-- Model: {model.ReplicateIdentifier}");
        sql.AppendLine();

        // Step 1: Insert/Lookup ModelAuthor
        // CORRECT TABLE NAME: ModelAuthors (plural)
        sql.AppendLine($"WITH author AS (");
        sql.AppendLine($"  INSERT INTO \"ModelAuthors\" (\"Name\", \"Description\", \"WebsiteUrl\")");
        sql.AppendLine($"  VALUES ('{EscapeSqlString(model.Owner)}', NULL, NULL)");
        sql.AppendLine($"  ON CONFLICT (\"Name\") DO UPDATE SET \"Name\" = EXCLUDED.\"Name\"");
        sql.AppendLine($"  RETURNING \"Id\", \"Name\"");
        sql.AppendLine($"),");
        sql.AppendLine();

        // Step 2: Insert/Lookup ModelSeries
        var seriesParameters = model.InputSchema != null && model.InputSchema.Count > 0
            ? ConvertParametersToJsonString(model.InputSchema)
            : "{}";

        sql.AppendLine($"series AS (");
        sql.AppendLine($"  INSERT INTO \"ModelSeries\" (\"Name\", \"Description\", \"AuthorId\", \"TokenizerType\", \"Parameters\")");
        sql.AppendLine($"  SELECT '{EscapeSqlString(model.SeriesName)}', NULL, author.\"Id\", {tokenizerTypeEnum}, '{seriesParameters}'");
        sql.AppendLine($"  FROM author WHERE author.\"Name\" = '{EscapeSqlString(model.Owner)}'");
        sql.AppendLine($"  ON CONFLICT (\"AuthorId\", \"Name\") DO UPDATE SET \"Name\" = EXCLUDED.\"Name\"");
        sql.AppendLine($"  RETURNING \"Id\", \"Name\"");
        sql.AppendLine($"),");
        sql.AppendLine();

        // Step 3: Insert Model
        // CORRECT TABLE NAME: Models (plural)
        // CORRECT COLUMN NAME: Parameters (not ModelParameters - see [Column("Parameters")] attribute)
        sql.AppendLine($"model AS (");
        sql.AppendLine($"  INSERT INTO \"Models\" (");
        sql.AppendLine($"    \"Name\", \"Version\", \"Description\", \"ModelCardUrl\", \"ModelSeriesId\",");
        sql.AppendLine($"    \"SupportsVision\", \"SupportsImageGeneration\", \"SupportsVideoGeneration\",");
        sql.AppendLine($"    \"SupportsEmbeddings\", \"SupportsChat\", \"SupportsFunctionCalling\", \"SupportsStreaming\",");
        sql.AppendLine($"    \"TokenizerType\", \"MaxInputTokens\", \"MaxOutputTokens\",");
        sql.AppendLine($"    \"IsActive\", \"Parameters\", \"CreatedAt\", \"UpdatedAt\"");
        sql.AppendLine($"  )");
        sql.AppendLine($"  SELECT");
        sql.AppendLine($"    '{EscapeSqlString(model.ModelName)}',");
        sql.AppendLine($"    NULL,");  // Version
        sql.AppendLine($"    NULL,");  // Description
        sql.AppendLine($"    '{EscapeSqlString(model.Url)}',");
        sql.AppendLine($"    series.\"Id\",");
        sql.AppendLine($"    {FormatBool(model.Capabilities.SupportsVision)},");
        sql.AppendLine($"    {FormatBool(model.Capabilities.SupportsImageGeneration)},");
        sql.AppendLine($"    {FormatBool(model.Capabilities.SupportsVideoGeneration)},");
        sql.AppendLine($"    {FormatBool(model.Capabilities.SupportsEmbeddings)},");
        sql.AppendLine($"    {FormatBool(model.Capabilities.SupportsChat)},");
        sql.AppendLine($"    {FormatBool(model.Capabilities.SupportsFunctionCalling)},");
        sql.AppendLine($"    {FormatBool(model.Capabilities.SupportsStreaming)},");
        sql.AppendLine($"    {tokenizerTypeEnum},");
        sql.AppendLine($"    {FormatNullableInt(maxInputTokens)},");
        sql.AppendLine($"    {FormatNullableInt(maxOutputTokens)},");
        sql.AppendLine($"    true,");  // IsActive
        sql.AppendLine($"    '{seriesParameters}',");
        sql.AppendLine($"    NOW(),");
        sql.AppendLine($"    NOW()");
        sql.AppendLine($"  FROM series WHERE series.\"Name\" = '{EscapeSqlString(model.SeriesName)}'");
        sql.AppendLine($"  ON CONFLICT DO NOTHING");
        sql.AppendLine($"  RETURNING \"Id\", \"Name\"");
        sql.AppendLine($")");
        sql.AppendLine();

        // Step 4: Insert ModelProviderTypeAssociation
        // CORRECT TABLE NAME: ModelIdentifiers (FluentAPI override for backward compatibility)
        sql.AppendLine($"INSERT INTO \"ModelIdentifiers\" (");
        sql.AppendLine($"  \"ModelId\", \"Identifier\", \"Provider\", \"IsEnabled\",");
        sql.AppendLine($"  \"MaxInputTokens\", \"MaxOutputTokens\", \"IsPrimary\"");
        sql.AppendLine($")");
        sql.AppendLine($"SELECT");
        sql.AppendLine($"  model.\"Id\",");
        sql.AppendLine($"  '{EscapeSqlString(model.ReplicateIdentifier)}',");
        sql.AppendLine($"  3,");  // ProviderType.Replicate = 3
        sql.AppendLine($"  true,");
        sql.AppendLine($"  {FormatNullableInt(maxInputTokens)},");
        sql.AppendLine($"  {FormatNullableInt(maxOutputTokens)},");
        sql.AppendLine($"  true");
        sql.AppendLine($"FROM model WHERE model.\"Name\" = '{EscapeSqlString(model.ModelName)}'");
        sql.AppendLine($"ON CONFLICT (\"Provider\", \"Identifier\") DO NOTHING;");
        sql.AppendLine();
    }

    sql.AppendLine("COMMIT;");
    sql.AppendLine();
    sql.AppendLine($"-- Successfully generated SQL for {models.Count} models");

    // Write to file
    await File.WriteAllTextAsync(outputFilename, sql.ToString());
    Console.WriteLine($"✅ SQL output written to: {outputFilename}");
    Console.WriteLine($"   Total models: {models.Count}");
    Console.WriteLine();
    Console.WriteLine("To execute:");
    Console.WriteLine($"  psql -h localhost -U conduit -d conduit_db < {outputFilename}");
}

static int MapTokenizerTypeToEnum(string tokenizerType)
{
    return tokenizerType switch
    {
        "None" => 0,
        "Cl100KBase" or "cl100k_base" => 1,
        "P50KBase" or "p50k_base" => 2,
        "P50KEdit" => 3,
        "R50KBase" => 4,
        "O200KBase" or "o200k_base" or "O200KHarmony" => 5,
        "Claude" => 6,  // Claude 2 and earlier
        "Claude3" => 7,  // Claude 3/4 family
        "Gemini" or "SentencePiece" => 8,  // Google models
        "PaLM" => 9,
        "LLaMA" or "Llama" => 10,
        "LLaMA2" => 11,
        "LLaMA3" => 12,
        "Mistral" => 13,
        "Cohere" => 14,
        "Kimi" => 16,
        "Groq" => 17,
        "Cerebras" => 18,
        "MiniMax" => 19,
        "BPE" or "ByteLevelBPE" or "GPTNeoX" => 21,
        "WordPiece" => 22,
        "Tiktoken" or "tiktoken" => 23,
        "T5" => 8,  // T5 uses SentencePiece
        _ => 0  // Default to None
    };
}

static string FormatBool(bool value) => value ? "true" : "false";

static string FormatNullableInt(int? value) => value.HasValue ? value.Value.ToString() : "NULL";

static string EscapeSqlString(string value)
{
    return value.Replace("'", "''");
}

static string ConvertParametersToJsonString(Dictionary<string, object> parameters)
{
    // Manually construct JSON string to avoid JsonSerializer issues with AOT
    var jsonParts = new List<string>();

    foreach (var kvp in parameters)
    {
        var key = kvp.Key;
        var value = SerializeObjectToJson(kvp.Value);
        jsonParts.Add($"\"{key}\":{value}");
    }

    var json = "{" + string.Join(",", jsonParts) + "}";
    // Escape for SQL string literal
    return json.Replace("'", "''");
}

static string SerializeObjectToJson(object obj)
{
    if (obj == null) return "null";

    if (obj is string str)
        return $"\"{EscapeJsonString(str)}\"";

    if (obj is bool b)
        return b ? "true" : "false";

    if (obj is int || obj is long || obj is double || obj is decimal)
        return obj.ToString() ?? "null";

    if (obj is Dictionary<string, object> dict)
    {
        var items = new List<string>();
        foreach (var kvp in dict)
        {
            items.Add($"\"{kvp.Key}\":{SerializeObjectToJson(kvp.Value)}");
        }
        return "{" + string.Join(",", items) + "}";
    }

    if (obj is Dictionary<string, string> strDict)
    {
        var items = new List<string>();
        foreach (var kvp in strDict)
        {
            items.Add($"\"{kvp.Key}\":\"{EscapeJsonString(kvp.Value)}\"");
        }
        return "{" + string.Join(",", items) + "}";
    }

    if (obj is List<Dictionary<string, string>> listDict)
    {
        var items = new List<string>();
        foreach (var item in listDict)
        {
            items.Add(SerializeObjectToJson(item));
        }
        return "[" + string.Join(",", items) + "]";
    }

    return "null";
}

// Data models

record TokenLimitReference
{
    public int MaxInputTokens { get; init; }
    public string TokenizerType { get; init; } = "None";
    public string? Notes { get; init; }
}

record BasicModel
{
    public string Owner { get; init; } = "";
    public string Name { get; init; } = "";
    public string Url { get; init; } = "";
}

record DetailedModel
{
    public string Owner { get; init; } = "";
    public string ModelName { get; init; } = "";
    public string ModelType { get; init; } = "";
    public string ModelFamily { get; init; } = "";
    public string SeriesName { get; init; } = "";
    public string Url { get; init; } = "";
    public string ReplicateIdentifier { get; init; } = "";
    public string SchemaUrl { get; init; } = "";
    public Dictionary<string, object>? InputSchema { get; init; }
    public ModelCapabilities Capabilities { get; init; } = new();
    public int? MaxOutputTokens { get; init; }
    public int? MaxInputTokens { get; init; }
    public string TokenizerType { get; init; } = "None";
}

record ModelCapabilities
{
    public bool SupportsImageGeneration { get; init; }
    public bool SupportsVision { get; init; }
    public bool SupportsChat { get; init; }
    public bool SupportsEmbeddings { get; init; }
    public bool SupportsFunctionCalling { get; init; }
    public bool SupportsVideoGeneration { get; init; }
    public bool SupportsStreaming { get; init; }
}
