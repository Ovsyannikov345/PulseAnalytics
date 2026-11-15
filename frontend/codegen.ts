import type { CodegenConfig } from '@graphql-codegen/cli';

const config: CodegenConfig = {
  overwrite: true,
  schema: '../services/ApiGateway/ApiGateway.Api/GraphQL/schema.graphql',
  documents: ['src/**/*.graphql'],
  generates: {
    'src/graphql/generated/': {
      preset: 'client',
      presetConfig: {
        fragmentMasking: false,
      },
      config: {
        scalars: {
          DateTime: 'string',
          UUID: 'string',
        },
        defaultScalarType: 'unknown',
        avoidOptionals: {
          field: true,
          inputValue: false,
        },
        // Apollo adds `__typename` to every selection except the root type.
        nonOptionalTypename: true,
        skipTypeNameForRoot: true,
        useTypeImports: true,
      },
    },
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
