import { CreateModelCostDto } from '../types/modelCost';

export interface ParsedModelCost {
  costName: string;
  modelAliases: string[];
  modelType: string;
  inputCostPerMillion: number;
  outputCostPerMillion: number;
  cachedInputCostPerMillion?: number;
  cachedInputWriteCostPerMillion?: number;
  embeddingCostPerMillion?: number;
  imageCostPerImage?: number;
  audioCostPerMinute?: number;
  audioCostPerKCharacters?: number;
  audioInputCostPerMinute?: number;
  audioOutputCostPerMinute?: number;
  videoCostPerSecond?: number;
  videoResolutionMultipliers?: string;
  batchProcessingMultiplier?: number;
  supportsBatchProcessing: boolean;
  imageQualityMultipliers?: string;
  searchUnitCostPer1K?: number;
  costPerInferenceStep?: number;
  defaultInferenceSteps?: number;
  priority: number;
  active: boolean;
  description?: string;
  // Validation
  isValid: boolean;
  errors: string[];
  // Tracking
  rowNumber: number;
  isSkipped?: boolean;
  skipReason?: string;
}

export const parseCSVLine = (line: string): string[] => {
  const result: string[] = [];
  let current = '';
  let inQuotes = false;
  
  for (let i = 0; i < line.length; i++) {
    const char = line[i];
    const nextChar = line[i + 1];
    
    if (char === '"' && nextChar === '"' && inQuotes) {
      current += '"';
      i++; // Skip next quote
    } else if (char === '"') {
      inQuotes = !inQuotes;
    } else if (char === ',' && !inQuotes) {
      result.push(current.trim());
      current = '';
    } else {
      current += char;
    }
  }
  
  result.push(current.trim());
  return result;
};

