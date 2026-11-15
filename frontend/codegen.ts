import type { CodegenConfig } from '@graphql-codegen/cli';

const config: CodegenConfig = {
  overwrite: true,
  schema: '../services/ApiGateway/ApiGateway.Api/GraphQL/schema.graphql',
  documents: ['src/**/*.graphql'],
  generates: {
    // Typed documents and result types of the operations in the pages' .graphql files.
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
        // Nullable fields are `T | null`, as the gateway returns them; nullable inputs may be left out.
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
