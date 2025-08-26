/**
 * Conversation export and import utilities
 * Extracted from WebUI for framework-agnostic reuse
 */

/**
 * Export options for conversation formatting
 */
export interface ExportOptions {
  /** Include message metadata in export */
  includeMetadata?: boolean;
  /** Include timestamp information */
  includeTimestamps?: boolean;
  /** Include system messages in export */
  includeSystemMessages?: boolean;
  /** Date format for timestamps (ISO 8601 by default) */
  dateFormat?: 'iso' | 'locale' | 'custom';
  /** Custom date format string (when dateFormat is 'custom') */
  customDateFormat?: string;
  /** Include conversation statistics */
  includeStats?: boolean;
}

/**
 * Markdown-specific export options
 */
export interface MarkdownOptions extends ExportOptions {
  /** Include table of contents */
  includeTableOfContents?: boolean;
  /** Code block style for formatting */
  codeBlockStyle?: 'fenced' | 'indented';
  /** Header level for messages */
  headerLevel?: 1 | 2 | 3 | 4;
  /** Include message IDs */
  includeMessageIds?: boolean;
}

/**
 * CSV export options
 */
export interface CSVOptions extends ExportOptions {
  /** Columns to include in CSV */
  columns?: CSVColumn[];
  /** Field delimiter */
  delimiter?: ',' | ';' | '\t';
  /** Text qualifier */
  textQualifier?: '"' | "'";
  /** Include headers in CSV */
  includeHeaders?: boolean;
}

/**
 * Available CSV columns
 */
export type CSVColumn = 
  | 'id'
  | 'role' 
  | 'content'
  | 'timestamp'
  | 'model'
  | 'tokensUsed'
  | 'latency'
  | 'provider'
  | 'finishReason'
  | 'hasImages'
  | 'imageCount'
  | 'error';

/**
 * Export template for custom formats
 */
export interface ExportTemplate {
  /** Template header */
  header?: string;
  /** Template footer */
  footer?: string;
  /** Message template (receives message data) */
  messageTemplate: string;
  /** Message separator */
  messageSeparator?: string;
  /** Variables available in templates */
  variables?: Record<string, unknown>;
}

/**
 * Import options for conversation validation
 */
export interface ImportOptions {
  /** Validate message structure */
  validateStructure?: boolean;
  /** Sanitize content */
  sanitize?: boolean;
  /** Max messages to import */
  maxMessages?: number;
  /** Allow missing required fields */
  allowPartial?: boolean;
}

/**
 * Validation result wrapper
 */
export interface ValidationResult<T = unknown> {
  /** Whether validation was successful */
  success: boolean;
  /** Validation result data (if successful) */
  data?: T;
  /** Validation errors */
  errors?: ValidationError[];
  /** Validation warnings */
  warnings?: ValidationWarning[];
}

/**
 * Validation error details
 */
export interface ValidationError {
  /** Error code */
  code: string;
  /** Human-readable error message */
  message: string;
  /** Field path (for nested errors) */
  path?: string;
  /** Invalid value */
  value?: unknown;
}

/**
 * Validation warning details
 */
export interface ValidationWarning {
  /** Warning code */
  code: string;
  /** Human-readable warning message */
  message: string;
  /** Field path */
  path?: string;
  /** Warning value */
  value?: unknown;
}

/**
 * Conversation export metadata
 */
export interface ConversationMetadata {
  /** Export format version */
  version: string;
  /** Export timestamp */
  exported: string;
  /** Total message count */
  totalMessages: number;
  /** Primary model used */
  model?: string;
  /** Total tokens (estimated) */
  totalTokens?: number;
  /** Conversation duration */
  duration?: number;
  /** Unique participants */
  participants?: string[];
  /** Export options used */
  exportOptions?: ExportOptions;
}

/**
 * Message interface for export (framework-agnostic)
 */
