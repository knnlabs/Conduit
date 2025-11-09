-- Seed Model Data (Updated for current schema)
-- Recovered from migration 20250819013218_SeedModelData and adapted to current schema
-- Provider enum values: Groq=2, Fireworks=4, Cerebras=9, DeepInfra=11
-- TokenizerType: LLaMA3=11, O200KHarmony=14, Kimi=15, Mistral=12, Tiktoken=22, SentencePiece=19

BEGIN;

-- Step 1: Insert ModelAuthors
INSERT INTO "ModelAuthors" ("Id", "Name", "Description", "WebsiteUrl")
VALUES
    (1, 'Meta', 'Meta AI (formerly Facebook AI)', 'https://ai.meta.com'),
    (2, 'OpenAI', 'OpenAI - creators of GPT models', 'https://openai.com'),
    (3, 'Groq', 'Groq - high-performance inference', 'https://groq.com'),
    (4, 'Fireworks', 'Fireworks AI - fast inference platform', 'https://fireworks.ai'),
    (5, 'Cerebras', 'Cerebras - ultra-fast AI compute', 'https://cerebras.net'),
    (6, 'DeepInfra', 'DeepInfra - serverless AI inference', 'https://deepinfra.com'),
    (7, 'ByteDance', 'ByteDance - creators of SeeDance and other models', 'https://bytedance.com'),
    (8, 'Wan-AI', 'Wan AI - video generation models', NULL),
    (9, 'Moonshot', 'Moonshot AI - creators of Kimi models', 'https://moonshotai.com'),
    (10, 'ZAI', 'ZAI Organization', NULL),
    (11, 'Zhipu', 'Zhipu AI - creators of GLM models', 'https://zhipuai.cn'),
    (12, 'Qwen', 'Alibaba Qwen Team', 'https://qwenlm.github.io');

-- Step 2: Insert ModelSeries
INSERT INTO "ModelSeries" ("Id", "AuthorId", "Name", "Description", "TokenizerType", "Parameters")
VALUES
    (1, 1, 'LLaMA 3.1', 'Meta''s LLaMA 3.1 series', 11, '{}'),
    (2, 1, 'LLaMA 3.3', 'Meta''s LLaMA 3.3 series', 11, '{}'),
    (3, 1, 'LLaMA 4', 'Meta''s LLaMA 4 series including Scout and Maverick', 11, '{}'),
    (4, 1, 'LLaMA Guard', 'Meta''s content moderation models', 11, '{}'),
    (5, 2, 'GPT-OSS', 'OpenAI''s open-source GPT models', 14, '{"reasoning_effort":{"type":"select","options":[{"value":"low","label":"Low"},{"value":"medium","label":"Medium"},{"value":"high","label":"High"}],"default":"medium","label":"Reasoning Effort"}}'),
    (6, 4, 'Flux', 'Fireworks'' image generation models', 22, '{"guidance_scale":{"type":"slider","min":1,"max":20,"step":0.5,"default":7.5,"label":"Guidance Scale"},"num_inference_steps":{"type":"slider","min":20,"max":50,"step":1,"default":30,"label":"Inference Steps"}}'),
    (7, 4, 'SSD', 'Stable Diffusion models', 22, '{"negative_prompt":{"type":"text","label":"Negative Prompt"},"scheduler":{"type":"select","options":[{"value":"DDIM","label":"DDIM"},{"value":"DPM","label":"DPM"}],"default":"DDIM","label":"Scheduler"}}'),
    (8, 12, 'Qwen 3', 'Alibaba''s Qwen 3 series', 22, '{}'),
    (9, 11, 'GLM 4', 'Zhipu''s GLM 4 series', 19, '{}'),
    (10, 9, 'Kimi', 'Moonshot''s Kimi series', 15, '{}'),
    (11, 4, 'Chronos', 'Fireworks'' Chronos series', 12, '{}'),
    (12, 7, 'SeeDance', 'ByteDance''s video generation', 22, '{}'),
    (13, 8, 'Wan', 'Wan AI''s video generation', 22, '{"guidance_scale":{"type":"slider","min":1,"max":15,"step":0.5,"default":7,"label":"Guidance Scale"}}');

