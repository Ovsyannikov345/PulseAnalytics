import type { CodegenConfig } from '@graphql-codegen/cli';

// The output is committed; CI regenerates it and fails on a difference (FRONTEND_PLAN.md, decision 2).
const config: CodegenConfig = {
  overwrite: true,
  schema: '../services/ApiGateway/ApiGateway.Api/GraphQL/schema.graphql',
  generates: {
    // Every schema enum as a string-literal union, also those no operation uses yet.
    'src/graphql/generated/enums.ts': {
      plugins: ['typescript'],
      config: {
        onlyEnums: true,
        enumsAsTypes: true,
      },
    },
    // The implementations of each interface, for Apollo's cache (fragments on `MeterReading`).
    'src/graphql/generated/possibleTypes.ts': {
      plugins: ['fragment-matcher'],
      config: {
        apolloClientVersion: 3,
      },
    },
  },
  hooks: {
    afterAllFileWrite: ['prettier --write'],
  },
};

export default config;