export interface ExportableMessage {
  id: string;
  role: 'user' | 'assistant' | 'system' | 'function';
  content: string;
  timestamp: Date;
  model?: string;
  images?: Array<{
    url?: string;
    width?: number;
    height?: number;
    detail?: 'auto' | 'low' | 'high';
    mimeType?: string;
    size?: number;
  }>;
  functionCall?: {
    name: string;
    arguments: string;
  };
  toolCalls?: Array<{
    id: string;
    type: 'function';
    function: {
      name: string;
      arguments: string;
    };
  }>;
  metadata?: {
    tokensUsed?: number;
    tokensPerSecond?: number;
    latency?: number;
    finishReason?: string;
    provider?: string;
    model?: string;
    promptTokens?: number;
    completionTokens?: number;
    timeToFirstToken?: number;
    streaming?: boolean;
  };
  error?: {
    type: string;
    code?: string;
    statusCode?: number;
    retryAfter?: number;
    suggestions?: string[];
    technical?: string;
    recoverable: boolean;
  };
}

/**
 * Conversation export utilities
 */
export class ConversationExporter {
  /**
   * Export conversation to JSON format with full fidelity
   * @param messages Messages to export
   * @param options Export options
   * @returns JSON string representation
   */
  static toJSON(
    messages: ExportableMessage[], 
    options: ExportOptions = {}
  ): string {
    const filteredMessages = this.filterMessages(messages, options);
    const metadata = this.buildMetadata(messages, options);
    
    const exportData = {
      ...metadata,
      messages: filteredMessages.map(msg => this.serializeMessage(msg, options))
    };

    return JSON.stringify(exportData, null, 2);
  }

  /**
   * Export conversation to Markdown format
   * @param messages Messages to export
   * @param options Markdown-specific options
   * @returns Markdown string representation
   */
  static toMarkdown(
    messages: ExportableMessage[],
    options: MarkdownOptions = {}
  ): string {
    const filteredMessages = this.filterMessages(messages, options);
    const metadata = this.buildMetadata(messages, options);
    
    let markdown = '';
    
    // Header
    markdown += '# Conversation Export\n\n';
    
    if (options.includeStats !== false) {
      markdown += this.buildMarkdownMetadata(metadata, options);
      markdown += '\n';
    }
    
    // Table of contents
    if (options.includeTableOfContents) {
      markdown += this.buildTableOfContents(filteredMessages, options);
      markdown += '\n';
    }
    
    // Messages
    markdown += '## Messages\n\n';
    
    for (const message of filteredMessages) {
      markdown += this.formatMessageAsMarkdown(message, options);
      markdown += '\n';
    }
    
    return markdown;
  }

  /**
   * Export conversation to CSV format
   * @param messages Messages to export
   * @param options CSV-specific options
   * @returns CSV string representation
   */
  static toCSV(
    messages: ExportableMessage[],
    options: CSVOptions = {}
  ): string {
    const filteredMessages = this.filterMessages(messages, options);
    const columns = options.columns || ['id', 'role', 'content', 'timestamp'];
    const delimiter = options.delimiter || ',';
    const textQualifier = options.textQualifier || '"';
    
    let csv = '';
    
    // Headers
    if (options.includeHeaders !== false) {
      csv += `${columns.join(delimiter)  }\n`;
    }
    
    // Data rows
    for (const message of filteredMessages) {
      const row = columns.map(column => 
        this.formatCSVField(
          this.getMessageFieldValue(message, column), 
          textQualifier
        )
      );
      csv += `${row.join(delimiter)  }\n`;
    }
    
    return csv;
  }

  /**
   * Export conversation using a custom template
   * @param messages Messages to export
   * @param template Export template
   * @returns Custom formatted string
   */
  static toCustomFormat(
    messages: ExportableMessage[],
    template: ExportTemplate
  ): string {
    let output = '';
    
    // Header
    if (template.header) {
      output += this.processTemplate(template.header, { messages });
      output += '\n';
    }
    
    // Messages
    const messageOutputs = messages.map(message => 
      this.processTemplate(template.messageTemplate, { 
        message, 
        messages,
        ...template.variables 
      })
    );
    
    output += messageOutputs.join(template.messageSeparator || '\n');
    
    // Footer
    if (template.footer) {
      output += '\n';
      output += this.processTemplate(template.footer, { messages });
    }
    
    return output;
  }

