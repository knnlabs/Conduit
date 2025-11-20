-- Auto-generated SQL for Function Configurations and Cost Management
-- Generated: 2025-11-20 19:25:16 UTC
-- Total Configurations: 10
-- Configurations with Cost Data: 10

-- Providers:
--   Exa (1): Neural Search, Keyword Search, Content Retrieval
--   Tavily (4): General Search, News Search, Finance Search, Answer Generation
--   Perplexity (3): Sonar Search, Sonar Pro Search, Sonar Reasoning

-- Pricing Models:
--   FlatRate (1): Fixed cost per execution
--   PerResult (2): Cost per result/page returned
--   Hybrid (6): Combined request fee + token costs

-- NOTE: This script uses upsert logic to preserve manual changes.
-- Existing configurations and costs will be updated with new settings.

BEGIN;

-- Configuration: Exa Neural Search
-- Provider: Exa (Type: 1)
-- Purpose: Search (1)

DO $$
DECLARE
  v_config_id INTEGER;
BEGIN

  -- Check if configuration exists
  SELECT "Id" INTO v_config_id
  FROM "FunctionConfigurations"
  WHERE "ConfigurationName" = 'Exa Neural Search'
    AND "ProviderType" = 1;

  IF v_config_id IS NULL THEN
    -- Insert new configuration
    INSERT INTO "FunctionConfigurations" (
      "ProviderType", "ConfigurationName", "Purpose", "DefaultExecutionMode",
      "IsEnabled", "BaseUrl", "TimeoutSeconds", "MaxRetries",
      "ProviderSettings", "ParameterSchema", "Description",
      "CreatedAt", "UpdatedAt"
    ) VALUES (
      1, 'Exa Neural Search', 1, 1,
      true, NULL, 30, 3,
      '{
        "defaultSearchType": "neural",
        "defaultNumResults": 10,
        "defaultUseAutoprompt": true
      }'::jsonb, '{
        "required": ["query"],
        "optional": {
          "numResults": {
            "type": "number",
            "min": 1,
            "max": 100,
            "default": 10,
            "description": "Number of search results to return"
          },
          "type": {
            "type": "string",
            "enum": ["neural", "keyword", "auto"],
            "default": "neural",
            "description": "Search algorithm type - neural for semantic search, keyword for exact matching, auto for automatic selection"
          },
          "useAutoprompt": {
            "type": "boolean",
            "default": true,
            "description": "Automatically enhance the query for better results"
          },
          "category": {
            "type": "string",
            "enum": ["company", "research paper", "news", "linkedin profile", "github", "tweet", "movie", "song", "personal site", "pdf"],
            "description": "Limit results to specific content category"
          },
          "startPublishedDate": {
            "type": "string",
            "format": "date",
            "description": "Filter results published after this date (YYYY-MM-DD)"
          },
          "endPublishedDate": {
            "type": "string",
            "format": "date",
            "description": "Filter results published before this date (YYYY-MM-DD)"
          },
          "includeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "List of domains to include in search results"
          },
          "excludeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "List of domains to exclude from search results"
          },
          "includeText": {
            "type": "array",
            "items": { "type": "string" },
            "description": "Only include results containing these text snippets"
          },
          "excludeText": {
            "type": "array",
            "items": { "type": "string" },
            "description": "Exclude results containing these text snippets"
          },
          "contents": {
            "type": "object",
            "properties": {
              "text": {
                "type": "boolean",
                "default": false,
                "description": "Extract cleaned page text"
              },
              "highlights": {
                "type": "boolean",
                "default": false,
                "description": "Extract relevant highlights from content"
              },
              "summary": {
                "type": "boolean",
                "default": false,
                "description": "Generate AI summary of content"
              }
            },
            "description": "Content extraction options for search results"
          }
        }
      }'::jsonb, 'Neural semantic search powered by embeddings for finding conceptually similar content. Ideal for discovery, research, and finding related information based on meaning rather than exact keyword matches.',
      NOW(), NOW()
    );
  ELSE
    -- Update existing configuration
    UPDATE "FunctionConfigurations" SET
      "Purpose" = 1,
      "DefaultExecutionMode" = 1,
      "IsEnabled" = true,
      "BaseUrl" = NULL,
      "TimeoutSeconds" = 30,
      "MaxRetries" = 3,
      "ProviderSettings" = '{
        "defaultSearchType": "neural",
        "defaultNumResults": 10,
        "defaultUseAutoprompt": true
      }'::jsonb,
      "ParameterSchema" = '{
        "required": ["query"],
        "optional": {
          "numResults": {
            "type": "number",
            "min": 1,
            "max": 100,
            "default": 10,
            "description": "Number of search results to return"
          },
          "type": {
            "type": "string",
            "enum": ["neural", "keyword", "auto"],
            "default": "neural",
            "description": "Search algorithm type - neural for semantic search, keyword for exact matching, auto for automatic selection"
          },
          "useAutoprompt": {
            "type": "boolean",
            "default": true,
            "description": "Automatically enhance the query for better results"
          },
          "category": {
            "type": "string",
            "enum": ["company", "research paper", "news", "linkedin profile", "github", "tweet", "movie", "song", "personal site", "pdf"],
            "description": "Limit results to specific content category"
          },
          "startPublishedDate": {
            "type": "string",
            "format": "date",
            "description": "Filter results published after this date (YYYY-MM-DD)"
          },
          "endPublishedDate": {
            "type": "string",
            "format": "date",
            "description": "Filter results published before this date (YYYY-MM-DD)"
          },
          "includeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "List of domains to include in search results"
          },
          "excludeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "List of domains to exclude from search results"
          },
          "includeText": {
            "type": "array",
            "items": { "type": "string" },
            "description": "Only include results containing these text snippets"
          },
          "excludeText": {
            "type": "array",
            "items": { "type": "string" },
            "description": "Exclude results containing these text snippets"
          },
          "contents": {
            "type": "object",
            "properties": {
              "text": {
                "type": "boolean",
                "default": false,
                "description": "Extract cleaned page text"
              },
              "highlights": {
                "type": "boolean",
                "default": false,
                "description": "Extract relevant highlights from content"
              },
              "summary": {
                "type": "boolean",
                "default": false,
                "description": "Generate AI summary of content"
              }
            },
            "description": "Content extraction options for search results"
          }
        }
      }'::jsonb,
      "Description" = 'Neural semantic search powered by embeddings for finding conceptually similar content. Ideal for discovery, research, and finding related information based on meaning rather than exact keyword matches.',
      "UpdatedAt" = NOW()
    WHERE "Id" = v_config_id;
  END IF;

END $$;

-- Cost Configuration: Exa Neural Search - Standard Pricing (1-25 results)
-- Pricing Model: FlatRate (1)

DO $$
DECLARE
  v_cost_id INTEGER;
  v_function_config_id INTEGER;
BEGIN

  -- Get function configuration ID
  SELECT "Id" INTO v_function_config_id
  FROM "FunctionConfigurations"
  WHERE "ConfigurationName" = 'Exa Neural Search'
    AND "ProviderType" = 1;

  -- Check if cost configuration exists
  SELECT "Id" INTO v_cost_id
  FROM "FunctionCosts"
  WHERE "CostName" = 'Exa Neural Search - Standard Pricing (1-25 results)';

  IF v_cost_id IS NULL THEN
    -- Insert new cost configuration
    INSERT INTO "FunctionCosts" (
      "CostName", "PricingModel", "CostPerExecution", "CostPerResult",
      "CostPerToken", "CostPerMinute", "TieredPricing", "PricingConfiguration",
      "IsActive", "EffectiveDate", "Priority", "CreatedAt", "UpdatedAt"
    ) VALUES (
      'Exa Neural Search - Standard Pricing (1-25 results)', 1,
      0.00500000, NULL, NULL, NULL,
      NULL::jsonb, NULL::jsonb,
      true, NOW(), 1,
      NOW(), NOW()
    )
    RETURNING "Id" INTO v_cost_id;
  ELSE
    -- Update existing cost configuration
    UPDATE "FunctionCosts" SET
      "PricingModel" = 1,
      "CostPerExecution" = 0.00500000,
      "CostPerResult" = NULL,
      "CostPerToken" = NULL,
      "CostPerMinute" = NULL,
      "TieredPricing" = NULL::jsonb,
      "PricingConfiguration" = NULL::jsonb,
      "IsActive" = true,
      "Priority" = 1,
      "UpdatedAt" = NOW()
    WHERE "Id" = v_cost_id;
  END IF;

  -- Create/update function cost mapping
  IF NOT EXISTS (
    SELECT 1 FROM "FunctionCostMappings"
    WHERE "FunctionConfigurationId" = v_function_config_id
      AND "FunctionCostId" = v_cost_id
  ) THEN
    -- Deactivate any existing mappings for this configuration
    UPDATE "FunctionCostMappings"
    SET "IsActive" = false
    WHERE "FunctionConfigurationId" = v_function_config_id;

    -- Insert new mapping
    INSERT INTO "FunctionCostMappings" (
      "FunctionConfigurationId", "FunctionCostId", "IsActive", "CreatedAt"
    ) VALUES (
      v_function_config_id, v_cost_id, true, NOW()
    );
  END IF;

END $$;

-- Configuration: Exa Keyword Search
-- Provider: Exa (Type: 1)
-- Purpose: Search (1)

DO $$
DECLARE
  v_config_id INTEGER;
