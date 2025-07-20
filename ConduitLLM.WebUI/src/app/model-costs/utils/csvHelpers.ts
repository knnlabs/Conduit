import { CreateModelCostDto } from '../types/modelCost';

export interface ParsedModelCost {
  modelPattern: string;
  provider: string;
  modelType: string;
  inputCostPer1K: number;
  outputCostPer1K: number;
  cachedInputCostPer1K?: number;
  cachedInputWriteCostPer1K?: number;
  embeddingCostPer1K?: number;
  imageCostPerImage?: number;
  audioCostPerMinute?: number;
  videoCostPerSecond?: number;
  batchProcessingMultiplier?: number;
  supportsBatchProcessing: boolean;
  imageQualityMultipliers?: string;
  searchUnitCostPer1K?: number;
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
  const requiredHeaders = ['model pattern', 'provider', 'model type'];
  
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
        modelPattern: '',
        provider: '',
        modelType: '',
        inputCostPer1K: 0,
        outputCostPer1K: 0,
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
      modelPattern: row['model pattern']?.trim() ?? '',
      provider: row['provider']?.trim() ?? '',
      modelType: row['model type']?.trim().toLowerCase() ?? 'chat',
      inputCostPer1K: parseNumericValue(row['input cost (per 1k tokens)'], 0) ?? 0,
      outputCostPer1K: parseNumericValue(row['output cost (per 1k tokens)'], 0) ?? 0,
      cachedInputCostPer1K: parseNumericValue(row['cached input cost (per 1k tokens)']),
      cachedInputWriteCostPer1K: parseNumericValue(row['cache write cost (per 1k tokens)']),
      embeddingCostPer1K: parseNumericValue(row['embedding cost (per 1k tokens)']),
      imageCostPerImage: parseNumericValue(row['image cost (per image)']),
      audioCostPerMinute: parseNumericValue(row['audio cost (per minute)']),
      videoCostPerSecond: parseNumericValue(row['video cost (per second)']),
      batchProcessingMultiplier: parseNumericValue(row['batch processing multiplier']),
      supportsBatchProcessing: row['supports batch processing']?.toLowerCase() === 'yes' || row['supports batch processing']?.toLowerCase() === 'true',
      imageQualityMultipliers: row['image quality multipliers']?.trim(),
      searchUnitCostPer1K: parseNumericValue(row['search unit cost (per 1k units)']),
      priority: parseNumericValue(row['priority'], 0) ?? 0,
      active: row['active']?.toLowerCase() === 'yes' || row['active']?.toLowerCase() === 'true',
      description: row['description']?.trim(),
      isValid: true,
      errors: [],
      rowNumber,
    };

    // Validate row
    const errors: string[] = [];
    if (!cost.modelPattern) errors.push('Model pattern is required');
    if (!cost.provider) errors.push('Provider is required');
    if (!['chat', 'embedding', 'image', 'audio', 'video'].includes(cost.modelType)) {
      errors.push(`Invalid model type: ${cost.modelType}. Must be one of: chat, embedding, image, audio, video`);
    }
    if (cost.priority < 0) errors.push('Priority must be non-negative');
    
    // Cost validation
    if (cost.inputCostPer1K < 0) errors.push('Input cost cannot be negative');
    if (cost.outputCostPer1K < 0) errors.push('Output cost cannot be negative');
    if (cost.cachedInputCostPer1K !== undefined && cost.cachedInputCostPer1K < 0) errors.push('Cached input cost cannot be negative');
    if (cost.cachedInputWriteCostPer1K !== undefined && cost.cachedInputWriteCostPer1K < 0) errors.push('Cache write cost cannot be negative');
    if (cost.embeddingCostPer1K !== undefined && cost.embeddingCostPer1K < 0) errors.push('Embedding cost cannot be negative');
    if (cost.imageCostPerImage !== undefined && cost.imageCostPerImage < 0) errors.push('Image cost cannot be negative');
    if (cost.audioCostPerMinute !== undefined && cost.audioCostPerMinute < 0) errors.push('Audio cost cannot be negative');
    if (cost.videoCostPerSecond !== undefined && cost.videoCostPerSecond < 0) errors.push('Video cost cannot be negative');
    if (cost.batchProcessingMultiplier !== undefined && cost.batchProcessingMultiplier < 0) errors.push('Batch processing multiplier cannot be negative');
    if (cost.batchProcessingMultiplier !== undefined && cost.batchProcessingMultiplier > 1) errors.push('Batch processing multiplier cannot be greater than 1 (>100% cost)');
    if (cost.searchUnitCostPer1K !== undefined && cost.searchUnitCostPer1K < 0) errors.push('Search unit cost cannot be negative');
    
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
    
    // Reasonable upper bounds validation
    if (cost.inputCostPer1K > 1000) errors.push('Input cost seems unreasonably high (>$1000 per 1K tokens)');
    if (cost.outputCostPer1K > 1000) errors.push('Output cost seems unreasonably high (>$1000 per 1K tokens)');

    cost.isValid = errors.length === 0;
    cost.errors = errors;

    parsed.push(cost);
  }

  // Check for duplicates
  const seenPatterns = new Set<string>();
  parsed.forEach(cost => {
    if (cost.isValid && seenPatterns.has(cost.modelPattern)) {
      cost.isValid = false;
      cost.errors.push(`Duplicate model pattern: ${cost.modelPattern}`);
    } else if (cost.isValid) {
      seenPatterns.add(cost.modelPattern);
    }
  });

  return parsed;
};

export const convertParsedToDto = (parsedData: ParsedModelCost[]): CreateModelCostDto[] => {
  return parsedData
    .filter(d => d.isValid)
    .map(cost => ({
      modelIdPattern: cost.modelPattern,
      providerName: cost.provider,
      modelType: cost.modelType as 'chat' | 'embedding' | 'image' | 'audio' | 'video',
      inputTokenCost: cost.inputCostPer1K * 1000000, // Convert to per million tokens
      outputTokenCost: cost.outputCostPer1K * 1000000,
      cachedInputTokenCost: cost.cachedInputCostPer1K ? cost.cachedInputCostPer1K * 1000000 : undefined,
      cachedInputWriteTokenCost: cost.cachedInputWriteCostPer1K ? cost.cachedInputWriteCostPer1K * 1000000 : undefined,
      embeddingTokenCost: cost.embeddingCostPer1K ? cost.embeddingCostPer1K * 1000000 : undefined,
      imageCostPerImage: cost.imageCostPerImage,
      audioCostPerMinute: cost.audioCostPerMinute,
      videoCostPerSecond: cost.videoCostPerSecond,
      batchProcessingMultiplier: cost.batchProcessingMultiplier,
      supportsBatchProcessing: cost.supportsBatchProcessing,
      imageQualityMultipliers: cost.imageQualityMultipliers,
      costPerSearchUnit: cost.searchUnitCostPer1K,
      priority: cost.priority,
      description: cost.description,
    }));
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