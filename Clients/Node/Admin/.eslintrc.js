module.exports = {
  parser: '@typescript-eslint/parser',
  parserOptions: {
    ecmaVersion: 2022,
    sourceType: 'module',
    project: './tsconfig.json',
  },
  plugins: ['@typescript-eslint'],
  extends: [
    'eslint:recommended',
    'plugin:@typescript-eslint/recommended',
    'plugin:@typescript-eslint/recommended-requiring-type-checking',
  ],
  root: true,
  env: {
    node: true,
    jest: true,
    browser: true,
  },
  globals: {
    RequestInit: 'readonly',
    ResponseInit: 'readonly',
  },
  ignorePatterns: ['.eslintrc.js', 'dist/', 'node_modules/', '*.js', 'src/generated/'],
  rules: {
    // TypeScript specific rules (from WebUI)
    '@typescript-eslint/no-unused-vars': ['error', { 
      'argsIgnorePattern': '^_',
      'destructuredArrayIgnorePattern': '^_',
      'ignoreRestSiblings': true
    }],
    '@typescript-eslint/explicit-function-return-type': 'off',
    '@typescript-eslint/no-explicit-any': 'error',
    '@typescript-eslint/no-non-null-assertion': 'warn',
    '@typescript-eslint/no-floating-promises': 'warn',
    '@typescript-eslint/no-misused-promises': 'warn',
    '@typescript-eslint/await-thenable': 'warn',
    '@typescript-eslint/no-unsafe-assignment': 'error',
    '@typescript-eslint/no-unsafe-member-access': 'error',
    '@typescript-eslint/no-unsafe-call': 'error',
    '@typescript-eslint/no-unsafe-return': 'error',
    '@typescript-eslint/no-unsafe-argument': 'error',
    '@typescript-eslint/prefer-nullish-coalescing': 'warn',
    '@typescript-eslint/prefer-optional-chain': 'warn',
    '@typescript-eslint/consistent-type-assertions': [
      'error',
      {
        'assertionStyle': 'as',
        'objectLiteralTypeAssertions': 'allow'
      }
    ],
    
    // General code quality rules
    'no-unused-vars': 'off',
    'no-case-declarations': 'error',
    'no-console': ['warn', { 'allow': ['warn', 'error'] }],
    'eqeqeq': ['error', 'always'],
    'no-debugger': 'error',
    'no-var': 'error',
    'prefer-const': 'error',
    'prefer-arrow-callback': 'error',
    'no-param-reassign': 'error',
    'no-return-await': 'error',
    'prefer-template': 'error',
    'no-duplicate-imports': 'warn',
  },
  overrides: [
    {
      files: ['**/__tests__/**/*.ts', '**/*.test.ts', '**/*.spec.ts'],
      rules: {
        '@typescript-eslint/no-explicit-any': 'off',
        '@typescript-eslint/no-unsafe-assignment': 'off',
        '@typescript-eslint/no-unsafe-member-access': 'off',
        '@typescript-eslint/no-unsafe-call': 'off',
        '@typescript-eslint/no-unsafe-return': 'off',
        '@typescript-eslint/no-unsafe-argument': 'off',
        'no-return-await': 'off',
      },
    },
  ],
};