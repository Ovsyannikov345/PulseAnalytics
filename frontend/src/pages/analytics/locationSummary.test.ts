import { describe, expect, it } from 'vitest';

import {
  toAirQualitySummary,
  toEnergySummary,
  toLocationSummary,
  toMotionSummary,
} from './locationSummary';

function metric(average: number, minimum = average, maximum = average) {
  return { __typename: 'MetricStatistics' as const, average, minimum, maximum };
}

describe('per-operation summaries', () => {
  it('keys energy by location and drops rows without one', () => {
    expect(
      toEnergySummary([
        {
          __typename: 'EnergyStatistics',
          location: 'KITCHEN',
          energy: metric(512.5, 12.19, 989.23),
        },
        { __typename: 'EnergyStatistics', location: null, energy: metric(1) },
      ]),
    ).toEqual({ KITCHEN: { average: 512.5, minimum: 12.19, maximum: 989.23 } });
  });

  it('keys air quality averages by location', () => {
    expect(
      toAirQualitySummary([
        {
          __typename: 'AirQualityStatistics',
          location: 'OFFICE',
          co2: metric(712),
          pm25: metric(22.4),
          humidity: metric(38.6),
        },
      ]),
    ).toEqual({ OFFICE: { co2: 712, pm25: 22.4, humidity: 38.6 } });
  });

  it('keys motion activity and samples by location', () => {
    expect(
      toMotionSummary([
        { __typename: 'MotionStatistics', location: 'GARAGE', samples: 4435, activityRatio: 0.42 },
      ]),
    ).toEqual({ GARAGE: { activityRatio: 0.42, samples: 4435 } });
  });
});

describe('toLocationSummary', () => {
  it('joins the operations per location, null where a location or an operation has nothing', () => {
    const rows = toLocationSummary(['KITCHEN', 'BEDROOM'], {
      energy: { KITCHEN: { average: 500, minimum: 10, maximum: 990 } },
      airQuality: null,
      motion: {
        KITCHEN: { activityRatio: 0.5, samples: 100 },
        BEDROOM: { activityRatio: 0, samples: 90 },
      },
    });

    expect(rows).toEqual([
      {
        location: 'KITCHEN',
        energy: { average: 500, minimum: 10, maximum: 990 },
        airQuality: null,
        motion: { activityRatio: 0.5, samples: 100 },
      },
      {
        location: 'BEDROOM',
        energy: null,
        airQuality: null,
        motion: { activityRatio: 0, samples: 90 },
      },
    ]);
  });
});
