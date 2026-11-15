import type { MockLink } from '@apollo/client/testing';

import type { MeterLocation, MeterType } from '../graphql/generated/enums';
import { ReadingsMatchCountsDocument } from '../graphql/generated/graphql';

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
