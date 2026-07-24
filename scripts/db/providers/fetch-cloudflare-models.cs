#!/usr/bin/env -S dotnet run

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

Console.WriteLine("=== Cloudflare Workers AI Model Fetcher ===");
Console.WriteLine("Fetches the Workers AI catalog and generates cloudflare-models.json");
Console.WriteLine();

// Cloudflare's authenticated /accounts/{id}/ai/models/search endpoint has no
// accountless equivalent, but the cloudflare-docs repo publishes verbatim dumps
// of its per-model entries (same shape: name/description/task/properties), so
// the catalog can be fetched without credentials.
const string DocsListingUrl =
    "https://api.github.com/repos/cloudflare/cloudflare-docs/contents/src/content/workers-ai-models";

// Determine output path — find the scripts/db/providers/ directory
var outputFilename = args.Length > 0 ? args[0] : "cloudflare-models.json";
var scriptDirCandidates = new[]
{
    "scripts/db/providers",           // From repo root
    ".",                               // Same directory as script
    Path.GetDirectoryName(AppContext.BaseDirectory) ?? "."
};

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

using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");
httpClient.Timeout = TimeSpan.FromSeconds(30);

Console.WriteLine($"Fetching model file listing from {DocsListingUrl} ...");

string listingResponse;
try
{
    listingResponse = await httpClient.GetStringAsync(DocsListingUrl);
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error fetching model listing: {ex.Message}");
    return;
}

var listing = JsonDocument.Parse(listingResponse);
var modelFiles = listing.RootElement.EnumerateArray()
    .Where(f => f.GetProperty("name").GetString()?.EndsWith(".json") == true)
    .Select(f => (Name: f.GetProperty("name").GetString()!,
                  Url: f.GetProperty("download_url").GetString()!))
    .OrderBy(f => f.Name, StringComparer.Ordinal)
    .ToList();

if (modelFiles.Count == 0)
{
    Console.WriteLine("❌ Listing returned no model JSON files — the docs repo layout may have moved.");
    return;
}

Console.WriteLine($"✅ Found {modelFiles.Count} model files");
Console.WriteLine();

// Task names Conduit can represent, mapped onto capability flags when building
// each entry. Everything else (classification, detection, translation, ...) is
// skipped — Conduit has no route for those tasks.
var supportedTasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "Text Generation",
    "Text Embeddings",
    "Text-to-Image",
    "Text-to-Speech",
    "Automatic Speech Recognition",
    "Image-to-Text"
};

var outputModels = new JsonObject();
var skippedByTask = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
var taskCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
int failures = 0;

