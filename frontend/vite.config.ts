import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

// The dev server proxies the API like the image's nginx does, so the browser
// always talks to one origin and the gateway needs no CORS.
const apiGatewayUrl = process.env.API_GATEWAY_URL ?? 'http://localhost:5103';

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/graphql': apiGatewayUrl,
      '/api': apiGatewayUrl,
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    restoreMocks: true,
    coverage: {
      provider: 'v8',
      reporter: ['text', 'lcov'],
      include: ['src/**/*.{ts,tsx}'],
      exclude: ['src/main.tsx', 'src/test/**', 'src/**/*.test.{ts,tsx}'],
    },
  },
});