  // Private helper methods
  private static filterMessages(
    messages: ExportableMessage[], 
    options: ExportOptions
  ): ExportableMessage[] {
    let filtered = messages;
    
    if (options.includeSystemMessages === false) {
      filtered = filtered.filter(msg => msg.role !== 'system');
    }
    
    return filtered;
  }

  private static buildMetadata(
    messages: ExportableMessage[], 
    _options: ExportOptions
  ): ConversationMetadata {
    const now = new Date();
    const firstMessage = messages[0];
    const lastMessage = messages[messages.length - 1];
    
    return {
      version: '1.0',
      exported: this.formatDate(now, _options),
      totalMessages: messages.length,
      model: this.getMostCommonModel(messages),
      totalTokens: this.calculateTotalTokens(messages),
      duration: firstMessage && lastMessage 
        ? lastMessage.timestamp.getTime() - firstMessage.timestamp.getTime()
        : undefined,
      participants: this.getUniqueParticipants(messages),
      exportOptions: _options
    };
  }

  private static serializeMessage(
    message: ExportableMessage, 
    options: ExportOptions
  ): Record<string, unknown> {
    const serialized: Record<string, unknown> = {
      id: message.id,
      role: message.role,
      content: message.content
    };

    if (options.includeTimestamps !== false) {
      serialized.timestamp = this.formatDate(message.timestamp, options);
    }

    if (options.includeMetadata !== false && message.metadata) {
      serialized.metadata = message.metadata;
    }

    if (message.model) {
      serialized.model = message.model;
    }

    if (message.images && message.images.length > 0) {
      serialized.images = message.images;
    }

    if (message.functionCall) {
      serialized.functionCall = message.functionCall;
    }

    if (message.toolCalls) {
      serialized.toolCalls = message.toolCalls;
    }

    if (message.error) {
      serialized.error = message.error;
    }

    return serialized;
  }

  private static buildMarkdownMetadata(
    metadata: ConversationMetadata, 
    _options: MarkdownOptions
  ): string {
    let md = '';
    
    if (metadata.exported) {
      md += `**Export Date:** ${metadata.exported}\n`;
    }
    
    if (metadata.model) {
      md += `**Primary Model:** ${metadata.model}\n`;
    }
    
    md += `**Total Messages:** ${metadata.totalMessages}\n`;
    
    if (metadata.totalTokens) {
      md += `**Estimated Tokens:** ${metadata.totalTokens.toLocaleString()}\n`;
    }
    
    if (metadata.duration) {
      const durationMinutes = Math.round(metadata.duration / 1000 / 60);
      md += `**Duration:** ${durationMinutes} minutes\n`;
    }
    
    if (metadata.participants && metadata.participants.length > 0) {
      md += `**Participants:** ${metadata.participants.join(', ')}\n`;
    }
    
    return md;
  }

  private static buildTableOfContents(
    messages: ExportableMessage[], 
    _options: MarkdownOptions
  ): string {
    let toc = '## Table of Contents\n\n';
    
    messages.forEach((message, index) => {
      const title = message.content.substring(0, 50).replace(/\n/g, ' ');
      const suffix = message.content.length > 50 ? '...' : '';
      const link = `message-${index + 1}`;
      
      toc += `${index + 1}. [${message.role}: ${title}${suffix}](#${link})\n`;
    });
    
    return toc;
  }

