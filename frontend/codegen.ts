import type { CodegenConfig } from '@graphql-codegen/cli';

const config: CodegenConfig = {
  overwrite: true,
  schema: '../services/ApiGateway/ApiGateway.Api/GraphQL/schema.graphql',
  generates: {
    'src/graphql/generated/enums.ts': {
      plugins: ['typescript'],
      config: {
        onlyEnums: true,
        enumsAsTypes: true,
      },
    },
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
