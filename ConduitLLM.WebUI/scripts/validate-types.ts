#!/usr/bin/env tsx

import { execSync } from 'child_process';
import chalk from 'chalk';
import { existsSync } from 'fs';
import { resolve } from 'path';

console.log(chalk.blue('🔍 Validating TypeScript compilation...\n'));

const rootDir = resolve(__dirname, '..');
process.chdir(rootDir);

let hasErrors = false;

// Check for TypeScript errors
console.log(chalk.cyan('📝 Checking TypeScript compilation...'));
try {
  execSync('npx tsc --noEmit', { stdio: 'inherit' });
  console.log(chalk.green('✅ TypeScript compilation successful\n'));
} catch (error) {
  console.error(chalk.red('❌ TypeScript compilation failed\n'));
  hasErrors = true;
}

// Check for ESLint issues
console.log(chalk.cyan('🔍 Running ESLint validation...'));
try {
  execSync('npm run lint', { stdio: 'inherit' });
  console.log(chalk.green('✅ ESLint validation passed\n'));
} catch (error) {
  console.error(chalk.red('❌ ESLint validation failed\n'));
  hasErrors = true;
}

// Check for any remaining SDK-related TODO comments
console.log(chalk.cyan('📋 Checking for SDK-related TODOs...'));
try {
  const todos = execSync('grep -r "TODO.*SDK\\|TODO.*sdk" src/ || true', { encoding: 'utf-8' });
  if (todos.trim()) {
    console.log(chalk.yellow('⚠️  Found SDK-related TODO comments:'));
    console.log(todos);
  } else {
    console.log(chalk.green('✅ No SDK-related TODOs found\n'));
  }
} catch (error) {
  console.log(chalk.green('✅ No SDK-related TODOs found\n'));
}

// Check for direct fetch calls to backend
console.log(chalk.cyan('🔍 Checking for direct backend fetch calls...'));
try {
  const directFetch = execSync(
    'grep -r "fetch.*localhost:[0-9]\\|fetch.*http://api\\|fetch.*http://admin" src/ --include="*.ts" --include="*.tsx" || true',
    { encoding: 'utf-8' }
  );
  if (directFetch.trim()) {
    console.log(chalk.red('❌ Found direct backend fetch calls:'));
    console.log(directFetch);
    hasErrors = true;
  } else {
    console.log(chalk.green('✅ No direct backend fetch calls found\n'));
  }
} catch (error) {
  console.log(chalk.green('✅ No direct backend fetch calls found\n'));
}

// Check for duplicate type definitions
console.log(chalk.cyan('🔍 Checking for duplicate SDK type definitions...'));
try {
  const duplicateTypes = execSync(
    'grep -r "interface VirtualKey\\|interface Provider\\|interface ModelMapping" src/ --include="*.ts" --include="*.tsx" | grep -v "UIVirtualKey\\|UIProvider\\|UIModelMapping" || true',
    { encoding: 'utf-8' }
  );
  if (duplicateTypes.trim()) {
    console.log(chalk.yellow('⚠️  Found potential duplicate type definitions:'));
    console.log(duplicateTypes);
  } else {
    console.log(chalk.green('✅ No duplicate SDK type definitions found\n'));
  }
} catch (error) {
  console.log(chalk.green('✅ No duplicate SDK type definitions found\n'));
}

// Check for proper SDK imports
console.log(chalk.cyan('🔍 Checking SDK import patterns...'));
try {
  const sdkImports = execSync(
    'grep -r "from.*@knn_labs/conduit" src/ --include="*.ts" --include="*.tsx" | wc -l',
    { encoding: 'utf-8' }
  );
  const importCount = parseInt(sdkImports.trim());
  console.log(chalk.green(`✅ Found ${importCount} SDK imports\n`));
  
  if (importCount === 0) {
    console.log(chalk.yellow('⚠️  Warning: No SDK imports found. This might indicate an issue.\n'));
  }
} catch (error) {
  console.log(chalk.yellow('⚠️  Could not count SDK imports\n'));
}

// Check for environment variable usage
console.log(chalk.cyan('🔍 Checking environment variable usage in routes...'));
try {
  const envInRoutes = execSync(
    'grep -r "process\\.env\\." src/app/api --include="*.ts" --include="*.tsx" || true',
    { encoding: 'utf-8' }
  );
  if (envInRoutes.trim()) {
    console.log(chalk.yellow('⚠️  Found direct environment variable access in API routes:'));
    console.log(envInRoutes);
    console.log(chalk.yellow('Consider using centralized configuration instead.\n'));
  } else {
    console.log(chalk.green('✅ No direct environment variable access in API routes\n'));
  }
} catch (error) {
  console.log(chalk.green('✅ No direct environment variable access in API routes\n'));
}

// Summary
console.log(chalk.blue('\n📊 Validation Summary:'));
if (hasErrors) {
  console.log(chalk.red('❌ Validation failed. Please fix the errors above.'));
  process.exit(1);
} else {
  console.log(chalk.green('✨ All validations passed! The SDK migration appears to be successful.'));
  
  // Additional recommendations
  console.log(chalk.cyan('\n💡 Next steps:'));
  console.log('1. Run the test suite: npm test');
  console.log('2. Check the build: npm run build');
  console.log('3. Test the application manually');
  console.log('4. Run performance tests');
}