  private static formatMessageAsMarkdown(
    message: ExportableMessage, 
    options: MarkdownOptions
  ): string {
    const headerLevel = options.headerLevel || 3;
    const headerPrefix = '#'.repeat(headerLevel);
    
    let md = `${headerPrefix} ${this.capitalizeRole(message.role)}`;
    
    if (options.includeMessageIds) {
      md += ` (${message.id})`;
    }
    
    if (options.includeTimestamps !== false) {
      md += ` - ${this.formatDate(message.timestamp, options)}`;
    }
    
    md += '\n\n';
    
    // Content
    md += message.content;
    
    // Images
    if (message.images && message.images.length > 0) {
      md += '\n\n**Images:**\n';
      message.images.forEach((img, index) => {
        md += `- Image ${index + 1}`;
        if (img.width && img.height) {
          md += ` (${img.width}x${img.height})`;
        }
        if (img.url) {
          md += `: ${img.url}`;
        }
        md += '\n';
      });
    }
    
    // Metadata
    if (options.includeMetadata && message.metadata) {
      md += '\n\n**Metadata:**\n';
      if (message.metadata.tokensUsed) {
        md += `- Tokens: ${message.metadata.tokensUsed}\n`;
      }
      if (message.metadata.latency) {
        md += `- Latency: ${message.metadata.latency}ms\n`;
      }
      if (message.metadata.model) {
        md += `- Model: ${message.metadata.model}\n`;
      }
    }
    
    return `${md  }\n`;
  }

  private static formatCSVField(value: unknown, qualifier: string): string {
    const str = String(value || '');
    const needsQualification = str.includes(',') || str.includes('\n') || str.includes(qualifier);
    
    if (needsQualification) {
      return qualifier + str.replace(new RegExp(qualifier, 'g'), qualifier + qualifier) + qualifier;
    }
    
    return str;
  }

  private static getMessageFieldValue(message: ExportableMessage, field: CSVColumn): unknown {
    switch (field) {
      case 'id': return message.id;
      case 'role': return message.role;
      case 'content': return message.content.replace(/\n/g, ' ');
      case 'timestamp': return message.timestamp.toISOString();
      case 'model': return message.model || message.metadata?.model || '';
      case 'tokensUsed': return message.metadata?.tokensUsed !== undefined ? message.metadata.tokensUsed : '';
      case 'latency': return message.metadata?.latency !== undefined ? message.metadata.latency : '';
      case 'provider': return message.metadata?.provider || '';
      case 'finishReason': return message.metadata?.finishReason || '';
      case 'hasImages': return (message.images && message.images.length > 0) ? 'true' : 'false';
      case 'imageCount': return message.images?.length !== undefined ? message.images.length : '';
      case 'error': return message.error ? `${message.error.type}:${message.error.code || ''}` : '';
      default: return '';
    }
  }

  private static processTemplate(template: string, variables: Record<string, unknown>): string {
    return template.replace(/\{\{(\w+(?:\.\w+)*)\}\}/g, (match, path) => {
      const value = this.getNestedValue(variables, path);
      return value !== undefined ? String(value) : match;
    });
  }

  private static getNestedValue(obj: Record<string, unknown>, path: string): unknown {
    return path.split('.').reduce((current: unknown, key: string) => {
      if (current && typeof current === 'object' && key in current) {
        return (current as Record<string, unknown>)[key];
      }
      return undefined;
    }, obj as unknown);
  }

  private static formatDate(date: Date, options: ExportOptions): string {
    switch (options.dateFormat) {
      case 'locale':
        return date.toLocaleString();
      case 'custom':
        // For custom formats, would need a date formatting library
        return date.toISOString();
      case 'iso':
      default:
        return date.toISOString();
    }
  }

  private static getMostCommonModel(messages: ExportableMessage[]): string | undefined {
    const modelCounts = new Map<string, number>();
    
    for (const message of messages) {
      const model = message.model || message.metadata?.model;
      if (model) {
        modelCounts.set(model, (modelCounts.get(model) || 0) + 1);
      }
    }
    
    let maxCount = 0;
    let mostCommon: string | undefined;
    
    for (const [model, count] of modelCounts) {
      if (count > maxCount) {
        maxCount = count;
        mostCommon = model;
      }
    }
    
    return mostCommon;
  }