BEGIN

  -- Check if configuration exists
  SELECT "Id" INTO v_config_id
  FROM "FunctionConfigurations"
  WHERE "ConfigurationName" = 'Exa Keyword Search'
    AND "ProviderType" = 1;

  IF v_config_id IS NULL THEN
    -- Insert new configuration
    INSERT INTO "FunctionConfigurations" (
      "ProviderType", "ConfigurationName", "Purpose", "DefaultExecutionMode",
      "IsEnabled", "BaseUrl", "TimeoutSeconds", "MaxRetries",
      "ProviderSettings", "ParameterSchema", "Description",
      "CreatedAt", "UpdatedAt"
    ) VALUES (
      1, 'Exa Keyword Search', 1, 1,
      true, NULL, 30, 3,
      '{
        "defaultSearchType": "keyword",
        "defaultNumResults": 10,
        "defaultUseAutoprompt": false
      }'::jsonb, '{
        "required": ["query"],
        "optional": {
          "numResults": {
            "type": "number",
            "min": 1,
            "max": 100,
            "default": 10,
            "description": "Number of search results to return"
          },
          "type": {
            "type": "string",
            "enum": ["neural", "keyword", "auto"],
            "default": "keyword",
            "description": "Search algorithm type - keyword for exact matching"
          },
          "category": {
            "type": "string",
            "enum": ["company", "research paper", "news", "linkedin profile", "github", "tweet", "movie", "song", "personal site", "pdf"],
            "description": "Limit results to specific content category"
          },
          "startPublishedDate": {
            "type": "string",
            "format": "date",
            "description": "Filter results published after this date (YYYY-MM-DD)"
          },
          "endPublishedDate": {
            "type": "string",
            "format": "date",
            "description": "Filter results published before this date (YYYY-MM-DD)"
          },
          "includeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "List of domains to include in search results"
          },
          "excludeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "List of domains to exclude from search results"
          },
          "includeText": {
            "type": "array",
            "items": { "type": "string" },
            "description": "Only include results containing these text snippets"
          },
          "excludeText": {
            "type": "array",
            "items": { "type": "string" },
            "description": "Exclude results containing these text snippets"
          },
          "contents": {
            "type": "object",
            "properties": {
              "text": {
                "type": "boolean",
                "default": false,
                "description": "Extract cleaned page text"
              },
              "highlights": {
                "type": "boolean",
                "default": false,
                "description": "Extract relevant highlights from content"
              },
              "summary": {
                "type": "boolean",
                "default": false,
                "description": "Generate AI summary of content"
              }
            },
            "description": "Content extraction options for search results"
          }
        }
      }'::jsonb, 'Traditional keyword-based search for exact term matching and technical queries. Best for finding specific phrases, technical documentation, code snippets, or when you know the exact terminology.',
      NOW(), NOW()
    );
  ELSE
    -- Update existing configuration
    UPDATE "FunctionConfigurations" SET
      "Purpose" = 1,
      "DefaultExecutionMode" = 1,
      "IsEnabled" = true,
      "BaseUrl" = NULL,
      "TimeoutSeconds" = 30,
      "MaxRetries" = 3,
      "ProviderSettings" = '{
        "defaultSearchType": "keyword",
        "defaultNumResults": 10,
        "defaultUseAutoprompt": false
      }'::jsonb,
      "ParameterSchema" = '{
        "required": ["query"],
        "optional": {
          "numResults": {
            "type": "number",
            "min": 1,
            "max": 100,
            "default": 10,
            "description": "Number of search results to return"
          },
          "type": {
            "type": "string",
            "enum": ["neural", "keyword", "auto"],
            "default": "keyword",
            "description": "Search algorithm type - keyword for exact matching"
          },
          "category": {
            "type": "string",
            "enum": ["company", "research paper", "news", "linkedin profile", "github", "tweet", "movie", "song", "personal site", "pdf"],
            "description": "Limit results to specific content category"
          },
          "startPublishedDate": {
            "type": "string",
            "format": "date",
            "description": "Filter results published after this date (YYYY-MM-DD)"
          },
          "endPublishedDate": {
            "type": "string",
            "format": "date",
            "description": "Filter results published before this date (YYYY-MM-DD)"
          },
          "includeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "List of domains to include in search results"
          },
          "excludeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "List of domains to exclude from search results"
          },
          "includeText": {
            "type": "array",
            "items": { "type": "string" },
            "description": "Only include results containing these text snippets"
          },
          "excludeText": {
            "type": "array",
            "items": { "type": "string" },
            "description": "Exclude results containing these text snippets"
          },
          "contents": {
            "type": "object",
            "properties": {
              "text": {
                "type": "boolean",
                "default": false,
                "description": "Extract cleaned page text"
              },
              "highlights": {
                "type": "boolean",
                "default": false,
                "description": "Extract relevant highlights from content"
              },
              "summary": {
                "type": "boolean",
                "default": false,
                "description": "Generate AI summary of content"
              }
            },
            "description": "Content extraction options for search results"
          }
        }
      }'::jsonb,
      "Description" = 'Traditional keyword-based search for exact term matching and technical queries. Best for finding specific phrases, technical documentation, code snippets, or when you know the exact terminology.',
      "UpdatedAt" = NOW()
    WHERE "Id" = v_config_id;
  END IF;

END $$;

-- Cost Configuration: Exa Keyword Search - Standard Pricing (1-100 results)
-- Pricing Model: FlatRate (1)

DO $$
DECLARE
  v_cost_id INTEGER;
  v_function_config_id INTEGER;
BEGIN

  -- Get function configuration ID
  SELECT "Id" INTO v_function_config_id
  FROM "FunctionConfigurations"
  WHERE "ConfigurationName" = 'Exa Keyword Search'
    AND "ProviderType" = 1;

  -- Check if cost configuration exists
  SELECT "Id" INTO v_cost_id
  FROM "FunctionCosts"
  WHERE "CostName" = 'Exa Keyword Search - Standard Pricing (1-100 results)';

  IF v_cost_id IS NULL THEN
    -- Insert new cost configuration
    INSERT INTO "FunctionCosts" (
      "CostName", "PricingModel", "CostPerExecution", "CostPerResult",
      "CostPerToken", "CostPerMinute", "TieredPricing", "PricingConfiguration",
      "IsActive", "EffectiveDate", "Priority", "CreatedAt", "UpdatedAt"
    ) VALUES (
      'Exa Keyword Search - Standard Pricing (1-100 results)', 1,
      0.00250000, NULL, NULL, NULL,
      NULL::jsonb, NULL::jsonb,
      true, NOW(), 1,
      NOW(), NOW()
    )
    RETURNING "Id" INTO v_cost_id;
  ELSE
    -- Update existing cost configuration
    UPDATE "FunctionCosts" SET
      "PricingModel" = 1,
      "CostPerExecution" = 0.00250000,
      "CostPerResult" = NULL,
      "CostPerToken" = NULL,
      "CostPerMinute" = NULL,
      "TieredPricing" = NULL::jsonb,
      "PricingConfiguration" = NULL::jsonb,
      "IsActive" = true,
      "Priority" = 1,
      "UpdatedAt" = NOW()
    WHERE "Id" = v_cost_id;
  END IF;

  -- Create/update function cost mapping
  IF NOT EXISTS (
    SELECT 1 FROM "FunctionCostMappings"
    WHERE "FunctionConfigurationId" = v_function_config_id
      AND "FunctionCostId" = v_cost_id
  ) THEN
    -- Deactivate any existing mappings for this configuration
    UPDATE "FunctionCostMappings"
    SET "IsActive" = false
    WHERE "FunctionConfigurationId" = v_function_config_id;

    -- Insert new mapping
    INSERT INTO "FunctionCostMappings" (
      "FunctionConfigurationId", "FunctionCostId", "IsActive", "CreatedAt"
    ) VALUES (
      v_function_config_id, v_cost_id, true, NOW()
    );
  END IF;

END $$;

-- Configuration: Exa Content Retrieval
-- Provider: Exa (Type: 1)
-- Purpose: ContentRetrieval (3)

DO $$
DECLARE
  v_config_id INTEGER;