-- Step 3: Insert Models (with capabilities as individual columns)
INSERT INTO "Models" ("Id", "Name", "Version", "Description", "ModelCardUrl", "ModelSeriesId",
                     "SupportsVision", "SupportsImageGeneration", "SupportsVideoGeneration",
                     "SupportsEmbeddings", "SupportsChat", "SupportsFunctionCalling", "SupportsStreaming",
                     "TokenizerType", "MaxInputTokens", "MaxOutputTokens", "IsActive", "CreatedAt", "UpdatedAt")
VALUES
    -- Groq Models
    (1, 'llama-3.1-8b-instant', '3.1', 'Fast 8B parameter LLaMA model', NULL, 1,
     false, false, false, false, true, true, true, 11, 131072, 131072, true, '2025-01-19 00:00:00+00', '2025-01-19 00:00:00+00'),
    (2, 'llama-3.3-70b-versatile', '3.3', 'Versatile 70B parameter LLaMA model', NULL, 2,
     false, false, false, false, true, true, true, 11, 131072, 131072, true, '2025-01-19 00:00:00+00', '2025-01-19 00:00:00+00'),
    (3, 'llama-guard-4-12b', '4', 'Content moderation model with vision', NULL, 4,
     true, false, false, false, true, false, true, 11, 1024, 1024, true, '2025-01-19 00:00:00+00', '2025-01-19 00:00:00+00'),
    (4, 'gpt-oss-120b', '120b', '120B parameter GPT-OSS model', NULL, 5,
     false, false, false, false, true, true, true, 14, 131072, 131072, true, '2025-01-19 00:00:00+00', '2025-01-19 00:00:00+00'),
    (5, 'gpt-oss-20b', '20b', '20B parameter GPT-OSS model', NULL, 5,
     false, false, false, false, true, true, true, 14, 131072, 131072, true, '2025-01-19 00:00:00+00', '2025-01-19 00:00:00+00'),

    -- Fireworks Models
    (6, 'flux-kontext-pro', 'pro', 'Professional image-to-image model', NULL, 6,
     false, true, false, false, false, false, false, 22, 77, 77, true, '2025-01-19 00:00:00+00', '2025-01-19 00:00:00+00'),
    (7, 'qwen3-coder-480b-a35b-instruct', '480b', 'Large coding model with extended context', NULL, 8,
     false, false, false, false, true, true, true, 22, 262144, 262144, true, '2025-01-19 00:00:00+00', '2025-01-19 00:00:00+00'),
    (8, 'glm-4p5', '4.5', 'GLM 4.5 model', NULL, 9,
     false, false, false, false, true, true, true, 19, 131072, 131072, true, '2025-01-19 00:00:00+00', '2025-01-19 00:00:00+00'),
    (9, 'kimi-k2-instruct', 'k2', 'Kimi K2 instruction model', NULL, 10,
     false, false, false, false, true, true, true, 15, 131072, 131072, true, '2025-01-19 00:00:00+00', '2025-01-19 00:00:00+00'),
    (10, 'SSD-1B', '1B', '1B parameter Stable Diffusion', NULL, 7,
     false, true, false, false, false, false, false, 22, 77, 77, true, '2025-01-19 00:00:00+00', '2025-01-19 00:00:00+00'),
    (11, 'chronos-hermes-13b-v2', 'v2', '13B parameter Chronos model', NULL, 11,
     false, false, false, false, true, false, true, 12, 4096, 4096, true, '2025-01-19 00:00:00+00', '2025-01-19 00:00:00+00'),

    -- Cerebras Models
    (12, 'llama-4-scout', '4', 'Multimodal LLaMA 4 Scout', NULL, 3,
     true, false, false, false, true, true, true, 11, 131072, 131072, true, '2025-01-19 00:00:00+00', '2025-01-19 00:00:00+00'),
    (13, 'llama-3.1-8b', '3.1', '8B parameter LLaMA 3.1', NULL, 1,
     false, false, false, false, true, true, true, 11, 32768, 32768, true, '2025-01-19 00:00:00+00', '2025-01-19 00:00:00+00'),
    (14, 'llama-3.3-70b', '3.3', '70B parameter LLaMA 3.3', NULL, 2,
     false, false, false, false, true, true, true, 11, 65536, 65536, true, '2025-01-19 00:00:00+00', '2025-01-19 00:00:00+00'),
    (15, 'openai-oss', '120b', 'OpenAI OSS reasoning model', NULL, 5,
     false, false, false, false, true, false, true, 14, 65536, 65536, true, '2025-01-19 00:00:00+00', '2025-01-19 00:00:00+00'),
    (16, 'qwen-3-32b', '3', '32B Qwen reasoning model', NULL, 8,
     false, false, false, false, true, false, true, 22, 65536, 65536, true, '2025-01-19 00:00:00+00', '2025-01-19 00:00:00+00'),

    -- DeepInfra Models
    (17, 'Kimi-K2-Instruct', 'k2', 'Kimi K2 instruction model', NULL, 10,
     false, false, false, false, true, true, true, 15, 131072, 131072, true, '2025-01-19 00:00:00+00', '2025-01-19 00:00:00+00'),
    (18, 'GLM-4.5V', '4.5V', 'Multimodal GLM with vision', NULL, 9,
     true, false, false, false, true, true, true, 19, 65536, 65536, true, '2025-01-19 00:00:00+00', '2025-01-19 00:00:00+00'),
    (19, 'Llama-4-Maverick', '4', 'LLaMA 4 Maverick with 1M context', NULL, 3,
     true, false, false, false, true, true, true, 11, 1048576, 1048576, true, '2025-01-19 00:00:00+00', '2025-01-19 00:00:00+00'),
    (20, 'SeeDance-T2V', '1.0', 'Text-to-video generation', NULL, 12,
     false, false, true, false, false, false, false, 22, 77, 77, true, '2025-01-19 00:00:00+00', '2025-01-19 00:00:00+00'),
    (21, 'Wan2.1-T2V-14B', '2.1', '14B text-to-video model', NULL, 13,
     false, false, true, false, false, false, false, 22, 77, 77, true, '2025-01-19 00:00:00+00', '2025-01-19 00:00:00+00');

