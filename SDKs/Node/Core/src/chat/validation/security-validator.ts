/**
 * Security validation utilities for chat content
 * Comprehensive security scanning and threat detection
 */

import {
  ValidationSeverity,
  ValidationCategory,
  ValidationConstants,
  type ValidationResult,
  type ValidationWarning,
  type EnhancedValidationError,
  type SecurityValidationOptions,
  type SanitizeOptions
} from './types';

/**
 * Security threat categories
 */
export enum ThreatType {
  XSS = 'xss',
  SQL_INJECTION = 'sql_injection',
  SCRIPT_INJECTION = 'script_injection',
  PATH_TRAVERSAL = 'path_traversal',
  COMMAND_INJECTION = 'command_injection',
  LDAP_INJECTION = 'ldap_injection',
  XML_INJECTION = 'xml_injection',
  NOSQL_INJECTION = 'nosql_injection',
  SERVER_SIDE_TEMPLATE_INJECTION = 'ssti',
  MALICIOUS_URL = 'malicious_url',
  SUSPICIOUS_PATTERN = 'suspicious_pattern'
}

/**
 * Security patterns for threat detection
 */
const SECURITY_PATTERNS = {
  [ThreatType.XSS]: [
    /<script[^>]*>.*?<\/script>/gi,
    /<iframe[^>]*>.*?<\/iframe>/gi,
    /<object[^>]*>.*?<\/object>/gi,
    /<embed[^>]*>/gi,
    /<link[^>]*>/gi,
    /<meta[^>]*>/gi,
    /on\w+\s*=\s*['"]/gi,
    /javascript\s*:/gi,
    /vbscript\s*:/gi,
    /data\s*:\s*text\/html/gi,
    /expression\s*\(/gi,
    /@import/gi,
    /<!--[\s\S]*?-->/gi,
    /<\s*\/?\s*\w+[^>]*>/gi // Generic HTML tags
  ],
  
  [ThreatType.SQL_INJECTION]: [
    /(union\s+select|select\s+.*\s+from)/gi,
    /(insert\s+into|update\s+.*\s+set|delete\s+from)/gi,
    /(drop\s+(table|database|index)|alter\s+table)/gi,
    /(exec\s*\(|execute\s*\()/gi,
    /('\s*(or|and)\s*'?\w)/gi,
    /(--|#|\/\*|\*\/)/gi,
    /(0x[a-f0-9]+|char\s*\()/gi,
    /(waitfor\s+delay|benchmark\s*\()/gi,
    /(information_schema|sys\.tables)/gi,
    /(load_file\s*\(|into\s+outfile)/gi
  ],
  
  [ThreatType.SCRIPT_INJECTION]: [
    /<\?\s*(php|asp|jsp)/gi,
    /<%[\s\S]*?%>/gi,
    /\{\{\s*.*\s*\}\}/gi,
    /\$\{\s*.*\s*\}/gi,
    /eval\s*\(/gi,
    /setTimeout\s*\(/gi,
    /setInterval\s*\(/gi,
    /Function\s*\(/gi,
    /new\s+Function\s*\(/gi,
    /document\.(cookie|domain|location)/gi
  ],
  
  [ThreatType.PATH_TRAVERSAL]: [
    /\.\.[/\\]/gi,
    /(\.{2,}[/\\]){2,}/gi,
    /[/\\]\.\.[/\\]/gi,
    /(file|ftp|gopher|dict|expect|zip):\/\//gi,
    /\/(etc\/passwd|windows\/system32)/gi,
    /\.\.%2f/gi,
    /%2e%2e%2f/gi,
    /\.\.\\|\.\.%5c/gi
  ],
  
  [ThreatType.COMMAND_INJECTION]: [
    /[;&|`$(){}[\]]/gi,
    /(sh|bash|cmd|powershell)\s/gi,
    /(wget|curl|nc|netcat)\s/gi,
    /(rm|del|rmdir)\s/gi,
    /(cat|type|more|less)\s/gi,
    /(ps|tasklist|kill|killall)\s/gi,
    /\|\|\s*\w+/gi,
    /&&\s*\w+/gi
  ],
  
  [ThreatType.LDAP_INJECTION]: [
    /[()*/|&=!><~]/gi,
    /\u0000/gi,
    /(objectclass=\*)/gi,
    /(\|\(|\)\(|&\()/gi
  ],
  
  [ThreatType.XML_INJECTION]: [
    /<\?xml[^>]*>/gi,
    /<!DOCTYPE[^>]*>/gi,
    /<!ENTITY[^>]*>/gi,
    /<!\[CDATA\[[\s\S]*?\]\]>/gi,
    /&\w+;/gi
  ],
  
  [ThreatType.NOSQL_INJECTION]: [
    /\$where/gi,
    /\$ne|\$gt|\$lt|\$gte|\$lte/gi,
    /\$regex|\$options/gi,
    /\$or|\$and|\$nor/gi,
    /\$exists|\$type|\$mod/gi,
    /this\./gi
  ],
  
  [ThreatType.SERVER_SIDE_TEMPLATE_INJECTION]: [
    /\{\{.*?\}\}/gi,
    /\{%.*?%\}/gi,
    /\{#.*?#\}/gi,
    /<\?.*?\?>/gi,
    /<%.*?%>/gi,
    /\$\{.*?\}/gi,
    /#\{.*?\}/gi
  ],
  
  [ThreatType.MALICIOUS_URL]: [
    /data:text\/html/gi,
    /javascript:/gi,
    /vbscript:/gi,
    /file:\/\//gi,
    /ftp:\/\/.*@/gi,
    /https?:\/\/(\d+\.){3}\d+/gi, // IP addresses
    /https?:\/\/[^/]*\.(tk|ml|ga|cf)[/$]/gi, // Suspicious TLDs
    /(bit\.ly|tinyurl|t\.co|goo\.gl|short\.link)\/\w+/gi // URL shorteners
  ],
  
  [ThreatType.SUSPICIOUS_PATTERN]: [
    /(password|passwd|pwd|secret|key|token)\s*[:=]/gi,
    /(api[_-]?key|access[_-]?token|auth[_-]?token)\s*[:=]/gi,
    /-----BEGIN\s+(RSA\s+)?PRIVATE\s+KEY-----/gi,
    /(smtp|mail|email)\s*[:=].*@/gi,
    /(database|db)[_-]?(host|server|url)\s*[:=]/gi,
    /[a-f0-9]{32,}/gi, // Hex strings (potential hashes/keys)
    /[A-Za-z0-9+/]{40,}={0,2}/gi // Base64 strings
  ]
};

/**
 * Malicious URL patterns and domains
 */
const MALICIOUS_DOMAINS = [
  // Common malware domains (example patterns)
  /malware.*\.com/gi,
  /phishing.*\.net/gi,
  /spam.*\.org/gi,
  // Add more patterns as needed
];

/**
 * File extension patterns that might contain scripts
 */
const DANGEROUS_EXTENSIONS = [
  '.exe', '.bat', '.cmd', '.com', '.scr', '.pif', '.vbs', '.js', '.jar',
  '.app', '.deb', '.dmg', '.iso', '.msi', '.pkg', '.rpm'
];

/**
 * Security validation result with threat details
 */
export interface SecurityValidationResult extends ValidationResult {
  /** Detected threats */
  threats?: ThreatDetection[];
  /** Security score (0-100, higher is safer) */
  securityScore?: number;
}

/**
 * Detected threat information
 */
export interface ThreatDetection {
  /** Type of threat */
  type: ThreatType;
  /** Threat severity */
  severity: ValidationSeverity;
  /** Pattern that matched */
  pattern: string;
  /** Matched content */
  match: string;
  /** Position in content */
  position?: { start: number; end: number };
  /** Confidence score (0-1) */
  confidence: number;
  /** Recommended action */
  action: 'block' | 'sanitize' | 'warn';
}

/**
 * Comprehensive security validation utilities
 */
export class SecurityValidator {
  /**
   * Perform comprehensive security scan on content
   */
  static validateSecurity(
    content: string,
    options: SecurityValidationOptions = {}
  ): SecurityValidationResult {
    const errors: EnhancedValidationError[] = [];
    const warnings: ValidationWarning[] = [];
    const threats: ThreatDetection[] = [];
    
    if (!content || typeof content !== 'string') {
      return { valid: true, errors, warnings, threats, securityScore: 100 };
    }
    
    const defaultOptions: Required<SecurityValidationOptions> = {
      checkXSS: true,
      checkSQLInjection: true,
      checkScriptTags: true,
      checkDangerousUrls: true,
      validateJson: true,
      maxJsonDepth: 10,
      checkPathTraversal: true,
      ...options
    };
    
    // Run security scans
    if (defaultOptions.checkXSS) {
      threats.push(...this.detectThreats(content, ThreatType.XSS));
    }
    
    if (defaultOptions.checkSQLInjection) {
      threats.push(...this.detectThreats(content, ThreatType.SQL_INJECTION));
    }
    
    if (defaultOptions.checkScriptTags) {
      threats.push(...this.detectThreats(content, ThreatType.SCRIPT_INJECTION));
    }
    
    if (defaultOptions.checkPathTraversal) {
      threats.push(...this.detectThreats(content, ThreatType.PATH_TRAVERSAL));
    }
    
    if (defaultOptions.checkDangerousUrls) {
      threats.push(...this.detectThreats(content, ThreatType.MALICIOUS_URL));
    }
    
    // Additional security checks
    threats.push(...this.detectThreats(content, ThreatType.COMMAND_INJECTION));
    threats.push(...this.detectThreats(content, ThreatType.LDAP_INJECTION));
    threats.push(...this.detectThreats(content, ThreatType.XML_INJECTION));
    threats.push(...this.detectThreats(content, ThreatType.NOSQL_INJECTION));
    threats.push(...this.detectThreats(content, ThreatType.SERVER_SIDE_TEMPLATE_INJECTION));
    threats.push(...this.detectThreats(content, ThreatType.SUSPICIOUS_PATTERN));
    
    // Validate JSON structure if requested
    if (defaultOptions.validateJson) {
      try {
        const parsed = JSON.parse(content);
        if (this.getObjectDepth(parsed) > defaultOptions.maxJsonDepth) {
          errors.push({
            code: 'JSON_TOO_DEEP',
            message: `JSON structure exceeds maximum depth of ${defaultOptions.maxJsonDepth}`,
            severity: ValidationSeverity.MEDIUM,
            category: ValidationCategory.SECURITY,
            suggestion: 'Reduce JSON nesting depth'
          });
        }
      } catch {
        // Not valid JSON, continue with other checks
      }
    }
    
    // Convert high-severity threats to errors
    for (const threat of threats) {
      if (threat.action === 'block') {
        errors.push({
          code: `SECURITY_THREAT_${threat.type.toUpperCase()}`,
          message: `Detected ${threat.type} threat: ${threat.match}`,
          severity: threat.severity,
          category: ValidationCategory.SECURITY,
          field: 'content',
          value: threat.match,
          suggestion: this.getThreatSuggestion(threat.type)
        });
      } else if (threat.action === 'warn') {
        warnings.push({
          code: `SECURITY_WARNING_${threat.type.toUpperCase()}`,
          message: `Potential ${threat.type} pattern detected: ${threat.match}`,
          field: 'content',
          value: threat.match,
          recommendation: this.getThreatSuggestion(threat.type)
        });
      }
    }
    
    // Calculate security score
    const securityScore = this.calculateSecurityScore(threats);
    
    return {
      valid: errors.length === 0,
      errors,
      warnings,
      threats,
      securityScore,
      metadata: {
        threatsDetected: threats.length,
        highSeverityThreats: threats.filter(t => t.severity === ValidationSeverity.HIGH).length,
        criticalThreats: threats.filter(t => t.severity === ValidationSeverity.CRITICAL).length
      }
    };
  }
  
  /**
   * Detect specific threat types in content
   */
  static detectThreats(content: string, threatType: ThreatType): ThreatDetection[] {
    const threats: ThreatDetection[] = [];
    const patterns = SECURITY_PATTERNS[threatType] || [];
    
    for (const pattern of patterns) {
      const matches = content.match(pattern);
      if (matches) {
        for (const match of matches) {
          const confidence = this.calculateConfidence(match, threatType);
          const severity = this.calculateSeverity(threatType, confidence);
          const action = this.determineAction(severity, confidence);
          
          threats.push({
            type: threatType,
            severity,
            pattern: pattern.toString(),
            match: match.substring(0, 100), // Truncate long matches
            confidence,
            action,
            position: this.findMatchPosition(content, match)
          });
        }
      }
    }
    
    return threats;
  }
  
  /**
   * Sanitize content based on security options
   */
  static sanitizeContent(content: string, options: SanitizeOptions = {}): string {
    if (!content || typeof content !== 'string') return content;
    
    let sanitized = content;
    
    const defaultOptions: Required<SanitizeOptions> = {
      removeHtml: false,
      removeScripts: true,
      escapeSpecialChars: false,
      removeUrls: false,
      replaceUrls: false,
      urlReplacement: '[URL_REMOVED]',
      normalizeWhitespace: false,
      trim: true,
      customRules: [],
      ...options
    };
    
    // Remove script tags
    if (defaultOptions.removeScripts) {
      sanitized = sanitized.replace(ValidationConstants.SECURITY_PATTERNS.SCRIPT_TAG, '');
      sanitized = sanitized.replace(ValidationConstants.SECURITY_PATTERNS.ON_EVENT, '');
      sanitized = sanitized.replace(ValidationConstants.SECURITY_PATTERNS.JAVASCRIPT_URL, '');
    }
    
    // Remove HTML tags
    if (defaultOptions.removeHtml) {
      sanitized = sanitized.replace(/<[^>]*>/g, '');
    }
    
    // Handle URLs
    if (defaultOptions.removeUrls) {
      sanitized = sanitized.replace(/https?:\/\/[^\s]+/gi, '');
    } else if (defaultOptions.replaceUrls) {
      sanitized = sanitized.replace(/https?:\/\/[^\s]+/gi, defaultOptions.urlReplacement);
    }
    
    // Escape special characters
    if (defaultOptions.escapeSpecialChars) {
      sanitized = sanitized
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
    }
    
    // Normalize whitespace
    if (defaultOptions.normalizeWhitespace) {
      sanitized = sanitized.replace(/\s+/g, ' ');
    }
    
    // Apply custom rules
    for (const rule of defaultOptions.customRules) {
      const flags = rule.global ? 'g' : '';
      const regex = rule.pattern instanceof RegExp ? rule.pattern : new RegExp(rule.pattern, flags);
      sanitized = sanitized.replace(regex, rule.replacement);
    }
    
    // Trim
    if (defaultOptions.trim) {
      sanitized = sanitized.trim();
    }
    
    return sanitized;
  }
  
  /**
   * Check if URL is potentially malicious
   */
  static isUrlMalicious(url: string): boolean {
    if (!url || typeof url !== 'string') return false;
    
    // Check against known malicious patterns
    for (const pattern of MALICIOUS_DOMAINS) {
      if (pattern.test(url)) return true;
    }
    
    // Check for suspicious URL patterns
    const suspiciousPatterns = SECURITY_PATTERNS[ThreatType.MALICIOUS_URL];
    for (const pattern of suspiciousPatterns) {
      if (pattern.test(url)) return true;
    }
    
    return false;
  }
  
  /**
   * Check if file extension is potentially dangerous
   */
  static isDangerousFileExtension(filename: string): boolean {
    if (!filename || typeof filename !== 'string') return false;
    
    const extension = filename.toLowerCase().substring(filename.lastIndexOf('.'));
    return DANGEROUS_EXTENSIONS.includes(extension);
  }
  
  /**
   * Calculate confidence score for a match
   */
  private static calculateConfidence(match: string, threatType: ThreatType): number {
    // Base confidence varies by threat type
    let baseConfidence = 0.7;
    
    switch (threatType) {
      case ThreatType.XSS:
      case ThreatType.SCRIPT_INJECTION:
        baseConfidence = 0.9;
        break;
      case ThreatType.SQL_INJECTION:
        baseConfidence = 0.8;
        break;
      case ThreatType.SUSPICIOUS_PATTERN:
        baseConfidence = 0.5;
        break;
    }
    
    // Adjust based on match characteristics
    if (match.length > 20) baseConfidence += 0.1;
    if (/[<>'"]/g.test(match)) baseConfidence += 0.1;
    if (/[()[\]{}]/g.test(match)) baseConfidence += 0.05;
    
    return Math.min(1.0, baseConfidence);
  }
  
  /**
   * Calculate severity based on threat type and confidence
   */
  private static calculateSeverity(threatType: ThreatType, confidence: number): ValidationSeverity {
    if (confidence < 0.3) return ValidationSeverity.LOW;
    
    switch (threatType) {
      case ThreatType.XSS:
      case ThreatType.SCRIPT_INJECTION:
      case ThreatType.SQL_INJECTION:
        return confidence > 0.8 ? ValidationSeverity.CRITICAL : ValidationSeverity.HIGH;
      
      case ThreatType.COMMAND_INJECTION:
      case ThreatType.PATH_TRAVERSAL:
        return confidence > 0.7 ? ValidationSeverity.HIGH : ValidationSeverity.MEDIUM;
      
      case ThreatType.MALICIOUS_URL:
        return confidence > 0.8 ? ValidationSeverity.HIGH : ValidationSeverity.MEDIUM;
      
      default:
        return confidence > 0.7 ? ValidationSeverity.MEDIUM : ValidationSeverity.LOW;
    }
  }
  
  /**
   * Determine action based on severity and confidence
   */
  private static determineAction(severity: ValidationSeverity, confidence: number): 'block' | 'sanitize' | 'warn' {
    if (severity === ValidationSeverity.CRITICAL || (severity === ValidationSeverity.HIGH && confidence > 0.8)) {
      return 'block';
    }
    
    if (severity === ValidationSeverity.HIGH || (severity === ValidationSeverity.MEDIUM && confidence > 0.7)) {
      return 'sanitize';
    }
    
    return 'warn';
  }
  
  /**
   * Calculate overall security score
   */
  private static calculateSecurityScore(threats: ThreatDetection[]): number {
    if (threats.length === 0) return 100;
    
    let score = 100;
    
    for (const threat of threats) {
      switch (threat.severity) {
        case ValidationSeverity.CRITICAL:
          score -= 30 * threat.confidence;
          break;
        case ValidationSeverity.HIGH:
          score -= 20 * threat.confidence;
          break;
        case ValidationSeverity.MEDIUM:
          score -= 10 * threat.confidence;
          break;
        case ValidationSeverity.LOW:
          score -= 5 * threat.confidence;
          break;
      }
    }
    
    return Math.max(0, Math.round(score));
  }
  
  /**
   * Get object nesting depth for JSON validation
   */
  private static getObjectDepth(obj: unknown, depth = 0): number {
    if (depth > 50) return depth; // Prevent infinite recursion
    
    if (obj === null || typeof obj !== 'object') return depth;
    
    if (Array.isArray(obj)) {
      return Math.max(...obj.map(item => this.getObjectDepth(item, depth + 1)));
    }
    
    const values = Object.values(obj as Record<string, unknown>);
    if (values.length === 0) return depth;
    
    return Math.max(...values.map(value => this.getObjectDepth(value, depth + 1)));
  }
  
  /**
   * Find position of match in content
   */
  private static findMatchPosition(content: string, match: string): { start: number; end: number } {
    const start = content.indexOf(match);
    return {
      start: start >= 0 ? start : 0,
      end: start >= 0 ? start + match.length : 0
    };
  }
  
  /**
   * Get suggestion for threat type
   */
  private static getThreatSuggestion(threatType: ThreatType): string {
    const suggestions: Record<ThreatType, string> = {
      [ThreatType.XSS]: 'Remove or escape HTML/JavaScript content',
      [ThreatType.SQL_INJECTION]: 'Use parameterized queries and validate input',
      [ThreatType.SCRIPT_INJECTION]: 'Remove script tags and JavaScript code',
      [ThreatType.PATH_TRAVERSAL]: 'Validate and sanitize file paths',
      [ThreatType.COMMAND_INJECTION]: 'Remove shell commands and special characters',
      [ThreatType.LDAP_INJECTION]: 'Escape LDAP special characters',
      [ThreatType.XML_INJECTION]: 'Validate and escape XML content',
      [ThreatType.NOSQL_INJECTION]: 'Validate NoSQL query operators',
      [ThreatType.SERVER_SIDE_TEMPLATE_INJECTION]: 'Remove template injection patterns',
      [ThreatType.MALICIOUS_URL]: 'Verify URL legitimacy before use',
      [ThreatType.SUSPICIOUS_PATTERN]: 'Review content for sensitive information'
    };
    
    return suggestions[threatType] || 'Review and sanitize suspicious content';
  }
}