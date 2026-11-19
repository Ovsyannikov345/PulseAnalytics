import type { MockLink } from '@apollo/client/testing';

import type { MeterLocation } from '../graphql/generated/enums';
import { LiveLatestReadingsDocument } from '../graphql/generated/graphql';
import { meterLocationRestName } from '../domain/meters';
import type { LatestReadingRow } from '../pages/live/latestReadings';
import { mockupSamples } from './overviewFixtures';

type Samples = Partial<
  Record<
    MeterLocation,
    Partial<{
      energy: number;
      co2: number;
      pm25: number;
      humidity: number;
      motionDetected: boolean;
    }>
  >
>;

/** The Live page's 18 stored readings, all received at `receivedAt`, with the mockup's values. */
export function liveLatestRows(receivedAt: string, samples: Samples = {}): LatestReadingRow[] {
  const locations = Object.keys(mockupSamples) as MeterLocation[];
  const sample = (location: MeterLocation) => ({
    ...mockupSamples[location],
    ...samples[location],
  });
  const id = (type: string, location: MeterLocation) => `${type}-${location}-${receivedAt}`;

  return [
    ...locations.map((location): LatestReadingRow => {
      const { co2, pm25, humidity } = sample(location);
      return {
        __typename: 'AirQualityReading',
        id: id('air-quality', location),
        location,
        receivedAt,
        co2,
        pm25,
        humidity,
      };
    }),
    ...locations.map((location): LatestReadingRow => ({
      __typename: 'EnergyReading',
      id: id('energy', location),
      location,
      receivedAt,
      energy: sample(location).energy,
    })),
    ...locations.map((location): LatestReadingRow => ({
      __typename: 'MotionReading',
      id: id('motion', location),
      location,
      receivedAt,
      motionDetected: sample(location).motionDetected,
    })),
  ];
}

export function liveLatestReadingsMock(rows: LatestReadingRow[]): MockLink.MockedResponse {
  return {
    request: { query: LiveLatestReadingsDocument },
    result: { data: { latestReadings: rows } },
  };
}

/** A `ReadingStored` message as the hub sends it (C# enum names). */
export function hubMessage(
  type: 'Motion' | 'Energy' | 'AirQuality',
  location: MeterLocation,
  receivedAt: string,
  values: Record<string, number | boolean>,
) {
  return {
    type,
    id: `pushed-${type}-${location}-${receivedAt}`,
    location: meterLocationRestName(location),
    receivedAt,
    ...values,
  };
}
