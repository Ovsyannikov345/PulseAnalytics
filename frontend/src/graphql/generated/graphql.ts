/* eslint-disable */
/** Internal type. DO NOT USE DIRECTLY. */
type Exact<T extends { [key: string]: unknown }> = { [K in keyof T]: T[K] };
/** Internal type. DO NOT USE DIRECTLY. */
export type Incremental<T> =
  T | { [P in keyof T]?: P extends ' $fragmentName' | '__typename' ? T[P] : never };
import type { TypedDocumentNode as DocumentNode } from '@graphql-typed-document-node/core';
export type MeterLocation =
  'BEDROOM' | 'CORRIDOR' | 'GARAGE' | 'KITCHEN' | 'LIVING_ROOM' | 'OFFICE';

export type TimeBucket = 'DAY' | 'FIFTEEN_MINUTES' | 'FIVE_MINUTES' | 'HOUR' | 'MINUTE';

export type OverviewEnergySparklinesQueryVariables = Exact<{
  from: string;
  to: string;
  bucket: TimeBucket;
}>;

export type OverviewEnergySparklinesQuery = {
  energyStatistics: Array<{
    __typename: 'EnergyStatistics';
    location: MeterLocation | null;
    bucketStart: string | null;
    energy: { __typename: 'MetricStatistics'; average: number };
  }>;
};

export type OverviewEnergyTrendQueryVariables = Exact<{
  from: string;
  to: string;
  bucket: TimeBucket;
}>;

export type OverviewEnergyTrendQuery = {
  energyStatistics: Array<{
    __typename: 'EnergyStatistics';
    bucketStart: string | null;
    samples: number;
    energy: { __typename: 'MetricStatistics'; average: number; minimum: number; maximum: number };
  }>;
};

export type OverviewIngestionCoverageQueryVariables = Exact<{
  from: string;
  to: string;
  bucket: TimeBucket;
}>;

export type OverviewIngestionCoverageQuery = {
  motionStatistics: Array<{
    __typename: 'MotionStatistics';
    bucketStart: string | null;
    samples: number;
  }>;
};

export type OverviewLatestReadingsQueryVariables = Exact<{ [key: string]: never }>;

export type OverviewLatestReadingsQuery = {
  latestReadings: Array<
    | {
        __typename: 'AirQualityReading';
        co2: number;
        pm25: number;
        humidity: number;
        location: MeterLocation;
        receivedAt: string;
      }
    | { __typename: 'EnergyReading'; energy: number; location: MeterLocation; receivedAt: string }
    | {
        __typename: 'MotionReading';
        motionDetected: boolean;
        location: MeterLocation;
        receivedAt: string;
      }
  >;
};

export type OverviewSampleCountQueryVariables = Exact<{
  from: string;
  to: string;
}>;

export type OverviewSampleCountQuery = {
  readingCounts: Array<{ __typename: 'ReadingCount'; count: number }>;
};

