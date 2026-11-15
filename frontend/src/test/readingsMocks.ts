import type { MockLink } from '@apollo/client/testing';

import type { MeterLocation, MeterType } from '../graphql/generated/enums';
import { ReadingsMatchCountsDocument, ReadingsTableDocument } from '../graphql/generated/graphql';

export interface ReadingFilterVariables {
  types: MeterType[];
  locations: MeterLocation[];
  from: string;
  to: string;
}

/** The page's default filter at 14:29:47 in Moscow: every meter, 13:30–14:30. */
export const defaultFilterVariables: ReadingFilterVariables = {
  types: ['AIR_QUALITY', 'ENERGY', 'MOTION'],
  locations: [],
  from: '2026-11-15T13:30:00.000+03:00',
  to: '2026-11-15T14:30:00.000+03:00',
};

export function matchCountsMock(
  filter: ReadingFilterVariables,
  counts: Partial<Record<MeterType, number>>,
): MockLink.MockedResponse {
  return {
    request: { query: ReadingsMatchCountsDocument, variables: { filter } },
    result: {
      data: {
        readingCounts: Object.entries(counts).map(([type, count]) => ({
          __typename: 'ReadingCount',
          type,
          count,
        })),
      },
    },
  };
}

/** A request that fails as if the gateway were unreachable. */
export function failed(mock: MockLink.MockedResponse): MockLink.MockedResponse {
  return { request: mock.request, error: new Error('Failed to fetch') };
}

/** The gateway's answer to a filter it rejects: HTTP 200, `data: null` and a `BAD_REQUEST` error. */
export function rejected(mock: MockLink.MockedResponse): MockLink.MockedResponse {
  return {
    request: mock.request,
    result: {
      data: null,
      errors: [
        { message: "'From' must be earlier than 'To'.", extensions: { code: 'BAD_REQUEST' } },
      ],
    },
  };
}

const readingTypes = {
  AirQualityReading: 'AIR_QUALITY',
  EnergyReading: 'ENERGY',
  MotionReading: 'MOTION',
} as const;

type ReadingValues =
  | { __typename: 'AirQualityReading'; co2: number; pm25: number; humidity: number }
  | { __typename: 'EnergyReading'; energy: number }
  | { __typename: 'MotionReading'; motionDetected: boolean };

/** A reading as the table selects it; the id doubles as its cursor. */
export function reading(
  id: string,
  receivedAt: string,
  location: MeterLocation,
  values: ReadingValues,
) {
  return { id, type: readingTypes[values.__typename], location, receivedAt, ...values };
}

export type MockReading = ReturnType<typeof reading>;

/** Energy readings `first`…`last`, newest first, received a second apart before 14:29:47 Moscow time. */
export function energyReadings(first: number, last: number): MockReading[] {
  return Array.from({ length: last - first + 1 }, (_, index) =>
    reading(
      `e${String(first + index)}`,
      new Date(Date.UTC(2026, 10, 15, 11, 29, 47 - first - index)).toISOString(),
      'KITCHEN',
      { __typename: 'EnergyReading', energy: first + index },
    ),
  );
}

interface PageRequest {
  first?: number;
  after?: string;
}

/** One page of the readings table; `hasNextPage` says whether more pages follow. */
export function readingsMock(
  filter: ReadingFilterVariables,
  readings: MockReading[],
  { first = 25, after }: PageRequest = {},
  hasNextPage = false,
): MockLink.MockedResponse {
  return {
    request: {
      query: ReadingsTableDocument,
      variables: after === undefined ? { filter, first } : { filter, first, after },
    },
    result: {
      data: {
        readings: {
          __typename: 'MeterReadingConnection',
          edges: readings.map((node) => ({
            __typename: 'MeterReadingEdge',
            cursor: node.id,
            node,
          })),
          pageInfo: {
            __typename: 'PageInfo',
            hasNextPage,
            endCursor: readings.at(-1)?.id ?? null,
          },
        },
      },
    },
  };
}