  private static calculateTotalTokens(messages: ExportableMessage[]): number {
    return messages.reduce((total, message) => {
      return total + (message.metadata?.tokensUsed || 0);
    }, 0);
  }

  private static getUniqueParticipants(messages: ExportableMessage[]): string[] {
    const participants = new Set<string>();
    
    for (const message of messages) {
      participants.add(message.role);
    }
    
    return Array.from(participants).sort();
  }

  private static capitalizeRole(role: string): string {
    return role.charAt(0).toUpperCase() + role.slice(1);
  }
}

/**
 * Conversation import utilities
 */
export class ConversationImporter {
  /**
   * Import conversation from JSON format
   * @param json JSON string to parse
   * @param options Import validation options
   * @returns Validation result with messages or errors
   */
  static fromJSON(
    json: string, 
    options: ImportOptions = {}
  ): ValidationResult<ExportableMessage[]> {
    try {
      const data = JSON.parse(json);
      return this.validate(data, options);
    } catch (error) {
      return {
        success: false,
        errors: [{
          code: 'INVALID_JSON',
          message: 'Invalid JSON format',
          value: error instanceof Error ? error.message : String(error)
        }]
      };
    }
  }

  /**
   * Validate conversation data structure
   * @param data Raw data to validate
   * @param options Validation options
   * @returns Validation result
   */
  static validate(
    data: unknown, 
    options: ImportOptions = {}
  ): ValidationResult<ExportableMessage[]> {
    const errors: ValidationError[] = [];
    const warnings: ValidationWarning[] = [];

    // Check if data is an object
    if (typeof data !== 'object' || data === null) {
      return {
        success: false,
        errors: [{
          code: 'INVALID_FORMAT',
          message: 'Data must be an object',
          value: typeof data
        }]
      };
    }

    const conversationData = data as Record<string, unknown>;

    // Check for messages array
    if (!Array.isArray(conversationData.messages)) {
      return {
        success: false,
        errors: [{
          code: 'MISSING_MESSAGES',
          message: 'Messages array is required',
          path: 'messages'
        }]
      };
    }

    const messages = conversationData.messages as unknown[];
    
    // Apply message limit
    const limitedMessages = options.maxMessages 
      ? messages.slice(0, options.maxMessages)
      : messages;

    if (options.maxMessages && messages.length > options.maxMessages) {
      warnings.push({
        code: 'MESSAGE_LIMIT_EXCEEDED',
        message: `Only importing first ${options.maxMessages} messages`,
        value: messages.length
      });
    }

    // Validate each message
    const validatedMessages: ExportableMessage[] = [];
    
    for (let i = 0; i < limitedMessages.length; i++) {
      const messageResult = this.validateMessage(
        limitedMessages[i], 
        i, 
        options
      );
      
      if (!messageResult.success) {
        errors.push(...(messageResult.errors || []));
        if (!options.allowPartial) {
          break;
        }
      } else if (messageResult.data) {
        validatedMessages.push(messageResult.data);
      }
      
      warnings.push(...(messageResult.warnings || []));
    }

    // Apply sanitization if requested
    const finalMessages = options.sanitize 
      ? this.sanitize(validatedMessages)
      : validatedMessages;

    return {
      success: errors.length === 0,
      data: finalMessages,
      errors: errors.length > 0 ? errors : undefined,
      warnings: warnings.length > 0 ? warnings : undefined
    };
  }

  /**
   * Sanitize imported messages
   * @param messages Messages to sanitize
   * @returns Sanitized messages
   */
  static sanitize(messages: ExportableMessage[]): ExportableMessage[] {
    return messages.map(message => ({
      ...message,
      // Sanitize content (remove potential XSS)
      content: this.sanitizeContent(message.content),
      // Ensure required fields have valid values
      id: message.id || this.generateId(),
      role: this.validateRole(message.role),
      timestamp: message.timestamp instanceof Date 
        ? message.timestamp 
        : new Date(message.timestamp || Date.now())
    }));
  }

