# ESLint Configuration Guide

## Overview

This repository uses ESLint v9 with the new flat configuration format (`eslint.config.js`). All TypeScript projects in this monorepo must use CommonJS format for their ESLint configurations to avoid module loading issues.

## Important Rules

1. **NEVER** have both `.eslintrc.*` and `eslint.config.js` files in the same project
2. **ALWAYS** use CommonJS format (`module.exports`) in `eslint.config.js` files
3. **ALWAYS** test ESLint locally before pushing changes

## Configuration Locations

- **Admin SDK**: `/SDKs/Node/Admin/eslint.config.js`
- **Core SDK**: `/SDKs/Node/Core/eslint.config.js`
- **WebUI**: `/ConduitLLM.WebUI/eslint.config.js`

## Testing ESLint Configurations

### Before Pushing

Always run the validation script before pushing:

```bash
./scripts/validate-eslint.sh
```

This script will:
- Check for conflicting config files
- Test ESLint in each project
- Report any configuration errors

### Manual Testing

To test a specific project:

```bash
cd SDKs/Node/Core
npm run lint
```

## Common Issues and Solutions

### Issue: "Module type of file ... is not specified"

**Solution**: Ensure the ESLint config uses CommonJS format:
```javascript
// ✅ CORRECT
const js = require('@eslint/js');
module.exports = [...]

// ❌ WRONG
import js from '@eslint/js';
export default [...]
```

### Issue: "jest is not defined" in test files

**Solution**: Add test globals to the configuration:
```javascript
{
  files: ['**/*.test.ts', '**/*.spec.ts'],
  languageOptions: {
    globals: {
      jest: 'readonly',
      describe: 'readonly',
      it: 'readonly',
      expect: 'readonly',
      // ... other test globals
    },
  },
}
```

### Issue: Missing Node.js/Browser globals

**Solution**: Add the required globals to the main configuration:
```javascript
globals: {
  // Node.js
  console: 'readonly',
  process: 'readonly',
  Buffer: 'readonly',
  setTimeout: 'readonly',
  
  // Browser
  window: 'readonly',
  document: 'readonly',
  fetch: 'readonly',
  
  // TypeScript
  NodeJS: 'readonly',
  
  // Add others as needed
}
```

## Adding New Projects

When adding a new TypeScript project:

1. Create `eslint.config.js` (NOT `.eslintrc.js`)
2. Use CommonJS format
3. Copy from an existing project and modify as needed
4. Test with `npm run lint`
5. Update `/scripts/validate-eslint.sh` to include the new project

## CI/CD Integration

The GitHub Actions workflows run `npm run lint` during the build process. Any ESLint errors will cause the build to fail. This is why local testing is critical.

## Maintenance

- Keep ESLint and TypeScript plugin versions in sync across projects
- When upgrading ESLint, test all projects before pushing
- Document any new globals or rules in this file