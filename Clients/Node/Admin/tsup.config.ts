import { defineConfig } from 'tsup';

export default defineConfig({
  entry: {
    index: 'src/index.ts',
    nextjs: 'src/nextjs/index.ts',
    'react-query': 'src/react-query/index.ts',
  },
  format: ['cjs', 'esm'],
  dts: true,
  sourcemap: true,
  clean: true,
  minify: false,
  splitting: false,
  shims: true,
  external: ['next', 'react', '@tanstack/react-query'],
});