BEGIN

  -- Check if configuration exists
  SELECT "Id" INTO v_config_id
  FROM "FunctionConfigurations"
  WHERE "ConfigurationName" = 'Exa Content Retrieval'
    AND "ProviderType" = 1;

  IF v_config_id IS NULL THEN
    -- Insert new configuration
    INSERT INTO "FunctionConfigurations" (
      "ProviderType", "ConfigurationName", "Purpose", "DefaultExecutionMode",
      "IsEnabled", "BaseUrl", "TimeoutSeconds", "MaxRetries",
      "ProviderSettings", "ParameterSchema", "Description",
      "CreatedAt", "UpdatedAt"
    ) VALUES (
      1, 'Exa Content Retrieval', 3, 2,
      true, NULL, 60, 3,
      '{
        "defaultText": true,
        "defaultHighlights": false,
        "defaultSummary": false
      }'::jsonb, '{
        "required": ["urls"],
        "optional": {
          "text": {
            "type": "boolean",
            "default": true,
            "description": "Extract cleaned, readable text from pages"
          },
          "highlights": {
            "type": "boolean",
            "default": false,
            "description": "Extract key highlights and important passages"
          },
          "summary": {
            "type": "boolean",
            "default": false,
            "description": "Generate AI-powered summary of content"
          },
          "subpages": {
            "type": "number",
            "min": 0,
            "max": 10,
            "description": "Number of subpages to crawl and extract (0 for none)"
          },
          "subpageTarget": {
            "type": "string",
            "description": "Natural language description of desired subpages to crawl"
          },
          "livecrawl": {
            "type": "string",
            "enum": ["always", "never", "fallback"],
            "default": "fallback",
            "description": "Live crawling strategy - always for fresh content, never for cached only, fallback for cached with live backup"
          }
        }
      }'::jsonb, 'Extract and process content from specific URLs with highlights, summaries, and subpage crawling. Ideal for deep content analysis, article extraction, and comprehensive site scraping.',
      NOW(), NOW()
    );
  ELSE
    -- Update existing configuration
    UPDATE "FunctionConfigurations" SET
      "Purpose" = 3,
      "DefaultExecutionMode" = 2,
      "IsEnabled" = true,
      "BaseUrl" = NULL,
      "TimeoutSeconds" = 60,
      "MaxRetries" = 3,
      "ProviderSettings" = '{
        "defaultText": true,
        "defaultHighlights": false,
        "defaultSummary": false
      }'::jsonb,
      "ParameterSchema" = '{
        "required": ["urls"],
        "optional": {
          "text": {
            "type": "boolean",
            "default": true,
            "description": "Extract cleaned, readable text from pages"
          },
          "highlights": {
            "type": "boolean",
            "default": false,
            "description": "Extract key highlights and important passages"
          },
          "summary": {
            "type": "boolean",
            "default": false,
            "description": "Generate AI-powered summary of content"
          },
          "subpages": {
            "type": "number",
            "min": 0,
            "max": 10,
            "description": "Number of subpages to crawl and extract (0 for none)"
          },
          "subpageTarget": {
            "type": "string",
            "description": "Natural language description of desired subpages to crawl"
          },
          "livecrawl": {
            "type": "string",
            "enum": ["always", "never", "fallback"],
            "default": "fallback",
            "description": "Live crawling strategy - always for fresh content, never for cached only, fallback for cached with live backup"
          }
        }
      }'::jsonb,
      "Description" = 'Extract and process content from specific URLs with highlights, summaries, and subpage crawling. Ideal for deep content analysis, article extraction, and comprehensive site scraping.',
      "UpdatedAt" = NOW()
    WHERE "Id" = v_config_id;
  END IF;

END $$;

-- Cost Configuration: Exa Content Retrieval - Per Page Pricing
-- Pricing Model: PerResult (2)

DO $$
DECLARE
  v_cost_id INTEGER;
  v_function_config_id INTEGER;
BEGIN

  -- Get function configuration ID
  SELECT "Id" INTO v_function_config_id
  FROM "FunctionConfigurations"
  WHERE "ConfigurationName" = 'Exa Content Retrieval'
    AND "ProviderType" = 1;

  -- Check if cost configuration exists
  SELECT "Id" INTO v_cost_id
  FROM "FunctionCosts"
  WHERE "CostName" = 'Exa Content Retrieval - Per Page Pricing';

  IF v_cost_id IS NULL THEN
    -- Insert new cost configuration
    INSERT INTO "FunctionCosts" (
      "CostName", "PricingModel", "CostPerExecution", "CostPerResult",
      "CostPerToken", "CostPerMinute", "TieredPricing", "PricingConfiguration",
      "IsActive", "EffectiveDate", "Priority", "CreatedAt", "UpdatedAt"
    ) VALUES (
      'Exa Content Retrieval - Per Page Pricing', 2,
      NULL, 0.00100000, NULL, NULL,
      NULL::jsonb, NULL::jsonb,
      true, NOW(), 1,
      NOW(), NOW()
    )
    RETURNING "Id" INTO v_cost_id;
  ELSE
    -- Update existing cost configuration
    UPDATE "FunctionCosts" SET
      "PricingModel" = 2,
      "CostPerExecution" = NULL,
      "CostPerResult" = 0.00100000,
      "CostPerToken" = NULL,
      "CostPerMinute" = NULL,
      "TieredPricing" = NULL::jsonb,
      "PricingConfiguration" = NULL::jsonb,
      "IsActive" = true,
      "Priority" = 1,
      "UpdatedAt" = NOW()
    WHERE "Id" = v_cost_id;
  END IF;

  -- Create/update function cost mapping
  IF NOT EXISTS (
    SELECT 1 FROM "FunctionCostMappings"
    WHERE "FunctionConfigurationId" = v_function_config_id
      AND "FunctionCostId" = v_cost_id
  ) THEN
    -- Deactivate any existing mappings for this configuration
    UPDATE "FunctionCostMappings"
    SET "IsActive" = false
    WHERE "FunctionConfigurationId" = v_function_config_id;

    -- Insert new mapping
    INSERT INTO "FunctionCostMappings" (
      "FunctionConfigurationId", "FunctionCostId", "IsActive", "CreatedAt"
    ) VALUES (
      v_function_config_id, v_cost_id, true, NOW()
    );
  END IF;

END $$;

-- Configuration: Tavily General Search
-- Provider: Tavily (Type: 4)
-- Purpose: Search (1)

DO $$
DECLARE
  v_config_id INTEGER;
BEGIN

  -- Check if configuration exists
  SELECT "Id" INTO v_config_id
  FROM "FunctionConfigurations"
  WHERE "ConfigurationName" = 'Tavily General Search'
    AND "ProviderType" = 4;

  IF v_config_id IS NULL THEN
    -- Insert new configuration
    INSERT INTO "FunctionConfigurations" (
      "ProviderType", "ConfigurationName", "Purpose", "DefaultExecutionMode",
      "IsEnabled", "BaseUrl", "TimeoutSeconds", "MaxRetries",
      "ProviderSettings", "ParameterSchema", "Description",
      "CreatedAt", "UpdatedAt"
    ) VALUES (
      4, 'Tavily General Search', 1, 1,
      true, NULL, 30, 3,
      '{
        "defaultTopic": "general",
        "defaultSearchDepth": "basic",
        "defaultMaxResults": 5
      }'::jsonb, '{
        "required": ["query"],
        "optional": {
          "searchDepth": {
            "type": "string",
            "enum": ["basic", "advanced"],
            "default": "basic",
            "description": "Search depth - basic for quick results, advanced for comprehensive research"
          },
          "topic": {
            "type": "string",
            "enum": ["general", "news", "finance"],
            "default": "general",
            "description": "Search topic category for optimized results"
          },
          "maxResults": {
            "type": "number",
            "min": 1,
            "max": 20,
            "default": 5,
            "description": "Maximum number of search results"
          },
          "includeImages": {
            "type": "boolean",
            "default": false,
            "description": "Include relevant images in results"
          },
          "includeAnswer": {
            "type": "boolean",
            "default": false,
            "description": "Include AI-generated answer to the query"
          },
          "includeRawContent": {
            "type": "boolean",
            "default": false,
            "description": "Include raw HTML content from pages"
          },
          "includeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "List of domains to prioritize in results"
          },
          "excludeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "List of domains to exclude from results"
          },
          "days": {
            "type": "number",
            "min": 1,
            "description": "Limit results to content from the past N days"
          }
        }
      }'::jsonb, 'RAG-optimized search for general queries with structured results and snippets. Provides clean, relevant content optimized for AI consumption and context augmentation.',
      NOW(), NOW()
    );
  ELSE
    -- Update existing configuration
    UPDATE "FunctionConfigurations" SET
      "Purpose" = 1,
      "DefaultExecutionMode" = 1,
      "IsEnabled" = true,
      "BaseUrl" = NULL,
      "TimeoutSeconds" = 30,
      "MaxRetries" = 3,
      "ProviderSettings" = '{
        "defaultTopic": "general",
        "defaultSearchDepth": "basic",
        "defaultMaxResults": 5
      }'::jsonb,
      "ParameterSchema" = '{
        "required": ["query"],
        "optional": {
          "searchDepth": {
            "type": "string",
            "enum": ["basic", "advanced"],
            "default": "basic",
            "description": "Search depth - basic for quick results, advanced for comprehensive research"
          },
          "topic": {
            "type": "string",
            "enum": ["general", "news", "finance"],
            "default": "general",
            "description": "Search topic category for optimized results"
          },
          "maxResults": {
            "type": "number",
            "min": 1,
            "max": 20,
            "default": 5,
            "description": "Maximum number of search results"
          },
          "includeImages": {
            "type": "boolean",
            "default": false,
            "description": "Include relevant images in results"
          },
          "includeAnswer": {
            "type": "boolean",
            "default": false,
            "description": "Include AI-generated answer to the query"
          },
          "includeRawContent": {
            "type": "boolean",
            "default": false,
            "description": "Include raw HTML content from pages"
          },
          "includeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "List of domains to prioritize in results"
          },
          "excludeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "List of domains to exclude from results"
          },
          "days": {
            "type": "number",
            "min": 1,
            "description": "Limit results to content from the past N days"
          }
        }
      }'::jsonb,
      "Description" = 'RAG-optimized search for general queries with structured results and snippets. Provides clean, relevant content optimized for AI consumption and context augmentation.',
      "UpdatedAt" = NOW()
    WHERE "Id" = v_config_id;
  END IF;

END $$;

-- Cost Configuration: Tavily General Search - Basic Depth (1 credit)
-- Pricing Model: FlatRate (1)

