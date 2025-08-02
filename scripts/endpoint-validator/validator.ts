#!/usr/bin/env tsx

import * as fs from 'fs';
import * as path from 'path';
import * as http from 'http';
import * as https from 'https';

interface EndpointInfo {
  path: string;
  location?: string;
  line?: number;
  type: 'static' | 'function';
}

interface ValidationResult {
  service: string;
  summary: {
    totalSdk: number;
    totalApi: number;
    matched: number;
    sdkOnly: number;
    apiOnly: number;
    matchRate: string;
  };
  issues: {
    missingInApi: string[];
    missingInSdk: string[];
    suggestions: Array<{
      current: string;
      suggested: string;
      confidence: 'high' | 'medium' | 'low';
      reason: string;
    }>;
  };
}

interface SwaggerDoc {
  paths: Record<string, any>;
}

class EndpointValidator {
  private readonly fixPatterns = [
    {
      pattern: /\/api\/Providers\//g,
      replacement: '/api/ProviderCredentials/',
      reason: 'Provider endpoints should use ProviderCredentials'
    },
    {
      pattern: /\/api\/IpFilter\b/g,
      replacement: '/api/IpRules',
      reason: 'IpFilter renamed to IpRules'
    },
    {
      pattern: /\/api\/Logs\b/g,
      replacement: '/api/RequestLogs',
      reason: 'Logs renamed to RequestLogs'
    },
    {
      pattern: /\/api\/GlobalSettings\/by-key\//g,
      replacement: '/api/GlobalSettings/',
      reason: 'by-key path not needed'
    }
  ];

  async validateService(
    serviceName: string,
    sdkConstantsPath: string,
    apiUrl: string
  ): Promise<ValidationResult> {
    // Extract SDK endpoints
    const sdkEndpoints = this.extractSdkEndpoints(sdkConstantsPath);
    
    // Fetch API endpoints
    const apiEndpoints = await this.fetchApiEndpoints(apiUrl);
    
    // Compare and analyze
    const result = this.compareEndpoints(serviceName, sdkEndpoints, apiEndpoints);
    
    return result;
  }

