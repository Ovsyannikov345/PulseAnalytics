import { describe, expect, it } from 'vitest';

import type { MeterLocation } from '../../graphql/generated/enums';
import { toMotionActivity } from './motionActivity';

function row(location: MeterLocation, bucketStart: string, samples: number, motionSamples: number) {
  return {
    __typename: 'MotionStatistics' as const,
    location,
    bucketStart,
    samples,
    motionSamples,
    activityRatio: motionSamples / samples,
  };
}

const range = { from: new Date('2026-11-15T10:00:00Z'), to: new Date('2026-11-15T12:00:00Z') };

describe('toMotionActivity', () => {
  it('gives every location a row with a cell per bucket', () => {
    const activity = toMotionActivity(
      [
        row('KITCHEN', '2026-11-15T10:00:00Z', 240, 60),
        row('OFFICE', '2026-11-15T11:00:00Z', 240, 240),
      ],
      range,
      'HOUR',
      null,
    );

    expect(activity.range).toBe(range);
    expect(activity.bucket).toBe('HOUR');
    expect(activity.rows.map((each) => each.location)).toEqual([
      'KITCHEN',
      'LIVING_ROOM',
      'BEDROOM',
      'GARAGE',
      'OFFICE',
      'CORRIDOR',
    ]);
    expect(activity.rows[0]?.cells).toEqual([
      {
        start: new Date('2026-11-15T10:00:00Z'),
        stats: { samples: 240, motionSamples: 60, activityRatio: 0.25 },
      },
      { start: new Date('2026-11-15T11:00:00Z'), stats: null },
    ]);
    expect(activity.rows[4]?.cells[1]?.stats?.activityRatio).toBe(1);
  });

  it("gives each row its share of samples with motion, weighted by the buckets' samples", () => {
    const activity = toMotionActivity(
      [
        row('GARAGE', '2026-11-15T10:00:00Z', 100, 10),
        row('GARAGE', '2026-11-15T11:00:00Z', 300, 150),
      ],
      range,
      'HOUR',
      'GARAGE',
    );

    expect(activity.rows).toHaveLength(1);
    expect(activity.rows[0]?.overallRatio).toBe(0.4);
  });

  it('has no share for a location without samples', () => {
    const activity = toMotionActivity([], range, 'HOUR', 'BEDROOM');

    expect(activity.rows[0]?.overallRatio).toBeNull();
  });
});