export const parseCSVContent = (text: string): ParsedModelCost[] => {
  const lines = text.split('\n').filter(line => line.trim());
  
  if (lines.length < 2) {
    throw new Error('CSV file must contain headers and at least one data row');
  }

  // Parse headers
  const headers = parseCSVLine(lines[0]).map(h => h.trim().toLowerCase());
  const requiredHeaders = ['cost name', 'associated model aliases', 'model type'];
  
  for (const required of requiredHeaders) {
    if (!headers.some(h => h.includes(required))) {
      throw new Error(`Missing required column: ${required}`);
    }
  }

  // Parse data rows
  const parsed: ParsedModelCost[] = [];
  
  for (let i = 1; i < lines.length; i++) {
    const values = parseCSVLine(lines[i]);
    const rowNumber = i + 1;
    
    // Handle malformed rows
    if (values.length !== headers.length) {
      parsed.push({
        costName: '',
        modelAliases: [],
        modelType: '',
        inputCostPerMillion: 0,
        outputCostPerMillion: 0,
        priority: 0,
        active: false,
        supportsBatchProcessing: false,
        imageQualityMultipliers: undefined,
        isValid: false,
        errors: [`Row has ${values.length} columns but expected ${headers.length}`],
        rowNumber,
        isSkipped: true,
        skipReason: `Malformed row: expected ${headers.length} columns, got ${values.length}`,
      });
      continue;
    }

    const row: Record<string, string> = {};
    headers.forEach((header, index) => {
      row[header] = values[index];
    });

    // Helper function to parse numeric values with proper fallbacks
    const parseNumericValue = (value: string | undefined, defaultValue?: number): number | undefined => {
      if (!value || value.trim() === '') return defaultValue;
      const parsed = parseFloat(value);
      return isNaN(parsed) ? defaultValue : parsed;
    };

    const cost: ParsedModelCost = {
      costName: row['cost name']?.trim() ?? '',
      modelAliases: row['associated model aliases']?.split(',').map(a => a.trim()).filter(a => a) ?? [],
      modelType: row['model type']?.trim().toLowerCase() ?? 'chat',
      inputCostPerMillion: parseNumericValue(row['input cost (per million tokens)'], 0) ?? 0,
      outputCostPerMillion: parseNumericValue(row['output cost (per million tokens)'], 0) ?? 0,
      cachedInputCostPerMillion: parseNumericValue(row['cached input cost (per million tokens)']),
      cachedInputWriteCostPerMillion: parseNumericValue(row['cache write cost (per million tokens)']),
      embeddingCostPerMillion: parseNumericValue(row['embedding cost (per million tokens)']),
      imageCostPerImage: parseNumericValue(row['image cost (per image)']),
      audioCostPerMinute: parseNumericValue(row['audio cost (per minute)']),
      audioCostPerKCharacters: parseNumericValue(row['audio cost (per 1k characters)']),
      audioInputCostPerMinute: parseNumericValue(row['audio input cost (per minute)']),
      audioOutputCostPerMinute: parseNumericValue(row['audio output cost (per minute)']),
      videoCostPerSecond: parseNumericValue(row['video cost (per second)']),
      videoResolutionMultipliers: row['video resolution multipliers']?.trim(),
      batchProcessingMultiplier: parseNumericValue(row['batch processing multiplier']),
      supportsBatchProcessing: row['supports batch processing']?.toLowerCase() === 'yes' || row['supports batch processing']?.toLowerCase() === 'true',
      imageQualityMultipliers: row['image quality multipliers']?.trim(),
      searchUnitCostPer1K: parseNumericValue(row['search unit cost (per 1k units)']),
      costPerInferenceStep: parseNumericValue(row['cost per inference step']),
      defaultInferenceSteps: parseNumericValue(row['default inference steps']),
      priority: parseNumericValue(row['priority'], 0) ?? 0,
      active: row['active']?.toLowerCase() === 'yes' || row['active']?.toLowerCase() === 'true',
      description: row['description']?.trim(),
      isValid: true,
      errors: [],
      rowNumber,
    };

    // Validate row
    const errors: string[] = [];
    if (!cost.costName) errors.push('Cost name is required');
    if (cost.modelAliases.length === 0) errors.push('At least one model alias is required');
    if (!['chat', 'embedding', 'image', 'audio', 'video'].includes(cost.modelType)) {
      errors.push(`Invalid model type: ${cost.modelType}. Must be one of: chat, embedding, image, audio, video`);
    }
    if (cost.priority < 0) errors.push('Priority must be non-negative');
    
    // Cost validation
    if (cost.inputCostPerMillion < 0) errors.push('Input cost cannot be negative');
    if (cost.outputCostPerMillion < 0) errors.push('Output cost cannot be negative');
    if (cost.cachedInputCostPerMillion !== undefined && cost.cachedInputCostPerMillion < 0) errors.push('Cached input cost cannot be negative');
    if (cost.cachedInputWriteCostPerMillion !== undefined && cost.cachedInputWriteCostPerMillion < 0) errors.push('Cache write cost cannot be negative');
    if (cost.embeddingCostPerMillion !== undefined && cost.embeddingCostPerMillion < 0) errors.push('Embedding cost cannot be negative');
    if (cost.imageCostPerImage !== undefined && cost.imageCostPerImage < 0) errors.push('Image cost cannot be negative');
    if (cost.audioCostPerMinute !== undefined && cost.audioCostPerMinute < 0) errors.push('Audio cost per minute cannot be negative');
    if (cost.audioCostPerKCharacters !== undefined && cost.audioCostPerKCharacters < 0) errors.push('Audio cost per 1k characters cannot be negative');
    if (cost.audioInputCostPerMinute !== undefined && cost.audioInputCostPerMinute < 0) errors.push('Audio input cost per minute cannot be negative');
    if (cost.audioOutputCostPerMinute !== undefined && cost.audioOutputCostPerMinute < 0) errors.push('Audio output cost per minute cannot be negative');
    if (cost.videoCostPerSecond !== undefined && cost.videoCostPerSecond < 0) errors.push('Video cost cannot be negative');
    if (cost.batchProcessingMultiplier !== undefined && cost.batchProcessingMultiplier < 0) errors.push('Batch processing multiplier cannot be negative');
    if (cost.batchProcessingMultiplier !== undefined && cost.batchProcessingMultiplier > 1) errors.push('Batch processing multiplier cannot be greater than 1 (>100% cost)');
    if (cost.searchUnitCostPer1K !== undefined && cost.searchUnitCostPer1K < 0) errors.push('Search unit cost cannot be negative');
    if (cost.costPerInferenceStep !== undefined && cost.costPerInferenceStep < 0) errors.push('Cost per inference step cannot be negative');
    if (cost.defaultInferenceSteps !== undefined && cost.defaultInferenceSteps < 0) errors.push('Default inference steps cannot be negative');
    
    // Validate image quality multipliers JSON
    if (cost.imageQualityMultipliers) {
      try {
        const multipliers = JSON.parse(cost.imageQualityMultipliers) as unknown;
        if (typeof multipliers !== 'object' || Array.isArray(multipliers)) {
          errors.push('Image quality multipliers must be a JSON object');
        } else {
          // Validate each multiplier value
          const multipliersObj = multipliers as Record<string, unknown>;
          for (const [key, value] of Object.entries(multipliersObj)) {
            if (typeof value !== 'number' || value < 0) {
              errors.push(`Image quality multiplier for "${key}" must be a positive number`);
            } else if (value > 10) {
              errors.push(`Image quality multiplier for "${key}" seems unreasonably high (>10x)`);
            }
          }
        }
      } catch {
        errors.push('Image quality multipliers must be valid JSON');
      }
    }

    // Validate video resolution multipliers JSON
    if (cost.videoResolutionMultipliers) {
      try {
        const multipliers = JSON.parse(cost.videoResolutionMultipliers) as unknown;
        if (typeof multipliers !== 'object' || Array.isArray(multipliers)) {
          errors.push('Video resolution multipliers must be a JSON object');
        } else {
          // Validate each multiplier value
          const multipliersObj = multipliers as Record<string, unknown>;
          for (const [key, value] of Object.entries(multipliersObj)) {
            if (typeof value !== 'number' || value < 0) {
              errors.push(`Video resolution multiplier for "${key}" must be a positive number`);
            } else if (value > 10) {
              errors.push(`Video resolution multiplier for "${key}" seems unreasonably high (>10x)`);
            }
          }
        }
      } catch {
        errors.push('Video resolution multipliers must be valid JSON');
      }
    }
    
    // Reasonable upper bounds validation
    if (cost.inputCostPerMillion > 1000000) errors.push('Input cost seems unreasonably high (>$1,000,000 per million tokens)');
    if (cost.outputCostPerMillion > 1000000) errors.push('Output cost seems unreasonably high (>$1,000,000 per million tokens)');

    cost.isValid = errors.length === 0;
    cost.errors = errors;

    parsed.push(cost);
  }

  // Check for duplicates
  const seenCostNames = new Set<string>();
  parsed.forEach(cost => {
    if (cost.isValid && seenCostNames.has(cost.costName)) {
      cost.isValid = false;
      cost.errors.push(`Duplicate cost name: ${cost.costName}`);
    } else if (cost.isValid) {
      seenCostNames.add(cost.costName);
    }
  });

  return parsed;
};