DO $$
DECLARE
  v_cost_id INTEGER;
  v_function_config_id INTEGER;
BEGIN

  -- Get function configuration ID
  SELECT "Id" INTO v_function_config_id
  FROM "FunctionConfigurations"
  WHERE "ConfigurationName" = 'Tavily General Search'
    AND "ProviderType" = 4;

  -- Check if cost configuration exists
  SELECT "Id" INTO v_cost_id
  FROM "FunctionCosts"
  WHERE "CostName" = 'Tavily General Search - Basic Depth (1 credit)';

  IF v_cost_id IS NULL THEN
    -- Insert new cost configuration
    INSERT INTO "FunctionCosts" (
      "CostName", "PricingModel", "CostPerExecution", "CostPerResult",
      "CostPerToken", "CostPerMinute", "TieredPricing", "PricingConfiguration",
      "IsActive", "EffectiveDate", "Priority", "CreatedAt", "UpdatedAt"
    ) VALUES (
      'Tavily General Search - Basic Depth (1 credit)', 1,
      0.00750000, NULL, NULL, NULL,
      NULL::jsonb, NULL::jsonb,
      true, NOW(), 1,
      NOW(), NOW()
    )
    RETURNING "Id" INTO v_cost_id;
  ELSE
    -- Update existing cost configuration
    UPDATE "FunctionCosts" SET
      "PricingModel" = 1,
      "CostPerExecution" = 0.00750000,
      "CostPerResult" = NULL,
      "CostPerToken" = NULL,
      "CostPerMinute" = NULL,
      "TieredPricing" = NULL::jsonb,
      "PricingConfiguration" = NULL::jsonb,
      "IsActive" = true,
      "Priority" = 1,
      "UpdatedAt" = NOW()
    WHERE "Id" = v_cost_id;
  END IF;

  -- Create/update function cost mapping
  IF NOT EXISTS (
    SELECT 1 FROM "FunctionCostMappings"
    WHERE "FunctionConfigurationId" = v_function_config_id
      AND "FunctionCostId" = v_cost_id
  ) THEN
    -- Deactivate any existing mappings for this configuration
    UPDATE "FunctionCostMappings"
    SET "IsActive" = false
    WHERE "FunctionConfigurationId" = v_function_config_id;

    -- Insert new mapping
    INSERT INTO "FunctionCostMappings" (
      "FunctionConfigurationId", "FunctionCostId", "IsActive", "CreatedAt"
    ) VALUES (
      v_function_config_id, v_cost_id, true, NOW()
    );
  END IF;

END $$;

-- Configuration: Tavily News Search
-- Provider: Tavily (Type: 4)
-- Purpose: Search (1)

DO $$
DECLARE
  v_config_id INTEGER;
BEGIN

  -- Check if configuration exists
  SELECT "Id" INTO v_config_id
  FROM "FunctionConfigurations"
  WHERE "ConfigurationName" = 'Tavily News Search'
    AND "ProviderType" = 4;

  IF v_config_id IS NULL THEN
    -- Insert new configuration
    INSERT INTO "FunctionConfigurations" (
      "ProviderType", "ConfigurationName", "Purpose", "DefaultExecutionMode",
      "IsEnabled", "BaseUrl", "TimeoutSeconds", "MaxRetries",
      "ProviderSettings", "ParameterSchema", "Description",
      "CreatedAt", "UpdatedAt"
    ) VALUES (
      4, 'Tavily News Search', 1, 1,
      true, NULL, 30, 3,
      '{
        "defaultTopic": "news",
        "defaultSearchDepth": "basic",
        "defaultMaxResults": 5,
        "defaultDays": 7
      }'::jsonb, '{
        "required": ["query"],
        "optional": {
          "searchDepth": {
            "type": "string",
            "enum": ["basic", "advanced"],
            "default": "basic",
            "description": "Search depth - basic for quick results, advanced for comprehensive coverage"
          },
          "topic": {
            "type": "string",
            "enum": ["general", "news", "finance"],
            "default": "news",
            "description": "Search topic category - news for current events"
          },
          "maxResults": {
            "type": "number",
            "min": 1,
            "max": 20,
            "default": 5,
            "description": "Maximum number of news articles"
          },
          "days": {
            "type": "number",
            "min": 1,
            "max": 30,
            "default": 7,
            "description": "Limit results to articles from the past N days"
          },
          "includeImages": {
            "type": "boolean",
            "default": true,
            "description": "Include article images"
          },
          "includeAnswer": {
            "type": "boolean",
            "default": false,
            "description": "Include AI-generated news summary"
          },
          "includeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "List of news domains to prioritize"
          },
          "excludeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "List of domains to exclude"
          }
        }
      }'::jsonb, 'Real-time news search with temporal filtering for current events and breaking news. Optimized for journalistic content with recency bias and source diversity.',
      NOW(), NOW()
    );
  ELSE
    -- Update existing configuration
    UPDATE "FunctionConfigurations" SET
      "Purpose" = 1,
      "DefaultExecutionMode" = 1,
      "IsEnabled" = true,
      "BaseUrl" = NULL,
      "TimeoutSeconds" = 30,
      "MaxRetries" = 3,
      "ProviderSettings" = '{
        "defaultTopic": "news",
        "defaultSearchDepth": "basic",
        "defaultMaxResults": 5,
        "defaultDays": 7
      }'::jsonb,
      "ParameterSchema" = '{
        "required": ["query"],
        "optional": {
          "searchDepth": {
            "type": "string",
            "enum": ["basic", "advanced"],
            "default": "basic",
            "description": "Search depth - basic for quick results, advanced for comprehensive coverage"
          },
          "topic": {
            "type": "string",
            "enum": ["general", "news", "finance"],
            "default": "news",
            "description": "Search topic category - news for current events"
          },
          "maxResults": {
            "type": "number",
            "min": 1,
            "max": 20,
            "default": 5,
            "description": "Maximum number of news articles"
          },
          "days": {
            "type": "number",
            "min": 1,
            "max": 30,
            "default": 7,
            "description": "Limit results to articles from the past N days"
          },
          "includeImages": {
            "type": "boolean",
            "default": true,
            "description": "Include article images"
          },
          "includeAnswer": {
            "type": "boolean",
            "default": false,
            "description": "Include AI-generated news summary"
          },
          "includeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "List of news domains to prioritize"
          },
          "excludeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "List of domains to exclude"
          }
        }
      }'::jsonb,
      "Description" = 'Real-time news search with temporal filtering for current events and breaking news. Optimized for journalistic content with recency bias and source diversity.',
      "UpdatedAt" = NOW()
    WHERE "Id" = v_config_id;
  END IF;

END $$;

-- Cost Configuration: Tavily News Search - Basic Depth (1 credit)
-- Pricing Model: FlatRate (1)

DO $$
DECLARE
  v_cost_id INTEGER;
  v_function_config_id INTEGER;
BEGIN

  -- Get function configuration ID
  SELECT "Id" INTO v_function_config_id
  FROM "FunctionConfigurations"
  WHERE "ConfigurationName" = 'Tavily News Search'
    AND "ProviderType" = 4;

  -- Check if cost configuration exists
  SELECT "Id" INTO v_cost_id
  FROM "FunctionCosts"
  WHERE "CostName" = 'Tavily News Search - Basic Depth (1 credit)';

  IF v_cost_id IS NULL THEN
    -- Insert new cost configuration
    INSERT INTO "FunctionCosts" (
      "CostName", "PricingModel", "CostPerExecution", "CostPerResult",
      "CostPerToken", "CostPerMinute", "TieredPricing", "PricingConfiguration",
      "IsActive", "EffectiveDate", "Priority", "CreatedAt", "UpdatedAt"
    ) VALUES (
      'Tavily News Search - Basic Depth (1 credit)', 1,
      0.00750000, NULL, NULL, NULL,
      NULL::jsonb, NULL::jsonb,
      true, NOW(), 1,
      NOW(), NOW()
    )
    RETURNING "Id" INTO v_cost_id;
  ELSE
    -- Update existing cost configuration
    UPDATE "FunctionCosts" SET
      "PricingModel" = 1,
      "CostPerExecution" = 0.00750000,
      "CostPerResult" = NULL,
      "CostPerToken" = NULL,
      "CostPerMinute" = NULL,
      "TieredPricing" = NULL::jsonb,
      "PricingConfiguration" = NULL::jsonb,
      "IsActive" = true,
      "Priority" = 1,
      "UpdatedAt" = NOW()
    WHERE "Id" = v_cost_id;
  END IF;

  -- Create/update function cost mapping
  IF NOT EXISTS (
    SELECT 1 FROM "FunctionCostMappings"
    WHERE "FunctionConfigurationId" = v_function_config_id
      AND "FunctionCostId" = v_cost_id
  ) THEN
    -- Deactivate any existing mappings for this configuration
    UPDATE "FunctionCostMappings"
    SET "IsActive" = false
    WHERE "FunctionConfigurationId" = v_function_config_id;

    -- Insert new mapping
    INSERT INTO "FunctionCostMappings" (
      "FunctionConfigurationId", "FunctionCostId", "IsActive", "CreatedAt"
    ) VALUES (
      v_function_config_id, v_cost_id, true, NOW()
    );
  END IF;

END $$;

-- Configuration: Tavily Finance Search
-- Provider: Tavily (Type: 4)
-- Purpose: Search (1)

