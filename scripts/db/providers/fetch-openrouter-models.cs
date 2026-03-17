#!/usr/bin/env -S dotnet run

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

Console.WriteLine("=== OpenRouter Model Fetcher ===");
Console.WriteLine("Fetches models from OpenRouter API and generates openrouter-models.json");
Console.WriteLine();

// Determine output path — find the scripts/db/providers/ directory
var outputFilename = args.Length > 0 ? args[0] : "openrouter-models.json";
var scriptDirCandidates = new[]
{
    "scripts/db/providers",           // From repo root
    ".",                               // Same directory as script
    Path.GetDirectoryName(AppContext.BaseDirectory) ?? "."
};

// Find the script directory by looking for provider-config.json
string scriptDir = ".";
foreach (var dir in scriptDirCandidates)
{
    if (File.Exists(Path.Combine(dir, "provider-config.json")))
    {
        scriptDir = dir;
        break;
    }
}

string outputPath = Path.Combine(scriptDir, outputFilename);

// Fetch models from OpenRouter API
Console.WriteLine("Fetching models from https://openrouter.ai/api/v1/models ...");

using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");
httpClient.Timeout = TimeSpan.FromSeconds(30);

string apiResponse;
try
{
    apiResponse = await httpClient.GetStringAsync("https://openrouter.ai/api/v1/models");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error fetching models: {ex.Message}");
    return;
}

var apiDoc = JsonDocument.Parse(apiResponse);
var dataArray = apiDoc.RootElement.GetProperty("data");

Console.WriteLine($"✅ Received {dataArray.GetArrayLength()} models from API");
Console.WriteLine();

// Transform models into the standard provider JSON format
var outputModels = new JsonObject();
int skipped = 0;
int included = 0;

foreach (var model in dataArray.EnumerateArray())
{
    var id = model.GetProperty("id").GetString() ?? "";
    var name = model.GetProperty("name").GetString() ?? id;

    // Skip models without a provider/model format
    if (!id.Contains('/'))
    {
        skipped++;
        continue;
    }

    // Skip free models (pricing.prompt === "0" or missing)
    var pricing = model.TryGetProperty("pricing", out var pricingProp) ? pricingProp : default;
    var promptPrice = pricing.ValueKind != JsonValueKind.Undefined
        ? pricing.TryGetProperty("prompt", out var pp) ? pp.GetString() ?? "0" : "0"
        : "0";
    var completionPrice = pricing.ValueKind != JsonValueKind.Undefined
        ? pricing.TryGetProperty("completion", out var cp) ? cp.GetString() ?? "0" : "0"
        : "0";

    if (promptPrice == "0" && completionPrice == "0")
    {
        skipped++;
        continue;
    }

    // Extract owner from model ID prefix
    var owner = id.Split('/')[0];

    // Convert per-token pricing to per-million-tokens
    var inputPricePerMillion = double.Parse(promptPrice) * 1_000_000;
    var outputPricePerMillion = double.Parse(completionPrice) * 1_000_000;

    // Round to avoid floating point noise (e.g., 0.9600000000000001)
    inputPricePerMillion = Math.Round(inputPricePerMillion, 4);
    outputPricePerMillion = Math.Round(outputPricePerMillion, 4);

    // Extract context length and max output tokens
    var contextLength = model.TryGetProperty("context_length", out var ctx)
        ? ctx.GetInt32() : 4096;
    var maxOutputTokens = model.TryGetProperty("top_provider", out var tp)
        && tp.TryGetProperty("max_completion_tokens", out var mct)
        && mct.ValueKind == JsonValueKind.Number
        ? mct.GetInt32()
        : contextLength / 4; // Default: 25% of context

    // Extract capabilities from architecture and supported_parameters
    var inputModalities = new List<string>();
    if (model.TryGetProperty("architecture", out var arch))
    {
        if (arch.TryGetProperty("input_modalities", out var inMod))
        {
            foreach (var mod in inMod.EnumerateArray())
                inputModalities.Add(mod.GetString() ?? "");
        }
    }

    var outputModalities = new List<string>();
    if (model.TryGetProperty("architecture", out var arch2))
    {
        if (arch2.TryGetProperty("output_modalities", out var outMod))
        {
            foreach (var mod in outMod.EnumerateArray())
                outputModalities.Add(mod.GetString() ?? "");
        }
    }

    var supportedParams = new List<string>();
    if (model.TryGetProperty("supported_parameters", out var sp))
    {
        foreach (var param in sp.EnumerateArray())
            supportedParams.Add(param.GetString() ?? "");
    }

    var supportsVision = inputModalities.Contains("image");
    var supportsFunctionCalling = supportedParams.Contains("tools");
    var supportsImageGeneration = outputModalities.Contains("image");

    // Map tokenizer
    var tokenizerRaw = arch.TryGetProperty("tokenizer", out var tok)
        ? tok.GetString() ?? "Other" : "Other";
    var tokenizerType = MapTokenizer(tokenizerRaw);

    // Use the slug (part after provider/) as the model name for consistency
    // with other providers and to enable deduplication across provider types.
    // The OpenRouter display name (e.g., "OpenAI: gpt-oss-120b") is stored in notes.
    var slug = id.Split('/', 2)[1];

    // Derive family and series from model name/id
    var (family, series) = DeriveSeriesInfo(id, name, owner);

    // Build the model description — prepend display name if it differs from slug
    var description = model.TryGetProperty("description", out var desc)
        ? desc.GetString() : null;
    // Truncate long descriptions
    if (description != null && description.Length > 500)
        description = description.Substring(0, 497) + "...";

    var modelObj = new JsonObject
    {
        ["name"] = slug,
        ["family"] = family,
        ["series"] = series,
        ["owner"] = owner,
        ["maxInputTokens"] = contextLength,
        ["maxOutputTokens"] = maxOutputTokens,
        ["tokenizerType"] = tokenizerType,
        ["supportsChat"] = true,
        ["supportsStreaming"] = true,
        ["supportsVision"] = supportsVision,
        ["supportsFunctionCalling"] = supportsFunctionCalling,
        ["supportsEmbeddings"] = false,
        ["inputPricePerMillion"] = inputPricePerMillion,
        ["outputPricePerMillion"] = outputPricePerMillion,
        ["speedTokensPerSec"] = null,
        ["notes"] = description
    };

    outputModels[id] = modelObj;
    included++;
}

