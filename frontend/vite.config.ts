import react from '@vitejs/plugin-react';
import { searchForWorkspaceRoot } from 'vite';
import { defineConfig } from 'vitest/config';

// The dev server proxies the API and the readings hub like the image's nginx does
const apiGatewayUrl = process.env.API_GATEWAY_URL ?? 'http://localhost:5103';
const notificationServiceUrl = process.env.NOTIFICATION_SERVICE_URL ?? 'http://localhost:5104';

export default defineConfig({
  plugins: [react()],
  build: {
    rolldownOptions: {
      output: {
        // Libraries change less often than the app, so they get their own long-cached chunks.
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
            {
              name: 'signalr',
              test: /[\\/]node_modules[\\/]@microsoft[\\/]signalr[\\/]/,
            },
            // Catch-all last: on equal priority the earlier group wins.
            { name: 'vendor', test: /[\\/]node_modules[\\/]/ },
          ],
        },
      },
    },
  },
  server: {
    fs: {
      allow: [
        searchForWorkspaceRoot(process.cwd()),
        // The readings hub's message examples, which the parser tests read
        '../services/NotificationService/tests/NotificationService.UnitTests/Api/Hubs/Readings/Snapshots',
      ],
    },
    proxy: {
      '/graphql': apiGatewayUrl,
      '/api': apiGatewayUrl,
      '/hubs': { target: notificationServiceUrl, ws: true },
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
