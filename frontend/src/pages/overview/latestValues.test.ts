import { describe, expect, it } from 'vitest';

import type { MeterLocation } from '../../graphql/generated/enums';
import { latestReadings } from '../../test/overviewFixtures';
import { summarizeLatest } from './latestValues';

const receivedAt = '2026-11-15T11:29:47Z';

describe('summarizeLatest', () => {
  it('sums the energy and averages the air quality over the locations', () => {
    const values = summarizeLatest(latestReadings(receivedAt));

    expect(values.energyTotal).toBeCloseTo(3012.52, 6);
    expect(values.averageAirQuality?.co2).toBeCloseTo(4033 / 6, 6);
    expect(values.averageAirQuality?.pm25).toBeCloseTo(18.5, 6);
    expect(values.averageAirQuality?.humidity).toBeCloseTo(272 / 6, 6);
  });

  it('lists the locations with motion in location order', () => {
    const values = summarizeLatest(latestReadings(receivedAt));

    expect(values.motionLocations).toEqual(['LIVING_ROOM', 'GARAGE']);
    expect(values.motionSensorCount).toBe(6);
  });

  it("gives every location's values in the gateway's location order", () => {
    const values = summarizeLatest(latestReadings(receivedAt));

    expect(values.locations.map((location) => location.location)).toEqual([
      'KITCHEN',
      'LIVING_ROOM',
      'BEDROOM',
      'GARAGE',
      'OFFICE',
      'CORRIDOR',
    ]);
    expect(values.locations[0]).toEqual({
      location: 'KITCHEN',
      energy: 845.62,
      airQuality: { co2: 803, pm25: 25, humidity: 36 },
      motionDetected: false,
    });
  });

  it('takes the newest receive time of all readings', () => {
    const readings = latestReadings(receivedAt).map((reading, index) =>
      index === 4 ? { ...reading, receivedAt: '2026-11-15T11:29:52Z' } : reading,
    );

    expect(summarizeLatest(readings).latestReceivedAt).toEqual(new Date('2026-11-15T11:29:52Z'));
  });

  it('compares receive times as instants, whatever their offset', () => {
    const readings = latestReadings('2026-11-15T14:29:47+03:00').map((reading, index) =>
      index === 0 ? { ...reading, receivedAt: '2026-11-15T11:29:50Z' } : reading,
    );

    expect(summarizeLatest(readings).latestReceivedAt).toEqual(new Date('2026-11-15T11:29:50Z'));
  });

  it("leaves a meter's values empty when it has no reading", () => {
    const readings = latestReadings(receivedAt).filter(
      (reading) => !(reading.location === 'GARAGE' && reading.__typename !== 'AirQualityReading'),
    );
    const values = summarizeLatest(readings);

    expect(values.locations[3]).toMatchObject({
      energy: null,
      motionDetected: null,
      airQuality: { co2: 921 },
    });
    expect(values.energyTotal).toBeCloseTo(3012.52 - 946, 6);
    expect(values.motionLocations).toEqual(['LIVING_ROOM']);
    expect(values.motionSensorCount).toBe(5);
  });

  it('skips readings of a location the app does not know yet', () => {
    const unknown = {
      __typename: 'EnergyReading' as const,
      location: 'ATTIC' as MeterLocation,
      receivedAt: '2026-11-15T11:30:30Z',
      energy: 5000,
    };
    const values = summarizeLatest([...latestReadings(receivedAt), unknown]);

    expect(values.energyTotal).toBeCloseTo(3012.52, 6);
    expect(values.latestReceivedAt).toEqual(new Date(receivedAt));
    expect(values.locations).toHaveLength(6);
  });

  it('has no values without readings', () => {
    const values = summarizeLatest([]);

    expect(values).toMatchObject({
      energyTotal: null,
      averageAirQuality: null,
      motionLocations: [],
      motionSensorCount: 0,
      latestReceivedAt: null,
    });
    expect(values.locations).toHaveLength(6);
    expect(values.locations.every((location) => location.energy === null)).toBe(true);
  });
});