export const OverviewEnergySparklinesDocument = {
  kind: 'Document',
  definitions: [
    {
      kind: 'OperationDefinition',
      operation: 'query',
      name: { kind: 'Name', value: 'OverviewEnergySparklines' },
      variableDefinitions: [
        {
          kind: 'VariableDefinition',
          variable: { kind: 'Variable', name: { kind: 'Name', value: 'from' } },
          type: {
            kind: 'NonNullType',
            type: { kind: 'NamedType', name: { kind: 'Name', value: 'DateTime' } },
          },
        },
        {
          kind: 'VariableDefinition',
          variable: { kind: 'Variable', name: { kind: 'Name', value: 'to' } },
          type: {
            kind: 'NonNullType',
            type: { kind: 'NamedType', name: { kind: 'Name', value: 'DateTime' } },
          },
        },
        {
          kind: 'VariableDefinition',
          variable: { kind: 'Variable', name: { kind: 'Name', value: 'bucket' } },
          type: {
            kind: 'NonNullType',
            type: { kind: 'NamedType', name: { kind: 'Name', value: 'TimeBucket' } },
          },
        },
      ],
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          {
            kind: 'Field',
            name: { kind: 'Name', value: 'energyStatistics' },
            arguments: [
              {
                kind: 'Argument',
                name: { kind: 'Name', value: 'filter' },
                value: {
                  kind: 'ObjectValue',
                  fields: [
                    {
                      kind: 'ObjectField',
                      name: { kind: 'Name', value: 'from' },
                      value: { kind: 'Variable', name: { kind: 'Name', value: 'from' } },
                    },
                    {
                      kind: 'ObjectField',
                      name: { kind: 'Name', value: 'to' },
                      value: { kind: 'Variable', name: { kind: 'Name', value: 'to' } },
                    },
                  ],
                },
              },
              {
                kind: 'Argument',
                name: { kind: 'Name', value: 'groupBy' },
                value: {
                  kind: 'ListValue',
                  values: [
                    { kind: 'EnumValue', value: 'LOCATION' },
                    { kind: 'EnumValue', value: 'TIME' },
                  ],
                },
              },
              {
                kind: 'Argument',
                name: { kind: 'Name', value: 'bucket' },
                value: { kind: 'Variable', name: { kind: 'Name', value: 'bucket' } },
              },
            ],
            selectionSet: {
              kind: 'SelectionSet',
              selections: [
                { kind: 'Field', name: { kind: 'Name', value: 'location' } },
                { kind: 'Field', name: { kind: 'Name', value: 'bucketStart' } },
                {
                  kind: 'Field',
                  name: { kind: 'Name', value: 'energy' },
                  selectionSet: {
                    kind: 'SelectionSet',
                    selections: [{ kind: 'Field', name: { kind: 'Name', value: 'average' } }],
                  },
                },
              ],
            },
          },
        ],
      },
    },
  ],
} as unknown as DocumentNode<OverviewEnergySparklinesQuery, OverviewEnergySparklinesQueryVariables>;
export const OverviewEnergyTrendDocument = {
  kind: 'Document',
  definitions: [
    {
      kind: 'OperationDefinition',
      operation: 'query',
      name: { kind: 'Name', value: 'OverviewEnergyTrend' },
      variableDefinitions: [
        {
          kind: 'VariableDefinition',
          variable: { kind: 'Variable', name: { kind: 'Name', value: 'from' } },
          type: {
            kind: 'NonNullType',
            type: { kind: 'NamedType', name: { kind: 'Name', value: 'DateTime' } },
          },
        },
        {
          kind: 'VariableDefinition',
          variable: { kind: 'Variable', name: { kind: 'Name', value: 'to' } },
          type: {
            kind: 'NonNullType',
            type: { kind: 'NamedType', name: { kind: 'Name', value: 'DateTime' } },
          },
        },
        {
          kind: 'VariableDefinition',
          variable: { kind: 'Variable', name: { kind: 'Name', value: 'bucket' } },
          type: {
            kind: 'NonNullType',
            type: { kind: 'NamedType', name: { kind: 'Name', value: 'TimeBucket' } },
          },
        },
      ],
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          {
            kind: 'Field',
            name: { kind: 'Name', value: 'energyStatistics' },
            arguments: [
              {
                kind: 'Argument',
                name: { kind: 'Name', value: 'filter' },
                value: {
                  kind: 'ObjectValue',
                  fields: [
                    {
                      kind: 'ObjectField',
                      name: { kind: 'Name', value: 'from' },
                      value: { kind: 'Variable', name: { kind: 'Name', value: 'from' } },
                    },
                    {
                      kind: 'ObjectField',
                      name: { kind: 'Name', value: 'to' },
                      value: { kind: 'Variable', name: { kind: 'Name', value: 'to' } },
                    },
                  ],
                },
              },
              {
                kind: 'Argument',
                name: { kind: 'Name', value: 'groupBy' },
                value: { kind: 'ListValue', values: [{ kind: 'EnumValue', value: 'TIME' }] },
              },
              {
                kind: 'Argument',
                name: { kind: 'Name', value: 'bucket' },
                value: { kind: 'Variable', name: { kind: 'Name', value: 'bucket' } },
              },
            ],
            selectionSet: {
              kind: 'SelectionSet',
              selections: [
                { kind: 'Field', name: { kind: 'Name', value: 'bucketStart' } },
                { kind: 'Field', name: { kind: 'Name', value: 'samples' } },
                {
                  kind: 'Field',
                  name: { kind: 'Name', value: 'energy' },
                  selectionSet: {
                    kind: 'SelectionSet',
                    selections: [
                      { kind: 'Field', name: { kind: 'Name', value: 'average' } },
                      { kind: 'Field', name: { kind: 'Name', value: 'minimum' } },
                      { kind: 'Field', name: { kind: 'Name', value: 'maximum' } },
                    ],
                  },
                },
              ],
            },
          },
        ],
      },
    },
  ],
} as unknown as DocumentNode<OverviewEnergyTrendQuery, OverviewEnergyTrendQueryVariables>;
export const OverviewIngestionCoverageDocument = {
  kind: 'Document',
  definitions: [
    {
      kind: 'OperationDefinition',
      operation: 'query',
      name: { kind: 'Name', value: 'OverviewIngestionCoverage' },
      variableDefinitions: [
        {
          kind: 'VariableDefinition',
          variable: { kind: 'Variable', name: { kind: 'Name', value: 'from' } },
          type: {
            kind: 'NonNullType',
            type: { kind: 'NamedType', name: { kind: 'Name', value: 'DateTime' } },
          },
        },
        {
          kind: 'VariableDefinition',
          variable: { kind: 'Variable', name: { kind: 'Name', value: 'to' } },
          type: {
            kind: 'NonNullType',
            type: { kind: 'NamedType', name: { kind: 'Name', value: 'DateTime' } },
          },
        },
        {
          kind: 'VariableDefinition',
          variable: { kind: 'Variable', name: { kind: 'Name', value: 'bucket' } },
          type: {
            kind: 'NonNullType',
            type: { kind: 'NamedType', name: { kind: 'Name', value: 'TimeBucket' } },
          },
        },
      ],
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          {
            kind: 'Field',
            name: { kind: 'Name', value: 'motionStatistics' },
            arguments: [
              {
                kind: 'Argument',
                name: { kind: 'Name', value: 'filter' },
                value: {
                  kind: 'ObjectValue',
                  fields: [
                    {
                      kind: 'ObjectField',
                      name: { kind: 'Name', value: 'from' },
                      value: { kind: 'Variable', name: { kind: 'Name', value: 'from' } },
                    },
                    {
                      kind: 'ObjectField',
                      name: { kind: 'Name', value: 'to' },
                      value: { kind: 'Variable', name: { kind: 'Name', value: 'to' } },
                    },
                  ],
                },
              },
              {
                kind: 'Argument',
                name: { kind: 'Name', value: 'groupBy' },
                value: { kind: 'ListValue', values: [{ kind: 'EnumValue', value: 'TIME' }] },
              },
              {
                kind: 'Argument',
                name: { kind: 'Name', value: 'bucket' },
                value: { kind: 'Variable', name: { kind: 'Name', value: 'bucket' } },
              },
            ],
            selectionSet: {
              kind: 'SelectionSet',
              selections: [
                { kind: 'Field', name: { kind: 'Name', value: 'bucketStart' } },
                { kind: 'Field', name: { kind: 'Name', value: 'samples' } },
              ],
            },
          },
        ],
      },
    },
  ],
} as unknown as DocumentNode<
  OverviewIngestionCoverageQuery,
  OverviewIngestionCoverageQueryVariables