DO $$
DECLARE
  v_config_id INTEGER;
BEGIN

  -- Check if configuration exists
  SELECT "Id" INTO v_config_id
  FROM "FunctionConfigurations"
  WHERE "ConfigurationName" = 'Tavily Finance Search'
    AND "ProviderType" = 4;

  IF v_config_id IS NULL THEN
    -- Insert new configuration
    INSERT INTO "FunctionConfigurations" (
      "ProviderType", "ConfigurationName", "Purpose", "DefaultExecutionMode",
      "IsEnabled", "BaseUrl", "TimeoutSeconds", "MaxRetries",
      "ProviderSettings", "ParameterSchema", "Description",
      "CreatedAt", "UpdatedAt"
    ) VALUES (
      4, 'Tavily Finance Search', 1, 1,
      true, NULL, 30, 3,
      '{
        "defaultTopic": "finance",
        "defaultSearchDepth": "basic",
        "defaultMaxResults": 5
      }'::jsonb, '{
        "required": ["query"],
        "optional": {
          "searchDepth": {
            "type": "string",
            "enum": ["basic", "advanced"],
            "default": "basic",
            "description": "Search depth - basic for quick data, advanced for comprehensive analysis"
          },
          "topic": {
            "type": "string",
            "enum": ["general", "news", "finance"],
            "default": "finance",
            "description": "Search topic category - finance for market data and analysis"
          },
          "maxResults": {
            "type": "number",
            "min": 1,
            "max": 20,
            "default": 5,
            "description": "Maximum number of financial sources"
          },
          "days": {
            "type": "number",
            "min": 1,
            "description": "Limit results to recent financial data from past N days"
          },
          "includeImages": {
            "type": "boolean",
            "default": false,
            "description": "Include charts and financial graphics"
          },
          "includeAnswer": {
            "type": "boolean",
            "default": false,
            "description": "Include AI-generated financial analysis"
          },
          "includeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "List of financial domains to prioritize"
          },
          "excludeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "List of domains to exclude"
          }
        }
      }'::jsonb, 'Financial data and market information search optimized for finance-related queries. Prioritizes authoritative financial sources, market data, and economic analysis.',
      NOW(), NOW()
    );
  ELSE
    -- Update existing configuration
    UPDATE "FunctionConfigurations" SET
      "Purpose" = 1,
      "DefaultExecutionMode" = 1,
      "IsEnabled" = true,
      "BaseUrl" = NULL,
      "TimeoutSeconds" = 30,
      "MaxRetries" = 3,
      "ProviderSettings" = '{
        "defaultTopic": "finance",
        "defaultSearchDepth": "basic",
        "defaultMaxResults": 5
      }'::jsonb,
      "ParameterSchema" = '{
        "required": ["query"],
        "optional": {
          "searchDepth": {
            "type": "string",
            "enum": ["basic", "advanced"],
            "default": "basic",
            "description": "Search depth - basic for quick data, advanced for comprehensive analysis"
          },
          "topic": {
            "type": "string",
            "enum": ["general", "news", "finance"],
            "default": "finance",
            "description": "Search topic category - finance for market data and analysis"
          },
          "maxResults": {
            "type": "number",
            "min": 1,
            "max": 20,
            "default": 5,
            "description": "Maximum number of financial sources"
          },
          "days": {
            "type": "number",
            "min": 1,
            "description": "Limit results to recent financial data from past N days"
          },
          "includeImages": {
            "type": "boolean",
            "default": false,
            "description": "Include charts and financial graphics"
          },
          "includeAnswer": {
            "type": "boolean",
            "default": false,
            "description": "Include AI-generated financial analysis"
          },
          "includeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "List of financial domains to prioritize"
          },
          "excludeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "List of domains to exclude"
          }
        }
      }'::jsonb,
      "Description" = 'Financial data and market information search optimized for finance-related queries. Prioritizes authoritative financial sources, market data, and economic analysis.',
      "UpdatedAt" = NOW()
    WHERE "Id" = v_config_id;
  END IF;

END $$;

-- Cost Configuration: Tavily Finance Search - Basic Depth (1 credit)
-- Pricing Model: FlatRate (1)

DO $$
DECLARE
  v_cost_id INTEGER;
  v_function_config_id INTEGER;
BEGIN

  -- Get function configuration ID
  SELECT "Id" INTO v_function_config_id
  FROM "FunctionConfigurations"
  WHERE "ConfigurationName" = 'Tavily Finance Search'
    AND "ProviderType" = 4;

  -- Check if cost configuration exists
  SELECT "Id" INTO v_cost_id
  FROM "FunctionCosts"
  WHERE "CostName" = 'Tavily Finance Search - Basic Depth (1 credit)';

  IF v_cost_id IS NULL THEN
    -- Insert new cost configuration
    INSERT INTO "FunctionCosts" (
      "CostName", "PricingModel", "CostPerExecution", "CostPerResult",
      "CostPerToken", "CostPerMinute", "TieredPricing", "PricingConfiguration",
      "IsActive", "EffectiveDate", "Priority", "CreatedAt", "UpdatedAt"
    ) VALUES (
      'Tavily Finance Search - Basic Depth (1 credit)', 1,
      0.00750000, NULL, NULL, NULL,
      NULL::jsonb, NULL::jsonb,
      true, NOW(), 1,
      NOW(), NOW()
    )
    RETURNING "Id" INTO v_cost_id;
  ELSE
    -- Update existing cost configuration
    UPDATE "FunctionCosts" SET
      "PricingModel" = 1,
      "CostPerExecution" = 0.00750000,
      "CostPerResult" = NULL,
      "CostPerToken" = NULL,
      "CostPerMinute" = NULL,
      "TieredPricing" = NULL::jsonb,
      "PricingConfiguration" = NULL::jsonb,
      "IsActive" = true,
      "Priority" = 1,
      "UpdatedAt" = NOW()
    WHERE "Id" = v_cost_id;
  END IF;

  -- Create/update function cost mapping
  IF NOT EXISTS (
    SELECT 1 FROM "FunctionCostMappings"
    WHERE "FunctionConfigurationId" = v_function_config_id
      AND "FunctionCostId" = v_cost_id
  ) THEN
    -- Deactivate any existing mappings for this configuration
    UPDATE "FunctionCostMappings"
    SET "IsActive" = false
    WHERE "FunctionConfigurationId" = v_function_config_id;

    -- Insert new mapping
    INSERT INTO "FunctionCostMappings" (
      "FunctionConfigurationId", "FunctionCostId", "IsActive", "CreatedAt"
    ) VALUES (
      v_function_config_id, v_cost_id, true, NOW()
    );
  END IF;

END $$;

-- Configuration: Tavily Answer Generation
-- Provider: Tavily (Type: 4)
-- Purpose: Answer (2)

DO $$
DECLARE
  v_config_id INTEGER;
BEGIN

  -- Check if configuration exists
  SELECT "Id" INTO v_config_id
  FROM "FunctionConfigurations"
  WHERE "ConfigurationName" = 'Tavily Answer Generation'
    AND "ProviderType" = 4;

  IF v_config_id IS NULL THEN
    -- Insert new configuration
    INSERT INTO "FunctionConfigurations" (
      "ProviderType", "ConfigurationName", "Purpose", "DefaultExecutionMode",
      "IsEnabled", "BaseUrl", "TimeoutSeconds", "MaxRetries",
      "ProviderSettings", "ParameterSchema", "Description",
      "CreatedAt", "UpdatedAt"
    ) VALUES (
      4, 'Tavily Answer Generation', 2, 1,
      true, NULL, 45, 3,
      '{
        "defaultIncludeAnswer": true,
        "defaultSearchDepth": "advanced",
        "defaultMaxResults": 10
      }'::jsonb, '{
        "required": ["query"],
        "optional": {
          "searchDepth": {
            "type": "string",
            "enum": ["basic", "advanced"],
            "default": "advanced",
            "description": "Search depth - advanced recommended for comprehensive answers"
          },
          "topic": {
            "type": "string",
            "enum": ["general", "news", "finance"],
            "default": "general",
            "description": "Search topic category for context gathering"
          },
          "maxResults": {
            "type": "number",
            "min": 1,
            "max": 20,
            "default": 10,
            "description": "Number of sources to use for answer generation"
          },
          "includeAnswer": {
            "type": "boolean",
            "default": true,
            "description": "Generate AI answer (must be true for this configuration)"
          },
          "includeRawContent": {
            "type": "boolean",
            "default": false,
            "description": "Include full source content with answer"
          },
          "includeImages": {
            "type": "boolean",
            "default": true,
            "description": "Include relevant images with answer"
          },
          "days": {
            "type": "number",
            "min": 1,
            "description": "Limit sources to content from past N days"
          },
          "includeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "Prioritize specific domains for answer sources"
          },
          "excludeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "Exclude specific domains from answer sources"
          }
        }
      }'::jsonb, 'LLM-powered answer generation from search results with citations and source grounding. Combines advanced search with AI synthesis to provide comprehensive, cited answers to complex questions.',
      NOW(), NOW()
    );
  ELSE
    -- Update existing configuration
    UPDATE "FunctionConfigurations" SET
      "Purpose" = 2,
      "DefaultExecutionMode" = 1,
      "IsEnabled" = true,
      "BaseUrl" = NULL,
      "TimeoutSeconds" = 45,
      "MaxRetries" = 3,
      "ProviderSettings" = '{
        "defaultIncludeAnswer": true,
        "defaultSearchDepth": "advanced",
        "defaultMaxResults": 10
      }'::jsonb,
      "ParameterSchema" = '{
        "required": ["query"],
        "optional": {
          "searchDepth": {
            "type": "string",
            "enum": ["basic", "advanced"],
            "default": "advanced",
            "description": "Search depth - advanced recommended for comprehensive answers"
          },
          "topic": {
            "type": "string",
            "enum": ["general", "news", "finance"],
            "default": "general",
            "description": "Search topic category for context gathering"
          },
          "maxResults": {
            "type": "number",
            "min": 1,
            "max": 20,
            "default": 10,
            "description": "Number of sources to use for answer generation"
          },
          "includeAnswer": {
            "type": "boolean",
            "default": true,
            "description": "Generate AI answer (must be true for this configuration)"
          },
          "includeRawContent": {
            "type": "boolean",
            "default": false,
            "description": "Include full source content with answer"
          },
          "includeImages": {
            "type": "boolean",
            "default": true,
            "description": "Include relevant images with answer"
          },
          "days": {
            "type": "number",
            "min": 1,
            "description": "Limit sources to content from past N days"
          },
          "includeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "Prioritize specific domains for answer sources"
          },
          "excludeDomains": {
            "type": "array",
            "items": { "type": "string" },
            "description": "Exclude specific domains from answer sources"
          }
        }
      }'::jsonb,
      "Description" = 'LLM-powered answer generation from search results with citations and source grounding. Combines advanced search with AI synthesis to provide comprehensive, cited answers to complex questions.',
      "UpdatedAt" = NOW()
    WHERE "Id" = v_config_id;
  END IF;