foreach (var (fileName, url) in modelFiles)
{
    JsonDocument doc;
    try
    {
        doc = JsonDocument.Parse(await httpClient.GetStringAsync(url));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️  {fileName}: fetch/parse failed ({ex.Message})");
        failures++;
        continue;
    }

    using (doc)
    {
        var root = doc.RootElement;
        var slug = root.GetProperty("name").GetString() ?? "";
        var taskName = root.TryGetProperty("task", out var task)
            && task.TryGetProperty("name", out var tn)
            ? tn.GetString() ?? "Unknown" : "Unknown";

        // Slugs are "@cf/{author}/{model}" (occasionally "@hf/{author}/{model}")
        var slugParts = slug.TrimStart('@').Split('/');
        if (slugParts.Length < 3)
        {
            Console.WriteLine($"⚠️  {fileName}: unexpected slug format '{slug}', skipped");
            failures++;
            continue;
        }

        if (!supportedTasks.Contains(taskName))
        {
            skippedByTask[taskName] = skippedByTask.GetValueOrDefault(taskName) + 1;
            continue;
        }

        var owner = slugParts[1];
        var modelName = string.Join("/", slugParts.Skip(2));

        // Flatten the properties array into a lookup
        var properties = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("properties", out var props))
        {
            foreach (var prop in props.EnumerateArray())
            {
                var id = prop.GetProperty("property_id").GetString();
                if (id != null)
                    properties[id] = prop.GetProperty("value");
            }
        }

        var isChat = taskName.Equals("Text Generation", StringComparison.OrdinalIgnoreCase);
        var isEmbeddings = taskName.Equals("Text Embeddings", StringComparison.OrdinalIgnoreCase);
        var isImageGen = taskName.Equals("Text-to-Image", StringComparison.OrdinalIgnoreCase);
        var isTts = taskName.Equals("Text-to-Speech", StringComparison.OrdinalIgnoreCase);
        var isStt = taskName.Equals("Automatic Speech Recognition", StringComparison.OrdinalIgnoreCase);
        var isImageToText = taskName.Equals("Image-to-Text", StringComparison.OrdinalIgnoreCase);

        var supportsVision = GetBoolProperty(properties, "vision") || isImageToText;
        var supportsFunctionCalling = GetBoolProperty(properties, "function_calling");

        // Cloudflare publishes context_window (and occasionally max_input_tokens);
        // it does not publish a max output limit anywhere, so mirror the
        // OpenRouter fetcher's 25%-of-context default for chat models.
        var contextWindow = GetIntProperty(properties, "context_window")
            ?? GetIntProperty(properties, "max_input_tokens");
        var maxInputTokens = Math.Max(1024, contextWindow ?? 4096);
        var maxOutputTokens = isChat || isImageToText
            ? Math.Max(1024, maxInputTokens / 4)
            : maxInputTokens;

        // Pricing: per-M-token units map to the catalog fields; other units
        // (per step, per tile, per audio minute, ...) don't fit and are noted.
        double inputPricePerMillion = 0;
        double outputPricePerMillion = 0;
        var pricingNotes = new List<string>();
        if (properties.TryGetValue("price", out var price) && price.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in price.EnumerateArray())
            {
                var unit = entry.GetProperty("unit").GetString() ?? "";
                var amount = entry.GetProperty("price").GetDouble();
                if (unit.Equals("per M input tokens", StringComparison.OrdinalIgnoreCase))
                    inputPricePerMillion = Math.Round(amount, 4);
                else if (unit.Equals("per M output tokens", StringComparison.OrdinalIgnoreCase))
                    outputPricePerMillion = Math.Round(amount, 4);
                else
                    pricingNotes.Add($"${amount.ToString("0.######", CultureInfo.InvariantCulture)} {unit}");
            }
        }

        var (family, series) = DeriveSeriesInfo(modelName, owner);
        var tokenizerType = MapTokenizer(family, modelName);

        var inputModalities = new List<string>();
        if (isChat || isEmbeddings || isImageGen || isTts) inputModalities.Add("text");
        if (supportsVision) inputModalities.Add("image");
        if (isStt) inputModalities.Add("audio");

        var outputModalities = new List<string>();
        if (isChat || isEmbeddings || isStt || isImageToText) outputModalities.Add("text");
        if (isImageGen) outputModalities.Add("image");
        if (isTts) outputModalities.Add("audio");

        var description = root.TryGetProperty("description", out var desc)
            ? desc.GetString() : null;
        if (description != null && description.Length > 500)
            description = description.Substring(0, 497) + "...";

        var notes = description ?? "";
        if (GetBoolProperty(properties, "beta"))
            notes = AppendNote(notes, "Beta model.");
        if (pricingNotes.Count > 0)
            notes = AppendNote(notes, $"Pricing: {string.Join(", ", pricingNotes)}.");
        if (GetBoolProperty(properties, "lora"))
            notes = AppendNote(notes, "Supports LoRA adapters.");

        var modelObj = new JsonObject
        {
            ["name"] = modelName,
            ["family"] = family,
            ["series"] = series,
            ["owner"] = owner,
            ["maxInputTokens"] = maxInputTokens,
            ["maxOutputTokens"] = maxOutputTokens,
            ["tokenizerType"] = tokenizerType,
            ["supportsChat"] = isChat,
            ["supportsStreaming"] = isChat,
            ["supportsVision"] = supportsVision,
            ["supportsFunctionCalling"] = supportsFunctionCalling,
            ["supportsEmbeddings"] = isEmbeddings,
            ["supportsImageGeneration"] = isImageGen,
            ["supportsVideoGeneration"] = false,
            ["supportsSpeechToText"] = isStt,
            ["supportsTextToSpeech"] = isTts,
            ["supportsAudio"] = isStt || isTts,
            ["supportsRerank"] = false,
            ["inputModalities"] = new JsonArray(inputModalities
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(m => (JsonNode?)JsonValue.Create(m))
                .ToArray()),
            ["outputModalities"] = new JsonArray(outputModalities
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(m => (JsonNode?)JsonValue.Create(m))
                .ToArray()),
            ["capabilitySource"] = "ProviderApi",
            ["capabilitiesLastVerifiedAt"] = DateTime.UtcNow,
            ["inputPricePerMillion"] = inputPricePerMillion,
            ["outputPricePerMillion"] = outputPricePerMillion,
            ["speedTokensPerSec"] = null,
            ["notes"] = string.IsNullOrWhiteSpace(notes) ? null : notes
        };

        // Key on the full "@cf/..." slug — it becomes the
        // ModelProviderTypeAssociation.Identifier, which must match what the
        // gateway sends to Cloudflare's API.
        outputModels[slug] = modelObj;
        taskCounts[taskName] = taskCounts.GetValueOrDefault(taskName) + 1;
    }
}