export const convertParsedToDto = (parsedData: ParsedModelCost[]): CreateModelCostDto[] => {
  return parsedData
    .filter(d => d.isValid)
    .map(cost => ({
      costName: cost.costName,
      modelProviderMappingIds: [], // Will be resolved during import
      modelType: cost.modelType as 'chat' | 'embedding' | 'image' | 'audio' | 'video',
      inputCostPerMillionTokens: cost.inputCostPerMillion, // Already per million
      outputCostPerMillionTokens: cost.outputCostPerMillion,
      cachedInputCostPerMillionTokens: cost.cachedInputCostPerMillion,
      cachedInputWriteCostPerMillionTokens: cost.cachedInputWriteCostPerMillion,
      embeddingCostPerMillionTokens: cost.embeddingCostPerMillion,
      imageCostPerImage: cost.imageCostPerImage,
      audioCostPerMinute: cost.audioCostPerMinute,
      videoCostPerSecond: cost.videoCostPerSecond,
      batchProcessingMultiplier: cost.batchProcessingMultiplier,
      supportsBatchProcessing: cost.supportsBatchProcessing,
      imageQualityMultipliers: cost.imageQualityMultipliers,
      costPerSearchUnit: cost.searchUnitCostPer1K,
      costPerInferenceStep: cost.costPerInferenceStep,
      defaultInferenceSteps: cost.defaultInferenceSteps,
      priority: cost.priority,
      description: cost.description,
    } as CreateModelCostDto));
};

export const downloadFile = (blob: Blob, filename: string) => {
  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  window.URL.revokeObjectURL(url);
  document.body.removeChild(a);
};