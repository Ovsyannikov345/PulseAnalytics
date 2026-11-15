import { describe, expect, it } from 'vitest';

import type { MeterLocation } from '../../graphql/generated/enums';
import { toEnergySparklines } from './energySparklines';

function row(location: MeterLocation, bucketStart: string, average: number) {
  return {
    __typename: 'EnergyStatistics' as const,
    location,
    bucketStart,
    energy: { __typename: 'MetricStatistics' as const, average },
  };
}

// 10:02–10:17 touches the buckets starting 10:00, 10:05, 10:10 and 10:15.
const range = { from: new Date('2026-11-15T10:02:00Z'), to: new Date('2026-11-15T10:17:00Z') };

describe('toEnergySparklines', () => {
  it("puts each location's averages on the bucket grid, with gaps where it had no samples", () => {
    const sparklines = toEnergySparklines(
      [
        row('KITCHEN', '2026-11-15T10:00:00Z', 400),
        row('KITCHEN', '2026-11-15T10:10:00Z', 420.5),
        row('KITCHEN', '2026-11-15T10:15:00Z', 410),
        row('GARAGE', '2026-11-15T10:05:00Z', 900),
      ],
      range,
      'FIVE_MINUTES',
    );

    expect(sparklines.values.get('KITCHEN')).toEqual([400, null, 420.5, 410]);
    expect(sparklines.values.get('GARAGE')).toEqual([null, 900, null, null]);
    expect(sparklines.values.get('OFFICE')).toEqual([null, null, null, null]);
    expect(sparklines.values.size).toBe(6);
  });

  it('shares the highest value of all locations as the scale', () => {
    const sparklines = toEnergySparklines(
      [row('KITCHEN', '2026-11-15T10:00:00Z', 400), row('GARAGE', '2026-11-15T10:05:00Z', 900)],
      range,
      'FIVE_MINUTES',
    );

    expect(sparklines.max).toBe(900);
  });

  it('has a scale of 0 without rows', () => {
    expect(toEnergySparklines([], range, 'FIVE_MINUTES').max).toBe(0);
  });
});
