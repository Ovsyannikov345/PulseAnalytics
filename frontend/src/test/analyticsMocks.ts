import type { MockLink } from '@apollo/client/testing';

import type { MeterLocation, TimeBucket } from '../graphql/generated/enums';
import { AnalyticsAirQualityDocument, AnalyticsEnergyDocument } from '../graphql/generated/graphql';

export interface StatisticsVariables {
  from: string;
  to: string;
  locations: MeterLocation[];
}

export interface BucketedVariables extends StatisticsVariables {
  bucket: TimeBucket;
}

function metric(average: number, minimum: number, maximum: number) {
  return { __typename: 'MetricStatistics', average, minimum, maximum };
}

export function energyMock(
  variables: BucketedVariables,
  rows: { location: MeterLocation; bucketStart: string; average: number; samples?: number }[],
): MockLink.MockedResponse {
  return {
    request: { query: AnalyticsEnergyDocument, variables },
    result: {
      data: {
        energyStatistics: rows.map(({ location, bucketStart, average, samples = 60 }) => ({
          __typename: 'EnergyStatistics',
          location,
          bucketStart,
          samples,
          energy: metric(average, average - 100, average + 100),
        })),
      },
    },
  };
}

export function airQualityMock(
  variables: BucketedVariables,
  rows: { bucketStart: string; co2: number; pm25: number; humidity: number; samples?: number }[],
): MockLink.MockedResponse {
  return {
    request: { query: AnalyticsAirQualityDocument, variables },
    result: {
      data: {
        airQualityStatistics: rows.map(({ bucketStart, co2, pm25, humidity, samples = 360 }) => ({
          __typename: 'AirQualityStatistics',
          bucketStart,
          samples,
          co2: metric(co2, co2 - 200, co2 + 200),
          pm25: metric(pm25, 0, pm25 * 2),
          humidity: metric(humidity, humidity - 10, humidity + 10),
        })),
      },
    },
  };
}

/** The gateway's answer to a filter it rejects: HTTP 200, `data: null` and a `BAD_REQUEST` error. */
export function rejected(mock: MockLink.MockedResponse): MockLink.MockedResponse {
  return {
    request: mock.request,
    result: {
      data: null,
      errors: [{ message: 'The range is too long.', extensions: { code: 'BAD_REQUEST' } }],
    },
  };
}