  private extractSdkEndpoints(filePath: string): Map<string, EndpointInfo> {
    const endpoints = new Map<string, EndpointInfo>();
    
    if (!fs.existsSync(filePath)) {
      console.error(`Warning: SDK file not found: ${filePath}`);
      return endpoints;
    }
    
    const content = fs.readFileSync(filePath, 'utf8');
    
    // Find all API endpoint patterns
    const patterns = [
      // Static strings: '/api/...'
      /['"`](\/api\/[^'"`]+)['"`]/g,
      // Template literals: `/api/...${...}`
      /`(\/api\/[^`]+)`/g,
      // Function returns: => `/api/...`
      /=>\s*['"`](\/api\/[^'"`]+)['"`]/g,
      /=>\s*`(\/api\/[^`]+)`/g
    ];
    
    patterns.forEach(pattern => {
      const matches = content.matchAll(pattern);
      for (const match of matches) {
        const endpoint = match[1];
        // Get line number
        const lines = content.substring(0, match.index).split('\n');
        const lineNumber = lines.length;
        
        // Normalize parameterized endpoints
        const normalized = endpoint
          .replace(/\$\{[^}]+\}/g, '{param}')
          .replace(/\([^)]+\)/g, '');
        
        endpoints.set(normalized, {
          path: endpoint,
          line: lineNumber,
          type: endpoint.includes('${') ? 'function' : 'static'
        });
      }
    });
    
    return endpoints;
  }

  private async fetchApiEndpoints(apiUrl: string): Promise<Set<string>> {
    return new Promise((resolve, reject) => {
      const url = `${apiUrl}/swagger/v1/swagger.json`;
      const client = url.startsWith('https') ? https : http;
      
      client.get(url, (res) => {
        let data = '';
        res.on('data', chunk => data += chunk);
        res.on('end', () => {
          try {
            const swagger: SwaggerDoc = JSON.parse(data);
            const endpoints = new Set(Object.keys(swagger.paths || {}));
            resolve(endpoints);
          } catch (e) {
            reject(new Error(`Failed to parse Swagger JSON: ${e}`));
          }
        });
      }).on('error', (e) => {
        reject(new Error(`Failed to fetch Swagger spec from ${apiUrl}: ${e.message}`));
      });
    });
  }

  private compareEndpoints(
    serviceName: string,
    sdkEndpoints: Map<string, EndpointInfo>,
    apiEndpoints: Set<string>
  ): ValidationResult {
    const matched = new Set<string>();
    const sdkOnly: string[] = [];
    const apiOnly: string[] = [];
    const suggestions: ValidationResult['issues']['suggestions'] = [];
    
    // Find matches and SDK-only endpoints
    sdkEndpoints.forEach((info, sdkPath) => {
      let found = false;
      
      // Direct match
      if (apiEndpoints.has(info.path)) {
        matched.add(info.path);
        found = true;
      } else {
        // Try normalized matching
        const normalizedSdk = this.normalizePath(sdkPath);
        apiEndpoints.forEach(apiPath => {
          const normalizedApi = this.normalizePath(apiPath);
          if (normalizedSdk === normalizedApi) {
            matched.add(info.path);
            found = true;
          }
        });
      }
      
      if (!found) {
        sdkOnly.push(info.path);
        
        // Generate suggestion
        const suggestion = this.findSuggestion(info.path, Array.from(apiEndpoints));
        if (suggestion) {
          suggestions.push(suggestion);
        }
      }
    });
    
    // Find API-only endpoints
    apiEndpoints.forEach(apiPath => {
      let found = false;
      const normalizedApi = this.normalizePath(apiPath);
      
      sdkEndpoints.forEach((info) => {
        const normalizedSdk = this.normalizePath(info.path);
        if (normalizedApi === normalizedSdk) {
          found = true;
        }
      });
      
      if (!found && !matched.has(apiPath)) {
        apiOnly.push(apiPath);
      }
    });
    
    const totalSdk = sdkEndpoints.size;
    const totalApi = apiEndpoints.size;
    const matchedCount = matched.size;
    
    return {
      service: serviceName,
      summary: {
        totalSdk,
        totalApi,
        matched: matchedCount,
        sdkOnly: sdkOnly.length,
        apiOnly: apiOnly.length,
        matchRate: totalSdk > 0 ? `${(matchedCount / totalSdk * 100).toFixed(1)}%` : '0%'
      },
      issues: {
        missingInApi: sdkOnly,
        missingInSdk: apiOnly,
        suggestions
      }
    };
  }

  private normalizePath(path: string): string {
    return path
      .toLowerCase()
      .replace(/\{[^}]+\}/g, '{param}')
      .replace(/\$\{[^}]+\}/g, '{param}')
      .replace(/\/\d+/g, '/{param}')
      .replace(/\([^)]+\)/g, '');
  }

  private findSuggestion(
    sdkPath: string,
    apiPaths: string[]
  ): ValidationResult['issues']['suggestions'][0] | null {
    // Try known fix patterns first
    for (const fix of this.fixPatterns) {
      if (fix.pattern.test(sdkPath)) {
        const suggested = sdkPath.replace(fix.pattern, fix.replacement);
        const normalizedSuggested = this.normalizePath(suggested);
        
        if (apiPaths.some(api => this.normalizePath(api) === normalizedSuggested)) {
          return {
            current: sdkPath,
            suggested,
            confidence: 'high',
            reason: fix.reason
          };
        }
      }
    }
    
    // Try fuzzy matching
    const sdkSegments = sdkPath.split('/').filter(s => s);
    let bestMatch: string | null = null;
    let bestScore = 0;
    
    apiPaths.forEach(apiPath => {
      const apiSegments = apiPath.split('/').filter(s => s);
      
      // Skip if too different in length
      if (Math.abs(sdkSegments.length - apiSegments.length) > 2) return;
      
      let score = 0;
      const minLength = Math.min(sdkSegments.length, apiSegments.length);
      
      for (let i = 0; i < minLength; i++) {
        if (sdkSegments[i].toLowerCase() === apiSegments[i].toLowerCase()) {
          score += 1;
        } else if (
          (sdkSegments[i].includes('{') && apiSegments[i].includes('{')) ||
          (sdkSegments[i].includes('$') && apiSegments[i].includes('{'))
        ) {
          score += 0.5;
        }
      }
      
      const similarity = score / Math.max(sdkSegments.length, apiSegments.length);
      if (similarity > bestScore && similarity > 0.6) {
        bestScore = similarity;
        bestMatch = apiPath;
      }
    });
    
    if (bestMatch) {
      return {
        current: sdkPath,
        suggested: bestMatch,
        confidence: bestScore > 0.8 ? 'high' : 'medium',
        reason: 'Similar path structure'
      };
    }
    
    return null;
  }

  generateReport(results: ValidationResult[]): void {
    console.log('\n' + '='.repeat(80));
    console.log('SDK ENDPOINT VALIDATION REPORT');
    console.log('='.repeat(80) + '\n');
    
    // Overall summary
    let totalMatched = 0;
    let totalSdkOnly = 0;
    let totalApiOnly = 0;
    
    results.forEach(result => {
      totalMatched += result.summary.matched;
      totalSdkOnly += result.summary.sdkOnly;
      totalApiOnly += result.summary.apiOnly;
    });
    
    console.log('OVERALL SUMMARY');
    console.log('-'.repeat(40));
    console.log(`✓ Matched endpoints:    ${totalMatched}`);
    console.log(`⚠ SDK-only endpoints:   ${totalSdkOnly}`);
    console.log(`⚠ API-only endpoints:   ${totalApiOnly}`);
    console.log(`\nTotal issues to fix:    ${totalSdkOnly + totalApiOnly}`);
    
    // Service-specific details
    results.forEach(result => {
      console.log('\n' + '='.repeat(80));
      console.log(`${result.service.toUpperCase()} SERVICE`);
      console.log('='.repeat(80));
      
      console.log('\nSummary:');
      console.log(`  SDK endpoints:  ${result.summary.totalSdk}`);
      console.log(`  API endpoints:  ${result.summary.totalApi}`);
      console.log(`  Match rate:     ${result.summary.matchRate}`);
      
      if (result.issues.missingInApi.length > 0) {
        console.log('\n❌ SDK endpoints NOT in API:');
        result.issues.missingInApi.slice(0, 10).forEach(endpoint => {
          console.log(`  - ${endpoint}`);
        });
        if (result.issues.missingInApi.length > 10) {
          console.log(`  ... and ${result.issues.missingInApi.length - 10} more`);
        }
      }
      
      if (result.issues.suggestions.length > 0) {
        console.log('\n🔧 HIGH CONFIDENCE FIXES:');
        result.issues.suggestions
          .filter(s => s.confidence === 'high')
          .slice(0, 10)
          .forEach(s => {
            console.log(`  ${s.current}`);
            console.log(`  → ${s.suggested}`);
            console.log(`  Reason: ${s.reason}\n`);
          });
      }
      
      if (result.issues.missingInSdk.length > 0) {
        console.log('\n📝 API endpoints NOT in SDK:');
        result.issues.missingInSdk.slice(0, 5).forEach(endpoint => {
          console.log(`  - ${endpoint}`);
        });
        if (result.issues.missingInSdk.length > 5) {
          console.log(`  ... and ${result.issues.missingInSdk.length - 5} more`);
        }
      }
    });
    
    // Action items
    console.log('\n' + '='.repeat(80));
    console.log('RECOMMENDED ACTIONS');
    console.log('='.repeat(80));
    console.log('\n1. Apply high-confidence fixes automatically');
    console.log('2. Review and remove deprecated SDK endpoints');
    console.log('3. Add missing API endpoints to SDK');
    console.log('4. Consider generating SDK from Swagger spec\n');
  }

  async generateFixScript(results: ValidationResult[]): Promise<void> {
    const fixes: string[] = ['#!/bin/bash', '', '# SDK Endpoint Fixes', '# Generated: ' + new Date().toISOString(), ''];
    
    results.forEach(result => {
      const highConfidenceFixes = result.issues.suggestions.filter(s => s.confidence === 'high');
      if (highConfidenceFixes.length === 0) return;
      
      fixes.push(`# ${result.service} fixes`);
      highConfidenceFixes.forEach(fix => {
        const escaped = fix.current.replace(/[[\]{}()*+?.\\^$|]/g, '\\$&');
        fixes.push(`sed -i 's|${escaped}|${fix.suggested}|g' $1`);
      });
      fixes.push('');
    });
    
    const fixScriptPath = path.join(__dirname, 'apply-fixes.sh');
    fs.writeFileSync(fixScriptPath, fixes.join('\n'), { mode: 0o755 });
    console.log(`\n✏️  Fix script generated: ${fixScriptPath}`);
    console.log('   Apply with: ./apply-fixes.sh <path-to-constants.ts>\n');
  }
}

// Main execution
async function main() {
  const validator = new EndpointValidator();
  const results: ValidationResult[] = [];
  
  try {
    // Validate Admin SDK
    const adminResult = await validator.validateService(
      'Admin',
      path.join(__dirname, '../../SDKs/Node/Admin/src/constants.ts'),
      process.env.ADMIN_API_URL || 'http://localhost:5002'
    );
    results.push(adminResult);
    
    // Validate Core SDK if it exists
    const coreConstantsPath = path.join(__dirname, '../../SDKs/Node/Core/src/constants.ts');
    if (fs.existsSync(coreConstantsPath)) {
      const coreResult = await validator.validateService(
        'Core',
        coreConstantsPath,
        process.env.CORE_API_URL || 'http://localhost:5000'
      );
      results.push(coreResult);
    }
    
    // Generate comprehensive report
    validator.generateReport(results);
    
    // Save detailed JSON report
    const reportPath = path.join(__dirname, 'validation-report.json');
    fs.writeFileSync(reportPath, JSON.stringify(results, null, 2));
    console.log(`📄 Detailed report saved to: ${reportPath}`);
    
    // Generate fix script
    await validator.generateFixScript(results);
    
    // Exit with error if there are issues
    const hasIssues = results.some(r => r.summary.sdkOnly > 0 || r.summary.apiOnly > 0);
    process.exit(hasIssues ? 1 : 0);
    
  } catch (error) {
    console.error('❌ Validation failed:', error instanceof Error ? error.message : error);
    process.exit(2);
  }
}

main();