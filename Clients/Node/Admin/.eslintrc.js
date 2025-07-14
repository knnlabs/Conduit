module.exports = {
  parser: '@typescript-eslint/parser',
  parserOptions: {
    ecmaVersion: 2022,
    sourceType: 'module',
  },
  plugins: ['@typescript-eslint'],
  extends: [
    'eslint:recommended',
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
  ignorePatterns: ['.eslintrc.js', 'dist/', 'node_modules/', '*.js'],
  rules: {
    'no-unused-vars': 'off',
    '@typescript-eslint/no-unused-vars': ['error', { 
      'argsIgnorePattern': '^_',
      'destructuredArrayIgnorePattern': '^_',
      'ignoreRestSiblings': true
    }],
    'no-case-declarations': 'error',
  },
};