-- Polymorphic Pricing Model Seed Script
-- This script adds model costs using the new polymorphic pricing model

-- MiniMax Video Models (Per-Video Flat Rate)
INSERT INTO "ModelCosts" ("CostName", "PricingModel", "PricingConfiguration", "InputCostPerMillionTokens", "OutputCostPerMillionTokens", "ModelType", "IsActive", "Priority", "CreatedAt", "UpdatedAt", "EffectiveDate")
VALUES 
('MiniMax Hailuo-02 Video', 1, -- PricingModel.PerVideo = 1
'{
  "rates": {
    "512p_6": 0.10,
    "512p_10": 0.15,
    "768p_6": 0.28,
    "768p_10": 0.56,
    "1080p_6": 0.49
  }
}', 0, 0, 'video', true, 100, NOW(), NOW(), NOW()),

('MiniMax S2V-01 Video', 1, -- PricingModel.PerVideo = 1
'{
  "rates": {
    "standard_5": 0.65,
    "standard_10": 0.65
  }
}', 0, 0, 'video', true, 90, NOW(), NOW(), NOW()),

('MiniMax T2V/I2V-01-Director', 1, -- PricingModel.PerVideo = 1
'{
  "rates": {
    "standard_5": 0.43,
    "standard_10": 0.43
  }
}', 0, 0, 'video', true, 90, NOW(), NOW(), NOW()),

('MiniMax I2V-01-live', 1, -- PricingModel.PerVideo = 1
'{
  "rates": {
    "standard_5": 0.43,
    "standard_10": 0.43
  }
}', 0, 0, 'video', true, 90, NOW(), NOW(), NOW());

-- MiniMax Text Models
INSERT INTO "ModelCosts" ("CostName", "PricingModel", "PricingConfiguration", "InputCostPerMillionTokens", "OutputCostPerMillionTokens", "ModelType", "IsActive", "Priority", "CreatedAt", "UpdatedAt", "EffectiveDate")
VALUES 
('MiniMax-M1 Tiered', 4, -- PricingModel.TieredTokens = 4
'{
  "tiers": [
    {
      "maxContext": 200000,
      "inputCost": 400,
      "outputCost": 2200
    },
    {
      "maxContext": null,
      "inputCost": 1300,
      "outputCost": 2200
    }
  ]
}', 0, 0, 'chat', true, 100, NOW(), NOW(), NOW()),

('MiniMax-Text-01', 0, -- PricingModel.Standard = 0
NULL, 200, 1100, 'chat', true, 90, NOW(), NOW(), NOW());

-- MiniMax Image Model
INSERT INTO "ModelCosts" ("CostName", "PricingModel", "PricingConfiguration", "InputCostPerMillionTokens", "OutputCostPerMillionTokens", "ImageCostPerImage", "ModelType", "IsActive", "Priority", "CreatedAt", "UpdatedAt", "EffectiveDate")
VALUES 
('MiniMax Image-01', 5, -- PricingModel.PerImage = 5
'{
  "baseRate": 0.0035,
  "qualityMultipliers": {
    "standard": 1.0,
    "hd": 1.5
  },
  "resolutionMultipliers": {
    "512x512": 0.5,
    "1024x1024": 1.0,
    "1792x1024": 1.5,
    "1024x1792": 1.5
  }
}', 0, 0, 0.0035, 'image', true, 100, NOW(), NOW(), NOW());

-- MiniMax Audio (TTS)
INSERT INTO "ModelCosts" ("CostName", "PricingModel", "InputCostPerMillionTokens", "OutputCostPerMillionTokens", "AudioCostPerKCharacters", "ModelType", "IsActive", "Priority", "CreatedAt", "UpdatedAt", "EffectiveDate")
VALUES 
('MiniMax Speech-2.5-turbo', 0, 0, 0, 0.06, 'audio', true, 90, NOW(), NOW(), NOW()),
('MiniMax Speech-02-turbo', 0, 0, 0, 0.06, 'audio', true, 90, NOW(), NOW(), NOW()),
('MiniMax Speech-2.5-hd', 0, 0, 0, 0.10, 'audio', true, 100, NOW(), NOW(), NOW()),
('MiniMax Speech-02-hd', 0, 0, 0, 0.10, 'audio', true, 100, NOW(), NOW(), NOW());

-- Replicate Video Models (Per-Second with Resolution Multipliers)
INSERT INTO "ModelCosts" ("CostName", "PricingModel", "PricingConfiguration", "InputCostPerMillionTokens", "OutputCostPerMillionTokens", "VideoCostPerSecond", "ModelType", "IsActive", "Priority", "CreatedAt", "UpdatedAt", "EffectiveDate")
VALUES 
('Replicate MiniMax Video', 2, -- PricingModel.PerSecondVideo = 2
'{
  "baseRate": 0.09,
  "resolutionMultipliers": {
    "480p": 0.5,
    "720p": 1.0,
    "1080p": 1.5,
    "4k": 2.5
  }
}', 0, 0, 0.09, 'video', true, 100, NOW(), NOW(), NOW()),

('Replicate Wan 2.1 I2V 480p', 2, -- PricingModel.PerSecondVideo = 2
'{
  "baseRate": 0.09,
  "resolutionMultipliers": {
    "480p": 1.0,
    "720p": 2.78
  }
}', 0, 0, 0.09, 'video', true, 90, NOW(), NOW(), NOW()),