-- Step 4: Insert ModelIdentifiers (Provider as integer: Groq=2, Fireworks=4, Cerebras=9, DeepInfra=11)
INSERT INTO "ModelIdentifiers" ("Id", "ModelId", "Identifier", "Provider", "IsPrimary", "Metadata", "IsEnabled")
VALUES
    -- Groq identifiers (Provider=2)
    (1, 1, 'llama-3.1-8b-instant', 2, true, NULL, true),
    (2, 2, 'llama-3.3-70b-versatile', 2, true, NULL, true),
    (3, 3, 'meta-llama/llama-guard-4-12b', 2, true, NULL, true),
    (4, 4, 'openai/gpt-oss-120b', 2, true, NULL, true),
    (5, 5, 'openai/gpt-oss-20b', 2, true, NULL, true),

    -- Fireworks identifiers (Provider=4)
    (6, 5, 'gpt-oss-20b', 4, true, NULL, true),
    (7, 4, 'gpt-oss-120b', 4, true, NULL, true),
    (8, 6, 'flux-kontext-pro', 4, true, NULL, true),
    (9, 7, 'qwen3-coder-480b-a35b-instruct', 4, true, NULL, true),
    (10, 8, 'glm-4p5', 4, true, NULL, true),
    (11, 9, 'kimi-k2-instruct', 4, true, NULL, true),
    (12, 10, 'SSD-1B', 4, true, NULL, true),
    (13, 11, 'chronos-hermes-13b-v2', 4, true, NULL, true),

    -- Cerebras identifiers (Provider=9)
    (14, 12, 'llama-4-scout', 9, true, NULL, true),
    (15, 13, 'llama-3.1-8b', 9, true, NULL, true),
    (16, 14, 'llama-3.3-70b', 9, true, NULL, true),
    (17, 15, 'openai-oss', 9, true, NULL, true),
    (18, 15, 'gpt-oss-120b', 9, false, NULL, true),
    (19, 16, 'qwen-3-32b', 9, true, NULL, true),

    -- DeepInfra identifiers (Provider=11)
    (20, 17, 'moonshotai/Kimi-K2-Instruct', 11, true, NULL, true),
    (21, 4, 'openai/gpt-oss-120b', 11, true, NULL, true),
    (22, 18, 'zai-org/GLM-4.5V', 11, true, NULL, true),
    (23, 19, 'meta-llama/Llama-4-Maverick', 11, true, NULL, true),
    (24, 20, 'ByteDance/SeeDance-T2V', 11, true, NULL, true),
    (25, 21, 'Wan-AI/Wan2.1-T2V-14B', 11, true, NULL, true);

COMMIT;
