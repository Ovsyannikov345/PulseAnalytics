import { describe, expect, it } from 'vitest';

import type { MeterLocation } from '../../graphql/generated/enums';
import { combineEnergy, toEnergySeries } from './energySeries';

function row(
  location: MeterLocation | null,
  bucketStart: string | null,
  average: number,
  samples: number,
) {
  return {
    __typename: 'EnergyStatistics' as const,
    location,
    bucketStart,
    samples,
    energy: {
      __typename: 'MetricStatistics' as const,
      average,
      minimum: average - 10,
      maximum: average + 10,
    },
  };
}

describe('combineEnergy', () => {
  it('weights the averages by samples and takes the extremes', () => {
    expect(
      combineEnergy([
        { average: 100, minimum: 10, maximum: 200, samples: 1 },
        { average: 400, minimum: 50, maximum: 900, samples: 3 },
      ]),
    ).toEqual({ average: 325, minimum: 10, maximum: 900, samples: 4 });
  });

  it('is null without groups', () => {
    expect(combineEnergy([])).toBeNull();
  });
});

describe('toEnergySeries', () => {
  const range = { from: new Date('2026-11-15T10:00:00Z'), to: new Date('2026-11-15T10:45:00Z') };

  it('puts every location of a bucket on one point and combines them', () => {
    const series = toEnergySeries(
      [
        row('KITCHEN', '2026-11-15T10:00:00Z', 100, 60),
        row('OFFICE', '2026-11-15T10:00:00Z', 400, 20),
        row('KITCHEN', '2026-11-15T10:30:00Z', 200, 60),
      ],
      range,
      'FIFTEEN_MINUTES',
      null,
    );

    expect(series.range).toBe(range);
    expect(series.bucket).toBe('FIFTEEN_MINUTES');
    expect(series.location).toBeNull();
    expect(series.points).toEqual([
      {
        time: Date.parse('2026-11-15T10:00:00Z'),
        byLocation: {
          KITCHEN: { average: 100, minimum: 90, maximum: 110, samples: 60 },
          OFFICE: { average: 400, minimum: 390, maximum: 410, samples: 20 },
        },
        combined: { average: 175, minimum: 90, maximum: 410, samples: 80 },
      },
      { time: Date.parse('2026-11-15T10:15:00Z'), byLocation: {}, combined: null },
      {
        time: Date.parse('2026-11-15T10:30:00Z'),
        byLocation: { KITCHEN: { average: 200, minimum: 190, maximum: 210, samples: 60 } },
        combined: { average: 200, minimum: 190, maximum: 210, samples: 60 },
      },
    ]);
  });

  it('keeps the selected location and drops rows without one', () => {
    const series = toEnergySeries(
      [row(null, '2026-11-15T10:00:00Z', 100, 60)],
      range,
      'FIFTEEN_MINUTES',
      'GARAGE',
    );

    expect(series.location).toBe('GARAGE');
    expect(series.points.every((point) => point.combined === null)).toBe(true);
  });
});
