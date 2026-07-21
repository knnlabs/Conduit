#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptsDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptsDir, '..', '..', '..');
const defaultFiles = [
  path.join(repoRoot, 'Services', 'ConduitLLM.Admin', 'openapi-admin.json'),
  path.join(repoRoot, 'Services', 'ConduitLLM.Gateway', 'openapi-gateway.json'),
];
const files = process.argv.slice(2).map((file) => path.resolve(file));
const contractFiles = files.length > 0 ? files : defaultFiles;
const methods = new Set(['get', 'put', 'post', 'delete', 'options', 'head', 'patch', 'trace']);
const errors = [];

function fail(file, location, message) {
  errors.push(`${path.relative(repoRoot, file)}: ${location}: ${message}`);
}

function schemaType(schema) {
  if (!schema || typeof schema !== 'object') return undefined;
  return Array.isArray(schema.type) ? schema.type[0] : schema.type;
}

for (const file of contractFiles) {
  const document = JSON.parse(fs.readFileSync(file, 'utf8'));
  if (document.openapi !== '3.1.1') {
    fail(file, 'openapi', `expected 3.1.1, found ${document.openapi ?? 'missing'}`);
  }

  const schemes = document.components?.securitySchemes ?? {};
  const operationIds = new Map();

  for (const [route, pathItem] of Object.entries(document.paths ?? {})) {
    for (const [method, operation] of Object.entries(pathItem)) {
      if (!methods.has(method)) continue;
      const location = `${method.toUpperCase()} ${route}`;
      const operationId = operation.operationId?.trim();

      if (!operationId) {
        fail(file, location, 'operationId is missing or empty');
      } else if (operationIds.has(operationId)) {
        fail(file, location, `operationId '${operationId}' duplicates ${operationIds.get(operationId)}`);
      } else {
        operationIds.set(operationId, location);
      }

      if (!Array.isArray(operation.tags) || operation.tags.length === 0) {
        fail(file, location, 'at least one tag is required');
      }

      if (!Array.isArray(operation.security)) {
        fail(file, location, 'security must be explicit (an empty array means anonymous)');
      } else {
        for (const requirement of operation.security) {
          for (const schemeName of Object.keys(requirement)) {
            if (!(schemeName in schemes)) {
              fail(file, location, `security requirement references unknown scheme '${schemeName}'`);
            }
          }
        }
      }

      for (const [status, response] of Object.entries(operation.responses ?? {})) {
        const responseLocation = `${location} response ${status}`;
        if (!response.description?.trim()) {
          fail(file, responseLocation, 'description is missing or empty');
        }

        const content = response.content ?? {};
        if ((status === '204' || status === '205' || method === 'head') && Object.keys(content).length > 0) {
          fail(file, responseLocation, 'bodyless response must not declare content');
          continue;
        }

        if (/^2\d\d$/.test(status) && status !== '204' && status !== '205' && method !== 'head') {
          if (Object.keys(content).length === 0) {
            fail(file, responseLocation, 'body-bearing success response must declare content and a schema');
          }
          for (const [contentType, media] of Object.entries(content)) {
            if (!media.schema) {
              fail(file, responseLocation, `${contentType} is missing a response schema`);
              continue;
            }

            const binary = contentType === 'application/octet-stream' ||
              contentType.startsWith('image/') || contentType.startsWith('audio/') || contentType.startsWith('video/');
            if (binary && (schemaType(media.schema) !== 'string' || media.schema.format !== 'binary')) {
              fail(file, responseLocation, `${contentType} must use a string/binary schema`);
            }
            if (contentType === 'text/event-stream' && schemaType(media.schema) !== 'string') {
              fail(file, responseLocation, 'text/event-stream must use a string schema');
            }
          }
        }
      }
    }
  }

  const relative = path.relative(repoRoot, file);
  if (relative.includes('ConduitLLM.Admin')) {
    const scheme = schemes.MasterKey;
    if (scheme?.type !== 'apiKey' || scheme?.in !== 'header' || scheme?.name !== 'X-Master-Key') {
      fail(file, 'components.securitySchemes.MasterKey', 'must be an X-Master-Key header apiKey scheme');
    }
  } else if (relative.includes('ConduitLLM.Gateway')) {
    const scheme = schemes.VirtualKey;
    if (scheme?.type !== 'http' || scheme?.scheme !== 'bearer') {
      fail(file, 'components.securitySchemes.VirtualKey', 'must be an HTTP Bearer scheme');
    }
  }
}

if (errors.length > 0) {
  console.error(`OpenAPI invariant validation failed with ${errors.length} error(s):`);
  for (const error of errors) console.error(`- ${error}`);
  process.exit(1);
}

console.log(`Validated ${contractFiles.length} OpenAPI contract(s).`);