END $$;

-- Cost Configuration: Tavily Answer Generation - Advanced Depth (2 credits)
-- Pricing Model: FlatRate (1)

DO $$
DECLARE
  v_cost_id INTEGER;
  v_function_config_id INTEGER;
BEGIN

  -- Get function configuration ID
  SELECT "Id" INTO v_function_config_id
  FROM "FunctionConfigurations"
  WHERE "ConfigurationName" = 'Tavily Answer Generation'
    AND "ProviderType" = 4;

  -- Check if cost configuration exists
  SELECT "Id" INTO v_cost_id
  FROM "FunctionCosts"
  WHERE "CostName" = 'Tavily Answer Generation - Advanced Depth (2 credits)';

  IF v_cost_id IS NULL THEN
    -- Insert new cost configuration
    INSERT INTO "FunctionCosts" (
      "CostName", "PricingModel", "CostPerExecution", "CostPerResult",
      "CostPerToken", "CostPerMinute", "TieredPricing", "PricingConfiguration",
      "IsActive", "EffectiveDate", "Priority", "CreatedAt", "UpdatedAt"
    ) VALUES (
      'Tavily Answer Generation - Advanced Depth (2 credits)', 1,
      0.01500000, NULL, NULL, NULL,
      NULL::jsonb, NULL::jsonb,
      true, NOW(), 1,
      NOW(), NOW()
    )
    RETURNING "Id" INTO v_cost_id;
  ELSE
    -- Update existing cost configuration
    UPDATE "FunctionCosts" SET
      "PricingModel" = 1,
      "CostPerExecution" = 0.01500000,
      "CostPerResult" = NULL,
      "CostPerToken" = NULL,
      "CostPerMinute" = NULL,
      "TieredPricing" = NULL::jsonb,
      "PricingConfiguration" = NULL::jsonb,
      "IsActive" = true,
      "Priority" = 1,
      "UpdatedAt" = NOW()
    WHERE "Id" = v_cost_id;
  END IF;

  -- Create/update function cost mapping
  IF NOT EXISTS (
    SELECT 1 FROM "FunctionCostMappings"
    WHERE "FunctionConfigurationId" = v_function_config_id
      AND "FunctionCostId" = v_cost_id
  ) THEN
    -- Deactivate any existing mappings for this configuration
    UPDATE "FunctionCostMappings"
    SET "IsActive" = false
    WHERE "FunctionConfigurationId" = v_function_config_id;

    -- Insert new mapping
    INSERT INTO "FunctionCostMappings" (
      "FunctionConfigurationId", "FunctionCostId", "IsActive", "CreatedAt"
    ) VALUES (
      v_function_config_id, v_cost_id, true, NOW()
    );
  END IF;

END $$;

-- Configuration: Perplexity Sonar Search
-- Provider: Perplexity (Type: 2)
-- Purpose: Answer (2)

DO $$
DECLARE
  v_config_id INTEGER;
BEGIN

  -- Check if configuration exists
  SELECT "Id" INTO v_config_id
  FROM "FunctionConfigurations"
  WHERE "ConfigurationName" = 'Perplexity Sonar Search'
    AND "ProviderType" = 2;

  IF v_config_id IS NULL THEN
    -- Insert new configuration
    INSERT INTO "FunctionConfigurations" (
      "ProviderType", "ConfigurationName", "Purpose", "DefaultExecutionMode",
      "IsEnabled", "BaseUrl", "TimeoutSeconds", "MaxRetries",
      "ProviderSettings", "ParameterSchema", "Description",
      "CreatedAt", "UpdatedAt"
    ) VALUES (
      2, 'Perplexity Sonar Search', 2, 1,
      true, NULL, 30, 3,
      '{
        "defaultModel": "sonar",
        "defaultReturnCitations": true,
        "defaultMaxTokens": 4096,
        "defaultTemperature": 0.2
      }'::jsonb, '{
        "required": ["messages"],
        "optional": {
          "model": {
            "type": "string",
            "enum": ["sonar", "sonar-pro", "sonar-reasoning"],
            "default": "sonar",
            "description": "Sonar model variant - standard for fast results"
          },
          "maxTokens": {
            "type": "number",
            "min": 1,
            "max": 4096,
            "default": 4096,
            "description": "Maximum tokens in response"
          },
          "temperature": {
            "type": "number",
            "min": 0,
            "max": 2,
            "default": 0.2,
            "description": "Response creativity (0=factual, 2=creative)"
          },
          "topP": {
            "type": "number",
            "min": 0,
            "max": 1,
            "default": 0.9,
            "description": "Nucleus sampling threshold"
          },
          "returnCitations": {
            "type": "boolean",
            "default": true,
            "description": "Include source citations in response"
          },
          "returnImages": {
            "type": "boolean",
            "default": false,
            "description": "Include relevant images in response"
          },
          "searchDomainFilter": {
            "type": "array",
            "items": { "type": "string" },
            "description": "Limit search to specific domains"
          },
          "searchRecencyFilter": {
            "type": "string",
            "enum": ["hour", "day", "week", "month", "year"],
            "description": "Limit search to recent content"
          }
        }
      }'::jsonb, 'Fast online search with LLM-generated answers and automatic citations using Sonar models. Provides real-time web knowledge with source attribution for general queries.',
      NOW(), NOW()
    );
  ELSE
    -- Update existing configuration
    UPDATE "FunctionConfigurations" SET
      "Purpose" = 2,
      "DefaultExecutionMode" = 1,
      "IsEnabled" = true,
      "BaseUrl" = NULL,
      "TimeoutSeconds" = 30,
      "MaxRetries" = 3,
      "ProviderSettings" = '{
        "defaultModel": "sonar",
        "defaultReturnCitations": true,
        "defaultMaxTokens": 4096,
        "defaultTemperature": 0.2
      }'::jsonb,
      "ParameterSchema" = '{
        "required": ["messages"],
        "optional": {
          "model": {
            "type": "string",
            "enum": ["sonar", "sonar-pro", "sonar-reasoning"],
            "default": "sonar",
            "description": "Sonar model variant - standard for fast results"
          },
          "maxTokens": {
            "type": "number",
            "min": 1,
            "max": 4096,
            "default": 4096,
            "description": "Maximum tokens in response"
          },
          "temperature": {
            "type": "number",
            "min": 0,
            "max": 2,
            "default": 0.2,
            "description": "Response creativity (0=factual, 2=creative)"
          },
          "topP": {
            "type": "number",
            "min": 0,
            "max": 1,
            "default": 0.9,
            "description": "Nucleus sampling threshold"
          },
          "returnCitations": {
            "type": "boolean",
            "default": true,
            "description": "Include source citations in response"
          },
          "returnImages": {
            "type": "boolean",
            "default": false,
            "description": "Include relevant images in response"
          },
          "searchDomainFilter": {
            "type": "array",
            "items": { "type": "string" },
            "description": "Limit search to specific domains"
          },
          "searchRecencyFilter": {
            "type": "string",
            "enum": ["hour", "day", "week", "month", "year"],
            "description": "Limit search to recent content"
          }
        }
      }'::jsonb,
      "Description" = 'Fast online search with LLM-generated answers and automatic citations using Sonar models. Provides real-time web knowledge with source attribution for general queries.',
      "UpdatedAt" = NOW()
    WHERE "Id" = v_config_id;
  END IF;

END $$;

-- Cost Configuration: Perplexity Sonar - Hybrid Pricing
-- Pricing Model: Hybrid (6)

DO $$
DECLARE
  v_cost_id INTEGER;
  v_function_config_id INTEGER;