>;
export const OverviewLatestReadingsDocument = {
  kind: 'Document',
  definitions: [
    {
      kind: 'OperationDefinition',
      operation: 'query',
      name: { kind: 'Name', value: 'OverviewLatestReadings' },
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          {
            kind: 'Field',
            name: { kind: 'Name', value: 'latestReadings' },
            selectionSet: {
              kind: 'SelectionSet',
              selections: [
                { kind: 'Field', name: { kind: 'Name', value: 'location' } },
                { kind: 'Field', name: { kind: 'Name', value: 'receivedAt' } },
                {
                  kind: 'InlineFragment',
                  typeCondition: {
                    kind: 'NamedType',
                    name: { kind: 'Name', value: 'EnergyReading' },
                  },
                  selectionSet: {
                    kind: 'SelectionSet',
                    selections: [{ kind: 'Field', name: { kind: 'Name', value: 'energy' } }],
                  },
                },
                {
                  kind: 'InlineFragment',
                  typeCondition: {
                    kind: 'NamedType',
                    name: { kind: 'Name', value: 'AirQualityReading' },
                  },
                  selectionSet: {
                    kind: 'SelectionSet',
                    selections: [
                      { kind: 'Field', name: { kind: 'Name', value: 'co2' } },
                      { kind: 'Field', name: { kind: 'Name', value: 'pm25' } },
                      { kind: 'Field', name: { kind: 'Name', value: 'humidity' } },
                    ],
                  },
                },
                {
                  kind: 'InlineFragment',
                  typeCondition: {
                    kind: 'NamedType',
                    name: { kind: 'Name', value: 'MotionReading' },
                  },
                  selectionSet: {
                    kind: 'SelectionSet',
                    selections: [
                      { kind: 'Field', name: { kind: 'Name', value: 'motionDetected' } },
                    ],
                  },
                },
              ],
            },
          },
        ],
      },
    },
  ],
} as unknown as DocumentNode<OverviewLatestReadingsQuery, OverviewLatestReadingsQueryVariables>;
export const OverviewSampleCountDocument = {
  kind: 'Document',
  definitions: [
    {
      kind: 'OperationDefinition',
      operation: 'query',
      name: { kind: 'Name', value: 'OverviewSampleCount' },
      variableDefinitions: [
        {
          kind: 'VariableDefinition',
          variable: { kind: 'Variable', name: { kind: 'Name', value: 'from' } },
          type: {
            kind: 'NonNullType',
            type: { kind: 'NamedType', name: { kind: 'Name', value: 'DateTime' } },
          },
        },
        {
          kind: 'VariableDefinition',
          variable: { kind: 'Variable', name: { kind: 'Name', value: 'to' } },
          type: {
            kind: 'NonNullType',
            type: { kind: 'NamedType', name: { kind: 'Name', value: 'DateTime' } },
          },
        },
      ],
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          {
            kind: 'Field',
            name: { kind: 'Name', value: 'readingCounts' },
            arguments: [
              {
                kind: 'Argument',
                name: { kind: 'Name', value: 'filter' },
                value: {
                  kind: 'ObjectValue',
                  fields: [
                    {
                      kind: 'ObjectField',
                      name: { kind: 'Name', value: 'from' },
                      value: { kind: 'Variable', name: { kind: 'Name', value: 'from' } },
                    },
                    {
                      kind: 'ObjectField',
                      name: { kind: 'Name', value: 'to' },
                      value: { kind: 'Variable', name: { kind: 'Name', value: 'to' } },
                    },
                  ],
                },
              },
              {
                kind: 'Argument',
                name: { kind: 'Name', value: 'groupBy' },
                value: { kind: 'ListValue', values: [] },
              },
            ],
            selectionSet: {
              kind: 'SelectionSet',
              selections: [{ kind: 'Field', name: { kind: 'Name', value: 'count' } }],
            },
          },
        ],
      },
    },
  ],
} as unknown as DocumentNode<OverviewSampleCountQuery, OverviewSampleCountQueryVariables>;