('Replicate Google Veo-2', 2, -- PricingModel.PerSecondVideo = 2
'{
  "baseRate": 0.50,
  "resolutionMultipliers": {
    "480p": 0.6,
    "720p": 1.0,
    "1080p": 1.5
  }
}', 0, 0, 0.50, 'video', true, 110, NOW(), NOW(), NOW());

-- Replicate Image Models (Various pricing models)
INSERT INTO "ModelCosts" ("CostName", "PricingModel", "PricingConfiguration", "InputCostPerMillionTokens", "OutputCostPerMillionTokens", "ImageCostPerImage", "ModelType", "IsActive", "Priority", "CreatedAt", "UpdatedAt", "EffectiveDate")
VALUES 
('Replicate Flux 1.1 Pro', 5, -- PricingModel.PerImage = 5
'{
  "baseRate": 0.04,
  "qualityMultipliers": {
    "standard": 1.0,
    "hd": 1.5
  },
  "resolutionMultipliers": {
    "512x512": 0.5,
    "1024x1024": 1.0,
    "1792x1024": 1.25,
    "1024x1792": 1.25,
    "2048x2048": 2.0
  }
}', 0, 0, 0.04, 'image', true, 100, NOW(), NOW(), NOW()),

('Replicate Flux Dev', 5, -- PricingModel.PerImage = 5
'{
  "baseRate": 0.025,
  "qualityMultipliers": null,
  "resolutionMultipliers": {
    "512x512": 0.5,
    "1024x1024": 1.0,
    "1792x1024": 1.25,
    "1024x1792": 1.25
  }
}', 0, 0, 0.025, 'image', true, 90, NOW(), NOW(), NOW()),

('Replicate Flux Schnell', 5, -- PricingModel.PerImage = 5
'{
  "baseRate": 0.003,
  "qualityMultipliers": null,
  "resolutionMultipliers": null
}', 0, 0, 0.003, 'image', true, 80, NOW(), NOW(), NOW()),

('Replicate SDXL', 5, -- PricingModel.PerImage = 5
'{
  "baseRate": 0.003,
  "qualityMultipliers": {
    "standard": 1.0,
    "detailed": 1.5
  },
  "resolutionMultipliers": {
    "512x512": 0.8,
    "768x768": 0.9,
    "1024x1024": 1.0,
    "1024x1792": 1.3
  }
}', 0, 0, 0.003, 'image', true, 85, NOW(), NOW(), NOW()),

('Replicate Ideogram V3', 5, -- PricingModel.PerImage = 5
'{
  "baseRate": 0.09,
  "qualityMultipliers": {
    "standard": 1.0,
    "premium": 1.5
  },
  "resolutionMultipliers": {
    "1024x1024": 1.0,
    "1280x1280": 1.2,
    "1920x1080": 1.5
  }
}', 0, 0, 0.09, 'image', true, 95, NOW(), NOW(), NOW()),

('Replicate Recraft V3', 5, -- PricingModel.PerImage = 5
'{
  "baseRate": 0.04,
  "qualityMultipliers": null,
  "resolutionMultipliers": {
    "1024x1024": 1.0,
    "1365x1024": 1.2,
    "1024x1365": 1.2
  }
}', 0, 0, 0.04, 'image', true, 90, NOW(), NOW(), NOW());

-- Fireworks AI Image Models (Inference Step Based)
INSERT INTO "ModelCosts" ("CostName", "PricingModel", "PricingConfiguration", "InputCostPerMillionTokens", "OutputCostPerMillionTokens", "CostPerInferenceStep", "DefaultInferenceSteps", "ModelType", "IsActive", "Priority", "CreatedAt", "UpdatedAt", "EffectiveDate")
VALUES 
('Fireworks FLUX.1[schnell]', 3, -- PricingModel.InferenceSteps = 3
'{
  "costPerStep": 0.00035,
  "defaultSteps": 4,
  "modelSteps": {
    "flux-schnell": 4,
    "flux-schnell-fast": 2
  }
}', 0, 0, 0.00035, 4, 'image', true, 100, NOW(), NOW(), NOW()),

('Fireworks FLUX.1[dev]', 3, -- PricingModel.InferenceSteps = 3
'{
  "costPerStep": 0.00025,
  "defaultSteps": 20,
  "modelSteps": {
    "flux-dev": 20,
    "flux-dev-fast": 10,
    "flux-dev-quality": 30
  }
}', 0, 0, 0.00025, 20, 'image', true, 95, NOW(), NOW(), NOW()),

('Fireworks SDXL', 3, -- PricingModel.InferenceSteps = 3
'{
  "costPerStep": 0.00013,
  "defaultSteps": 30,
  "modelSteps": {
    "sdxl": 30,
    "sdxl-fast": 15,
    "sdxl-quality": 50
  }
}', 0, 0, 0.00013, 30, 'image', true, 90, NOW(), NOW(), NOW());

-- Note: This script assumes the following model ID patterns will be used in ModelProviderMappings:
-- MiniMax: minimax/hailuo-02, minimax/s2v-01, minimax/t2v-01-director, minimax/i2v-01-live, minimax/m1, minimax/text-01, minimax/image-01
-- Replicate: replicate/minimax-video-*, replicate/flux-*, replicate/sdxl-*, replicate/ideogram-*, replicate/recraft-*
-- Fireworks: fireworks/flux-schnell-*, fireworks/flux-dev-*, fireworks/sdxl-*