BEGIN

  -- Get function configuration ID
  SELECT "Id" INTO v_function_config_id
  FROM "FunctionConfigurations"
  WHERE "ConfigurationName" = 'Perplexity Sonar Search'
    AND "ProviderType" = 2;

  -- Check if cost configuration exists
  SELECT "Id" INTO v_cost_id
  FROM "FunctionCosts"
  WHERE "CostName" = 'Perplexity Sonar - Hybrid Pricing';

  IF v_cost_id IS NULL THEN
    -- Insert new cost configuration
    INSERT INTO "FunctionCosts" (
      "CostName", "PricingModel", "CostPerExecution", "CostPerResult",
      "CostPerToken", "CostPerMinute", "TieredPricing", "PricingConfiguration",
      "IsActive", "EffectiveDate", "Priority", "CreatedAt", "UpdatedAt"
    ) VALUES (
      'Perplexity Sonar - Hybrid Pricing', 6,
      NULL, NULL, NULL, NULL,
      NULL::jsonb, '{
          "baseRequestCost": 0.005,
          "inputTokenCostPerMillion": 1.33,
          "outputTokenCostPerMillion": 1.33,
          "description": "$0.005/search + $1.33/M input tokens + $1.33/M output tokens"
        }'::jsonb,
      true, NOW(), 1,
      NOW(), NOW()
    )
    RETURNING "Id" INTO v_cost_id;
  ELSE
    -- Update existing cost configuration
    UPDATE "FunctionCosts" SET
      "PricingModel" = 6,
      "CostPerExecution" = NULL,
      "CostPerResult" = NULL,
      "CostPerToken" = NULL,
      "CostPerMinute" = NULL,
      "TieredPricing" = NULL::jsonb,
      "PricingConfiguration" = '{
          "baseRequestCost": 0.005,
          "inputTokenCostPerMillion": 1.33,
          "outputTokenCostPerMillion": 1.33,
          "description": "$0.005/search + $1.33/M input tokens + $1.33/M output tokens"
        }'::jsonb,
      "IsActive" = true,
      "Priority" = 1,
      "UpdatedAt" = NOW()
    WHERE "Id" = v_cost_id;
  END IF;

  -- Create/update function cost mapping
  IF NOT EXISTS (
    SELECT 1 FROM "FunctionCostMappings"
    WHERE "FunctionConfigurationId" = v_function_config_id
      AND "FunctionCostId" = v_cost_id
  ) THEN
    -- Deactivate any existing mappings for this configuration
    UPDATE "FunctionCostMappings"
    SET "IsActive" = false
    WHERE "FunctionConfigurationId" = v_function_config_id;

    -- Insert new mapping
    INSERT INTO "FunctionCostMappings" (
      "FunctionConfigurationId", "FunctionCostId", "IsActive", "CreatedAt"
    ) VALUES (
      v_function_config_id, v_cost_id, true, NOW()
    );
  END IF;

END $$;

-- Configuration: Perplexity Sonar Pro Search
-- Provider: Perplexity (Type: 2)
-- Purpose: Answer (2)

DO $$
DECLARE
  v_config_id INTEGER;
BEGIN

  -- Check if configuration exists
  SELECT "Id" INTO v_config_id
  FROM "FunctionConfigurations"
  WHERE "ConfigurationName" = 'Perplexity Sonar Pro Search'
    AND "ProviderType" = 2;

  IF v_config_id IS NULL THEN
    -- Insert new configuration
    INSERT INTO "FunctionConfigurations" (
      "ProviderType", "ConfigurationName", "Purpose", "DefaultExecutionMode",
      "IsEnabled", "BaseUrl", "TimeoutSeconds", "MaxRetries",
      "ProviderSettings", "ParameterSchema", "Description",
      "CreatedAt", "UpdatedAt"
    ) VALUES (
      2, 'Perplexity Sonar Pro Search', 2, 1,
      true, NULL, 45, 3,
      '{
        "defaultModel": "sonar-pro",
        "defaultReturnCitations": true,
        "defaultReturnImages": true,
        "defaultMaxTokens": 8192,
        "defaultTemperature": 0.2
      }'::jsonb, '{
        "required": ["messages"],
        "optional": {
          "model": {
            "type": "string",
            "enum": ["sonar", "sonar-pro", "sonar-reasoning"],
            "default": "sonar-pro",
            "description": "Sonar model variant - pro for comprehensive results"
          },
          "maxTokens": {
            "type": "number",
            "min": 1,
            "max": 8192,
            "default": 8192,
            "description": "Maximum tokens in response"
          },
          "temperature": {
            "type": "number",
            "min": 0,
            "max": 2,
            "default": 0.2,
            "description": "Response creativity (0=factual, 2=creative)"
          },
          "topP": {
            "type": "number",
            "min": 0,
            "max": 1,
            "default": 0.9,
            "description": "Nucleus sampling threshold"
          },
          "returnCitations": {
            "type": "boolean",
            "default": true,
            "description": "Include detailed source citations"
          },
          "returnImages": {
            "type": "boolean",
            "default": true,
            "description": "Include relevant images and media"
          },
          "searchDomainFilter": {
            "type": "array",
            "items": { "type": "string" },
            "description": "Limit search to specific domains"
          },
          "searchRecencyFilter": {
            "type": "string",
            "enum": ["hour", "day", "week", "month", "year"],
            "description": "Limit search to recent content"
          }
        }
      }'::jsonb, 'Advanced search with deeper reasoning and comprehensive answers using Sonar Pro models. Provides more thorough analysis with enhanced citation quality and image support for complex queries.',
      NOW(), NOW()
    );
  ELSE
    -- Update existing configuration
    UPDATE "FunctionConfigurations" SET
      "Purpose" = 2,
      "DefaultExecutionMode" = 1,
      "IsEnabled" = true,
      "BaseUrl" = NULL,
      "TimeoutSeconds" = 45,
      "MaxRetries" = 3,
      "ProviderSettings" = '{
        "defaultModel": "sonar-pro",
        "defaultReturnCitations": true,
        "defaultReturnImages": true,
        "defaultMaxTokens": 8192,
        "defaultTemperature": 0.2
      }'::jsonb,
      "ParameterSchema" = '{
        "required": ["messages"],
        "optional": {
          "model": {
            "type": "string",
            "enum": ["sonar", "sonar-pro", "sonar-reasoning"],
            "default": "sonar-pro",
            "description": "Sonar model variant - pro for comprehensive results"
          },
          "maxTokens": {
            "type": "number",
            "min": 1,
            "max": 8192,
            "default": 8192,
            "description": "Maximum tokens in response"
          },
          "temperature": {
            "type": "number",
            "min": 0,
            "max": 2,
            "default": 0.2,
            "description": "Response creativity (0=factual, 2=creative)"
          },
          "topP": {
            "type": "number",
            "min": 0,
            "max": 1,
            "default": 0.9,
            "description": "Nucleus sampling threshold"
          },
          "returnCitations": {
            "type": "boolean",
            "default": true,
            "description": "Include detailed source citations"
          },
          "returnImages": {
            "type": "boolean",
            "default": true,
            "description": "Include relevant images and media"
          },
          "searchDomainFilter": {
            "type": "array",
            "items": { "type": "string" },
            "description": "Limit search to specific domains"
          },
          "searchRecencyFilter": {
            "type": "string",
            "enum": ["hour", "day", "week", "month", "year"],
            "description": "Limit search to recent content"
          }
        }
      }'::jsonb,
      "Description" = 'Advanced search with deeper reasoning and comprehensive answers using Sonar Pro models. Provides more thorough analysis with enhanced citation quality and image support for complex queries.',
      "UpdatedAt" = NOW()
    WHERE "Id" = v_config_id;
  END IF;

END $$;

-- Cost Configuration: Perplexity Sonar Pro - Hybrid Pricing
-- Pricing Model: Hybrid (6)

DO $$
DECLARE
  v_cost_id INTEGER;
  v_function_config_id INTEGER;
BEGIN

  -- Get function configuration ID
  SELECT "Id" INTO v_function_config_id
  FROM "FunctionConfigurations"
  WHERE "ConfigurationName" = 'Perplexity Sonar Pro Search'
    AND "ProviderType" = 2;

  -- Check if cost configuration exists
  SELECT "Id" INTO v_cost_id
  FROM "FunctionCosts"
  WHERE "CostName" = 'Perplexity Sonar Pro - Hybrid Pricing';

  IF v_cost_id IS NULL THEN
    -- Insert new cost configuration
    INSERT INTO "FunctionCosts" (
      "CostName", "PricingModel", "CostPerExecution", "CostPerResult",
      "CostPerToken", "CostPerMinute", "TieredPricing", "PricingConfiguration",
      "IsActive", "EffectiveDate", "Priority", "CreatedAt", "UpdatedAt"
    ) VALUES (
      'Perplexity Sonar Pro - Hybrid Pricing', 6,
      NULL, NULL, NULL, NULL,
      NULL::jsonb, '{
          "baseRequestCost": 0.005,
          "inputTokenCostPerMillion": 4.0,
          "outputTokenCostPerMillion": 20.0,
          "description": "$0.005/search + $4/M input tokens + $20/M output tokens"
        }'::jsonb,
      true, NOW(), 1,
      NOW(), NOW()
    )
    RETURNING "Id" INTO v_cost_id;
  ELSE
    -- Update existing cost configuration
    UPDATE "FunctionCosts" SET
      "PricingModel" = 6,
      "CostPerExecution" = NULL,
      "CostPerResult" = NULL,
      "CostPerToken" = NULL,
      "CostPerMinute" = NULL,
      "TieredPricing" = NULL::jsonb,
      "PricingConfiguration" = '{
          "baseRequestCost": 0.005,
          "inputTokenCostPerMillion": 4.0,
          "outputTokenCostPerMillion": 20.0,
          "description": "$0.005/search + $4/M input tokens + $20/M output tokens"
        }'::jsonb,
      "IsActive" = true,
      "Priority" = 1,
      "UpdatedAt" = NOW()
    WHERE "Id" = v_cost_id;
  END IF;

  -- Create/update function cost mapping
  IF NOT EXISTS (
    SELECT 1 FROM "FunctionCostMappings"
    WHERE "FunctionConfigurationId" = v_function_config_id
      AND "FunctionCostId" = v_cost_id
  ) THEN
    -- Deactivate any existing mappings for this configuration
    UPDATE "FunctionCostMappings"
    SET "IsActive" = false
    WHERE "FunctionConfigurationId" = v_function_config_id;

    -- Insert new mapping
    INSERT INTO "FunctionCostMappings" (
      "FunctionConfigurationId", "FunctionCostId", "IsActive", "CreatedAt"
    ) VALUES (
      v_function_config_id, v_cost_id, true, NOW()
    );
  END IF;