if (outputModels.Count == 0)
{
    Console.WriteLine("❌ No usable models found — not writing output.");
    return;
}

var output = new JsonObject { ["models"] = outputModels };
var options = new JsonSerializerOptions { WriteIndented = true };
await File.WriteAllTextAsync(outputPath, output.ToJsonString(options));

Console.WriteLine($"✅ Written {outputModels.Count} models to {outputPath}");
if (failures > 0)
    Console.WriteLine($"⚠️  {failures} files failed to fetch/parse (see warnings above)");
Console.WriteLine();

Console.WriteLine("Included by task:");
foreach (var (task, count) in taskCounts.OrderByDescending(x => x.Value))
    Console.WriteLine($"  {task}: {count}");

if (skippedByTask.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("Skipped (task not routable by Conduit):");
    foreach (var (task, count) in skippedByTask.OrderByDescending(x => x.Value))
        Console.WriteLine($"  {task}: {count}");
}

Console.WriteLine();
Console.WriteLine("Next step: rebuild ConduitLLM.Configuration to embed the updated catalog,");
Console.WriteLine("or generate SQL with: dotnet run generate-provider-sql.cs -- cloudflare");

// --- Helper functions ---

static bool GetBoolProperty(Dictionary<string, JsonElement> properties, string id) =>
    properties.TryGetValue(id, out var value) &&
    (value.ValueKind == JsonValueKind.True ||
     (value.ValueKind == JsonValueKind.String &&
      string.Equals(value.GetString(), "true", StringComparison.OrdinalIgnoreCase)));

