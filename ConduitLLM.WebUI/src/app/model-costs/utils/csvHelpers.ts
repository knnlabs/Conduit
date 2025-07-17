import { CreateModelCostDto } from '../types/modelCost';

export interface ParsedModelCost {
  modelPattern: string;
  provider: string;
  modelType: string;
  inputCostPer1K: number;
  outputCostPer1K: number;
  embeddingCostPer1K?: number;
  imageCostPerImage?: number;
  audioCostPerMinute?: number;
  videoCostPerSecond?: number;
  priority: number;
  active: boolean;
  description?: string;
  // Validation
  isValid: boolean;
  errors: string[];
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
  const headers = lines[0].split(',').map(h => h.trim().toLowerCase());
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
    if (values.length !== headers.length) {
      continue; // Skip malformed rows
    }

    const row: any = {};
    headers.forEach((header, index) => {
      row[header] = values[index];
    });

    const cost: ParsedModelCost = {
      modelPattern: row['model pattern'] || '',
      provider: row['provider'] || '',
      modelType: row['model type'] || 'chat',
      inputCostPer1K: parseFloat(row['input cost (per 1k tokens)']) || 0,
      outputCostPer1K: parseFloat(row['output cost (per 1k tokens)']) || 0,
      embeddingCostPer1K: parseFloat(row['embedding cost (per 1k tokens)']) || undefined,
      imageCostPerImage: parseFloat(row['image cost (per image)']) || undefined,
      audioCostPerMinute: parseFloat(row['audio cost (per minute)']) || undefined,
      videoCostPerSecond: parseFloat(row['video cost (per second)']) || undefined,
      priority: parseInt(row['priority']) || 0,
      active: row['active']?.toLowerCase() === 'yes' || row['active']?.toLowerCase() === 'true',
      description: row['description'],
      isValid: true,
      errors: [],
    };

    // Validate row
    const errors: string[] = [];
    if (!cost.modelPattern) errors.push('Model pattern is required');
    if (!cost.provider) errors.push('Provider is required');
    if (!['chat', 'embedding', 'image', 'audio', 'video'].includes(cost.modelType)) {
      errors.push('Invalid model type');
    }
    if (cost.priority < 0) errors.push('Priority must be non-negative');

    cost.isValid = errors.length === 0;
    cost.errors = errors;

    parsed.push(cost);
  }

  return parsed;
};

export const convertParsedToDto = (parsedData: ParsedModelCost[]): CreateModelCostDto[] => {
  return parsedData
    .filter(d => d.isValid)
    .map(cost => ({
      modelIdPattern: cost.modelPattern,
      inputTokenCost: cost.inputCostPer1K * 1000, // Convert to per million
      outputTokenCost: cost.outputCostPer1K * 1000,
      embeddingTokenCost: cost.embeddingCostPer1K ? cost.embeddingCostPer1K * 1000 : undefined,
      imageCostPerImage: cost.imageCostPerImage,
      audioCostPerMinute: cost.audioCostPerMinute,
      videoCostPerSecond: cost.videoCostPerSecond,
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