END $$;

-- Configuration: Perplexity Sonar Reasoning
-- Provider: Perplexity (Type: 2)
-- Purpose: Answer (2)

DO $$
DECLARE
  v_config_id INTEGER;
BEGIN

  -- Check if configuration exists
  SELECT "Id" INTO v_config_id
  FROM "FunctionConfigurations"
  WHERE "ConfigurationName" = 'Perplexity Sonar Reasoning'
    AND "ProviderType" = 2;

  IF v_config_id IS NULL THEN
    -- Insert new configuration
    INSERT INTO "FunctionConfigurations" (
      "ProviderType", "ConfigurationName", "Purpose", "DefaultExecutionMode",
      "IsEnabled", "BaseUrl", "TimeoutSeconds", "MaxRetries",
      "ProviderSettings", "ParameterSchema", "Description",
      "CreatedAt", "UpdatedAt"
    ) VALUES (
      2, 'Perplexity Sonar Reasoning', 2, 1,
      true, NULL, 60, 3,
      '{
        "defaultModel": "sonar-reasoning",
        "defaultReturnCitations": true,
        "defaultMaxTokens": 8192,
        "defaultTemperature": 0.2
      }'::jsonb, '{
        "required": ["messages"],
        "optional": {
          "model": {
            "type": "string",
            "enum": ["sonar", "sonar-pro", "sonar-reasoning"],
            "default": "sonar-reasoning",
            "description": "Sonar model variant - reasoning for complex analysis"
          },
          "maxTokens": {
            "type": "number",
            "min": 1,
            "max": 8192,
            "default": 8192,
            "description": "Maximum tokens in response"
          },
          "temperature": {
            "type": "number",
            "min": 0,
            "max": 2,
            "default": 0.2,
            "description": "Response creativity (0=factual, 2=creative)"
          },
          "topP": {
            "type": "number",
            "min": 0,
            "max": 1,
            "default": 0.9,
            "description": "Nucleus sampling threshold"
          },
          "returnCitations": {
            "type": "boolean",
            "default": true,
            "description": "Include source citations with reasoning steps"
          },
          "returnImages": {
            "type": "boolean",
            "default": false,
            "description": "Include relevant images in response"
          },
          "searchDomainFilter": {
            "type": "array",
            "items": { "type": "string" },
            "description": "Limit search to specific domains"
          },
          "searchRecencyFilter": {
            "type": "string",
            "enum": ["hour", "day", "week", "month", "year"],
            "description": "Limit search to recent content"
          }
        }
      }'::jsonb, 'Complex reasoning over search results for analytical queries requiring deep understanding. Uses advanced chain-of-thought processing for multi-step problems and detailed analysis.',
      NOW(), NOW()
    );
  ELSE
    -- Update existing configuration
    UPDATE "FunctionConfigurations" SET
      "Purpose" = 2,
      "DefaultExecutionMode" = 1,
      "IsEnabled" = true,
      "BaseUrl" = NULL,
      "TimeoutSeconds" = 60,
      "MaxRetries" = 3,
      "ProviderSettings" = '{
        "defaultModel": "sonar-reasoning",
        "defaultReturnCitations": true,
        "defaultMaxTokens": 8192,
        "defaultTemperature": 0.2
      }'::jsonb,
      "ParameterSchema" = '{
        "required": ["messages"],
        "optional": {
          "model": {
            "type": "string",
            "enum": ["sonar", "sonar-pro", "sonar-reasoning"],
            "default": "sonar-reasoning",
            "description": "Sonar model variant - reasoning for complex analysis"
          },
          "maxTokens": {
            "type": "number",
            "min": 1,
            "max": 8192,
            "default": 8192,
            "description": "Maximum tokens in response"
          },
          "temperature": {
            "type": "number",
            "min": 0,
            "max": 2,
            "default": 0.2,
            "description": "Response creativity (0=factual, 2=creative)"
          },
          "topP": {
            "type": "number",
            "min": 0,
            "max": 1,
            "default": 0.9,
            "description": "Nucleus sampling threshold"
          },
          "returnCitations": {
            "type": "boolean",
            "default": true,
            "description": "Include source citations with reasoning steps"
          },
          "returnImages": {
            "type": "boolean",
            "default": false,
            "description": "Include relevant images in response"
          },
          "searchDomainFilter": {
            "type": "array",
            "items": { "type": "string" },
            "description": "Limit search to specific domains"
          },
          "searchRecencyFilter": {
            "type": "string",
            "enum": ["hour", "day", "week", "month", "year"],
            "description": "Limit search to recent content"
          }
        }
      }'::jsonb,
      "Description" = 'Complex reasoning over search results for analytical queries requiring deep understanding. Uses advanced chain-of-thought processing for multi-step problems and detailed analysis.',
      "UpdatedAt" = NOW()
    WHERE "Id" = v_config_id;
  END IF;

END $$;

-- Cost Configuration: Perplexity Sonar Reasoning - Hybrid Pricing
-- Pricing Model: Hybrid (6)

DO $$
DECLARE
  v_cost_id INTEGER;
  v_function_config_id INTEGER;
BEGIN

  -- Get function configuration ID
  SELECT "Id" INTO v_function_config_id
  FROM "FunctionConfigurations"
  WHERE "ConfigurationName" = 'Perplexity Sonar Reasoning'
    AND "ProviderType" = 2;

  -- Check if cost configuration exists
  SELECT "Id" INTO v_cost_id
  FROM "FunctionCosts"
  WHERE "CostName" = 'Perplexity Sonar Reasoning - Hybrid Pricing';

  IF v_cost_id IS NULL THEN
    -- Insert new cost configuration
    INSERT INTO "FunctionCosts" (
      "CostName", "PricingModel", "CostPerExecution", "CostPerResult",
      "CostPerToken", "CostPerMinute", "TieredPricing", "PricingConfiguration",
      "IsActive", "EffectiveDate", "Priority", "CreatedAt", "UpdatedAt"
    ) VALUES (
      'Perplexity Sonar Reasoning - Hybrid Pricing', 6,
      NULL, NULL, NULL, NULL,
      NULL::jsonb, '{
          "baseRequestCost": 0.018,
          "inputTokenCostPerMillion": 1.0,
          "outputTokenCostPerMillion": 5.0,
          "description": "$0.018/search + $1/M input tokens + $5/M output tokens"
        }'::jsonb,
      true, NOW(), 1,
      NOW(), NOW()
    )
    RETURNING "Id" INTO v_cost_id;
  ELSE
    -- Update existing cost configuration
    UPDATE "FunctionCosts" SET
      "PricingModel" = 6,
      "CostPerExecution" = NULL,
      "CostPerResult" = NULL,
      "CostPerToken" = NULL,
      "CostPerMinute" = NULL,
      "TieredPricing" = NULL::jsonb,
      "PricingConfiguration" = '{
          "baseRequestCost": 0.018,
          "inputTokenCostPerMillion": 1.0,
          "outputTokenCostPerMillion": 5.0,
          "description": "$0.018/search + $1/M input tokens + $5/M output tokens"
        }'::jsonb,
      "IsActive" = true,
      "Priority" = 1,
      "UpdatedAt" = NOW()
    WHERE "Id" = v_cost_id;
  END IF;

  -- Create/update function cost mapping
  IF NOT EXISTS (
    SELECT 1 FROM "FunctionCostMappings"
    WHERE "FunctionConfigurationId" = v_function_config_id
      AND "FunctionCostId" = v_cost_id
  ) THEN
    -- Deactivate any existing mappings for this configuration
    UPDATE "FunctionCostMappings"
    SET "IsActive" = false
    WHERE "FunctionConfigurationId" = v_function_config_id;

    -- Insert new mapping
    INSERT INTO "FunctionCostMappings" (
      "FunctionConfigurationId", "FunctionCostId", "IsActive", "CreatedAt"
    ) VALUES (
      v_function_config_id, v_cost_id, true, NOW()
    );
  END IF;

END $$;

COMMIT;

-- Successfully generated SQL for 10 function configurations
-- Created 10 cost configurations and mappings