// Write output
var output = new JsonObject { ["models"] = outputModels };
var options = new JsonSerializerOptions { WriteIndented = true };
var jsonOutput = output.ToJsonString(options);

await File.WriteAllTextAsync(outputPath, jsonOutput);

Console.WriteLine($"✅ Written {included} models to {outputPath}");
Console.WriteLine($"   Skipped {skipped} models (free or no provider prefix)");
Console.WriteLine();

// Summary by owner
var ownerCounts = outputModels
    .Select(kv => kv.Key.Split('/')[0])
    .GroupBy(o => o)
    .OrderByDescending(g => g.Count())
    .Take(10);

Console.WriteLine("Top 10 providers by model count:");
foreach (var group in ownerCounts)
{
    Console.WriteLine($"  {group.Key}: {group.Count()}");
}

Console.WriteLine();
Console.WriteLine("Next step: Generate SQL with:");
Console.WriteLine($"  dotnet script generate-provider-sql.cs openrouter");

// --- Helper functions ---

static string MapTokenizer(string openRouterTokenizer)
{
    return openRouterTokenizer switch
    {
        "GPT" => "O200KBase",
        "Claude" => "Claude3",
        "Llama2" => "LLaMA2",
        "Llama3" => "LLaMA3",
        "Llama4" => "LLaMA3",      // Same BPE family
        "Gemini" => "Gemini",
        "Mistral" => "Mistral",
        "Cohere" => "Cohere",
        "Qwen" or "Qwen3" => "Tiktoken",
        "DeepSeek" => "LLaMA3",     // DeepSeek uses LLaMA3-based tokenizer
        "Grok" => "BPE",
        "Other" or "Router" or "Nova" => "None",
        _ => "None"
    };
}

