import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./tests/setup.js'],
    coverage: {
      provider: 'v8',
      include: ['wwwroot/js/**/*.js'],
      reporter: ['text', 'cobertura', 'html', 'json-summary'],
      thresholds: {
        lines: 80,
        statements: 80,
        branches: 75,
        functions: 80
      }
    }
  }
});