  // Private validation helpers
  private static validateMessage(
    data: unknown, 
    index: number, 
    options: ImportOptions
  ): ValidationResult<ExportableMessage> {
    const errors: ValidationError[] = [];
    const warnings: ValidationWarning[] = [];
    
    if (typeof data !== 'object' || data === null) {
      return {
        success: false,
        errors: [{
          code: 'INVALID_MESSAGE_FORMAT',
          message: `Message at index ${index} must be an object`,
          path: `messages[${index}]`,
          value: typeof data
        }]
      };
    }
    
    const message = data as Record<string, unknown>;
    
    // Required fields validation
    const requiredFields = ['id', 'role', 'content'];
    
    for (const field of requiredFields) {
      if (!message[field] && !options.allowPartial) {
        errors.push({
          code: 'MISSING_REQUIRED_FIELD',
          message: `Missing required field: ${field}`,
          path: `messages[${index}].${field}`
        });
      }
    }
    
    // Role validation
    if (message.role && !['user', 'assistant', 'system', 'function'].includes(message.role as string)) {
      if (options.allowPartial) {
        warnings.push({
          code: 'INVALID_ROLE',
          message: `Invalid role: ${message.role}, defaulting to 'user'`,
          path: `messages[${index}].role`,
          value: message.role
        });
        message.role = 'user';
      } else {
        errors.push({
          code: 'INVALID_ROLE',
          message: `Invalid role: ${message.role}`,
          path: `messages[${index}].role`,
          value: message.role
        });
      }
    }
    
    // Timestamp validation
    if (message.timestamp && !this.isValidDate(message.timestamp)) {
      if (options.allowPartial) {
        warnings.push({
          code: 'INVALID_TIMESTAMP',
          message: 'Invalid timestamp, using current time',
          path: `messages[${index}].timestamp`,
          value: message.timestamp
        });
        message.timestamp = new Date();
      } else {
        errors.push({
          code: 'INVALID_TIMESTAMP',
          message: 'Invalid timestamp format',
          path: `messages[${index}].timestamp`,
          value: message.timestamp
        });
      }
    }
    
    if (errors.length > 0) {
      return {
        success: false,
        errors,
        warnings: warnings.length > 0 ? warnings : undefined
      };
    }
    
    return {
      success: true,
      data: {
        id: String(message.id || this.generateId()),
        role: message.role as 'user' | 'assistant' | 'system' | 'function',
        content: String(message.content || ''),
        timestamp: message.timestamp instanceof Date 
          ? message.timestamp 
          : new Date(String(message.timestamp) || Date.now()),
        model: message.model ? String(message.model) : undefined,
        images: Array.isArray(message.images) ? message.images as ExportableMessage['images'] : undefined,
        functionCall: message.functionCall as ExportableMessage['functionCall'],
        toolCalls: Array.isArray(message.toolCalls) ? message.toolCalls as ExportableMessage['toolCalls'] : undefined,
        metadata: message.metadata as ExportableMessage['metadata'],
        error: message.error as ExportableMessage['error']
      },
      warnings: warnings.length > 0 ? warnings : undefined
    };
  }
  
  private static sanitizeContent(content: string): string {
    // Basic XSS prevention - remove script tags and javascript: protocols
    return content
      .replace(/<script[^>]*>.*?<\/script>/gi, '')
      .replace(/javascript:/gi, '')
      .replace(/on\w+\s*=/gi, '');
  }
  
  private static validateRole(role: string): 'user' | 'assistant' | 'system' | 'function' {
    const validRoles: Array<'user' | 'assistant' | 'system' | 'function'> = 
      ['user', 'assistant', 'system', 'function'];
    
    return validRoles.includes(role as any) ? role as any : 'user';
  }
  
  private static generateId(): string {
    return `msg_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
  }
  
  private static isValidDate(value: unknown): boolean {
    if (value instanceof Date) {
      return !isNaN(value.getTime());
    }
    
    if (typeof value === 'string') {
      const date = new Date(value);
      return !isNaN(date.getTime());
    }
    
    return false;
  }
}