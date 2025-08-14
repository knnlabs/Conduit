import { parseCSVContent, parseCSVLine, convertParsedToDto } from './csvHelpers';

describe('Phase 2 CSV parsing', () => {
  it('should parse cached token costs', () => {
    const csv = `Cost Name,Associated Model Aliases,Model Type,Input Cost (per million tokens),Output Cost (per million tokens),Cached Input Cost (per million tokens),Cache Write Cost (per million tokens)
claude-opus-4,claude-3-opus,chat,15.00,75.00,1.50,18.75`;
    
    const result = parseCSVContent(csv);
    
    expect(result).toHaveLength(1);
    expect(result[0].cachedInputCostPerMillion).toBe(1.50);
    expect(result[0].cachedInputWriteCostPerMillion).toBe(18.75);
    expect(result[0].isValid).toBe(true);
  });
  
  it('should parse search unit costs', () => {
    const csv = `Cost Name,Associated Model Aliases,Model Type,Input Cost (per million tokens),Output Cost (per million tokens),Search Unit Cost (per 1K units)
rerank-3.5,rerank-3.5,chat,0,0,2.0`;
    
    const result = parseCSVContent(csv);
    
    expect(result).toHaveLength(1);
    expect(result[0].searchUnitCostPer1K).toBe(2.0);
    expect(result[0].isValid).toBe(true);
  });
  
  it('should parse inference step pricing', () => {
    const csv = `Cost Name,Associated Model Aliases,Model Type,Input Cost (per million tokens),Output Cost (per million tokens),Cost Per Inference Step,Default Inference Steps
stable-diffusion-xl,stable-diffusion-xl,image,0,0,0.00013,30`;
    
    const result = parseCSVContent(csv);
    
    expect(result).toHaveLength(1);
    expect(result[0].costPerInferenceStep).toBe(0.00013);
    expect(result[0].defaultInferenceSteps).toBe(30);
    expect(result[0].isValid).toBe(true);
  });

  it('should convert parsed Phase 2 data to DTOs correctly', () => {
    const csv = `Cost Name,Associated Model Aliases,Model Type,Input Cost (per million tokens),Output Cost (per million tokens),Cached Input Cost (per million tokens),Search Unit Cost (per 1K units),Priority,Active
claude-opus-4,claude-3-opus,chat,15.00,75.00,1.50,0,100,yes
rerank-3.5,rerank-3.5,chat,0,0,0,2.0,90,true`;
    
    const parsed = parseCSVContent(csv);
    const dtos = convertParsedToDto(parsed);
    
    expect(dtos).toHaveLength(2);
    
    // Check cached token costs (already per million)
    expect(dtos[0].cachedInputCostPerMillionTokens).toBe(1.50);
    expect(dtos[0].costPerSearchUnit).toBe(0); // Not used for this model
    
    // Check search unit cost (no conversion needed)
    expect(dtos[1].costPerSearchUnit).toBe(2.0);
    expect(dtos[1].cachedInputCostPerMillionTokens).toBeUndefined();
  });

  it('should validate negative Phase 2 costs', () => {
    const csv = `Model Pattern,Provider,Model Type,Input Cost (per 1K tokens),Output Cost (per 1K tokens),Cached Input Cost (per 1K tokens),Search Unit Cost (per 1K units)
bad-model,Test,chat,0.01,0.02,-0.001,-1.0`;
    
    const result = parseCSVContent(csv);
    
    expect(result[0].isValid).toBe(false);
    expect(result[0].errors).toContain('Cached input cost cannot be negative');
    expect(result[0].errors).toContain('Search unit cost cannot be negative');
  });

  it('should handle missing Phase 2 fields gracefully', () => {
    const csv = `Model Pattern,Provider,Model Type,Input Cost (per 1K tokens),Output Cost (per 1K tokens)
gpt-4,OpenAI,chat,0.03,0.06`;
    
    const result = parseCSVContent(csv);
    
    expect(result[0].isValid).toBe(true);
    expect(result[0].cachedInputCostPerMillion).toBeUndefined();
    expect(result[0].cachedInputWriteCostPerMillion).toBeUndefined();
    expect(result[0].searchUnitCostPer1K).toBeUndefined();
  });

  it('should parse complex CSV with all Phase 2 fields', () => {
    const csv = `Model Pattern,Provider,Model Type,Input Cost (per 1K tokens),Output Cost (per 1K tokens),Cached Input Cost (per 1K tokens),Cache Write Cost (per 1K tokens),Embedding Cost (per 1K tokens),Image Cost (per image),Search Unit Cost (per 1K units),Cost Per Inference Step,Default Inference Steps,Supports Batch Processing,Batch Processing Multiplier,Image Quality Multipliers,Priority,Active,Description
claude-opus-4,Anthropic,chat,0.015,0.075,0.0015,0.01875,0,0,0,0,0,yes,0.5,{},100,true,"Premium Claude model with caching"
embed-english-v3.0,Cohere,embedding,0,0,0,0,0.0001,0,0,0,0,no,0,{},80,yes,"Cohere embedding model"
stable-diffusion-xl,Fireworks,image,0,0,0,0,0,0,0,0.00013,30,false,0,"{""standard"": 1.0, ""hd"": 2.0}",70,true,"SDXL with step-based pricing"`;
    
    const result = parseCSVContent(csv);
    
    expect(result).toHaveLength(3);
    
    // Claude model with caching
    expect(result[0].cachedInputCostPerMillion).toBe(1.5); // 0.0015 * 1000 = 1.5 per million
    expect(result[0].cachedInputWriteCostPerMillion).toBe(18.75); // 0.01875 * 1000 = 18.75 per million
    expect(result[0].supportsBatchProcessing).toBe(true);
    expect(result[0].batchProcessingMultiplier).toBe(0.5);
    
    // Cohere embedding
    expect(result[1].embeddingCostPerMillion).toBe(0.1); // 0.0001 * 1000 = 0.1 per million
    expect(result[1].modelType).toBe('embedding');
    
    // SDXL with inference steps
    expect(result[2].costPerInferenceStep).toBe(0.00013);
    expect(result[2].defaultInferenceSteps).toBe(30);
    expect(result[2].imageQualityMultipliers).toBe('{"standard": 1.0, "hd": 2.0}');
    
    // All should be valid
    expect(result.every(r => r.isValid)).toBe(true);
  });

  describe('parseCSVLine', () => {
    it('should handle quoted fields with commas', () => {
      const line = 'model-1,Provider,"Description with, comma",0.01,0.02';
      const result = parseCSVLine(line);
      
      expect(result).toEqual(['model-1', 'Provider', 'Description with, comma', '0.01', '0.02']);
    });

    it('should handle escaped quotes', () => {
      const line = 'model-1,Provider,"Model with ""quotes"" in name",0.01,0.02';
      const result = parseCSVLine(line);
      
      expect(result).toEqual(['model-1', 'Provider', 'Model with "quotes" in name', '0.01', '0.02']);
    });
  });

  it('should validate inference step fields correctly', () => {
    const csv = `Model Pattern,Provider,Model Type,Input Cost (per 1K tokens),Output Cost (per 1K tokens),Cost Per Inference Step,Default Inference Steps
bad-steps,Test,image,0,0,-0.0001,-5`;
    
    const result = parseCSVContent(csv);
    
    expect(result[0].isValid).toBe(false);
    expect(result[0].errors).toContain('Cost per inference step cannot be negative');
    expect(result[0].errors).toContain('Default inference steps cannot be negative');
  });

  it('should handle batch processing with Phase 2 costs', () => {
    const csv = `Model Pattern,Provider,Model Type,Input Cost (per 1K tokens),Output Cost (per 1K tokens),Cached Input Cost (per 1K tokens),Supports Batch Processing,Batch Processing Multiplier
claude-batch,Anthropic,chat,0.015,0.075,0.0015,yes,0.5`;
    
    const result = parseCSVContent(csv);
    const dtos = convertParsedToDto(result);
    
    expect(dtos[0].supportsBatchProcessing).toBe(true);
    expect(dtos[0].batchProcessingMultiplier).toBe(0.5);
    expect(dtos[0].cachedInputCostPerMillionTokens).toBe(1500); // Cached costs work with batch
  });
});