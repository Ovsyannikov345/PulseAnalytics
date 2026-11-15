import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

// The dev server proxies the API like the image's nginx does
const apiGatewayUrl = process.env.API_GATEWAY_URL ?? 'http://localhost:5103';

export default defineConfig({
  plugins: [react()],
  build: {
    rolldownOptions: {
      output: {
        // Libraries in chunks of their own: they change less often than the app, so browsers keep them
        // cached across releases, and no chunk passes Vite's 500 kB warning.
        codeSplitting: {
          groups: [
            {
              name: 'react',
              test: /[\\/]node_modules[\\/](react|react-dom|react-router|scheduler|cookie|set-cookie-parser)[\\/]/,
            },
            {
              name: 'apollo',
              test: /[\\/]node_modules[\\/](@apollo|@wry|graphql|optimism|rxjs|tslib|@graphql-typed-document-node)[\\/]/,
            },
          ],
        },
      },
    },
  },
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
    unstubGlobals: true,
    unstubEnvs: true,
    coverage: {
      provider: 'v8',
      reporter: ['text', 'lcov'],
      include: ['src/**/*.{ts,tsx}'],
      exclude: [
        'src/main.tsx',
        'src/test/**',
        'src/**/*.test.{ts,tsx}',
        'src/graphql/generated/**',
      ],
    },
  },
});
