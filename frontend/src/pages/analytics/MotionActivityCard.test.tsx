import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { MeterLocation } from '../../graphql/generated/enums';
import type { WidgetQuery } from '../../graphql/widgetQuery';
import { toMotionActivity, type MotionActivity } from './motionActivity';
import { MotionActivityCard } from './MotionActivityCard';

// 15 Nov 14:00–15:00 in Moscow, in 15-minute buckets.
const range = { from: new Date('2026-11-15T11:00:00Z'), to: new Date('2026-11-15T12:00:00Z') };

function activity(location: MeterLocation | null, quarters: number[]): MotionActivity {
  return toMotionActivity(
    quarters.map((quarter) => ({
      __typename: 'MotionStatistics' as const,
      location: 'KITCHEN',
      bucketStart: new Date(range.from.getTime() + quarter * 15 * 60_000).toISOString(),
      samples: 60,
      motionSamples: 15 * (quarter + 1),
      activityRatio: 0.25 * (quarter + 1),
    })),
    range,
    'FIFTEEN_MINUTES',
    location,
  );
}

function renderCard(query: WidgetQuery<MotionActivity>) {
  return render(<MotionActivityCard activity={query} bucket="FIFTEEN_MINUTES" />);
}

describe('MotionActivityCard', () => {
  beforeEach(() => {
    vi.stubEnv('TZ', 'Europe/Moscow');
  });

  it('is busy while the first data loads', () => {
    renderCard({ status: 'loading' });

    expect(screen.getByRole('region', { name: 'Motion activity' })).toHaveAttribute(
      'aria-busy',
      'true',
    );
    expect(
      screen.getByText('Share of samples that detected motion, per location and 15 minutes'),
    ).toBeInTheDocument();
  });

  it('shows the error with Retry', async () => {
    const user = userEvent.setup();
    const retry = vi.fn();
    renderCard({ status: 'error', retry });

    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(retry).toHaveBeenCalledOnce();
  });

  it("shows each location's activity per bucket, summed up in the label", () => {
    renderCard({ status: 'ready', data: activity(null, [0, 1, 3]), refreshing: false });

    expect(
      screen.getByRole('img', {
        name: 'Share of samples that detected motion per 15 minutes from 15 Nov 14:00 to 15 Nov 15:00. Over the whole range: Kitchen 58 %, Living Room no samples, Bedroom no samples, Garage no samples, Office no samples, Corridor no samples.',
      }),
    ).toBeInTheDocument();
    expect(
      screen.getByTitle('Kitchen · 15 Nov 14:15–14:30 · 50 % of 60 samples with motion').className,
    ).toMatch(/level2/);
    expect(screen.getByTitle('Kitchen · 15 Nov 14:30–14:45 · No samples').className).toMatch(
      /empty/,
    );
    expect(
      screen.getByTitle('Kitchen · 15 Nov 14:45–15:00 · 100 % of 60 samples with motion').className,
    ).toMatch(/level4/);
  });

  it('labels the time axis and explains the colours', () => {
    renderCard({ status: 'ready', data: activity('KITCHEN', [0]), refreshing: false });

    expect(screen.getByText('14:15')).toBeInTheDocument();
    expect(screen.getByText('15:00')).toBeInTheDocument();
    expect(screen.getByRole('list')).toHaveTextContent(
      '0–20 %20–40 %40–60 %60–80 %80–100 %No samples',
    );
  });

  it('says so when the range has no samples', () => {
    renderCard({ status: 'ready', data: activity(null, []), refreshing: false });

    expect(screen.getByRole('heading', { name: 'No motion samples in this range' })).toBeVisible();
  });

  it('dims the heatmap while newer data loads', () => {
    renderCard({ status: 'ready', data: activity(null, [0]), refreshing: true });

    expect(screen.getByRole('status')).toHaveTextContent('Updating…');
  });
});