static int? GetIntProperty(Dictionary<string, JsonElement> properties, string id)
{
    if (!properties.TryGetValue(id, out var value))
        return null;
    if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        return number;
    if (value.ValueKind == JsonValueKind.String &&
        int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        return parsed;
    return null;
}

static string AppendNote(string notes, string addition)
{
    notes = notes.TrimEnd();
    if (string.IsNullOrWhiteSpace(notes))
        return addition;
    if (!notes.EndsWith('.') && !notes.EndsWith('!'))
        notes += ".";
    return notes + " " + addition;
}

static (string family, string series) DeriveSeriesInfo(string modelName, string owner)
{
    var slug = modelName.ToLowerInvariant();

    // Meta Llama
    if (slug.StartsWith("llama-4")) return ("Llama", "Llama 4 Series");
    if (slug.StartsWith("llama-3.3")) return ("Llama", "Llama 3.3 Series");
    if (slug.StartsWith("llama-3.2")) return ("Llama", "Llama 3.2 Series");
    if (slug.StartsWith("llama-3.1")) return ("Llama", "Llama 3.1 Series");
    if (slug.StartsWith("llama-3")) return ("Llama", "Llama 3 Series");
    if (slug.StartsWith("llama-2") || slug.StartsWith("llama-guard")) return ("Llama", "Llama 2 Series");
    if (slug.StartsWith("llamaguard")) return ("Llama", "Llama Guard Series");
    if (slug.Contains("tinyllama")) return ("Llama", "TinyLlama Series");

    // OpenAI
    if (slug.StartsWith("gpt-oss")) return ("GPT", "GPT OSS Series");
    if (slug.StartsWith("whisper")) return ("Whisper", "Whisper Series");

    // Qwen
    if (slug.StartsWith("qwen3") || slug.StartsWith("qwen-3")) return ("Qwen", "Qwen 3 Series");
    if (slug.StartsWith("qwen2.5") || slug.StartsWith("qwen2")) return ("Qwen", "Qwen 2.5 Series");
    if (slug.StartsWith("qwen1.5")) return ("Qwen", "Qwen 1.5 Series");
    if (slug.StartsWith("qwq")) return ("Qwen", "QwQ Series");
    if (slug.Contains("qwen")) return ("Qwen", "Qwen Series");

    // Google
    if (slug.StartsWith("gemma-3")) return ("Gemma", "Gemma 3 Series");
    if (slug.StartsWith("gemma-2")) return ("Gemma", "Gemma 2 Series");
    if (slug.Contains("gemma")) return ("Gemma", "Gemma Series");

    // Mistral
    if (slug.StartsWith("mistral-small")) return ("Mistral", "Mistral Small Series");
    if (slug.Contains("mistral") || slug.Contains("mixtral")) return ("Mistral", "Mistral Series");

    // DeepSeek
    if (slug.Contains("deepseek-r1")) return ("DeepSeek", "DeepSeek R1 Series");
    if (slug.Contains("deepseek")) return ("DeepSeek", "DeepSeek Series");

    // Zhipu
    if (slug.StartsWith("glm")) return ("GLM", "GLM Series");

    // BAAI embeddings / rerankers
    if (slug.StartsWith("bge")) return ("BGE", "BGE Series");

    // Image generation
    if (slug.StartsWith("flux")) return ("FLUX", "FLUX Series");
    if (slug.Contains("stable-diffusion")) return ("Stable Diffusion", "Stable Diffusion Series");
    if (slug.StartsWith("dreamshaper")) return ("Stable Diffusion", "DreamShaper Series");
    if (slug.StartsWith("phoenix") || slug.StartsWith("lucid-origin"))
        return ("Leonardo", "Leonardo Series");

    // Speech
    if (slug.Contains("melotts")) return ("MeloTTS", "MeloTTS Series");
    if (slug.StartsWith("aura")) return ("Aura", "Deepgram Aura Series");
    if (slug.StartsWith("nova")) return ("Nova", "Deepgram Nova Series");

    // Vision-language
    if (slug.StartsWith("llava")) return ("LLaVA", "LLaVA Series");
    if (slug.StartsWith("uform")) return ("UForm", "UForm Series");

    // Misc known families
    if (slug.StartsWith("phi")) return ("Phi", "Phi Series");
    if (slug.StartsWith("falcon")) return ("Falcon", "Falcon Series");
    if (slug.StartsWith("openchat")) return ("OpenChat", "OpenChat Series");
    if (slug.Contains("hermes")) return ("Hermes", "Hermes Series");
    if (slug.StartsWith("starling")) return ("Starling", "Starling Series");
    if (slug.StartsWith("zephyr")) return ("Zephyr", "Zephyr Series");
    if (slug.StartsWith("sqlcoder")) return ("SQLCoder", "SQLCoder Series");
    if (slug.Contains("smollm")) return ("SmolLM", "SmolLM Series");

    // Fallback: use the owner as the family
    var familyName = char.ToUpperInvariant(owner[0]) + owner.Substring(1);
    return (familyName, $"{familyName} Series");
}

static string MapTokenizer(string family, string modelName) => family switch
{
    "Llama" when modelName.StartsWith("llama-2", StringComparison.OrdinalIgnoreCase) => "LLaMA2",
    "Llama" => "LLaMA3",
    "GPT" => "O200KHarmony",
    "Whisper" => "Tiktoken",
    "Qwen" => "Tiktoken",
    "Gemma" => "SentencePiece",
    "Mistral" => "Mistral",
    "DeepSeek" => "LLaMA3",
    "BGE" => "WordPiece",
    "Phi" => "BPE",
    _ => "None"
};