static (string family, string series) DeriveSeriesInfo(string id, string displayName, string owner)
{
    // Extract model slug (part after provider/)
    var slug = id.Contains('/') ? id.Split('/', 2)[1] : id;

    // Known family patterns — match on slug to derive family and series
    // Order matters: more specific patterns first

    // OpenAI models
    if (slug.StartsWith("gpt-5")) return ("GPT", "GPT-5 Series");
    if (slug.StartsWith("gpt-4.1")) return ("GPT", "GPT-4.1 Series");
    if (slug.StartsWith("gpt-4o")) return ("GPT", "GPT-4o Series");
    if (slug.StartsWith("gpt-4.5")) return ("GPT", "GPT-4.5 Series");
    if (slug.StartsWith("gpt-4-")) return ("GPT", "GPT-4 Series");
    if (slug.StartsWith("gpt-3.5")) return ("GPT", "GPT-3.5 Series");
    if (slug.StartsWith("o4-")) return ("GPT", "o4 Series");
    if (slug.StartsWith("o3-")) return ("GPT", "o3 Series");
    if (slug.StartsWith("o1-")) return ("GPT", "o1 Series");
    if (slug.StartsWith("gpt-oss")) return ("GPT", "GPT OSS Series");

    // Anthropic models
    if (slug.StartsWith("claude-4")) return ("Claude", "Claude 4 Series");
    if (slug.StartsWith("claude-3.7")) return ("Claude", "Claude 3.7 Series");
    if (slug.StartsWith("claude-3.5")) return ("Claude", "Claude 3.5 Series");
    if (slug.StartsWith("claude-3")) return ("Claude", "Claude 3 Series");

    // Meta Llama models
    if (slug.StartsWith("llama-4")) return ("Llama", "Llama 4 Series");
    if (slug.StartsWith("llama-3.3")) return ("Llama", "Llama 3.3 Series");
    if (slug.StartsWith("llama-3.1")) return ("Llama", "Llama 3.1 Series");
    if (slug.StartsWith("llama-3")) return ("Llama", "Llama 3 Series");
    if (slug.StartsWith("llama-guard")) return ("Llama", "Llama Guard Series");

    // Google models
    if (slug.StartsWith("gemini-3")) return ("Gemini", "Gemini 3 Series");
    if (slug.StartsWith("gemini-2.5")) return ("Gemini", "Gemini 2.5 Series");
    if (slug.StartsWith("gemini-2")) return ("Gemini", "Gemini 2 Series");
    if (slug.StartsWith("gemma-3")) return ("Gemma", "Gemma 3 Series");
    if (slug.StartsWith("gemma-2")) return ("Gemma", "Gemma 2 Series");

    // Mistral models
    if (slug.StartsWith("mistral-large")) return ("Mistral", "Mistral Large Series");
    if (slug.StartsWith("mistral-medium")) return ("Mistral", "Mistral Medium Series");
    if (slug.StartsWith("mistral-small")) return ("Mistral", "Mistral Small Series");
    if (slug.StartsWith("codestral")) return ("Mistral", "Codestral Series");
    if (slug.StartsWith("pixtral")) return ("Mistral", "Pixtral Series");
    if (slug.StartsWith("ministral")) return ("Mistral", "Ministral Series");
    if (slug.Contains("mistral")) return ("Mistral", "Mistral Series");

    // DeepSeek models
    if (slug.StartsWith("deepseek-r1")) return ("DeepSeek", "DeepSeek R1 Series");
    if (slug.StartsWith("deepseek-v3")) return ("DeepSeek", "DeepSeek V3 Series");
    if (slug.StartsWith("deepseek-chat")) return ("DeepSeek", "DeepSeek Chat Series");
    if (slug.StartsWith("deepseek-prover")) return ("DeepSeek", "DeepSeek Prover Series");
    if (slug.Contains("deepseek")) return ("DeepSeek", "DeepSeek Series");

    // Qwen models
    if (slug.StartsWith("qwen3")) return ("Qwen", "Qwen 3 Series");
    if (slug.StartsWith("qwen2.5")) return ("Qwen", "Qwen 2.5 Series");
    if (slug.StartsWith("qwen2")) return ("Qwen", "Qwen 2 Series");
    if (slug.StartsWith("qwq")) return ("Qwen", "QwQ Series");
    if (slug.Contains("qwen")) return ("Qwen", "Qwen Series");

    // xAI models
    if (slug.StartsWith("grok-4")) return ("Grok", "Grok 4 Series");
    if (slug.StartsWith("grok-3")) return ("Grok", "Grok 3 Series");
    if (slug.StartsWith("grok-2")) return ("Grok", "Grok 2 Series");
    if (slug.Contains("grok")) return ("Grok", "Grok Series");

    // Cohere models
    if (slug.StartsWith("command-r")) return ("Command", "Command R Series");
    if (slug.StartsWith("command-a")) return ("Command", "Command A Series");
    if (slug.Contains("command")) return ("Command", "Command Series");

    // MiniMax models
    if (slug.Contains("minimax")) return ("MiniMax", "MiniMax Series");

    // NVIDIA models
    if (slug.StartsWith("llama-3.1-nemotron")) return ("Nemotron", "Nemotron Series");
    if (slug.Contains("nemotron")) return ("Nemotron", "Nemotron Series");

    // Fallback: use the owner as the family, and construct a series name
    var familyName = char.ToUpper(owner[0]) + owner.Substring(1);
    return (familyName, $"{familyName} Series");
}
