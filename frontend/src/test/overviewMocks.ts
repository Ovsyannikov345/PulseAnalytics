import type { MockLink } from '@apollo/client/testing';

import type { MeterLocation } from '../graphql/generated/enums';
import {
  OverviewEnergySparklinesDocument,
  OverviewLatestReadingsDocument,
  OverviewSampleCountDocument,
} from '../graphql/generated/graphql';
import type { LatestReading } from '../pages/overview/latestValues';

/** The Overview's mocked GraphQL responses, by operation. Variables must match exactly. */

export function latestReadingsMock(readings: LatestReading[]): MockLink.MockedResponse {
  return {
    request: { query: OverviewLatestReadingsDocument },
    result: { data: { latestReadings: readings } },
  };
}

export function sampleCountMock(
  variables: { from: string; to: string },
  count: number,
): MockLink.MockedResponse {
  return {
    request: { query: OverviewSampleCountDocument, variables },
    result: { data: { readingCounts: [{ __typename: 'ReadingCount', count }] } },
  };
}

export function energySparklinesMock(
  variables: { from: string; to: string },
  rows: { location: MeterLocation; bucketStart: string; average: number }[],
): MockLink.MockedResponse {
  return {
    request: {
      query: OverviewEnergySparklinesDocument,
      variables: { ...variables, bucket: 'FIVE_MINUTES' },
    },
    result: {
      data: {
        energyStatistics: rows.map(({ location, bucketStart, average }) => ({
          __typename: 'EnergyStatistics',
          location,
          bucketStart,
          energy: { __typename: 'MetricStatistics', average },
        })),
      },
    },
  };
}

/** The same request failing with a network error. */
export function failing(mock: MockLink.MockedResponse): MockLink.MockedResponse {
  return { request: mock.request, error: new Error('Failed to fetch') };
}
